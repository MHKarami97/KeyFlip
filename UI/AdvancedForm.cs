using System.Diagnostics;
using KeyFlip.Core;

namespace KeyFlip.UI;

public sealed class AdvancedForm : Form
{
    private const int FormWidth = 420;
    private new const int Margin = 16;

    private readonly AppSettings _settings;
    private readonly UserDictionary _user;
    private readonly Action _onChanged;
    private readonly float _scale;

    public AdvancedForm(AppSettings settings, UserDictionary user, Action onChanged)
    {
        _settings = settings;
        _user = user;
        _onChanged = onChanged;
        _scale = GetScale();

        Text = "KeyFlip - تنظیمات پیشرفته";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = Theme.Create(10);
        ClientSize = new Size(S(FormWidth), S(480));
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);

        AddToggle("برگرداندن تبدیل با Backspace", 16, _settings.UndoOnBackspace, v => _settings.UndoOnBackspace = v);
        AddToggle("استفاده از زمینه کلمات قبلی", 52, _settings.UseContext, v => _settings.UseContext = v);
        AddAppsPanel(100);
        AddWordsPanel(284);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Dwm.UseDarkTitleBar(Handle);
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
        var panel = new EditableListPanel(
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
        Controls.Add(panel);
    }

    private void AddWordsPanel(int top)
    {
        var panel = new EditableListPanel(
            "کلماتی که هرگز تبدیل نشوند (همان‌طور که تایپ می‌شوند)",
            FormWidth - Margin * 2,
            _scale,
            () => _user.GetNeverConvert(),
            word => _user.AddNeverConvert(word.ToLowerInvariant()),
            word => _user.RemoveNeverConvert(word))
        {
            Location = new Point(S(Margin), S(top))
        };
        Controls.Add(panel);
    }

    private static IEnumerable<string> GetRunningProcessNames() =>
        Process.GetProcesses()
            .Where(process => process.MainWindowHandle != IntPtr.Zero)
            .Select(process => process.ProcessName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static float GetScale()
    {
        using var graphics = Graphics.FromHwnd(IntPtr.Zero);
        return graphics.DpiX / 96f;
    }

    private int S(int value) => (int)Math.Round(value * _scale);
}
