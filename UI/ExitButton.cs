using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace KeyFlip.UI;

public sealed class ExitButton : Control
{
    private readonly float _scale;
    private readonly Font _font = Theme.Create(10);
    private bool _hover;
    private bool _pressed;

    public ExitButton(string text, float scale)
    {
        _scale = scale;
        Text = text;
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
        var fillColor = _pressed ? Color.FromArgb(70, Theme.Danger) : _hover ? Color.FromArgb(34, Theme.Danger) : Theme.Surface;
        using var fill = new SolidBrush(fillColor);
        g.FillPath(fill, path);
        using var border = new Pen(_hover ? Theme.Danger : Theme.Border);
        g.DrawPath(border, path);

        var foreground = _hover ? Theme.Danger : Theme.Muted;
        var textSize = g.MeasureString(Text, _font);
        var iconSize = (int)(14 * _scale);
        var gap = (int)(8 * _scale);
        var startX = (Width - (textSize.Width + gap + iconSize)) / 2f;

        using var textBrush = new SolidBrush(foreground);
        g.DrawString(Text, _font, textBrush, startX, (Height - textSize.Height) / 2f);
        DrawPowerIcon(g, foreground, (int)(startX + textSize.Width + gap), (Height - iconSize) / 2, iconSize);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _font.Dispose();
        }

        base.Dispose(disposing);
    }

    private void DrawPowerIcon(Graphics g, Color color, int x, int y, int size)
    {
        using var pen = new Pen(color, Math.Max(1.5f, 1.6f * _scale))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawArc(pen, x, y + 1, size, size, -50, 280);
        var centerX = x + size / 2f;
        g.DrawLine(pen, centerX, y - 1, centerX, y + size * 0.45f);
    }
}
