using System.Runtime.InteropServices;
using System.Text;

namespace KeyFlip.Core;

public sealed class LayoutService
{
    private const int EnglishPrimaryLanguage = 0x09;
    private const int PersianLanguageId = 0x0429;

    public InputLanguage GetCurrent()
    {
        var window = ResolveInputWindow();
        if (window == IntPtr.Zero)
        {
            return InputLanguage.Other;
        }

        var thread = NativeMethods.GetWindowThreadProcessId(window, out _);
        var languageId = (int)((long)NativeMethods.GetKeyboardLayout(thread) & 0xFFFF);

        if (languageId == PersianLanguageId)
        {
            return InputLanguage.Persian;
        }

        return (languageId & 0x3FF) == EnglishPrimaryLanguage ? InputLanguage.English : InputLanguage.Other;
    }

    public void Switch(InputLanguage target)
    {
        var layouts = new IntPtr[64];
        var count = NativeMethods.GetKeyboardLayoutList(layouts.Length, layouts);
        for (var i = 0; i < count; i++)
        {
            var languageId = (int)((long)layouts[i] & 0xFFFF);
            var matches = target == InputLanguage.Persian
                ? languageId == PersianLanguageId
                : (languageId & 0x3FF) == EnglishPrimaryLanguage;

            if (matches)
            {
                var window = ResolveInputWindow();
                if (window != IntPtr.Zero)
                {
                    NativeMethods.PostMessage(window, NativeMethods.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, layouts[i]);
                }

                return;
            }
        }
    }

    private static IntPtr ResolveInputWindow()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var info = new NativeMethods.GUITHREADINFO { cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
        if (NativeMethods.GetGUIThreadInfo(0, ref info) && info.hwndFocus != IntPtr.Zero)
        {
            return info.hwndFocus;
        }

        var className = new StringBuilder(64);
        NativeMethods.GetClassName(foreground, className, className.Capacity);
        if (string.Equals(className.ToString(), "ApplicationFrameWindow", StringComparison.Ordinal))
        {
            var coreWindow = IntPtr.Zero;
            NativeMethods.EnumChildWindows(foreground, (child, _) =>
            {
                var childClass = new StringBuilder(64);
                NativeMethods.GetClassName(child, childClass, childClass.Capacity);
                if (string.Equals(childClass.ToString(), "Windows.UI.Core.CoreWindow", StringComparison.Ordinal))
                {
                    coreWindow = child;
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            if (coreWindow != IntPtr.Zero)
            {
                return coreWindow;
            }
        }

        return foreground;
    }
}
