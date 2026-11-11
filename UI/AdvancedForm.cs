using System.Diagnostics;
using KeyFlip.Core;

namespace KeyFlip.UI;

public sealed class AdvancedForm : Form
{
    private const int FormWidth = 420;
    private new const int Margin = 16;

    private readonly AppSettings _settings;
    private readonly UserDictionary _user;
    private readonly UpdateMonitor _updates;
    private readonly Action _onChanged;
    private readonly Action _openKeyMapEditor;
    private readonly float _scale;
    private EditableListPanel? _appsPanel;
    private EditableListPanel? _wordsPanel;
    private Label? _updateStatusLabel;
    private ActionButton? _updateActionButton;
    private ActionButton? _intervalButton;
    private ContextMenuStrip? _intervalMenu;
    private Action<UpdateStatus>? _updateHandler;

    public AdvancedForm(
        AppSettings settings,
        UserDictionary user,
        UpdateMonitor updates,
        Action onChanged,
        Action openKeyMapEditor)
    {
        _settings = settings;
        _user = user;
        _updates = updates;
        _onChanged = onChanged;
        _openKeyMapEditor = openKeyMapEditor;
        _scale = GetScale();

        Text = "KeyFlip - تنظیمات پیشرفته";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.Create(10);
        ClientSize = new Size(S(FormWidth), S(634));
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);

        AddToggle("برگرداندن تبدیل با Backspace", 16, _settings.UndoOnBackspace, v => _settings.UndoOnBackspace = v);
        AddToggle("استفاده از زمینه کلمات قبلی", 52, _settings.UseContext, v => _settings.UseContext = v);
        AddToggle("حالت دستی: همه کلمات به زبان مقابل تبدیل شوند", 88, _settings.ManualConvertAllWords, v => _settings.ManualConvertAllWords = v);
        AddToggle("بررسی خودکار به‌روزرسانی از گیت‌هاب", 124, _settings.CheckForUpdates, v => _settings.CheckForUpdates = v);
        AddAppsPanel(168);
        AddWordsPanel(348);
        AddKeyMapButton(530);
        AddUpdateSection(580);

        _updateHandler = ApplyUpdateStatus;
        _updates.StateChanged += _updateHandler;
        ApplyUpdateStatus(_updates.Status);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Dwm.UseDarkTitleBar(Handle);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        if (Visible)
        {
            _appsPanel?.SetSuggestions(GetRunningProcessNames());
            _appsPanel?.Reload();
            _wordsPanel?.Reload();
            ApplyUpdateStatus(_updates.Status);
        }

        base.OnVisibleChanged(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_updateHandler is not null)
            {
                _updates.StateChanged -= _updateHandler;
                _updateHandler = null;
            }

