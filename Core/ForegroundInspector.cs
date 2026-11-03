using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyFlip.Core;

/// <summary>Identifies the foreground application and whether a classic password edit box has focus.</summary>
public sealed class ForegroundInspector
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int GwlStyle = -16;
    private const int EsPassword = 0x20;

    private readonly string _selfName = Process.GetCurrentProcess().ProcessName;
    private IntPtr _cachedWindow = IntPtr.Zero;
    private string _cachedName = string.Empty;

    public bool ShouldIgnore(IEnumerable<string> excludedApps)
    {
        var name = GetProcessName();
        return string.Equals(name, _selfName, StringComparison.OrdinalIgnoreCase)
               || excludedApps.Any(app => string.Equals(app, name, StringComparison.OrdinalIgnoreCase))
               || IsPasswordFieldFocused();
    }

    private string GetProcessName()
    {
        var window = GetForegroundWindow();
        if (window != _cachedWindow)
        {
            _cachedWindow = window;
            _cachedName = ResolveName(window);
        }

        return _cachedName;
    }

    private static string ResolveName(IntPtr window)
    {
        GetWindowThreadProcessId(window, out var processId);
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            var builder = new StringBuilder(1024);
            var size = builder.Capacity;
            return QueryFullProcessImageName(handle, 0, builder, ref size)
                ? Path.GetFileNameWithoutExtension(builder.ToString())
                : string.Empty;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private static bool IsPasswordFieldFocused()
    {
        var thread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var info = new GuiThreadInfo { cbSize = Marshal.SizeOf<GuiThreadInfo>() };
        if (!GetGUIThreadInfo(thread, ref info) || info.hwndFocus == IntPtr.Zero)
        {
            return false;
        }

        var className = new StringBuilder(64);
        GetClassName(info.hwndFocus, className, className.Capacity);
        return className.ToString().Contains("Edit", StringComparison.OrdinalIgnoreCase)
               && (GetWindowLong(info.hwndFocus, GwlStyle) & EsPassword) != 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public Rect rcCaret;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hWnd, int index);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref int size);
}
