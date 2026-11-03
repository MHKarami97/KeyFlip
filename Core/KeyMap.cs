namespace KeyFlip.Core;

/// <summary>Thread-safe key table: virtual key to (English character, Persian character).</summary>
public static class KeyMap
{
    private static volatile Dictionary<int, (char English, char Persian)> _table = KeyMapBuilder.CreateDefaults();

    public static IEnumerable<int> AllKeys => _table.Keys;

    public static void Replace(Dictionary<int, (char English, char Persian)> table) => _table = table;

    public static bool IsLetterKey(int virtualKey) => virtualKey is >= 0x41 and <= 0x5A;

    public static bool IsMapped(int virtualKey, bool persianLayout)
    {
        var table = _table;
        return table.ContainsKey(virtualKey) && (persianLayout || IsLetterKey(virtualKey));
    }

    public static char ToEnglish(int virtualKey) => _table.GetValueOrDefault(virtualKey).English;

    public static char ToPersian(int virtualKey) => _table.GetValueOrDefault(virtualKey).Persian;
}