            _intervalMenu?.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnFormClosing(e);
    }

    private void AddToggle(string text, int top, bool value, Action<bool> setter)
    {
        var contentWidth = FormWidth - Margin * 2;
        Controls.Add(new Label
        {
            Text = text,
            ForeColor = Theme.Text,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Bounds = new Rectangle(S(Margin) + S(52), S(top), S(contentWidth - 52), S(30))
        });

        var toggle = new ToggleSwitch
        {
            Checked = value,
            BackColor = Theme.Background,
            Size = new Size(S(40), S(22)),
            Location = new Point(S(Margin), S(top) + S(4))
        };
        toggle.CheckedChanged += (_, _) =>
        {
            setter(toggle.Checked);
            _onChanged();
        };
        Controls.Add(toggle);
    }

    private void AddAppsPanel(int top)
    {
        _appsPanel = new EditableListPanel(
            "برنامه‌هایی که تبدیل خودکار در آن‌ها غیرفعال است (نام پردازه)",
            FormWidth - Margin * 2,
            _scale,
            () => _settings.ExcludedApps,
            name =>
            {
                var normalized = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
                if (!_settings.ExcludedApps.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    _settings.ExcludedApps = new List<string>(_settings.ExcludedApps) { normalized };
                    _onChanged();
                }
            },
            name =>
            {
                _settings.ExcludedApps = _settings.ExcludedApps
                    .Where(app => !string.Equals(app, name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                _onChanged();
            },
            GetRunningProcessNames())
        {
            Location = new Point(S(Margin), S(top))
        };
        Controls.Add(_appsPanel);
    }

    private void AddWordsPanel(int top)
    {
        _wordsPanel = new EditableListPanel(
            "کلماتی که هرگز تبدیل نشوند (همان‌طور که تایپ می‌شوند)",
            FormWidth - Margin * 2,
            _scale,
            () => _user.GetNeverConvert(),
            word => _user.AddNeverConvert(word),
            word => _user.RemoveNeverConvert(word))
        {
            Location = new Point(S(Margin), S(top))
        };
        Controls.Add(_wordsPanel);
    }

    private void AddKeyMapButton(int top)
    {
        var button = new ActionButton("ویرایش نگاشت کلیدهای کیبورد", _scale, Theme.Accent)
        {
            BackColor = Theme.Background,
            Bounds = new Rectangle(S(Margin), S(top), S(FormWidth - Margin * 2), S(38))
        };
        button.Click += (_, _) => _openKeyMapEditor();
        Controls.Add(button);
    }

    private void AddUpdateSection(int top)
    {
        var contentWidth = FormWidth - Margin * 2;
        var actionWidth = 108;
        var intervalWidth = 82;
        var gap = 6;

        _updateActionButton = new ActionButton("بررسی نسخه", _scale, Theme.Accent)
        {
            BackColor = Theme.Background,
            Bounds = new Rectangle(S(Margin), S(top), S(actionWidth), S(36))
        };
        _updateActionButton.Click += async (_, _) =>
        {
            if (_updates.Status.IsChecking)
            {
                return;
            }

            if (_updates.Status.IsUpdateAvailable)
            {
                UpdateChecker.OpenReleasePage(_updates.Status.ReleaseUrl);
                return;
            }

            await _updates.CheckNowAsync(manual: true);
        };
        Controls.Add(_updateActionButton);

        _intervalButton = new ActionButton(FormatInterval(_settings.UpdateCheckIntervalHours), _scale, Theme.Accent)
        {
            BackColor = Theme.Background,
            Bounds = new Rectangle(S(Margin) + S(actionWidth + gap), S(top), S(intervalWidth), S(36))
        };
        _intervalButton.Click += (_, _) => ShowIntervalMenu();
        Controls.Add(_intervalButton);

        var labelLeft = S(Margin + actionWidth + gap + intervalWidth + gap);
        _updateStatusLabel = new Label
        {
            ForeColor = Theme.Muted,
            Font = Theme.Create(9),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Bounds = new Rectangle(labelLeft, S(top), S(Margin + contentWidth) - labelLeft, S(36))
        };
        Controls.Add(_updateStatusLabel);
    }

    private void ShowIntervalMenu()
    {
        _intervalMenu ??= BuildIntervalMenu();
        if (_intervalButton is not null)
        {
            _intervalMenu.Show(_intervalButton, new Point(0, _intervalButton.Height));
        }
    }

    private ContextMenuStrip BuildIntervalMenu()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            ShowImageMargin = false,
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            Font = Theme.Create(9.5f),
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(4)
        };

        foreach (var hours in UpdateChecker.SupportedIntervalHours)
        {
            var captured = hours;
            var item = new ToolStripMenuItem(FormatInterval(captured)) { Padding = new Padding(8, 5, 8, 5) };
            item.Click += (_, _) =>
            {
                _settings.UpdateCheckIntervalHours = captured;
                _intervalButton?.SetAppearance(FormatInterval(captured), Theme.Accent);
                _onChanged();
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());
        var recheck = new ToolStripMenuItem("بررسی مجدد اکنون") { Padding = new Padding(8, 5, 8, 5) };
        recheck.Click += async (_, _) => await _updates.CheckNowAsync(manual: true);
        menu.Items.Add(recheck);
        menu.HandleCreated += (_, _) => Dwm.Apply(menu.Handle);
        return menu;
    }

    private void ApplyUpdateStatus(UpdateStatus status)
    {
        if (IsDisposed || _updateStatusLabel is null || _updateActionButton is null)
        {
            return;
        }

        if (status.IsChecking)
        {
            _updateStatusLabel.Text = "در حال بررسی نسخه جدید...";
            _updateStatusLabel.ForeColor = Theme.Muted;
            _updateActionButton.SetAppearance("در حال بررسی", Theme.Accent);
            return;
        }

        if (status.IsUpdateAvailable)
        {
            _updateStatusLabel.Text = $"نسخه جدید {status.LatestTag} منتشر شد!";
            _updateStatusLabel.ForeColor = Theme.Success;
            _updateActionButton.SetAppearance("دریافت نسخه", Theme.Success);
            return;
        }

        _updateStatusLabel.ForeColor = Theme.Muted;
        _updateStatusLabel.Text = status.LastCheckFailed
            ? $"نسخه {status.FormattedCurrentVersion} (خطا در اتصال)"
            : status.LastCheckedUtc is not null
                ? $"نسخه {status.FormattedCurrentVersion} (به‌روز است)"
                : $"نسخه فعلی: {status.FormattedCurrentVersion}";

        _updateActionButton.SetAppearance("بررسی نسخه", Theme.Accent);
    }

    private static string FormatInterval(int hours) => $"هر {UpdateChecker.NormalizeIntervalHours(hours)} ساعت";

    private static IEnumerable<string> GetRunningProcessNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    names.Add(process.ProcessName);
                }
            }
        }

        return names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static float GetScale()
    {
        using var graphics = Graphics.FromHwnd(IntPtr.Zero);
        return graphics.DpiX / 96f;
    }

    private int S(int value) => (int)Math.Round(value * _scale);
}
