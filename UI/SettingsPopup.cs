using System.Drawing.Drawing2D;
using System.Drawing.Text;
using KeyFlip.Core;

namespace KeyFlip.UI;

public sealed class SettingsPopup : Form
{
    private const int ClassDropShadow = 0x20000;
    private const int ExToolWindow = 0x80;
    private new const int Margin = 16;
    private const int PopupWidth = 340;
    private const int PopupHeight = 496;
    private const int PopupHeightWithUpdate = 546;
    private const int EdgeGap = 12;

    private readonly AppSettings _settings;
    private readonly Action _onChanged;
    private readonly Action _openAdvanced;
    private readonly Action _openReleasePage;
    private readonly float _scale;
    private readonly Bitmap? _logo;
    private readonly System.Windows.Forms.Timer _fade = new() { Interval = 12 };
    private ModeList? _modeList;
    private ActionButton? _advancedButton;
    private ActionButton? _exitButton;
    private UpdateBanner? _updateBanner;
    private long _lastHiddenTick;

    public SettingsPopup(AppSettings settings, Action onChanged, Action openAdvanced, Action openReleasePage)
    {
        _settings = settings;
        _onChanged = onChanged;
        _openAdvanced = openAdvanced;
        _openReleasePage = openReleasePage;
        _scale = GetScale();
        _logo = LoadLogo();

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Theme.Background;
        ClientSize = new Size(S(PopupWidth), S(PopupHeight));
        Font = Theme.Create(10);

        BuildHeader();
        BuildModes();
        BuildToggle("تغییر زبان کیبورد", 324, _settings.SwitchKeyboardLayout, v => _settings.SwitchKeyboardLayout = v);
        BuildToggle("اجرا با روشن شدن ویندوز", 360, _settings.RunAtStartup, v => _settings.RunAtStartup = v);
        BuildHotkeyRow(398);
        BuildActions();

        _fade.Tick += OnFadeTick;
        Deactivate += (_, _) => Hide();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ClassStyle |= ClassDropShadow;
            parameters.ExStyle |= ExToolWindow;
            return parameters;
        }
    }

    public bool WasHiddenRecently() => Environment.TickCount64 - _lastHiddenTick < 300;

    public void PresentNear(Point anchor)
    {
        Location = ComputeLocation(anchor);
        Opacity = 0;
        Show();
        Activate();
        _fade.Start();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Dwm.Apply(Handle);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        if (!Visible)
        {
            _lastHiddenTick = Environment.TickCount64;
            _fade.Stop();
        }

        base.OnVisibleChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        if (_logo is not null)
        {
            g.DrawImage(_logo, new Rectangle(Width - S(Margin) - S(36), S(18), S(36), S(36)));
        }

        using var pen = new Pen(Theme.Border);
        g.DrawLine(pen, S(Margin), S(312), Width - S(Margin), S(312));
        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fade.Dispose();
            _logo?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildHeader()
    {
        var right = Width - S(Margin) - S(36) - S(12);
        var versionWidth = S(76);
        Controls.Add(new Label
        {
            Text = $"v{UpdateChecker.FormatVersion(UpdateChecker.ReadAssemblyVersion())}",
            Font = Theme.Create(8.5f),
            ForeColor = Theme.Muted,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Bounds = new Rectangle(S(Margin), S(18), versionWidth, S(20))
        });
        Controls.Add(new Label
        {
            Text = "KeyFlip",
            Font = Theme.Create(14, FontStyle.Bold),
            ForeColor = Theme.Text,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Bounds = new Rectangle(S(Margin) + versionWidth + S(4), S(16), right - S(Margin) - versionWidth - S(4), S(24))
        });
        Controls.Add(new Label
        {
            Text = "اصلاح خودکار زبان تایپ",
            Font = Theme.Create(9),
            ForeColor = Theme.Muted,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Bounds = new Rectangle(S(Margin), S(40), right - S(Margin), S(20))
        });
    }

    private void BuildModes()
    {
        var items = new[]
        {
            new ModeItem(ConversionMode.Off, "خاموش", "بدون هیچ تغییری"),
            new ModeItem(ConversionMode.ToEnglish, "انگلیسی", "فقط تبدیل فارسی به انگلیسی"),
            new ModeItem(ConversionMode.ToPersian, "فارسی", "فقط تبدیل انگلیسی به فارسی"),
            new ModeItem(ConversionMode.Auto, "خودکار", "تشخیص و تبدیل در هر دو جهت"),
            new ModeItem(ConversionMode.Manual, "دستی", ManualSubtitle())
        };

        _modeList = new ModeList(items, _settings.Mode, _scale) { Location = new Point(S(Margin), S(70)) };
        _modeList.ModeSelected += mode =>
        {
            _settings.Mode = mode;
            _onChanged();
        };
        Controls.Add(_modeList);
    }

    private void BuildToggle(string text, int top, bool value, Action<bool> setter)
    {
        Controls.Add(CreateRowLabel(text, top, S(52)));

        var toggle = new ToggleSwitch
        {
            Checked = value,
            BackColor = Theme.Background,
            Size = new Size(S(40), S(22)),
            Location = new Point(S(Margin), S(top) + S(3))
        };
        toggle.CheckedChanged += (_, _) =>
        {
            setter(toggle.Checked);
            _onChanged();
        };
        Controls.Add(toggle);
    }

    private void BuildHotkeyRow(int top)
    {
        var boxWidth = S(150);
        Controls.Add(CreateRowLabel("میانبر حالت دستی", top, boxWidth + S(8)));

        var box = new HotkeyBox(_settings.HotkeyModifiers, _settings.HotkeyKey, _scale)
        {
            BackColor = Theme.Background,
            Bounds = new Rectangle(S(Margin), S(top) + S(1), boxWidth, S(28))
        };
        box.HotkeyChanged += (modifiers, key) =>
        {
            _settings.HotkeyModifiers = modifiers;
            _settings.HotkeyKey = key;
            _modeList?.SetSubtitle(ConversionMode.Manual, ManualSubtitle());
            _onChanged();
        };
        Controls.Add(box);
    }

    public void SetUpdateStatus(UpdateStatus status)
    {
        if (_updateBanner is null || _advancedButton is null || _exitButton is null)
        {
            return;
        }

        var showUpdate = status.IsUpdateAvailable && !string.IsNullOrWhiteSpace(status.LatestTag);
        if (showUpdate)
        {
            _updateBanner.Text = $"نسخه جدید {status.LatestTag} منتشر شد — دریافت";
        }

        _updateBanner.Visible = showUpdate;
        var buttonsTop = S(showUpdate ? 490 : 440);
        _advancedButton.Top = buttonsTop;
        _exitButton.Top = buttonsTop;

        var targetHeight = S(showUpdate ? PopupHeightWithUpdate : PopupHeight);
        if (ClientSize.Height != targetHeight)
        {
            var delta = targetHeight - ClientSize.Height;
            ClientSize = new Size(S(PopupWidth), targetHeight);
            if (Visible)
            {
                var work = Screen.FromControl(this).WorkingArea;
                Top = ClampSafe(Top - delta, work.Top + S(EdgeGap), work.Bottom - Height - S(EdgeGap));
            }
        }

        Invalidate();
    }

    private void BuildActions()
    {
        var contentWidth = PopupWidth - Margin * 2;
        var buttonWidth = (contentWidth - 8) / 2;

        _updateBanner = new UpdateBanner(_scale)
        {
            BackColor = Theme.Background,
            Bounds = new Rectangle(S(Margin), S(438), S(contentWidth), S(42)),
            Visible = false
        };
        _updateBanner.Click += (_, _) =>
        {
            Hide();
            _openReleasePage();
        };
        Controls.Add(_updateBanner);

        _advancedButton = new ActionButton("تنظیمات پیشرفته", _scale, Theme.Accent)
        {
            BackColor = Theme.Background,
            Bounds = new Rectangle(S(Margin) + S(buttonWidth + 8), S(440), S(buttonWidth), S(40))
        };
        _advancedButton.Click += (_, _) => _openAdvanced();
        Controls.Add(_advancedButton);

        _exitButton = new ActionButton("خروج", _scale, Theme.Danger, powerIcon: true)
        {
            BackColor = Theme.Background,
            Bounds = new Rectangle(S(Margin), S(440), S(buttonWidth), S(40))
        };
        _exitButton.Click += (_, _) => Application.Exit();
        Controls.Add(_exitButton);
    }

    private Label CreateRowLabel(string text, int top, int reservedLeft) => new()
    {
        Text = text,
        ForeColor = Theme.Text,
        AutoSize = false,
        TextAlign = ContentAlignment.MiddleRight,
        Bounds = new Rectangle(S(Margin) + reservedLeft, S(top), S(PopupWidth - Margin * 2) - reservedLeft, S(30))
    };

    private string ManualSubtitle() =>
        $"با میانبر {HotkeyFormatter.Format(_settings.HotkeyModifiers, _settings.HotkeyKey)}";

    private Point ComputeLocation(Point anchor)
    {
        var screen = Screen.FromPoint(anchor);
        var work = screen.WorkingArea;
        var bounds = screen.Bounds;
        var gap = S(EdgeGap);

        var x = ClampSafe(anchor.X - Width / 2, work.Left + gap, work.Right - Width - gap);
        var y = ClampSafe(anchor.Y - Height - gap * 2, work.Top + gap, work.Bottom - Height - gap);

        if (work.Bottom < bounds.Bottom)
        {
            y = work.Bottom - Height - gap;
        }
        else if (work.Top > bounds.Top)
        {
            y = work.Top + gap;
        }
        else if (work.Right < bounds.Right || work.Left > bounds.Left)
        {
            x = work.Right < bounds.Right ? work.Right - Width - gap : work.Left + gap;
            y = ClampSafe(anchor.Y - Height / 2, work.Top + gap, work.Bottom - Height - gap);
        }

        return new Point(x, y);
    }

    private void OnFadeTick(object? sender, EventArgs e)
    {
        Opacity = Math.Min(1, Opacity + 0.2);
        if (Opacity >= 1)
        {
            _fade.Stop();
        }
    }

    private static Bitmap? LoadLogo()
    {
        if (Environment.ProcessPath is null)
        {
            return null;
        }

        using var icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath);
        return icon?.ToBitmap();
    }

    private static float GetScale()
    {
        using var graphics = Graphics.FromHwnd(IntPtr.Zero);
        return graphics.DpiX / 96f;
    }

    private static int ClampSafe(int value, int min, int max) => max < min ? min : Math.Clamp(value, min, max);

    private int S(int value) => (int)Math.Round(value * _scale);

    private sealed class UpdateBanner : Control
    {
        private readonly float _scale;
        private readonly Font _font = Theme.Create(9.5f);
        private bool _hover;
        private bool _pressed;

        public UpdateBanner(float scale)
        {
            _scale = scale;
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _pressed = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent?.BackColor ?? Theme.Background);

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Theme.RoundedRect(bounds, (int)(10 * _scale));
            var fillAlpha = _pressed ? 55 : _hover ? 38 : 24;
            using var fill = new SolidBrush(Color.FromArgb(fillAlpha, Theme.Success));
            g.FillPath(fill, path);

            using var border = new Pen(Color.FromArgb(_hover ? 230 : 160, Theme.Success));
            g.DrawPath(border, path);

            var textBounds = new Rectangle((int)(10 * _scale), 0, Width - (int)(20 * _scale), Height);
            TextRenderer.DrawText(
                g,
                Text,
                _font,
                textBounds,
                Theme.Success,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _font.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
