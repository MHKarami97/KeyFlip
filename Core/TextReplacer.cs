using System.Runtime.InteropServices;

namespace KeyFlip.Core;

public sealed class TextReplacer
{
    private const ushort VkBack = 0x08;

    public void Replace(int charsToDelete, string replacement)
    {
        var inputs = new List<NativeMethods.INPUT>();

        for (var i = 0; i < charsToDelete; i++)
        {
            inputs.Add(Create(VkBack, 0, 0));
            inputs.Add(Create(VkBack, 0, NativeMethods.KEYEVENTF_KEYUP));
        }

        foreach (var character in replacement)
        {
            inputs.Add(Create(0, character, NativeMethods.KEYEVENTF_UNICODE));
            inputs.Add(Create(0, character, NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP));
        }

        NativeMethods.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static NativeMethods.INPUT Create(ushort key, ushort scan, uint flags) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        u = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT { wVk = key, wScan = scan, dwFlags = flags }
        }
    };
}
