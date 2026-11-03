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

    /// <summary>Manual mode: swap every word to the opposite language instead of picking one direction for the whole text.</summary>
    public bool ManualConvertAllWords { get; set; }

    /// <summary>Process names (without .exe) where automatic conversion is disabled. Replace the list instead of mutating it.</summary>
    public List<string> ExcludedApps { get; set; } = new()
    {
        "WindowsTerminal", "cmd", "powershell", "pwsh", "conhost",
        "KeePass", "KeePassXC", "Bitwarden", "1Password"
    };

    /// <summary>Reads the Persian key layout from the Persian keyboard layout installed in Windows.</summary>
    public bool AutoDetectKeyMap { get; set; } = true;

    /// <summary>Virtual-key code to Persian character. An empty string removes the key from the map.</summary>
    public Dictionary<int, string> PersianKeyOverrides { get; set; } = new();

    /// <summary>Virtual-key code to English character.</summary>
    public Dictionary<int, string> EnglishKeyOverrides { get; set; } = new();

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
