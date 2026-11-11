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
    private readonly StringBuilder _classNameBuffer = new(64);
    private IntPtr _cachedWindow = IntPtr.Zero;
    private string _cachedName = string.Empty;

    public (IntPtr Window, IntPtr FocusWindow) GetFocusHandles()
    {
        var window = NativeMethods.GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return (IntPtr.Zero, IntPtr.Zero);
        }

        var thread = NativeMethods.GetWindowThreadProcessId(window, out _);
        var info = new NativeMethods.GUITHREADINFO { cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
        var focus = NativeMethods.GetGUIThreadInfo(thread, ref info) ? info.hwndFocus : IntPtr.Zero;
        return (window, focus);
    }

    public bool ShouldIgnore(IEnumerable<string> excludedApps)
    {
        var window = NativeMethods.GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return false;
        }

        var name = GetProcessName(window);
        return string.Equals(name, _selfName, StringComparison.OrdinalIgnoreCase)
               || excludedApps.Any(app => string.Equals(app, name, StringComparison.OrdinalIgnoreCase))
               || IsPasswordFieldFocused(window);
    }

    private string GetProcessName(IntPtr window)
    {
        if (window != _cachedWindow)
        {
            _cachedWindow = window;
            _cachedName = ResolveName(window);
        }

        return _cachedName;
    }

    private static string ResolveName(IntPtr window)
    {
        NativeMethods.GetWindowThreadProcessId(window, out var processId);
        if (processId == 0)
        {
            return string.Empty;
        }

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

    private bool IsPasswordFieldFocused(IntPtr window)
    {
        var thread = NativeMethods.GetWindowThreadProcessId(window, out _);
        var info = new NativeMethods.GUITHREADINFO { cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
        if (!NativeMethods.GetGUIThreadInfo(thread, ref info) || info.hwndFocus == IntPtr.Zero)
        {
            return false;
        }

        _classNameBuffer.Clear();
        NativeMethods.GetClassName(info.hwndFocus, _classNameBuffer, _classNameBuffer.Capacity);
        return _classNameBuffer.ToString().Contains("Edit", StringComparison.OrdinalIgnoreCase)
               && (NativeMethods.GetWindowLong(info.hwndFocus, GwlStyle) & EsPassword) != 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref int size);
}
