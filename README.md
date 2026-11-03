# KeyFlip
Build & publish (self-contained single exe, no .NET needed on target):

    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

Output: bin/Release/net10.0-windows/win-x64/publish/KeyFlip.exe

1. Detection is dictionary-based: extend Data/en.txt and Data/fa.txt (one word per line) with large word lists. You can also place Data/en.txt / Data/fa.txt next to the exe.
