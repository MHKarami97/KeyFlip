using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace KeyFlip.UI;

public static class Theme
{
    private static readonly PrivateFontCollection Fonts = new();
    private static readonly string Family = LoadFamily();

    public static readonly Color Background = Color.FromArgb(24, 24, 27);
    public static readonly Color Surface = Color.FromArgb(39, 39, 42);
    public static readonly Color Border = Color.FromArgb(63, 63, 70);
    public static readonly Color Accent = Color.FromArgb(99, 102, 241);
    public static readonly Color AccentSoft = Color.FromArgb(38, 99, 102, 241);
    public static readonly Color Success = Color.FromArgb(74, 222, 128);
    public static readonly Color Danger = Color.FromArgb(248, 113, 113);
    public static readonly Color Text = Color.FromArgb(244, 244, 245);
    public static readonly Color Muted = Color.FromArgb(161, 161, 170);

    public static Font Create(float size, FontStyle style = FontStyle.Regular) =>
        new(Family, size, style, GraphicsUnit.Point);

    public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var d = Math.Max(2, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static string LoadFamily()
    {
        var assembly = typeof(Theme).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            var data = new byte[stream.Length];
            stream.ReadExactly(data);
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                Fonts.AddMemoryFont(handle.AddrOfPinnedObject(), data.Length);
            }
            finally
            {
                handle.Free();
            }
        }

        return Fonts.Families.FirstOrDefault(f => f.Name.Contains("Vazir", StringComparison.OrdinalIgnoreCase))?.Name
               ?? "Segoe UI";
    }
}
