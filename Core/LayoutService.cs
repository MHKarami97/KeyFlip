namespace KeyFlip.Core;

public sealed class LayoutService
{
    private const int EnglishPrimaryLanguage = 0x09;
    private const int PersianLanguageId = 0x0429;

    public InputLanguage GetCurrent()
    {
        var window = NativeMethods.GetForegroundWindow();
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
                NativeMethods.PostMessage(NativeMethods.GetForegroundWindow(),
                    NativeMethods.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, layouts[i]);
                return;
            }
        }
    }
}
