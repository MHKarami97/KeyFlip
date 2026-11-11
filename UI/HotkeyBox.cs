using System.Drawing.Drawing2D;
using System.Drawing.Text;
using KeyFlip.Core;

namespace KeyFlip.UI;

/// <summary>Click-to-record control for choosing the manual-mode hotkey.</summary>
public sealed class HotkeyBox : Control
{
    private const uint AltBit = 1;
    private const uint CtrlBit = 2;
    private const uint ShiftBit = 4;

    private readonly float _scale;
    private readonly Font _font = Theme.Create(9.5f);
    private uint _modifiers;
    private uint _key;
    private bool _capturing;
    private bool _invalid;

    public HotkeyBox(uint modifiers, uint key, float scale)
    {
        _modifiers = modifiers;
        _key = key;
        _scale = scale;
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
    }

    public event Action<uint, uint>? HotkeyChanged;

    protected override void OnClick(EventArgs e)
    {
        _capturing = true;
        _invalid = false;
        Focus();
        Invalidate();
        base.OnClick(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        _capturing = false;
        _invalid = false;
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!_capturing)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        var code = keyData & Keys.KeyCode;
        if (code is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            return true;
        }

        if (code == Keys.Escape && (keyData & Keys.Modifiers) == Keys.None)
        {
            _capturing = false;
            Invalidate();
            return true;
        }

        var bits = ToBits(keyData);
        if ((bits & (AltBit | CtrlBit)) == 0)
        {
            _invalid = true;
            Invalidate();
            return true;
        }

        _modifiers = bits;
        _key = (uint)code;
        _capturing = false;
        _invalid = false;
        Invalidate();
        HotkeyChanged?.Invoke(_modifiers, _key);
        return true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Parent?.BackColor ?? Theme.Background);

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Theme.RoundedRect(bounds, (int)(8 * _scale));
        using var fill = new SolidBrush(_capturing ? Theme.AccentSoft : Theme.Surface);
        g.FillPath(fill, path);
        using var border = new Pen(_invalid ? Theme.Danger : _capturing ? Theme.Accent : Theme.Border);
        g.DrawPath(border, path);

        var text = _capturing
            ? (_invalid ? "Ctrl یا Alt لازم است" : "کلیدها را بزنید…")
            : HotkeyFormatter.Format(_modifiers, _key);
        var color = _invalid ? Theme.Danger : _capturing ? Theme.Muted : Theme.Text;
        using var brush = new SolidBrush(color);
        var flags = _capturing ? StringFormatFlags.DirectionRightToLeft : 0;
        using var format = new StringFormat(flags) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, _font, brush, new RectangleF(0, 0, Width, Height), format);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _font.Dispose();
        }

        base.Dispose(disposing);
    }

    private static uint ToBits(Keys keyData)
    {
        uint bits = 0;
        if ((keyData & Keys.Alt) != 0)
        {
            bits |= AltBit;
        }

        if ((keyData & Keys.Control) != 0)
        {
            bits |= CtrlBit;
        }

        if ((keyData & Keys.Shift) != 0)
        {
            bits |= ShiftBit;
        }

        return bits;
    }
}
