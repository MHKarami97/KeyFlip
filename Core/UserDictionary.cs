using System.Text.Json;

namespace KeyFlip.Core;

/// <summary>Learned user feedback: words never to convert, words always to convert, and accepted conversions.</summary>
public sealed class UserDictionary
{
    private const int PromotionThreshold = 2;

    private readonly object _gate = new();
    private readonly object _writeLock = new();
    private readonly string _path;
    private readonly HashSet<string> _never;
    private readonly HashSet<string> _always;
    private readonly Dictionary<string, int> _accepted;
    private int _saveScheduled;
    private volatile bool _dirty;

    public UserDictionary(string? path = null)
    {
        if (path is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            _path = path;
        }
        else
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeyFlip");
            Directory.CreateDirectory(folder);
            _path = Path.Combine(folder, "user-words.json");
        }

        var data = Read();
        _never = new HashSet<string>(data.NeverConvert.Select(Normalize).Where(w => w.Length > 0));
        _always = new HashSet<string>(data.AlwaysConvert.Select(Normalize).Where(w => w.Length > 0));
        _accepted = new Dictionary<string, int>();
        foreach (var (word, count) in data.Accepted)
        {
            var normalized = Normalize(word);
            if (normalized.Length > 0)
            {
                _accepted[normalized] = Math.Max(_accepted.GetValueOrDefault(normalized), count);
            }
        }
    }

    public bool IsNeverConvert(string word)
    {
        word = Normalize(word);
        lock (_gate)
        {
            return _never.Contains(word);
        }
    }

    public bool IsAlwaysConvert(string word)
    {
        word = Normalize(word);
        lock (_gate)
        {
            return _always.Contains(word);
        }
    }

    public IReadOnlyList<string> GetNeverConvert()
    {
        lock (_gate)
        {
            return _never.OrderBy(word => word, StringComparer.Ordinal).ToList();
        }
    }

    public void AddNeverConvert(string word)
    {
        word = Normalize(word);
        if (word.Length == 0)
        {
            return;
        }

        lock (_gate)
        {
            _never.Add(word);
            _always.Remove(word);
            _accepted.Remove(word);
        }

        ScheduleSave();
    }

    public void RemoveNeverConvert(string word)
    {
        word = Normalize(word);
        lock (_gate)
        {
            _never.Remove(word);
        }

        ScheduleSave();
    }

    /// <summary>Counts a conversion the user did not undo; repeated acceptance promotes the word.</summary>
    public void RecordAccepted(string word)
    {
        word = Normalize(word);
        if (word.Length == 0)
        {
            return;
        }

        lock (_gate)
        {
            if (_never.Contains(word) || _always.Contains(word))
            {
                return;
            }

            var count = _accepted.GetValueOrDefault(word) + 1;
            if (count >= PromotionThreshold)
            {
                _accepted.Remove(word);
                _always.Add(word);
            }
            else
            {
                _accepted[word] = count;
            }
        }

        ScheduleSave();
    }

    public void Flush()
    {
        if (_dirty)
        {
            Write();
        }
    }

    private void ScheduleSave()
    {
        _dirty = true;
        if (Interlocked.Exchange(ref _saveScheduled, 1) == 1)
        {
            return;
        }

        Task.Run(async () =>
        {
            await Task.Delay(500);
            Interlocked.Exchange(ref _saveScheduled, 0);
            Write();
        });
    }

    private void Write()
    {
        lock (_writeLock)
        {
            _dirty = false;
            UserData snapshot;
            lock (_gate)
            {
                snapshot = new UserData
                {
                    NeverConvert = _never.ToList(),
                    AlwaysConvert = _always.ToList(),
                    Accepted = new Dictionary<string, int>(_accepted)
                };
            }

            try
            {
                File.WriteAllText(_path, JsonSerializer.Serialize(snapshot, AppJsonContext.Default.UserData));
            }
            catch (IOException)
            {
            }
        }
    }

    private UserData Read()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize(File.ReadAllText(_path), AppJsonContext.Default.UserData) ?? new UserData()
                : new UserData();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new UserData();
        }
    }

    private static string Normalize(string word) =>
        word.Trim().Replace("\u200c", string.Empty).Replace('ي', 'ی').Replace('ك', 'ک').ToLowerInvariant();

    internal sealed class UserData
    {
        public List<string> NeverConvert { get; set; } = new();
        public List<string> AlwaysConvert { get; set; } = new();
        public Dictionary<string, int> Accepted { get; set; } = new();
    }
}
