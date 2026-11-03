using System.Reflection;

namespace KeyFlip.Core;

public sealed class WordValidator
{
    private readonly HashSet<string> _english;
    private readonly HashSet<string> _persian;

    public WordValidator()
    {
        _english = Load("en.txt", StringComparer.OrdinalIgnoreCase);
        _persian = Load("fa.txt", StringComparer.Ordinal);
    }

    public bool IsEnglishWord(string word) => word.Length > 1 && _english.Contains(word);

    public bool IsPersianWord(string word) => word.Length > 1 && _persian.Contains(word);

    private static HashSet<string> Load(string name, StringComparer comparer)
    {
        var set = new HashSet<string>(comparer);
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(name, StringComparison.Ordinal));
        if (resource is not null)
        {
            AddLines(set, assembly.GetManifestResourceStream(resource)!);
        }

        var external = Path.Combine(AppContext.BaseDirectory, "Data", name);
        if (File.Exists(external))
        {
            AddLines(set, File.OpenRead(external));
        }

        return set;
    }

    private static void AddLines(HashSet<string> set, Stream stream)
    {
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            var word = line.Trim();
            if (word.Length > 0)
            {
                set.Add(word);
            }
        }
    }
}
