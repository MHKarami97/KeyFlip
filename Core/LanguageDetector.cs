using System.Reflection;

namespace KeyFlip.Core;

/// <summary>Decides whether a word looks like English or Persian using word lists and n-gram models.</summary>
public sealed class LanguageDetector
{
    private const int MinWordsForNgram = 5000;
    private const double Margin = 1.0;
    private const int MinNgramLength = 4;

    private readonly HashSet<string> _english = new(StringComparer.Ordinal);
    private readonly HashSet<string> _persian = new(StringComparer.Ordinal);
    private readonly NgramModel _englishModel = new();
    private readonly NgramModel _persianModel = new();
    private volatile bool _ready;
    private bool _ngramEnabled;

    public LanguageDetector()
    {
        Task.Run(Initialize);
    }

    public bool IsReady => _ready;

    public bool ShouldFlip(string typed, string alternative, InputLanguage current)
    {
        if (!_ready || typed.Length < 2)
        {
            return false;
        }

        var typedIsEnglish = current == InputLanguage.English;
        var typedKnown = typedIsEnglish ? _english.Contains(typed) : _persian.Contains(typed);
        var alternativeKnown = typedIsEnglish ? _persian.Contains(alternative) : _english.Contains(alternative);

        if (typedKnown)
        {
            return false;
        }

        if (alternativeKnown)
        {
            return true;
        }

        if (!_ngramEnabled || typed.Length < MinNgramLength)
        {
            return false;
        }

        var typedScore = typedIsEnglish ? _englishModel.Score(typed) : _persianModel.Score(typed);
        var alternativeScore = typedIsEnglish ? _persianModel.Score(alternative) : _englishModel.Score(alternative);
        return alternativeScore - typedScore > Margin;
    }

    private void Initialize()
    {
        foreach (var line in ReadAllLines("en.txt"))
        {
            var word = NormalizeEnglish(line);
            if (word is not null)
            {
                _english.Add(word);
            }
        }

        foreach (var line in ReadAllLines("fa.txt"))
        {
            var word = NormalizePersian(line);
            if (word is not null)
            {
                _persian.Add(word);
            }
        }

        _englishModel.Train(_english);
        _persianModel.Train(_persian);
        _ngramEnabled = _english.Count >= MinWordsForNgram && _persian.Count >= MinWordsForNgram;
        _ready = true;
    }

    private static string? NormalizeEnglish(string raw)
    {
        var word = raw.Trim().ToLowerInvariant();
        return word.Length is >= 2 and <= 24 && word.All(c => c is >= 'a' and <= 'z') ? word : null;
    }

    private static string? NormalizePersian(string raw)
    {
        var word = raw.Trim().Replace('ي', 'ی').Replace('ك', 'ک');
        return word.Length is >= 2 and <= 24 && word.All(c => c is >= '\u0621' and <= '\u06CC') ? word : null;
    }

    private static IEnumerable<string> ReadAllLines(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(name, StringComparison.Ordinal));
        if (resource is not null)
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
            while (reader.ReadLine() is { } line)
            {
                yield return line;
            }
        }

        var external = Path.Combine(AppContext.BaseDirectory, "Data", name);
        if (File.Exists(external))
        {
            foreach (var line in File.ReadLines(external))
            {
                yield return line;
            }
        }
    }
}
