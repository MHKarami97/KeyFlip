# KeyFlip

1. `python tools/prepare_data.py` (downloads and normalizes word lists into Data/).
2. Put `Vazirmatn-Regular.ttf` in `Fonts/`.
3. Publish:

    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

Detection: a word is flipped when it is unknown in the current language and either known in the other language,
or (n-gram enabled, length >= 4) the other-language trigram score beats it by `Margin` (LanguageDetector.cs).
N-gram scoring is enabled only when both lists have >= 5000 words.
