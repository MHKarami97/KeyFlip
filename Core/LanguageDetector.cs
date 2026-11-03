using System.Globalization;
using System.Reflection;

namespace KeyFlip.Core;

public enum Verdict
{
    Keep,
    Maybe,
    Flip
}

public readonly record struct Evaluation(Verdict Verdict, bool TypedKnown);

/// <summary>Decides whether a word was typed in the wrong layout using frequency-ranked word lists, morphology and n-grams.</summary>
public sealed class LanguageDetector
{
    private const int EnglishLimit = 120_000;
    private const int PersianLimit = 150_000;
    private const int MinWordsForNgram = 5000;
    private const int MinNgramLength = 4;
    private const int MinNgramLengthWithContext = 3;
    private const double Margin = 1.0;
    private const double WeakFraction = 0.5;
    private const double StrongBias = 0.4;
    private const double SeedCount = 1e12;

    private readonly UserDictionary _user;
    private readonly NgramModel _englishModel = new();
    private readonly NgramModel _persianModel = new();
    private HashSet<string> _english = new();
    private HashSet<string> _persian = new();
    private volatile bool _ready;
    private bool _ngramEnabled;

    public LanguageDetector(UserDictionary user)
    {
        _user = user;
        Task.Run(Initialize);
    }

    /// <param name="bias">Positive values favour converting (recent words were in the alternative language).</param>
    public Evaluation Evaluate(string typed, string alternative, InputLanguage current, double bias)
    {
        if (!_ready || typed.Length < 2)
        {
            return new Evaluation(Verdict.Keep, false);
        }

        if (_user.IsNeverConvert(typed))
        {
            return new Evaluation(Verdict.Keep, true);
        }

        if (_user.IsAlwaysConvert(typed))
        {
            return new Evaluation(Verdict.Flip, false);
        }

        var typedIsEnglish = current == InputLanguage.English;
        var typedWords = typedIsEnglish ? _english : _persian;
        var alternativeWords = typedIsEnglish ? _persian : _english;

        if (Morphology.IsKnown(typed, typedWords, typedIsEnglish))
        {
            return new Evaluation(Verdict.Keep, true);
        }

        if (Morphology.IsKnown(alternative, alternativeWords, !typedIsEnglish))
        {
            return new Evaluation(Verdict.Flip, false);
        }

        var minimumLength = bias >= StrongBias ? MinNgramLengthWithContext : MinNgramLength;
        if (!_ngramEnabled || typed.Length < minimumLength)
        {
            return new Evaluation(Verdict.Keep, false);
        }

        var typedScore = typedIsEnglish ? _englishModel.Score(typed) : _persianModel.Score(typed);
        var alternativeScore = typedIsEnglish ? _persianModel.Score(alternative) : _englishModel.Score(alternative);
        var delta = alternativeScore - typedScore + bias;

        if (delta > Margin)
        {
            return new Evaluation(Verdict.Flip, false);
        }

        return new Evaluation(delta > Margin * WeakFraction ? Verdict.Maybe : Verdict.Keep, false);
    }

    private void Initialize()
    {
        try
        {
            _english = LoadWords("en.txt", EnglishLimit, NormalizeEnglish);
            _persian = LoadWords("fa.txt", PersianLimit, NormalizePersian);

            _englishModel.Train(_english);
            _englishModel.Build();
            _persianModel.Train(_persian);
            _persianModel.Build();

            _ngramEnabled = _english.Count >= MinWordsForNgram && _persian.Count >= MinWordsForNgram;
            _ready = true;
            DebugLog.Write($"Detector ready. en={_english.Count}, fa={_persian.Count}, ngram={_ngramEnabled}");
        }
        catch (Exception ex)
        {
            DebugLog.Write($"Detector init failed: {ex}");
        }
    }

    /// <summary>Loads "word[TAB]count" lines and keeps the most frequent <paramref name="limit"/> words.</summary>
    private static HashSet<string> LoadWords(string name, int limit, Func<string, string?> normalize)
    {
        var counts = new Dictionary<string, double>();
        foreach (var line in ReadAllLines(name))
        {
            var parts = line.Split('\t');
            var word = normalize(parts[0]);
            if (word is null)
            {
                continue;
            }

            var count = parts.Length > 1 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : SeedCount;
            counts[word] = Math.Max(counts.GetValueOrDefault(word), count);
        }

        return counts.OrderByDescending(pair => pair.Value).Take(limit).Select(pair => pair.Key).ToHashSet();
    }

    private static string? NormalizeEnglish(string raw)
    {
        var word = raw.Trim().ToLowerInvariant();
        return word.Length is >= 2 and <= 24 && word.All(c => c is >= 'a' and <= 'z') ? word : null;
    }

    private static string? NormalizePersian(string raw)
    {
        var word = raw.Trim().Replace("\u200c", string.Empty).Replace('ي', 'ی').Replace('ك', 'ک');
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
