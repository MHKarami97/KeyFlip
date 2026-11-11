using KeyFlip.Core;

namespace KeyFlip.UI;

public sealed class TrayApplicationContext : ApplicationContext
{
    private enum NotificationKind
    {
        None,
        Activated,
        UpdateAvailable
    }

    private readonly SettingsStore _store = new();
    private readonly StartupManager _startup = new();
    private readonly UserDictionary _user = new();
    private readonly KeyMapBuilder _keyMapBuilder = new();
    private readonly AppSettings _settings;
    private readonly ConversionEngine _engine;
    private readonly UpdateMonitor _updates;
    private readonly SynchronizationContext _uiContext;
    private readonly EventWaitHandle _activateEvent;
    private readonly RegisteredWaitHandle _activateWait;
    private readonly NotifyIcon _tray;
    private readonly SettingsPopup _popup;
    private ToolStripMenuItem? _updateMenuItem;
    private AdvancedForm? _advanced;
    private KeyMapForm? _keyMapEditor;
    private NotificationKind _lastNotification;
    private string? _lastNotificationUrl;
    private bool _disposed;

    public TrayApplicationContext(bool isStartupLaunch = false)
    {
        _settings = _store.Load();
        KeyMap.Replace(_keyMapBuilder.Build(_settings));

        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _updates = new UpdateMonitor(_settings, () => _store.Save(_settings), _uiContext);

        _engine = new ConversionEngine(_settings, _user);
        _engine.Start();

        _popup = new SettingsPopup(
            _settings,
            OnSettingsChanged,
            OpenAdvanced,
            () => UpdateChecker.OpenReleasePage(_updates.Status.ReleaseUrl));
        _tray = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Text = "KeyFlip",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _tray.MouseClick += OnTrayClick;
        _tray.BalloonTipClicked += OnBalloonTipClicked;

        _updates.StateChanged += OnUpdateStateChanged;
        _updates.NewReleaseDiscovered += OnNewReleaseDiscovered;
        _popup.SetUpdateStatus(_updates.Status);
        OnUpdateStateChanged(_updates.Status);

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ActivateEventName);
        _activateWait = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent,
            (_, timedOut) =>
            {
                if (!timedOut && !_disposed)
                {
                    _uiContext.Post(_ => ShowActivatedNotification(alreadyRunning: true), null);
                }
            },
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        var hotkeyOk = ApplyEngineMode();
        if (!isStartupLaunch && hotkeyOk)
        {
            _uiContext.Post(_ => ShowActivatedNotification(alreadyRunning: false), null);
        }

