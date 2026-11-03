using System.Runtime.InteropServices;

namespace KeyFlip.Core;

public sealed class KeyboardHook : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _callback;
    private IntPtr _handle = IntPtr.Zero;

    public KeyboardHook()
    {
        _callback = OnKey;
    }

    public event Action<int>? KeyPressed;

    public void Start()
    {
        if (_handle != IntPtr.Zero)
        {
            return;
        }

        var module = NativeMethods.GetModuleHandle(null);
        _handle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _callback, module, 0);
        if (_handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Cannot install keyboard hook.");
        }
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_handle);
        _handle = IntPtr.Zero;
    }

    private IntPtr OnKey(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && ((int)wParam is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN))
        {
            var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            if ((data.flags & NativeMethods.LLKHF_INJECTED) == 0)
            {
                KeyPressed?.Invoke((int)data.vkCode);
            }
        }

        return NativeMethods.CallNextHookEx(_handle, code, wParam, lParam);
    }
}
