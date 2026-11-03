#!/usr/bin/env python3
"""Downloads word lists and writes normalized Data/en.txt and Data/fa.txt (run once, needs internet).

Sources:
  English: https://github.com/dwyl/english-words  (words_alpha.txt)
  Persian: https://github.com/behnam/persian-words-frequency  (persian-wikipedia.txt, CC BY-SA 3.0)
Check each repository's license before redistributing the generated files.
"""
import re
import urllib.request
from pathlib import Path

EN_URL = "https://raw.githubusercontent.com/dwyl/english-words/master/words_alpha.txt"
FA_URL = "https://raw.githubusercontent.com/behnam/persian-words-frequency/master/persian-wikipedia.txt"
MIN_FA_COUNT = 5
DATA = Path(__file__).resolve().parent.parent / "Data"

PERSIAN_WORD = re.compile(r"^[\u0621-\u06CC]{2,24}$")
DIGITS = re.compile(r"^\d+$")


def download(url):
    with urllib.request.urlopen(url, timeout=60) as response:
        return response.read().decode("utf-8", errors="ignore").splitlines()


def normalize_persian(token):
    return token.replace("\u064a", "\u06cc").replace("\u0643", "\u06a9")


def build_english():
    words = {w.strip().lower() for w in download(EN_URL)}
    return sorted(w for w in words if re.fullmatch(r"[a-z]{2,24}", w))


def build_persian():
    words = set()
    for line in download(FA_URL):
        tokens = line.split()
        word = next((normalize_persian(t) for t in tokens if PERSIAN_WORD.match(normalize_persian(t))), None)
        count = next((int(t) for t in tokens if DIGITS.match(t)), None)
        if word and (count is None or count >= MIN_FA_COUNT):
            words.add(word)
    return sorted(words)


if __name__ == "__main__":
    DATA.mkdir(exist_ok=True)
    english, persian = build_english(), build_persian()
    (DATA / "en.txt").write_text("\n".join(english), encoding="utf-8")
    (DATA / "fa.txt").write_text("\n".join(persian), encoding="utf-8")
    print(f"en: {len(english)} words, fa: {len(persian)} words")
