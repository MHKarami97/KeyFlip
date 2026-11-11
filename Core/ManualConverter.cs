using System.Runtime.InteropServices;
using System.Text;

namespace KeyFlip.Core;

/// <summary>Converts the current selection (or the whole field) when the manual hotkey is pressed.</summary>
public sealed class ManualConverter : IDisposable
{
    private const ushort VkControl = NativeMethods.VK_LCONTROL;
    private const ushort VkA = 0x41;
    private const ushort VkC = 0x43;
    private const ushort VkV = 0x56;
    private const int SelectionCopyTimeoutMs = 130;
    private const int SelectAllCopyTimeoutMs = 250;
    private const int SelectAllSettleMs = 35;
    private const int PasteSettleMs = 140;
    private const int ClipboardOpenAttempts = 12;
    private const int ClipboardRetryDelayMs = 5;

    private readonly AppSettings _settings;
    private readonly TextReplacer _sender = new();
    private readonly LayoutService _layout = new();
    private readonly ClipboardWatcher _watcher = new();
    private readonly AutoResetEvent _workSignal = new(false);
    private readonly Thread _worker;
    private int _running;
    private volatile bool _disposed;

    public ManualConverter(AppSettings settings)
    {
        _settings = settings;
        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "KeyFlip.Manual",
            Priority = ThreadPriority.AboveNormal
        };
        _worker.SetApartmentState(ApartmentState.STA);
        _worker.Start();
    }

    public void Run()
    {
        if (_disposed || Interlocked.Exchange(ref _running, 1) == 1)
        {
            return;
        }

        _workSignal.Set();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _workSignal.Set();
        _worker.Join(500);
        _watcher.Dispose();
        _workSignal.Dispose();
    }

    private void WorkerLoop()
    {
        while (_workSignal.WaitOne())
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                Execute();
            }
            catch (Exception ex)
            {
                DebugLog.Write($"Manual conversion failed: {ex}");
            }
            finally
            {
                Volatile.Write(ref _running, 0);
            }
        }
    }

    private void Execute()
    {
        _sender.ReleaseHeldModifiers();
        var backup = ReadClipboardText();

        try
        {
            var text = IsClassicEditWithoutSelection() ? null : CopySelection(SelectionCopyTimeoutMs);
            if (text is null)
            {
                _sender.SendChord(VkControl, VkA);
                Thread.Sleep(SelectAllSettleMs);
                text = CopySelection(SelectAllCopyTimeoutMs);
            }

            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var converted = _settings.ManualConvertAllWords
                ? TextConverter.ConvertEachWord(text, out var target)
                : TextConverter.Convert(text, out target);

            if (converted == text || !SetClipboardText(converted))
            {
                return;
            }

            _sender.SendChord(VkControl, VkV);
            if (_settings.SwitchKeyboardLayout)
            {
                _layout.Switch(target);
            }

            Thread.Sleep(PasteSettleMs);
        }
        finally
        {
            RestoreClipboard(backup);
        }
    }

    private string? CopySelection(int timeoutMs)
    {
        if (!ClearClipboard())
        {
            return null;
        }

        var seqAfterEmpty = NativeMethods.GetClipboardSequenceNumber();
        _watcher.ResetAfter(seqAfterEmpty);
        _sender.SendChord(VkControl, VkC);

        var deadline = Environment.TickCount64 + timeoutMs;
        while (NativeMethods.GetClipboardSequenceNumber() == seqAfterEmpty)
        {
            var remaining = (int)(deadline - Environment.TickCount64);
            if (remaining <= 0)
            {
                return null;
            }

            if (_watcher.Wait(Math.Min(remaining, 25)) && NativeMethods.GetClipboardSequenceNumber() == seqAfterEmpty)
            {
                _watcher.ResetAfter(seqAfterEmpty);
            }
        }

        return ReadClipboardText();
    }

    private static bool IsClassicEditWithoutSelection()
    {
        var info = new NativeMethods.GUITHREADINFO { cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
        if (!NativeMethods.GetGUIThreadInfo(0, ref info) || info.hwndFocus == IntPtr.Zero)
        {
            return false;
        }

        var className = new StringBuilder(64);
        NativeMethods.GetClassName(info.hwndFocus, className, className.Capacity);
        if (!className.ToString().Contains("Edit", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (NativeMethods.SendMessageTimeout(
                info.hwndFocus, NativeMethods.EM_GETSEL, out var start, out var end,
                NativeMethods.SMTO_ABORTIFHUNG, 30, out _) == IntPtr.Zero)
        {
            return false;
        }

        return start == end && start != 0xFFFFFFFF;
    }

    private static bool ClearClipboard() => WithClipboardOpen(() => NativeMethods.EmptyClipboard());

    private static string? ReadClipboardText()
    {
        if (!NativeMethods.IsClipboardFormatAvailable(NativeMethods.CF_UNICODETEXT))
        {
            return null;
        }

        string? result = null;
        WithClipboardOpen(() =>
        {
            var handle = NativeMethods.GetClipboardData(NativeMethods.CF_UNICODETEXT);
            if (handle == IntPtr.Zero)
            {
                return true;
            }

            var pointer = NativeMethods.GlobalLock(handle);
            if (pointer != IntPtr.Zero)
            {
                try
                {
                    result = Marshal.PtrToStringUni(pointer);
                }
                finally
                {
                    NativeMethods.GlobalUnlock(handle);
                }
            }

            return true;
        });
        return result;
    }

    private static bool SetClipboardText(string text)
    {
        var byteCount = (nuint)((text.Length + 1) * sizeof(char));
        var hGlobal = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, byteCount);
        if (hGlobal == IntPtr.Zero)
        {
            return false;
        }

        var pointer = NativeMethods.GlobalLock(hGlobal);
        if (pointer == IntPtr.Zero)
        {
            NativeMethods.GlobalFree(hGlobal);
            return false;
        }

        try
        {
            Marshal.Copy(text.ToCharArray(), 0, pointer, text.Length);
            Marshal.WriteInt16(pointer, text.Length * sizeof(char), 0);
        }
        finally
        {
            NativeMethods.GlobalUnlock(hGlobal);
        }

        var ownershipTransferred = false;
        var ok = WithClipboardOpen(() =>
        {
            NativeMethods.EmptyClipboard();
            if (NativeMethods.SetClipboardData(NativeMethods.CF_UNICODETEXT, hGlobal) != IntPtr.Zero)
            {
                ownershipTransferred = true;
                return true;
            }

            return false;
        });

        if (!ownershipTransferred)
        {
            NativeMethods.GlobalFree(hGlobal);
        }

        return ok;
    }

    private static void RestoreClipboard(string? backup)
    {
        if (backup is null)
        {
            ClearClipboard();
        }
        else
        {
            SetClipboardText(backup);
        }
    }

    private static bool WithClipboardOpen(Func<bool> action)
    {
        for (var attempt = 0; attempt < ClipboardOpenAttempts; attempt++)
        {
            if (NativeMethods.OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    return action();
                }
                finally
                {
                    NativeMethods.CloseClipboard();
                }
            }

            Thread.Sleep(ClipboardRetryDelayMs);
        }

        return false;
    }

    private sealed class ClipboardWatcher : NativeWindow, IDisposable
    {
        private static readonly IntPtr MessageOnlyParent = new(-3);
        private readonly ManualResetEventSlim _changed = new(false);
        private uint _ignoreSeq;

        public ClipboardWatcher()
        {
            CreateHandle(new CreateParams { Parent = MessageOnlyParent });
            NativeMethods.AddClipboardFormatListener(Handle);
        }

        public void ResetAfter(uint sequenceNumber)
        {
            Volatile.Write(ref _ignoreSeq, sequenceNumber);
            _changed.Reset();
        }

        public bool Wait(int timeoutMs) => _changed.Wait(timeoutMs);

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                NativeMethods.RemoveClipboardFormatListener(Handle);
                DestroyHandle();
            }

            _changed.Dispose();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_CLIPBOARDUPDATE
                && NativeMethods.GetClipboardSequenceNumber() != Volatile.Read(ref _ignoreSeq))
            {
                _changed.Set();
            }

            base.WndProc(ref m);
        }
    }
}
