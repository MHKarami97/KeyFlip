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
            inputs.Add(VirtualKey(VkBack, keyUp: false));
            inputs.Add(VirtualKey(VkBack, keyUp: true));
        }

        foreach (var character in replacement)
        {
            inputs.Add(Unicode(character, keyUp: false));
            inputs.Add(Unicode(character, keyUp: true));
        }

        NativeMethods.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static NativeMethods.INPUT VirtualKey(ushort key, bool keyUp) => Create(key, 0, keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0);

    private static NativeMethods.INPUT Unicode(char character, bool keyUp) =>
        Create(0, character, NativeMethods.KEYEVENTF_UNICODE | (keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0));

    private static NativeMethods.INPUT Create(ushort key, ushort scan, uint flags) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        u = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT { wVk = key, wScan = scan, dwFlags = flags }
        }
    };
}
