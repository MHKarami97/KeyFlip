using KeyFlip.Core;

namespace KeyFlip.UI;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SettingsStore _store = new();
    private readonly StartupManager _startup = new();
    private readonly AppSettings _settings;
    private readonly ConversionEngine _engine;
    private readonly NotifyIcon _tray;
    private readonly SettingsPopup _popup;

    public TrayApplicationContext()
    {
        _settings = _store.Load();
        _engine = new ConversionEngine(_settings);
        _engine.Start();

        _popup = new SettingsPopup(_settings, OnSettingsChanged);
        _tray = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Text = "KeyFlip",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _tray.MouseClick += OnTrayClick;

        ApplyEngineMode();
    }

    protected override void ExitThreadCore()
    {
        _tray.Visible = false;
        _tray.Dispose();
        _popup.Dispose();
        _engine.Dispose();
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

        var exit = new ToolStripMenuItem("خروج") { Tag = DarkMenuRenderer.DangerTag, Padding = new Padding(10, 7, 10, 7) };
        exit.Click += (_, _) => Application.Exit();

        menu.Items.Add(settings);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);
        menu.HandleCreated += (_, _) => Dwm.Apply(menu.Handle);
        return menu;
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
        ApplyEngineMode();
    }

    private void ApplyEngineMode()
    {
        if (!_engine.ApplyMode())
        {
            _tray.ShowBalloonTip(4000, "KeyFlip", "میانبر انتخاب‌شده توسط برنامه دیگری اشغال شده است.", ToolTipIcon.Warning);
        }
    }
}
