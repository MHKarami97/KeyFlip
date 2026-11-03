#!/usr/bin/env python3
"""Builds Data/en.txt and Data/fa.txt as "word<TAB>count" from frequency word lists.

Usage:
  python tools/prepare_data.py
  python tools/prepare_data.py --en-file count_1w.txt --fa-file persian-wikipedia.txt

If a download fails (for example a blocked site or a slow network), download the file manually,
or set HTTPS_PROXY / HTTP_PROXY, and pass the local path with --en-file / --fa-file.

Sources:
  English: https://norvig.com/ngrams/count_1w.txt (also mirrored on GitHub, see EN_URLS)
  Persian: https://github.com/behnam/persian-words-frequency (persian-wikipedia.txt, CC BY-SA 3.0)
Check each source's license before redistributing the generated files.
"""
import argparse
import re
import sys
import time
import urllib.error
import urllib.request
from collections import defaultdict
from pathlib import Path

EN_URLS = [
    "https://norvig.com/ngrams/count_1w.txt",
    "https://raw.githubusercontent.com/anderscui/spellchecker/master/data/norvig/count_1w.txt",
]
FA_URLS = [
    "https://raw.githubusercontent.com/behnam/persian-words-frequency/master/persian-wikipedia.txt",
]
EN_LIMIT = 200_000
FA_LIMIT = 250_000
MIN_FA_COUNT = 5
ATTEMPTS = 3
TIMEOUT_SECONDS = 45
DATA = Path(__file__).resolve().parent.parent / "Data"

ENGLISH_WORD = re.compile(r"^[a-z]{2,24}$")
PERSIAN_WORD = re.compile(r"^[\u0621-\u06CC]{2,24}$")
NUMBER = re.compile(r"^\d+$")


def download(urls):
    for url in urls:
        for attempt in range(1, ATTEMPTS + 1):
            try:
                print(f"Downloading {url} (attempt {attempt}/{ATTEMPTS})")
                request = urllib.request.Request(url, headers={"User-Agent": "KeyFlip-data-builder"})
                with urllib.request.urlopen(request, timeout=TIMEOUT_SECONDS) as response:
                    return response.read().decode("utf-8", errors="ignore").splitlines()
            except (urllib.error.URLError, TimeoutError, OSError) as error:
                print(f"  failed: {error}")
                time.sleep(2 * attempt)
    raise RuntimeError("all sources failed")


def read_lines(local_path, urls):
    if local_path:
        return Path(local_path).read_text(encoding="utf-8", errors="ignore").splitlines()
    return download(urls)


def normalize_persian(token):
    return token.replace("\u200c", "").replace("\u064a", "\u06cc").replace("\u0643", "\u06a9")


def build_english(lines):
    counts = {}
    for line in lines:
        parts = line.split()
        if len(parts) == 2 and ENGLISH_WORD.match(parts[0]) and NUMBER.match(parts[1]):
            counts[parts[0]] = int(parts[1])
    return sorted(counts.items(), key=lambda item: -item[1])[:EN_LIMIT]


def build_persian(lines):
    counts = defaultdict(int)
    for index, line in enumerate(lines):
        tokens = line.split()
        word = next((normalize_persian(t) for t in tokens if PERSIAN_WORD.match(normalize_persian(t))), None)
        number = next((int(t) for t in tokens if NUMBER.match(t)), None)
        if not word:
            continue
        if number is None:
            number = len(lines) - index  # file order is assumed to be by descending frequency
        elif number < MIN_FA_COUNT:
            continue
        counts[word] += number
    return sorted(counts.items(), key=lambda item: -item[1])[:FA_LIMIT]


def write(path, rows):
    path.write_text("\n".join(f"{word}\t{count}" for word, count in rows), encoding="utf-8")
    print(f"Wrote {len(rows)} words to {path}")


def run(name, local_path, urls, builder, target):
    try:
        rows = builder(read_lines(local_path, urls))
    except (RuntimeError, OSError) as error:
        print(f"[{name}] skipped: {error}")
        return False
    if not rows:
        print(f"[{name}] skipped: no usable words found (unexpected file format?)")
        return False
    write(target, rows)
    return True


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--en-file", help="local English frequency list (word<space>count per line)")
    parser.add_argument("--fa-file", help="local Persian frequency list")
    args = parser.parse_args()

    DATA.mkdir(exist_ok=True)
    results = [
        run("English", args.en_file, EN_URLS, build_english, DATA / "en.txt"),
        run("Persian", args.fa_file, FA_URLS, build_persian, DATA / "fa.txt"),
    ]
    sys.exit(0 if all(results) else 1)