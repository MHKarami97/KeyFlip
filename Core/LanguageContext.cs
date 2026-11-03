namespace KeyFlip.Core;

/// <summary>Remembers the language of the last few words and turns it into a bias for borderline decisions.</summary>
public sealed class LanguageContext
{
    private const int Window = 3;
    private const long MaxAgeMs = 20_000;
    private const double MaxBias = 0.8;

    private readonly Queue<(InputLanguage Language, long Tick)> _recent = new();

    public void Record(InputLanguage intended)
    {
        Prune();
        _recent.Enqueue((intended, Environment.TickCount64));
        while (_recent.Count > Window)
        {
            _recent.Dequeue();
        }
    }

    /// <summary>Positive when recent words favour <paramref name="target"/>, negative when they oppose it.</summary>
    public double BiasToward(InputLanguage target)
    {
        Prune();
        var agree = _recent.Count(item => item.Language == target);
        var disagree = _recent.Count - agree;
        return (agree - disagree) / (double)Window * MaxBias;
    }

    private void Prune()
    {
        var now = Environment.TickCount64;
        while (_recent.Count > 0 && now - _recent.Peek().Tick > MaxAgeMs)
        {
            _recent.Dequeue();
        }
    }
}
