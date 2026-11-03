using System.Drawing.Drawing2D;
using KeyFlip.Core;

namespace KeyFlip.UI;

public sealed class SettingsPopup : Form
{
    private const int FormWidth = 360;
    private const int FormHeight = 300;
    private const int Margin = 16;
    private const int ContentWidth = FormWidth - Margin * 2;
    private const int ModeButtonWidth = 76;
    private const int ModeButtonGap = 8;

    private readonly AppSettings _settings;
    private readonly Action _onChanged;
    private readonly Dictionary<ConversionMode, Button> _modeButtons = new();

    public SettingsPopup(AppSettings settings, Action onChanged)
    {
        _settings = settings;
        _onChanged = onChanged;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Theme.Background;
        ClientSize = new Size(FormWidth, FormHeight);
        RightToLeft = RightToLeft.Yes;
        Font = Theme.Create(10);

        BuildTitle();
        BuildModes();
        BuildToggle("تغییر زبان کیبورد", 168, _settings.SwitchKeyboardLayout, v => _settings.SwitchKeyboardLayout = v);
        BuildToggle("اجرا با روشن شدن ویندوز", 208, _settings.RunAtStartup, v => _settings.RunAtStartup = v);
        BuildExit();
        RefreshModes();
        Deactivate += (_, _) => Hide();
        Region = RoundedRegion(Width, Height, 16);
    }

    public void ShowNearTray()
    {
        var area = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(area.Right - Width - 12, area.Bottom - Height - 12);
        Show();
        Activate();
    }

    private void BuildTitle()
    {
        Controls.Add(new Label
        {
            Text = "KeyFlip",
            Font = Theme.Create(15, FontStyle.Bold),
            ForeColor = Theme.Text,
            AutoSize = false,
            Bounds = new Rectangle(Margin, 16, ContentWidth, 34)
        });
        Controls.Add(new Label
        {
            Text = "اصلاح خودکار زبان تایپ",
            ForeColor = Theme.Muted,
            AutoSize = false,
            Bounds = new Rectangle(Margin, 48, ContentWidth, 24)
        });
    }

    private void BuildModes()
    {
        var items = new (ConversionMode Mode, string Text)[]
        {
            (ConversionMode.Off, "خاموش"),
            (ConversionMode.ToEnglish, "انگلیسی"),
            (ConversionMode.ToPersian, "فارسی"),
            (ConversionMode.Auto, "خودکار")
        };

        var x = FormWidth - Margin - ModeButtonWidth;
        foreach (var item in items)
        {
            var mode = item.Mode;
            var button = new Button
            {
                Text = item.Text,
                FlatStyle = FlatStyle.Flat,
                Bounds = new Rectangle(x, 92, ModeButtonWidth, 40),
                Cursor = Cursors.Hand,
                Font = Theme.Create(9),
                Padding = Padding.Empty,
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += (_, _) =>
            {
                _settings.Mode = mode;
                RefreshModes();
                _onChanged();
            };
            _modeButtons[mode] = button;
            Controls.Add(button);
            x -= ModeButtonWidth + ModeButtonGap;
        }
    }

    private void BuildToggle(string text, int top, bool value, Action<bool> setter)
    {
        Controls.Add(new Label
        {
            Text = text,
            ForeColor = Theme.Text,
            AutoSize = false,
            Bounds = new Rectangle(Margin + 52, top, ContentWidth - 52, 26),
            TextAlign = ContentAlignment.MiddleRight
        });
        var toggle = new ToggleSwitch { Checked = value, Location = new Point(Margin, top + 2), BackColor = Theme.Background };
        toggle.CheckedChanged += (_, _) =>
        {
            setter(toggle.Checked);
            _onChanged();
        };
        Controls.Add(toggle);
    }

    private void BuildExit()
    {
        var exit = new Button
        {
            Text = "خروج",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Theme.Muted,
            BackColor = Theme.Surface,
            Bounds = new Rectangle(Margin, 248, ContentWidth, 36),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        exit.FlatAppearance.BorderSize = 0;
        exit.Click += (_, _) => Application.Exit();
        Controls.Add(exit);
    }

    private void RefreshModes()
    {
        foreach (var pair in _modeButtons)
        {
            var selected = pair.Key == _settings.Mode;
            pair.Value.BackColor = selected ? Theme.Accent : Theme.Surface;
            pair.Value.ForeColor = selected ? Color.White : Theme.Muted;
        }
    }

    private static Region RoundedRegion(int width, int height, int radius)
    {
        var d = radius * 2;
        using var path = new GraphicsPath();
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(width - d, 0, d, d, 270, 90);
        path.AddArc(width - d, height - d, d, d, 0, 90);
        path.AddArc(0, height - d, d, d, 90, 90);
        path.CloseFigure();
        return new Region(path);
    }
}
