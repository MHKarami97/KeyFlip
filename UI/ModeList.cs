using System.Drawing.Drawing2D;
using System.Drawing.Text;
using KeyFlip.Core;

namespace KeyFlip.UI;

public sealed record ModeItem(ConversionMode Mode, string Title, string Subtitle);

public sealed class ModeList : Control
{
    private readonly IReadOnlyList<ModeItem> _items;
    private readonly float _scale;
    private readonly int _rowHeight;
    private readonly Font _titleFont = Theme.Create(10.5f);
    private readonly Font _subtitleFont = Theme.Create(8.5f);
    private ConversionMode _selected;
    private int _hover = -1;

    public ModeList(IReadOnlyList<ModeItem> items, ConversionMode selected, float scale)
    {
        _items = items;
        _selected = selected;
        _scale = scale;
        _rowHeight = Scale(46);
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        Size = new Size(Scale(308), _rowHeight * items.Count);
    }

    public event Action<ConversionMode>? ModeSelected;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var index = Math.Clamp(e.Y / _rowHeight, 0, _items.Count - 1);
        if (index != _hover)
        {
            _hover = index;
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        var index = e.Y / _rowHeight;
        if (e.Button == MouseButtons.Left && index >= 0 && index < _items.Count)
        {
            _selected = _items[index].Mode;
            Invalidate();
            ModeSelected?.Invoke(_selected);
        }

        base.OnMouseClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Parent?.BackColor ?? Theme.Background);

        using var format = new StringFormat(StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap)
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };

        for (var i = 0; i < _items.Count; i++)
        {
            DrawRow(g, format, i);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _subtitleFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void DrawRow(Graphics g, StringFormat format, int index)
    {
        var item = _items[index];
        var selected = item.Mode == _selected;
        var row = new Rectangle(0, index * _rowHeight + Scale(2), Width - 1, _rowHeight - Scale(4));

        if (selected || index == _hover)
        {
            using var path = Theme.RoundedRect(row, Scale(10));
            using var fill = new SolidBrush(selected ? Theme.AccentSoft : Theme.Surface);
            g.FillPath(fill, path);
            if (selected)
            {
                using var border = new Pen(Theme.Accent, Math.Max(1f, _scale));
                g.DrawPath(border, path);
            }
        }

        var diameter = Scale(16);
        var radioX = row.Right - Scale(14) - diameter;
        var radioY = row.Top + (row.Height - diameter) / 2;
        using (var ring = new Pen(selected ? Theme.Accent : Color.FromArgb(82, 82, 91), Math.Max(1.5f, 1.6f * _scale)))
        {
            g.DrawEllipse(ring, radioX, radioY, diameter, diameter);
        }

        if (selected)
        {
            var inner = diameter / 2;
            using var dot = new SolidBrush(Theme.Accent);
            g.FillEllipse(dot, radioX + (diameter - inner) / 2, radioY + (diameter - inner) / 2, inner, inner);
        }

        var left = Scale(12);
        var width = radioX - Scale(12) - left;
        var half = (row.Height - Scale(6)) / 2f;
        using var titleBrush = new SolidBrush(Theme.Text);
        using var subtitleBrush = new SolidBrush(Theme.Muted);
        g.DrawString(item.Title, _titleFont, titleBrush, new RectangleF(left, row.Top + Scale(3), width, half), format);
        g.DrawString(item.Subtitle, _subtitleFont, subtitleBrush, new RectangleF(left, row.Top + Scale(3) + half, width, half), format);
    }

    private int Scale(int value) => (int)Math.Round(value * _scale);
}
