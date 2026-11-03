using System.Runtime.InteropServices;

namespace KeyFlip.Core;

public sealed class KeyboardHook : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _callback;
    private readonly ManualResetEventSlim _started = new(false);
    private Thread? _thread;
    private uint _threadId;
    private IntPtr _handle = IntPtr.Zero;

    public KeyboardHook()
    {
        _callback = OnKey;
    }

    /// <summary>Return true to swallow the key.</summary>
    public Func<int, bool>? KeyPressed { get; set; }

    public void Start()
    {
        if (_thread is not null)
        {
            return;
        }

        _thread = new Thread(Run) { IsBackground = true, Name = "KeyFlip.Hook" };
        _thread.Start();
        _started.Wait();

        if (_handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Cannot install keyboard hook.");
        }
    }

    public void Dispose()
    {
        if (_thread is null)
        {
            return;
        }

        NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
        _thread.Join(1000);
        _thread = null;
    }

    private void Run()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        _handle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _callback,
            NativeMethods.GetModuleHandle(null), 0);
        _started.Set();

        if (_handle == IntPtr.Zero)
        {
            return;
        }

        while (NativeMethods.GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref message);
            NativeMethods.DispatchMessage(ref message);
        }

        NativeMethods.UnhookWindowsHookEx(_handle);
        _handle = IntPtr.Zero;
    }

    private IntPtr OnKey(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && ((int)wParam is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN))
        {
            var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            if ((data.flags & NativeMethods.LLKHF_INJECTED) == 0 && KeyPressed?.Invoke((int)data.vkCode) == true)
            {
                return (IntPtr)1;
            }
        }

        return NativeMethods.CallNextHookEx(_handle, code, wParam, lParam);
    }
}
