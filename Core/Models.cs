namespace KeyFlip.Core;

public enum ConversionMode
{
    Off = 0,
    ToEnglish = 1,
    ToPersian = 2,
    Auto = 3
}

public sealed class AppSettings
{
    public ConversionMode Mode { get; set; } = ConversionMode.Auto;
    public bool SwitchKeyboardLayout { get; set; } = true;
    public bool RunAtStartup { get; set; }
}

public enum InputLanguage
{
    Other,
    English,
    Persian
}
