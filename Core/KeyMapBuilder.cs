namespace KeyFlip.Core;

/// <summary>Builds the key table from built-in defaults, the installed Persian layout, and user overrides.</summary>
public sealed class KeyMapBuilder
{
    private const int MinDetectedKeys = 25;
    private static readonly int[] OemKeys = { 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xDB, 0xDC, 0xDD, 0xDE };

    private readonly LayoutKeyReader _reader = new();

    /// <summary>Every key the editor can configure: A-Z plus the OEM punctuation keys of a US keyboard.</summary>
    public static IReadOnlyList<int> CandidateKeys { get; } = Enumerable.Range(0x41, 26).Concat(OemKeys).ToArray();

    public static string DescribeKey(int virtualKey) => virtualKey switch
    {
        >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
        0xBA => ";",
        0xBB => "=",
        0xBC => ",",
        0xBD => "-",
        0xBE => ".",
        0xBF => "/",
        0xC0 => "`",
        0xDB => "[",
        0xDC => "\\",
        0xDD => "]",
        0xDE => "'",
        _ => $"0x{virtualKey:X}"
    };

    public static Dictionary<int, (char English, char Persian)> CreateDefaults()
    {
        var map = new Dictionary<int, (char, char)>();
        const string english = "qwertyuiopasdfghjklzxcvbnm";
        const string persian = "ضصثقفغعهخحشسیبلاتنمظطزرذدئ";
        for (var i = 0; i < english.Length; i++)
        {
            map[char.ToUpperInvariant(english[i])] = (english[i], persian[i]);
        }

        map[0xBA] = (';', 'ک');
        map[0xDE] = ('\'', 'گ');
        map[0xDB] = ('[', 'ج');
        map[0xDD] = (']', 'چ');
        map[0xBC] = (',', 'و');
        map[0xDC] = ('\\', 'پ');
        return map;
    }

    public Dictionary<int, (char English, char Persian)> Build(AppSettings settings)
    {
        var table = BuildBase(settings.AutoDetectKeyMap);
        ApplyOverrides(table, settings);
        return table;
    }

    /// <summary>Defaults, or the installed Persian layout when auto-detection is on and succeeds.</summary>
    public Dictionary<int, (char English, char Persian)> BuildBase(bool autoDetect) =>
        (autoDetect ? Detect() : null) ?? CreateDefaults();

    public Dictionary<int, (char English, char Persian)>? Detect()
    {
        var persian = _reader.Read(InputLanguage.Persian, CandidateKeys);
        if (persian is null)
        {
            return null;
        }

        var english = _reader.Read(InputLanguage.English, CandidateKeys) ?? new Dictionary<int, char>();
        var table = new Dictionary<int, (char, char)>();
        foreach (var (key, character) in persian)
        {
            if (IsPersianLetter(character))
            {
                table[key] = (ResolveEnglish(key, english), character);
            }
        }

        return table.Count >= MinDetectedKeys ? table : null;
    }

    public static char FallbackEnglish(int virtualKey) =>
        CreateDefaults().TryGetValue(virtualKey, out var entry)
            ? entry.English
            : char.ToLowerInvariant(DescribeKey(virtualKey)[0]);

    private static void ApplyOverrides(Dictionary<int, (char English, char Persian)> table, AppSettings settings)
    {
        foreach (var (key, text) in settings.PersianKeyOverrides)
        {
            if (string.IsNullOrEmpty(text))
            {
                table.Remove(key);
                continue;
            }

            var english = table.TryGetValue(key, out var existing) ? existing.English : FallbackEnglish(key);
            table[key] = (english, text[0]);
        }

        foreach (var (key, text) in settings.EnglishKeyOverrides)
        {
            if (!string.IsNullOrEmpty(text) && table.TryGetValue(key, out var existing))
            {
                table[key] = (text[0], existing.Persian);
            }
        }
    }

    private static char ResolveEnglish(int key, Dictionary<int, char> english) =>
        english.TryGetValue(key, out var character) && character is > ' ' and < '\u007f'
            ? char.ToLowerInvariant(character)
            : FallbackEnglish(key);

    private static bool IsPersianLetter(char c) => c is >= '\u0621' and <= '\u06CC';
}
