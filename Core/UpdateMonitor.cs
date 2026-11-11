namespace KeyFlip.Core;

public sealed record UpdateStatus(
    Version CurrentVersion,
    Version? LatestVersion,
    string? LatestTag,
    string ReleaseUrl,
    bool IsUpdateAvailable,
    bool IsChecking,
    DateTimeOffset? LastCheckedUtc,
    bool LastCheckFailed)
{
    public string FormattedCurrentVersion => UpdateChecker.FormatVersion(CurrentVersion);
}

public sealed class UpdateMonitor : IDisposable
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan PollPeriod = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan NotificationCooldown = TimeSpan.FromHours(24);

    private readonly AppSettings _settings;
    private readonly Action _saveSettings;
    private readonly Func<CancellationToken, Task<UpdateCheckResult>> _checkAsync;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly SynchronizationContext? _uiContext;
    private readonly UpdateChecker? _ownedChecker;
    private readonly CancellationTokenSource _cts = new();
    private System.Threading.Timer? _timer;
    private bool _checking;
    private bool _disposed;

    public UpdateStatus Status { get; private set; }

    public event Action<UpdateStatus>? StateChanged;
    public event Action<UpdateStatus>? NewReleaseDiscovered;

    public UpdateMonitor(
        AppSettings settings,
        Action saveSettings,
        SynchronizationContext? uiContext = null,
        UpdateChecker? checker = null,
        Func<CancellationToken, Task<UpdateCheckResult>>? checkFunc = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        _settings = settings;
        _saveSettings = saveSettings;
        _uiContext = uiContext;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);

        if (checkFunc is not null)
        {
            _checkAsync = checkFunc;
            Status = BuildInitialStatus(checker?.CurrentVersion ?? UpdateChecker.ReadAssemblyVersion());
        }
        else
        {
            var effectiveChecker = checker ?? (_ownedChecker = new UpdateChecker());
            _checkAsync = effectiveChecker.CheckAsync;
            Status = BuildInitialStatus(effectiveChecker.CurrentVersion);
        }
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _timer ??= new System.Threading.Timer(OnTimerCallback, null, StartupDelay, PollPeriod);
    }

    public void NotifySettingsChanged()
    {
        if (_disposed)
        {
            return;
        }

        _settings.UpdateCheckIntervalHours = UpdateChecker.NormalizeIntervalHours(_settings.UpdateCheckIntervalHours);
        if (_settings.CheckForUpdates && IsAutoCheckDue())
        {
            _ = CheckNowAsync(manual: false);
        }
    }

    public bool IsAutoCheckDue()
    {
        if (!_settings.CheckForUpdates)
        {
            return false;
        }

        if (_settings.LastUpdateCheckUtc is null)
        {
            return true;
        }

        var interval = TimeSpan.FromHours(UpdateChecker.NormalizeIntervalHours(_settings.UpdateCheckIntervalHours));
        return _utcNow() - _settings.LastUpdateCheckUtc.Value >= interval;
    }

    public async Task<UpdateStatus> CheckNowAsync(bool manual = true)
    {
        if (_disposed || _checking)
        {
            return Status;
        }

        _checking = true;
        Status = Status with { IsChecking = true, LastCheckFailed = false };
        StateChanged?.Invoke(Status);

        UpdateCheckResult? result = null;
        try
        {
            result = await _checkAsync(_cts.Token);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            result = null;
        }
        finally
        {
            _checking = false;
        }

        if (!_disposed && result is not null)
        {
            ApplyResult(result, manual);
        }

        return Status;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        _timer?.Dispose();
        _timer = null;
        _ownedChecker?.Dispose();
        _cts.Dispose();
    }

    private UpdateStatus BuildInitialStatus(Version currentVersion)
    {
        var hasCachedUpdate = UpdateChecker.TryParseVersion(_settings.LatestKnownVersion, out var known, out var isPre)
            && !isPre
            && known > currentVersion;

        var url = hasCachedUpdate && UpdateChecker.IsTrustedReleaseUrl(_settings.LatestKnownUrl)
            ? _settings.LatestKnownUrl!
            : UpdateChecker.ReleasesPageUrl;

        return new UpdateStatus(
            currentVersion,
            hasCachedUpdate ? known : null,
            hasCachedUpdate ? _settings.LatestKnownVersion : null,
            url,
            hasCachedUpdate,
            false,
            _settings.LastUpdateCheckUtc,
            false);
    }

    private void OnTimerCallback(object? _)
    {
        if (_disposed || !_settings.CheckForUpdates)
        {
            return;
        }

        void Run()
        {
            if (!_disposed && IsAutoCheckDue())
            {
                _ = CheckNowAsync(manual: false);
            }
        }

        if (_uiContext is not null)
        {
            _uiContext.Post(_ => Run(), null);
        }
        else
        {
            Run();
        }
    }

    private void ApplyResult(UpdateCheckResult result, bool manual)
    {
        var now = _utcNow();
        if (!result.Succeeded)
        {
            Status = Status with
            {
                IsChecking = false,
                LastCheckFailed = true
            };
            StateChanged?.Invoke(Status);
            return;
        }

        _settings.LastUpdateCheckUtc = now;

        if (result.IsUpdateAvailable && result.LatestVersion is not null && result.LatestTag is not null)
        {
            _settings.LatestKnownVersion = result.LatestTag;
            _settings.LatestKnownUrl = result.ReleaseUrl;

            var shouldNotify = ShouldNotify(result.LatestTag, now, manual);
            if (shouldNotify)
            {
                _settings.LastNotifiedVersion = result.LatestTag;
                _settings.LastNotifiedUtc = now;
            }

            _saveSettings();

            Status = new UpdateStatus(
                result.CurrentVersion,
                result.LatestVersion,
                result.LatestTag,
                result.ReleaseUrl,
                true,
                false,
                now,
                false);

            StateChanged?.Invoke(Status);
            if (shouldNotify)
            {
                NewReleaseDiscovered?.Invoke(Status);
            }

            return;
        }

        _settings.LatestKnownVersion = null;
        _settings.LatestKnownUrl = null;
        _saveSettings();

        Status = new UpdateStatus(
            result.CurrentVersion,
            result.LatestVersion,
            result.LatestTag,
            UpdateChecker.ReleasesPageUrl,
            false,
            false,
            now,
            false);

        StateChanged?.Invoke(Status);
    }

    private bool ShouldNotify(string tag, DateTimeOffset now, bool manual)
    {
        if (manual)
        {
            return true;
        }

        if (!string.Equals(_settings.LastNotifiedVersion, tag, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return _settings.LastNotifiedUtc is null || now - _settings.LastNotifiedUtc.Value >= NotificationCooldown;
    }
}
