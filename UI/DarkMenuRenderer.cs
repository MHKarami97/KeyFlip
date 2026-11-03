using System.Drawing.Drawing2D;

namespace KeyFlip.UI;

public sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public const string DangerTag = "danger";

    public DarkMenuRenderer() : base(new DarkColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled)
        {
            return;
        }

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
        using var path = Theme.RoundedRect(bounds, 8);
        var color = Equals(e.Item.Tag, DangerTag) ? Color.FromArgb(38, Theme.Danger) : Theme.Surface;
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        var danger = Equals(e.Item.Tag, DangerTag);
        e.TextColor = e.Item.Selected
            ? (danger ? Theme.Danger : Theme.Text)
            : (danger ? Color.FromArgb(200, Theme.Danger) : Theme.Text);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(Theme.Border);
        var y = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Background;
        public override Color ImageMarginGradientBegin => Theme.Background;
        public override Color ImageMarginGradientMiddle => Theme.Background;
        public override Color ImageMarginGradientEnd => Theme.Background;
        public override Color MenuBorder => Theme.Border;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Theme.Surface;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Border;
    }
}
