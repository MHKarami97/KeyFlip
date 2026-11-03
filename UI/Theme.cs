using System.Drawing.Text;

namespace KeyFlip.UI;

public static class Theme
{
    private static readonly PrivateFontCollection Fonts = new();
    private static readonly string Family = LoadFamily();

    public static readonly Color Background = Color.FromArgb(24, 24, 27);
    public static readonly Color Surface = Color.FromArgb(39, 39, 42);
    public static readonly Color Accent = Color.FromArgb(99, 102, 241);
    public static readonly Color Text = Color.FromArgb(244, 244, 245);
    public static readonly Color Muted = Color.FromArgb(161, 161, 170);

    public static Font Create(float size, FontStyle style = FontStyle.Regular) =>
        new(Family, size, style, GraphicsUnit.Point);

    private static string LoadFamily()
    {
        var assembly = typeof(Theme).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            var data = new byte[stream.Length];
            stream.ReadExactly(data);
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(data, System.Runtime.InteropServices.GCHandleType.Pinned);
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
