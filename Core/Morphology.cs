namespace KeyFlip.Core;

/// <summary>Lightweight affix stripping so inflected words are recognised without listing every form.</summary>
public static class Morphology
{
    private static readonly string[] EnglishSuffixes = { "ing", "ed", "es", "s", "ly", "er", "est", "d" };
    private static readonly string[] PersianPrefixes = { "نمی", "می" };

    private static readonly string[] PersianSuffixes =
    {
        "ترین", "هایی", "های", "مان", "تان", "شان", "ایم", "اید", "اند", "تر", "ها", "ان", "ات", "ین",
        "ام", "ای", "اش", "ید", "یم", "ند", "ی", "م", "ت", "ش", "ه"
    };

    public static bool IsKnown(string word, Dictionary<string, int> words, bool english) =>
        words.ContainsKey(word) || (english ? IsKnownEnglish(word, words) : IsKnownPersian(word, words));

    private static bool IsKnownEnglish(string word, Dictionary<string, int> words)
    {
        foreach (var suffix in EnglishSuffixes)
        {
            if (!word.EndsWith(suffix, StringComparison.Ordinal) || word.Length - suffix.Length < 3)
            {
                continue;
            }

            var stem = word[..^suffix.Length];
            if (words.ContainsKey(stem) || words.ContainsKey(stem + "e"))
            {
                return true;
            }

            if (stem.Length >= 4 && stem[^1] == stem[^2] && words.ContainsKey(stem[..^1]))
            {
                return true;
            }

            if (stem[^1] == 'i' && words.ContainsKey(stem[..^1] + "y"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsKnownPersian(string word, Dictionary<string, int> words)
    {
        if (StripSuffixes(word, words, 2))
        {
            return true;
        }

        foreach (var prefix in PersianPrefixes)
        {
            if (!word.StartsWith(prefix, StringComparison.Ordinal) || word.Length - prefix.Length < 2)
            {
                continue;
            }

            var rest = word[prefix.Length..];
            if (words.ContainsKey(rest) || StripSuffixes(rest, words, 2))
            {
                return true;
            }
        }

        return false;
    }

    private static bool StripSuffixes(string word, Dictionary<string, int> words, int depth)
    {
        foreach (var suffix in PersianSuffixes)
        {
            if (!word.EndsWith(suffix, StringComparison.Ordinal))
            {
                continue;
            }

            var stem = word[..^suffix.Length];
            var minimum = suffix.Length == 1 ? 3 : 2;
            if (stem.Length < minimum)
            {
                continue;
            }

            if (words.ContainsKey(stem) || (depth > 1 && StripSuffixes(stem, words, depth - 1)))
            {
                return true;
            }
        }

        return false;
    }
}
