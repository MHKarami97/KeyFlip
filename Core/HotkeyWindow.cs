using System.Runtime.InteropServices;

namespace KeyFlip.Core;

/// <summary>Message-only window that owns the global manual-mode hotkey.</summary>
public sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x4B46;
    private const uint ModNoRepeat = 0x4000;
    private static readonly IntPtr MessageOnlyParent = new(-3);

    private bool _registered;

    public HotkeyWindow()
    {
        CreateHandle(new CreateParams { Parent = MessageOnlyParent });
    }

    public event Action? Pressed;

    public bool Register(uint modifiers, uint key)
    {
        Unregister();
        _registered = RegisterHotKey(Handle, HotkeyId, modifiers | ModNoRepeat, key);
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        UnregisterHotKey(Handle, HotkeyId);
        _registered = false;
    }

    public void Dispose()
    {
        Unregister();
        DestroyHandle();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam == HotkeyId)
        {
            Pressed?.Invoke();
        }

        base.WndProc(ref m);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
