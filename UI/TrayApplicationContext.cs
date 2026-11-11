using KeyFlip.Core;

namespace KeyFlip.UI;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SettingsStore _store = new();
    private readonly StartupManager _startup = new();
    private readonly UserDictionary _user = new();
    private readonly KeyMapBuilder _keyMapBuilder = new();
    private readonly AppSettings _settings;
    private readonly ConversionEngine _engine;
    private readonly NotifyIcon _tray;
    private readonly SettingsPopup _popup;
    private AdvancedForm? _advanced;
    private KeyMapForm? _keyMapEditor;

    public TrayApplicationContext()
    {
        _settings = _store.Load();
        KeyMap.Replace(_keyMapBuilder.Build(_settings));

        _engine = new ConversionEngine(_settings, _user);
        _engine.Start();

        _popup = new SettingsPopup(_settings, OnSettingsChanged, OpenAdvanced);
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

        var exit = new ToolStripMenuItem("خروج") { Tag = DarkMenuRenderer.DangerTag, Padding = new Padding(10, 7, 10, 7) };
        exit.Click += (_, _) => Application.Exit();

        menu.Items.Add(settings);
        menu.Items.Add(advanced);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);
        menu.HandleCreated += (_, _) => Dwm.Apply(menu.Handle);
        return menu;
    }

    private void OpenAdvanced()
    {
        _advanced ??= new AdvancedForm(_settings, _user, OnSettingsChanged, OpenKeyMapEditor);
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
        ApplyEngineMode();
    }

    private void OnKeyMapChanged()
    {
        _store.Save(_settings);
        KeyMap.Replace(_keyMapBuilder.Build(_settings));
    }

    private void ApplyEngineMode()
    {
        if (!_engine.ApplyMode())
        {
            _tray.ShowBalloonTip(4000, "KeyFlip", "میانبر انتخاب‌شده توسط برنامه دیگری اشغال شده است.", ToolTipIcon.Warning);
        }
    }
}
