namespace KeyFlip.Core;

public enum ConversionMode
{
    Off = 0,
    ToEnglish = 1,
    ToPersian = 2,
    Auto = 3,
    Manual = 4
}

public sealed class AppSettings
{
    public ConversionMode Mode { get; set; } = ConversionMode.Auto;
    public bool SwitchKeyboardLayout { get; set; } = true;
    public bool RunAtStartup { get; set; }

    /// <summary>Pressing Backspace right after an automatic conversion restores the original text.</summary>
    public bool UndoOnBackspace { get; set; } = true;

    /// <summary>Uses the language of recently typed words as a prior for borderline decisions.</summary>
    public bool UseContext { get; set; } = true;

    /// <summary>Process names (without .exe) where automatic conversion is disabled. Replace the list instead of mutating it.</summary>
    public List<string> ExcludedApps { get; set; } = new()
    {
        "WindowsTerminal", "cmd", "powershell", "pwsh", "conhost",
        "KeePass", "KeePassXC", "Bitwarden", "1Password"
    };

    /// <summary>Manual-mode hotkey modifiers: Alt=1, Ctrl=2, Shift=4, Win=8.</summary>
    public uint HotkeyModifiers { get; set; } = 0x0003;

    /// <summary>Manual-mode hotkey virtual-key code (default 0x20 = Space).</summary>
    public uint HotkeyKey { get; set; } = 0x20;
}

public enum InputLanguage
{
    Other,
    English,
    Persian
}
