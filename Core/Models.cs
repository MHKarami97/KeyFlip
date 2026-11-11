using System.Text.Json.Serialization;

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

    /// <summary>Periodically checks GitHub Releases for newer versions.</summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>Automatic update check interval in hours.</summary>
    public int UpdateCheckIntervalHours { get; set; } = 6;

    /// <summary>UTC timestamp of the last successful release check.</summary>
    public DateTimeOffset? LastUpdateCheckUtc { get; set; }

    /// <summary>Cached latest release tag discovered from GitHub (for immediate UI display after restart).</summary>
    public string? LatestKnownVersion { get; set; }

    /// <summary>Cached GitHub release URL corresponding to <see cref="LatestKnownVersion"/>.</summary>
    public string? LatestKnownUrl { get; set; }

    /// <summary>Last release tag shown in a tray update notification.</summary>
    public string? LastNotifiedVersion { get; set; }

    /// <summary>UTC timestamp when <see cref="LastNotifiedVersion"/> was last notified.</summary>
    public DateTimeOffset? LastNotifiedUtc { get; set; }

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

public sealed class GitHubReleaseInfo
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }
}

public enum InputLanguage
{
    Other,
    English,
    Persian
}
