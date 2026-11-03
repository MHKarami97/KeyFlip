using System.Runtime.InteropServices;

namespace KeyFlip.Core;

internal static class InputState
{
    private const int VkShift = 0x10;
    private const int VkCapital = 0x14;

    public static bool IsShiftDown() => (GetAsyncKeyState(VkShift) & 0x8000) != 0;

    public static bool IsCapsLockOn() => (GetKeyState(VkCapital) & 1) != 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int vKey);
}
