using System.Runtime.InteropServices;

namespace KeyFlip.Core;

/// <summary>Character trigram model with additive smoothing.</summary>
public sealed class NgramModel
{
    private const char Start = '^';
    private const char End = '$';
    private const double Smoothing = 0.05;

    private readonly Dictionary<long, int> _trigrams = new();
    private readonly Dictionary<long, int> _contexts = new();
    private readonly HashSet<char> _alphabet = new();

    public int WordCount { get; private set; }

    public void Train(IEnumerable<string> words)
    {
        foreach (var word in words)
        {
            var padded = string.Concat(Start, Start, word, End);
            foreach (var c in word)
            {
                _alphabet.Add(c);
            }

            for (var i = 2; i < padded.Length; i++)
            {
                Increment(_trigrams, Key(padded[i - 2], padded[i - 1], padded[i]));
                Increment(_contexts, Key(padded[i - 2], padded[i - 1], '\0'));
            }

            WordCount++;
        }
    }

    /// <summary>Average natural-log probability per character (higher = more plausible).</summary>
    public double Score(string word)
    {
        var padded = string.Concat(Start, Start, word, End);
        var vocabulary = _alphabet.Count + 1;
        var total = 0.0;

        for (var i = 2; i < padded.Length; i++)
        {
            _trigrams.TryGetValue(Key(padded[i - 2], padded[i - 1], padded[i]), out var tri);
            _contexts.TryGetValue(Key(padded[i - 2], padded[i - 1], '\0'), out var context);
            total += Math.Log((tri + Smoothing) / (context + Smoothing * vocabulary));
        }

        return total / (word.Length + 1);
    }

    private static void Increment(Dictionary<long, int> map, long key) =>
        CollectionsMarshal.GetValueRefOrAddDefault(map, key, out _)++;

    private static long Key(char a, char b, char c) => ((long)a << 32) | ((long)b << 16) | c;
}
