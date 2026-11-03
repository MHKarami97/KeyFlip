using System.Text.Json;

namespace KeyFlip.Core;

/// <summary>Learned user feedback: words never to convert, words always to convert, and accepted conversions.</summary>
public sealed class UserDictionary
{
    private const int PromotionThreshold = 2;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly object _gate = new();
    private readonly string _path;
    private readonly HashSet<string> _never;
    private readonly HashSet<string> _always;
    private readonly Dictionary<string, int> _accepted;
    private int _saveScheduled;

    public UserDictionary()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeyFlip");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "user-words.json");

        var data = Read();
        _never = new HashSet<string>(data.NeverConvert);
        _always = new HashSet<string>(data.AlwaysConvert);
        _accepted = new Dictionary<string, int>(data.Accepted);
    }

    public bool IsNeverConvert(string word)
    {
        lock (_gate)
        {
            return _never.Contains(word);
        }
    }

    public bool IsAlwaysConvert(string word)
    {
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
        word = word.Trim();
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
        lock (_gate)
        {
            _never.Remove(word);
        }

        ScheduleSave();
    }

    /// <summary>Counts a conversion the user did not undo; repeated acceptance promotes the word.</summary>
    public void RecordAccepted(string word)
    {
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

    private void ScheduleSave()
    {
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
            File.WriteAllText(_path, JsonSerializer.Serialize(snapshot, Options));
        }
        catch (IOException)
        {
        }
    }

    private UserData Read()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<UserData>(File.ReadAllText(_path)) ?? new UserData()
                : new UserData();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new UserData();
        }
    }

    private sealed class UserData
    {
        public List<string> NeverConvert { get; set; } = new();
        public List<string> AlwaysConvert { get; set; } = new();
        public Dictionary<string, int> Accepted { get; set; } = new();
    }
}
