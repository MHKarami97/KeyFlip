using System.Runtime.InteropServices;
using System.Text;

namespace KeyFlip.Core;

/// <summary>Reads which character each key produces in an installed keyboard layout, without changing keyboard state.</summary>
public sealed class LayoutKeyReader
{
    private const int PersianLanguageId = 0x0429;
    private const int EnglishUsLanguageId = 0x0409;
    private const int EnglishPrimaryLanguage = 0x09;
    private const uint MapVkToVsc = 0;
    private const uint DoNotChangeKeyState = 0x4;
    private const uint VkSpace = 0x20;

    public Dictionary<int, char>? Read(InputLanguage language, IEnumerable<int> keys)
    {
        var layout = FindLayout(language);
        if (layout == IntPtr.Zero)
        {
            return null;
        }

        var state = new byte[256];
        var result = new Dictionary<int, char>();
        foreach (var key in keys)
        {
            var scanCode = MapVirtualKeyEx((uint)key, MapVkToVsc, layout);
            var buffer = new StringBuilder(8);
            var count = ToUnicodeEx((uint)key, scanCode, state, buffer, buffer.Capacity, DoNotChangeKeyState, layout);

            if (count == 1)
            {
                result[key] = buffer[0];
            }
            else if (count < 0)
            {
                ToUnicodeEx(VkSpace, MapVirtualKeyEx(VkSpace, MapVkToVsc, layout), state, new StringBuilder(8), 8, DoNotChangeKeyState, layout);
            }
        }

        return result;
    }

    private static IntPtr FindLayout(InputLanguage language)
    {
        var layouts = new IntPtr[64];
        var count = GetKeyboardLayoutList(layouts.Length, layouts);
        var installed = layouts.Take(count).ToArray();

        if (language == InputLanguage.Persian)
        {
            return installed.FirstOrDefault(layout => LanguageId(layout) == PersianLanguageId);
        }

        var english = installed.FirstOrDefault(layout => LanguageId(layout) == EnglishUsLanguageId);
        if (english != IntPtr.Zero)
        {
            return english;
        }

        english = installed.FirstOrDefault(layout => (LanguageId(layout) & 0x3FF) == EnglishPrimaryLanguage);
        return english != IntPtr.Zero ? english : LoadKeyboardLayout("00000409", 0);
    }

    private static int LanguageId(IntPtr layout) => (int)((long)layout & 0xFFFF);

    [DllImport("user32.dll")]
    private static extern int GetKeyboardLayoutList(int bufferSize, [Out] IntPtr[] list);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadKeyboardLayout(string layoutId, uint flags);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr layout);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(
        uint virtualKey,
        uint scanCode,
        byte[] keyState,
        [Out, MarshalAs(UnmanagedType.LPWStr, SizeConst = 64)] StringBuilder buffer,
        int bufferSize,
        uint flags,
        IntPtr layout);
}