        _updates.Start();
    }

    protected override void ExitThreadCore()
    {
        _disposed = true;
        _activateWait.Unregister(null);
        _activateEvent.Dispose();
        _updates.StateChanged -= OnUpdateStateChanged;
        _updates.NewReleaseDiscovered -= OnNewReleaseDiscovered;
        _updates.Dispose();

        _tray.Visible = false;
        var icon = _tray.Icon;
        _tray.Icon = null;
        icon?.Dispose();
        _tray.Dispose();
        _popup.Dispose();
        _advanced?.Dispose();
        _keyMapEditor?.Dispose();
        _engine.Dispose();
        _user.Flush();
        base.ExitThreadCore();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            ShowImageMargin = false,
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            Font = Theme.Create(10),
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(4),
            MinimumSize = new Size(170, 0)
        };

        var settings = new ToolStripMenuItem("تنظیمات") { Padding = new Padding(10, 7, 10, 7) };
        settings.Click += (_, _) => _popup.PresentNear(Cursor.Position);

        var advanced = new ToolStripMenuItem("تنظیمات پیشرفته") { Padding = new Padding(10, 7, 10, 7) };
        advanced.Click += (_, _) => OpenAdvanced();

        _updateMenuItem = new ToolStripMenuItem("بررسی به‌روزرسانی") { Padding = new Padding(10, 7, 10, 7) };
        _updateMenuItem.Click += async (_, _) => await HandleUpdateMenuClickAsync();

        var exit = new ToolStripMenuItem("خروج") { Tag = DarkMenuRenderer.DangerTag, Padding = new Padding(10, 7, 10, 7) };
        exit.Click += (_, _) => Application.Exit();

        menu.Items.Add(settings);
        menu.Items.Add(advanced);
        menu.Items.Add(_updateMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);
        menu.HandleCreated += (_, _) => Dwm.Apply(menu.Handle);
        return menu;
    }

    private void OpenAdvanced()
    {
        _advanced ??= new AdvancedForm(_settings, _user, _updates, OnSettingsChanged, OpenKeyMapEditor);
        _advanced.Show();
        _advanced.Activate();
    }

    private void OpenKeyMapEditor()
    {
        _keyMapEditor ??= new KeyMapForm(_settings, OnKeyMapChanged);
        _keyMapEditor.Show();
        _keyMapEditor.Activate();
    }

    private void OnTrayClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_popup.Visible)
        {
            _popup.Hide();
            return;
        }

        // The popup hides itself on deactivation, which happens before this click event fires.
        if (!_popup.WasHiddenRecently())
        {
            _popup.PresentNear(Cursor.Position);
        }
    }

    private void OnSettingsChanged()
    {
        _store.Save(_settings);
        _startup.Apply(_settings.RunAtStartup);
        _updates.NotifySettingsChanged();
        ApplyEngineMode();
    }

    private void OnKeyMapChanged()
    {
        _store.Save(_settings);
        KeyMap.Replace(_keyMapBuilder.Build(_settings));
    }

    private bool ApplyEngineMode()
    {
        if (!_engine.ApplyMode())
        {
            ShowBalloon(NotificationKind.None, "KeyFlip", "میانبر انتخاب‌شده توسط برنامه دیگری اشغال شده است.", ToolTipIcon.Warning);
            return false;
        }

        return true;
    }

    private void ShowActivatedNotification(bool alreadyRunning)
    {
        if (_disposed)
        {
            return;
        }

        var message = alreadyRunning
            ? "برنامه KeyFlip از قبل فعال است و آیکون آن در نوار وظیفه قرار دارد."
            : "برنامه KeyFlip فعال شد و آیکون آن در نوار وظیفه قرار دارد.";
        ShowBalloon(NotificationKind.Activated, "KeyFlip", message, ToolTipIcon.Info);
    }

    private void OnUpdateStateChanged(UpdateStatus status)
    {
        if (_disposed)
        {
            return;
        }

        _popup.SetUpdateStatus(status);

        if (_updateMenuItem is null)
        {
            return;
        }

        if (status.IsUpdateAvailable)
        {
            _updateMenuItem.Text = $"دریافت نسخه جدید ({status.LatestTag})";
            _updateMenuItem.Tag = DarkMenuRenderer.AccentTag;
            _updateMenuItem.Enabled = true;
        }
        else if (status.IsChecking)
        {
            _updateMenuItem.Text = "در حال بررسی به‌روزرسانی...";
            _updateMenuItem.Tag = null;
            _updateMenuItem.Enabled = false;
        }
        else
        {
            _updateMenuItem.Text = "بررسی به‌روزرسانی";
            _updateMenuItem.Tag = null;
            _updateMenuItem.Enabled = true;
        }
    }

    private void OnNewReleaseDiscovered(UpdateStatus status)
    {
        if (_disposed || !status.IsUpdateAvailable || string.IsNullOrWhiteSpace(status.LatestTag))
        {
            return;
        }

        ShowBalloon(
            NotificationKind.UpdateAvailable,
            "به‌روزرسانی KeyFlip",
            $"نسخه جدید {status.LatestTag} منتشر شد. برای دریافت کلیک کنید.",
            ToolTipIcon.Info,
            status.ReleaseUrl);
    }

    private async Task HandleUpdateMenuClickAsync()
    {
        if (_updates.Status.IsUpdateAvailable)
        {
            UpdateChecker.OpenReleasePage(_updates.Status.ReleaseUrl);
            return;
        }

        var status = await _updates.CheckNowAsync(manual: true);
        if (_disposed || status.IsUpdateAvailable)
        {
            return;
        }

        var message = status.LastCheckFailed
            ? "بررسی به‌روزرسانی ناموفق بود. اتصال اینترنت را بررسی کنید."
            : $"نسخه {status.FormattedCurrentVersion} آخرین نسخه موجود است.";
        ShowBalloon(NotificationKind.None, "KeyFlip", message, status.LastCheckFailed ? ToolTipIcon.Warning : ToolTipIcon.Info);
    }

    private void OnBalloonTipClicked(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        switch (_lastNotification)
        {
            case NotificationKind.UpdateAvailable:
                UpdateChecker.OpenReleasePage(_lastNotificationUrl);
                break;
            case NotificationKind.Activated:
                _popup.PresentNear(Cursor.Position);
                break;
        }
    }

    private void ShowBalloon(NotificationKind kind, string title, string text, ToolTipIcon icon, string? url = null)
    {
        if (_disposed)
        {
            return;
        }

        _lastNotification = kind;
        _lastNotificationUrl = url;
        _tray.ShowBalloonTip(5000, title, text, icon);
    }
}
