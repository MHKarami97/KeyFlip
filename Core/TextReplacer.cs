using System.Runtime.InteropServices;

namespace KeyFlip.Core;

public sealed class TextReplacer
{
    private const ushort VkBack = 0x08;
    private const ushort VkMenuMask = 0xE8;

    private static readonly int InputSize = Marshal.SizeOf<NativeMethods.INPUT>();

    private static readonly ushort[] ModifierKeys =
    {
        NativeMethods.VK_LMENU,
        NativeMethods.VK_RMENU,
        NativeMethods.VK_LSHIFT,
        NativeMethods.VK_RSHIFT,
        NativeMethods.VK_LWIN,
        NativeMethods.VK_RWIN,
        NativeMethods.VK_LCONTROL,
        NativeMethods.VK_RCONTROL
    };

    public void Replace(int charsToDelete, string replacement)
    {
        var inputs = new NativeMethods.INPUT[(charsToDelete + replacement.Length) * 2];
        var index = 0;
        var backScan = (ushort)NativeMethods.MapVirtualKey(VkBack, NativeMethods.MAPVK_VK_TO_VSC);

        for (var i = 0; i < charsToDelete; i++)
        {
            inputs[index++] = CreateVirtualKey(VkBack, backScan, 0);
            inputs[index++] = CreateVirtualKey(VkBack, backScan, NativeMethods.KEYEVENTF_KEYUP);
        }

        foreach (var character in replacement)
        {
            inputs[index++] = CreateUnicode(character, NativeMethods.KEYEVENTF_UNICODE);
            inputs[index++] = CreateUnicode(character, NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP);
        }

        Send(inputs);
    }

    public void SendChord(ushort modifier, ushort key)
    {
        var modScan = (ushort)NativeMethods.MapVirtualKey(modifier, NativeMethods.MAPVK_VK_TO_VSC);
        var keyScan = (ushort)NativeMethods.MapVirtualKey(key, NativeMethods.MAPVK_VK_TO_VSC);
        var modExtra = IsExtendedKey(modifier) ? NativeMethods.KEYEVENTF_EXTENDEDKEY : 0u;
        var keyExtra = IsExtendedKey(key) ? NativeMethods.KEYEVENTF_EXTENDEDKEY : 0u;

        Send(new[]
        {
            CreateVirtualKey(modifier, modScan, modExtra),
            CreateVirtualKey(key, keyScan, keyExtra),
            CreateVirtualKey(key, keyScan, keyExtra | NativeMethods.KEYEVENTF_KEYUP),
            CreateVirtualKey(modifier, modScan, modExtra | NativeMethods.KEYEVENTF_KEYUP)
        });
    }

    /// <summary>Releases any physically held modifier keys logically so subsequent SendChord calls are not altered.</summary>
    public void ReleaseHeldModifiers()
    {
        var inputs = new List<NativeMethods.INPUT>(10);
        var altOrWinDown = IsDown(NativeMethods.VK_LMENU) || IsDown(NativeMethods.VK_RMENU)
                           || IsDown(NativeMethods.VK_LWIN) || IsDown(NativeMethods.VK_RWIN);

        if (altOrWinDown)
        {
            // Mask Alt/Win with an unassigned virtual key so releasing them does not open menus or the Start menu.
            inputs.Add(CreateVirtualKey(VkMenuMask, 0, 0));
            inputs.Add(CreateVirtualKey(VkMenuMask, 0, NativeMethods.KEYEVENTF_KEYUP));
        }

        foreach (var vk in ModifierKeys)
        {
            if (!IsDown(vk))
            {
                continue;
            }

            var scan = (ushort)NativeMethods.MapVirtualKey(vk, NativeMethods.MAPVK_VK_TO_VSC);
            var flags = NativeMethods.KEYEVENTF_KEYUP | (IsExtendedKey(vk) ? NativeMethods.KEYEVENTF_EXTENDEDKEY : 0u);
            inputs.Add(CreateVirtualKey(vk, scan, flags));
        }

        if (inputs.Count > 0)
        {
            Send(inputs.ToArray());
        }
    }

    private static bool IsDown(int vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;

    private static bool IsExtendedKey(ushort vk) => vk is
        NativeMethods.VK_RCONTROL or NativeMethods.VK_RMENU or NativeMethods.VK_LWIN or NativeMethods.VK_RWIN;

    private static void Send(NativeMethods.INPUT[] inputs)
    {
        if (inputs.Length > 0)
        {
            NativeMethods.SendInput((uint)inputs.Length, inputs, InputSize);
        }
    }

    private static NativeMethods.INPUT CreateVirtualKey(ushort key, ushort scan, uint flags) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        u = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT { wVk = key, wScan = scan, dwFlags = flags }
        }
    };

    private static NativeMethods.INPUT CreateUnicode(ushort scan, uint flags) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        u = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT { wVk = 0, wScan = scan, dwFlags = flags }
        }
    };
}
