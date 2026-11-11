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
    private const int ShortWordLength = 3;
    private const int ShortWordRankLimit = 20_000;
    private const int MinWordsForNgram = 5000;
    private const int MinNgramLength = 4;
    private const int MinNgramLengthWithContext = 3;
    private const double Margin = 1.0;
    private const double WeakFraction = 0.5;
    private const double StrongBias = 0.4;
    private const double BothKnownMinBias = 0.25;
    private const double BothKnownRankRatio = 30;
    private const double SemicolonRankRatio = 10;
    private const double SeedCount = 1e12;
    private static readonly string[] ContractionSuffixes = { "s", "t", "re", "ve", "ll", "d", "m", "" };

    private readonly UserDictionary _user;
    private readonly NgramModel _englishModel = new();
    private readonly NgramModel _persianModel = new();
    private Dictionary<string, int> _english = new();
    private Dictionary<string, int> _persian = new();
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

        string? typedBase = null;
        var typedKnown = typedIsEnglish
            ? IsKnownEnglishToken(typed, bias, out typedBase)
            : Morphology.IsKnown(typed, typedWords, false);
        var alternativeKnown = Morphology.IsKnown(alternative, alternativeWords, !typedIsEnglish);

        if (typedKnown && alternativeKnown)
        {
            var flip = PrefersAlternative(typed, typedBase ?? typed, alternative, typedWords, alternativeWords, bias);
            return new Evaluation(flip ? Verdict.Flip : Verdict.Keep, !flip);
        }

        if (typedKnown)
        {
            return new Evaluation(Verdict.Keep, true);
        }

        if (alternativeKnown)
        {
            return new Evaluation(Verdict.Flip, false);
        }

        return EvaluateByNgram(typed, alternative, typedIsEnglish, bias);
    }

    /// <summary>Both readings are real words: convert only if recent words support it and the alternative is far more frequent.</summary>
    private static bool PrefersAlternative(
        string rawTyped,
        string typed,
        string alternative,
        Dictionary<string, int> typedWords,
        Dictionary<string, int> alternativeWords,
        double bias)
    {
        if (bias < BothKnownMinBias
            || !typedWords.TryGetValue(typed, out var typedRank)
            || !alternativeWords.TryGetValue(alternative, out var alternativeRank))
        {
            return false;
        }

        var ratio = rawTyped.EndsWith(';') ? SemicolonRankRatio : BothKnownRankRatio;
        return typedRank / (double)alternativeRank >= ratio;
    }

    private Evaluation EvaluateByNgram(string typed, string alternative, bool typedIsEnglish, double bias)
    {
        var typedForScore = typedIsEnglish && TryStripOuterEnglishPunctuation(typed, out var core) && core.Length >= 2
            ? core
            : typed;

        var minimumLength = bias >= StrongBias ? MinNgramLengthWithContext : MinNgramLength;
        if (!_ngramEnabled || Math.Min(typedForScore.Length, alternative.Length) < minimumLength)
        {
            return new Evaluation(Verdict.Keep, false);
        }

        var typedScore = typedIsEnglish ? _englishModel.Score(typedForScore) : _persianModel.Score(typed);
        var alternativeScore = typedIsEnglish ? _persianModel.Score(alternative) : _englishModel.Score(alternative);
        var delta = alternativeScore - typedScore + bias;

        if (delta > Margin)
        {
            return new Evaluation(Verdict.Flip, false);
        }

        return new Evaluation(delta > Margin * WeakFraction ? Verdict.Maybe : Verdict.Keep, false);
    }

    private bool IsKnownEnglishToken(string token, double bias, out string? baseWord)
    {
        if (Morphology.IsKnown(token, _english, true))
        {
            baseWord = token;
            return true;
        }

        if (token.All(char.IsAsciiLetter))
        {
            baseWord = null;
            return false;
        }

        var candidate = TryStripOuterEnglishPunctuation(token, out var stripped) ? stripped : token;

        if (TryMatchContraction(candidate, out baseWord))
        {
            return true;
        }

        if (candidate.All(char.IsAsciiLetter))
        {
            if (candidate.Length >= 2 && Morphology.IsKnown(candidate, _english, true))
            {
                baseWord = candidate;
                return true;
            }

            if (candidate.Length == 1 && (token.EndsWith(',') || token.EndsWith(';')) && bias < 0)
            {
                baseWord = candidate;
                return true;
            }
        }

        baseWord = null;
        return false;
    }

    private bool TryMatchContraction(string token, out string? baseWord)
    {
        baseWord = null;
        var quote = token.IndexOf('\'');
        if (quote <= 0 || quote != token.LastIndexOf('\''))
        {
            return false;
        }

        var stem = token[..quote];
        var suffix = token[(quote + 1)..];
        if (!stem.All(char.IsAsciiLetter) || !ContractionSuffixes.Contains(suffix, StringComparer.Ordinal))
        {
            return false;
        }

        if (stem == "i" && suffix is "m" or "d" or "ll" or "ve")
        {
            baseWord = stem + suffix;
            return true;
        }

        if (Morphology.IsKnown(stem, _english, true))
        {
            baseWord = stem;
            return true;
        }

        if (suffix == "t" && stem.EndsWith('n') && stem.Length >= 3)
        {
            var merged = stem + suffix;
            if (_english.ContainsKey(merged))
            {
                baseWord = merged;
                return true;
            }

            var root = stem[..^1];
            if (Morphology.IsKnown(root, _english, true))
            {
                baseWord = root;
                return true;
            }
        }

        return false;
    }

    private static bool TryStripOuterEnglishPunctuation(string token, out string core)
    {
        var start = 0;
        var end = token.Length;

        if (end > start && token[end - 1] is ',' or ';')
        {
            end--;
        }

        if (end - start >= 2 && ((token[start] == '[' && token[end - 1] == ']') || (token[start] == '\'' && token[end - 1] == '\'')))
        {
            start++;
            end--;
        }

        core = token[start..end];
        return core.Length > 0 && core.Length < token.Length;
    }

    private void Initialize()
    {
        try
        {
            _english = LoadWords("en.txt", EnglishLimit, NormalizeEnglish);
            _persian = LoadWords("fa.txt", PersianLimit, NormalizePersian);

            _englishModel.Train(_english.Keys);
            _englishModel.Build();
            _persianModel.Train(_persian.Keys);
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

    /// <summary>
    /// Loads "word[TAB]count" lines and returns word -> frequency rank (1 = most frequent).
    /// Very short words must be highly ranked, because frequency lists contain many short noise tokens.
    /// </summary>
    private static Dictionary<string, int> LoadWords(string name, int limit, Func<string, string?> normalize)
    {
        var counts = new Dictionary<string, double>();
        foreach (var line in ReadAllLines(name))
        {
            var span = line.AsSpan();
            var tab = span.IndexOf('\t');
            var rawWord = tab >= 0 ? span[..tab].ToString() : line;
            var word = normalize(rawWord);
            if (word is null)
            {
                continue;
            }

            var count = tab >= 0 && double.TryParse(span[(tab + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : SeedCount;
            counts[word] = Math.Max(counts.GetValueOrDefault(word), count);
        }

        var ranks = new Dictionary<string, int>();
        var rank = 0;
        foreach (var word in counts.OrderByDescending(pair => pair.Value).Take(limit).Select(pair => pair.Key))
        {
            rank++;
            if (word.Length <= ShortWordLength && rank > ShortWordRankLimit)
            {
                continue;
            }

            ranks[word] = rank;
        }

        return ranks;
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
