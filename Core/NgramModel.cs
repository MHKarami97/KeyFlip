using System.Runtime.InteropServices;

namespace KeyFlip.Core;

/// <summary>Character trigram model with interpolated Kneser-Ney smoothing. Call Build() after Train().</summary>
public sealed class NgramModel
{
    private const char Start = '^';
    private const char End = '$';
    private const double Discount = 0.75;
    private const double UnigramFloor = 0.01;

    private readonly Dictionary<long, int> _trigrams = new();
    private readonly Dictionary<long, int> _bigramContinuations = new();
    private readonly Dictionary<long, Context> _trigramContexts = new();
    private readonly Dictionary<char, Context> _bigramContexts = new();
    private readonly Dictionary<char, int> _unigramContinuations = new();
    private readonly HashSet<char> _alphabet = new();
    private int _unigramTotal;

    public int WordCount { get; private set; }

    public void Train(IEnumerable<string> words)
    {
        foreach (var word in words)
        {
            var padded = $"{Start}{Start}{word}{End}";
            foreach (var c in word)
            {
                _alphabet.Add(c);
            }

            for (var i = 2; i < padded.Length; i++)
            {
                Increment(_trigrams, Key3(padded[i - 2], padded[i - 1], padded[i]));
            }

            WordCount++;
        }
    }

    public void Build()
    {
        foreach (var (key, count) in _trigrams)
        {
            var context = GetOrAdd(_trigramContexts, key >> 16);
            context.Total += count;
            context.Types++;
            Increment(_bigramContinuations, key & 0xFFFFFFFF);
        }

        foreach (var (key, continuation) in _bigramContinuations)
        {
            var context = GetOrAdd(_bigramContexts, (char)(key >> 16));
            context.Total += continuation;
            context.Types++;
            Increment(_unigramContinuations, (char)(key & 0xFFFF));
            _unigramTotal++;
        }
    }

    /// <summary>Average natural-log probability per character (higher = more plausible).</summary>
    public double Score(string word)
    {
        var padded = $"{Start}{Start}{word}{End}";
        var total = 0.0;
        for (var i = 2; i < padded.Length; i++)
        {
            total += Math.Log(Trigram(padded[i - 2], padded[i - 1], padded[i]));
        }

        return total / (word.Length + 1);
    }

    private double Trigram(char a, char b, char c)
    {
        var lower = Bigram(b, c);
        if (!_trigramContexts.TryGetValue(Key2(a, b), out var context))
        {
            return lower;
        }

        _trigrams.TryGetValue(Key3(a, b, c), out var count);
        return (Math.Max(count - Discount, 0) + Discount * context.Types * lower) / context.Total;
    }

    private double Bigram(char b, char c)
    {
        var lower = Unigram(c);
        if (!_bigramContexts.TryGetValue(b, out var context))
        {
            return lower;
        }

        _bigramContinuations.TryGetValue(Key2(b, c), out var count);
        return (Math.Max(count - Discount, 0) + Discount * context.Types * lower) / context.Total;
    }

    private double Unigram(char c)
    {
        _unigramContinuations.TryGetValue(c, out var count);
        return (count + UnigramFloor) / (_unigramTotal + UnigramFloor * (_alphabet.Count + 1));
    }

    private static void Increment<TKey>(Dictionary<TKey, int> map, TKey key) where TKey : notnull =>
        CollectionsMarshal.GetValueRefOrAddDefault(map, key, out _)++;

    private static Context GetOrAdd<TKey>(Dictionary<TKey, Context> map, TKey key) where TKey : notnull
    {
        if (!map.TryGetValue(key, out var context))
        {
            context = new Context();
            map[key] = context;
        }

        return context;
    }

    private static long Key2(char a, char b) => ((long)a << 16) | b;

    private static long Key3(char a, char b, char c) => ((long)a << 32) | ((long)b << 16) | c;

    private sealed class Context
    {
        public int Total;
        public int Types;
    }
}
