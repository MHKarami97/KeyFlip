using System.Runtime.InteropServices;

namespace KeyFlip.Core;

public sealed class KeyboardHook : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _callback;
    private readonly ManualResetEventSlim _started = new(false);
    private readonly object _gate = new();
    private Thread? _thread;
    private uint _threadId;
    private IntPtr _handle = IntPtr.Zero;
    private bool _disposed;

    public KeyboardHook()
    {
        _callback = OnKey;
    }

    /// <summary>Return true to swallow the key.</summary>
    public Func<int, bool>? KeyPressed { get; set; }

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || _thread is not null)
            {
                return;
            }

            _started.Reset();
            _handle = IntPtr.Zero;
            _threadId = 0;
            _thread = new Thread(Run) { IsBackground = true, Name = "KeyFlip.Hook" };
            _thread.Start();
            _started.Wait();

            if (_handle == IntPtr.Zero)
            {
                _thread = null;
                throw new InvalidOperationException("Cannot install keyboard hook.");
            }
        }
    }

    public void Stop()
    {
        Thread? thread;
        uint threadId;
        lock (_gate)
        {
            if (_thread is null)
            {
                return;
            }

            thread = _thread;
            threadId = _threadId;
            _thread = null;
        }

        if (threadId != 0)
        {
            NativeMethods.PostThreadMessage(threadId, NativeMethods.WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
        }

        thread.Join(1000);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        Stop();
        _started.Dispose();
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
