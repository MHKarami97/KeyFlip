namespace KeyFlip.Core;

public static class KeyMap
{
    private static readonly Dictionary<int, (char English, char Persian)> Keys = Build();

    public static bool IsLetterKey(int virtualKey) => virtualKey is >= 0x41 and <= 0x5A;

    public static bool IsMapped(int virtualKey, bool persianLayout) =>
        IsLetterKey(virtualKey) || (persianLayout && Keys.ContainsKey(virtualKey));

    public static char ToEnglish(int virtualKey) => Keys[virtualKey].English;

    public static char ToPersian(int virtualKey) => Keys[virtualKey].Persian;

    private static Dictionary<int, (char, char)> Build()
    {
        var map = new Dictionary<int, (char, char)>();
        const string english = "qwertyuiopasdfghjklzxcvbnm";
        const string persian = "ضصثقفغعهخحشسیبلاتنمظطزرذدپ";
        for (var i = 0; i < english.Length; i++)
        {
            map[char.ToUpperInvariant(english[i])] = (english[i], persian[i]);
        }

        map[0xBA] = (';', 'ک');
        map[0xDE] = ('\'', 'گ');
        map[0xDB] = ('[', 'ج');
        map[0xDD] = (']', 'چ');
        map[0xBC] = (',', 'و');
        return map;
    }
}
