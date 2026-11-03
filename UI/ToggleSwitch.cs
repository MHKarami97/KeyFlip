using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace KeyFlip.UI;

public sealed class ToggleSwitch : Control
{
    private bool _checked;

    public ToggleSwitch()
    {
        Size = new Size(40, 22);
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
    }

    public event EventHandler? CheckedChanged;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Checked
    {
        get => _checked;
        set
        {
            _checked = value;
            Invalidate();
        }
    }

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        CheckedChanged?.Invoke(this, EventArgs.Empty);
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background);

        using var track = new GraphicsPath();
        track.AddArc(0, 0, Height, Height, 90, 180);
        track.AddArc(Width - Height - 1, 0, Height, Height, 270, 180);
        track.CloseFigure();
        using var trackBrush = new SolidBrush(_checked ? Theme.Accent : Color.FromArgb(63, 63, 70));
        g.FillPath(trackBrush, track);

        var knobX = _checked ? Width - Height + 2 : 2;
        using var knobBrush = new SolidBrush(Color.White);
        g.FillEllipse(knobBrush, knobX, 2, Height - 4, Height - 4);
    }
}
