namespace KeyFlip.Core;

/// <summary>Thread-safe key table: virtual key to (English character, Persian character).</summary>
public static class KeyMap
{
    public sealed record Snapshot(
        IReadOnlyDictionary<int, (char English, char Persian)> Table,
        IReadOnlyDictionary<char, char> ToPersianMap,
        IReadOnlyDictionary<char, char> ToEnglishMap);

    private static volatile Snapshot _snapshot = BuildSnapshot(KeyMapBuilder.CreateDefaults());

    public static IEnumerable<int> AllKeys => _snapshot.Table.Keys;

    public static Snapshot GetSnapshot() => _snapshot;

    public static void Replace(Dictionary<int, (char English, char Persian)> table) =>
        _snapshot = BuildSnapshot(new Dictionary<int, (char English, char Persian)>(table));

    public static bool IsLetterKey(int virtualKey) => virtualKey is >= 0x41 and <= 0x5A;

    public static bool IsMapped(int virtualKey) => _snapshot.Table.ContainsKey(virtualKey);

    public static char ToEnglish(int virtualKey) =>
        _snapshot.Table.TryGetValue(virtualKey, out var entry) ? entry.English : default;

    public static char ToPersian(int virtualKey) =>
        _snapshot.Table.TryGetValue(virtualKey, out var entry) ? entry.Persian : default;

    private static Snapshot BuildSnapshot(Dictionary<int, (char English, char Persian)> table)
    {
        var toPersian = new Dictionary<char, char>(table.Count);
        var toEnglish = new Dictionary<char, char>(table.Count);
        foreach (var (_, (english, persian)) in table)
        {
            toPersian[english] = persian;
            toEnglish[persian] = english;
        }

        return new Snapshot(table, toPersian, toEnglish);
    }
}
