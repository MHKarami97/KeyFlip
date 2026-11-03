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
            Visible = true
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _popup.ShowNearTray();
            }
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("خروج", null, (_, _) => Application.Exit());
        _tray.ContextMenuStrip = menu;
    }

    protected override void ExitThreadCore()
    {
        _tray.Visible = false;
        _tray.Dispose();
        _engine.Dispose();
        base.ExitThreadCore();
    }

    private void OnSettingsChanged()
    {
        _store.Save(_settings);
        _startup.Apply(_settings.RunAtStartup);
    }
}
