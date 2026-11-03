using System.Runtime.InteropServices;

namespace KeyFlip.UI;

internal static class Dwm
{
    private const int UseImmersiveDarkMode = 20;
    private const int WindowCornerPreference = 33;
    private const int RoundCorners = 2;

    /// <summary>Asks Windows 11 to round the window corners; ignored on older systems.</summary>
    public static void Apply(IntPtr handle)
    {
        var preference = RoundCorners;
        DwmSetWindowAttribute(handle, WindowCornerPreference, ref preference, sizeof(int));
    }

    public static void UseDarkTitleBar(IntPtr handle)
    {
        var enabled = 1;
        DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
