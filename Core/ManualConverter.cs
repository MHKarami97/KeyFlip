using System.Runtime.InteropServices;

namespace KeyFlip.Core;

/// <summary>Converts the current selection (or the whole field) when the manual hotkey is pressed.</summary>
public sealed class ManualConverter
{
    private const ushort VkControl = 0x11;
    private const ushort VkA = 0x41;
    private const ushort VkC = 0x43;
    private const ushort VkV = 0x56;
    private const int ModifierTimeoutMs = 1000;
    private const int ClipboardPolls = 15;
    private const int PollDelayMs = 20;
    private const int PasteSettleMs = 250;

    private readonly AppSettings _settings;
    private readonly TextReplacer _sender = new();
    private readonly LayoutService _layout = new();
    private int _running;

    public ManualConverter(AppSettings settings)
    {
        _settings = settings;
    }

    public void Run()
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            return;
        }

        var worker = new Thread(() =>
        {
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
        });
        worker.SetApartmentState(ApartmentState.STA);
        worker.IsBackground = true;
        worker.Start();
    }

    private void Execute()
    {
        WaitForModifiersRelease();
        var backup = ReadClipboardText();

        try
        {
            var text = CopySelection();
            if (text is null)
            {
                _sender.SendChord(VkControl, VkA);
                Thread.Sleep(50);
                text = CopySelection();
            }

            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            InputLanguage target;
            var converted = _settings.ManualConvertAllWords
                ? TextConverter.ConvertEachWord(text, out target)
                : TextConverter.Convert(text, out target);

            if (converted == text)
            {
                return;
            }

            Clipboard.SetDataObject(converted, true, 10, 20);
            _sender.SendChord(VkControl, VkV);
            Thread.Sleep(PasteSettleMs);

            if (_settings.SwitchKeyboardLayout)
            {
                _layout.Switch(target);
            }
        }
        finally
        {
            RestoreClipboard(backup);
        }
    }

    private string? CopySelection()
    {
        var sentinel = Guid.NewGuid().ToString();
        Clipboard.SetDataObject(sentinel, true, 10, 20);
        _sender.SendChord(VkControl, VkC);

        for (var i = 0; i < ClipboardPolls; i++)
        {
            Thread.Sleep(PollDelayMs);
            var current = ReadClipboardText();
            if (current is not null && current != sentinel)
            {
                return current;
            }
        }

        return null;
    }

    private static void WaitForModifiersRelease()
    {
        var deadline = Environment.TickCount64 + ModifierTimeoutMs;
        while (Environment.TickCount64 < deadline && AnyModifierDown())
        {
            Thread.Sleep(10);
        }
    }

    private static bool AnyModifierDown() =>
        new[] { NativeMethods.VK_CONTROL, NativeMethods.VK_MENU, 0x10, NativeMethods.VK_LWIN, NativeMethods.VK_RWIN }
            .Any(key => (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0);

    private static string? ReadClipboardText()
    {
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch (ExternalException)
        {
            return null;
        }
    }

    private static void RestoreClipboard(string? backup)
    {
        try
        {
            if (backup is null)
            {
                Clipboard.Clear();
            }
            else
            {
                Clipboard.SetDataObject(backup, true, 10, 20);
            }
        }
        catch (ExternalException)
        {
        }
    }
}
