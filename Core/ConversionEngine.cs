using System.Text;

namespace KeyFlip.Core;

public sealed class ConversionEngine : IDisposable
{
    private const int VkSpace = 0x20;
    private const int VkBack = 0x08;

    private readonly KeyboardHook _hook = new();
    private readonly LayoutService _layout = new();
    private readonly LanguageDetector _detector = new();
    private readonly TextReplacer _replacer = new();
    private readonly HotkeyWindow _hotkey = new();
    private readonly ManualConverter _manual;
    private readonly List<int> _buffer = new();
    private readonly object _sendLock = new();

    public ConversionEngine(AppSettings settings)
    {
        Settings = settings;
        _manual = new ManualConverter(settings);
        _hook.KeyPressed = OnKeyPressed;
        _hotkey.Pressed += _manual.Run;
    }

    public AppSettings Settings { get; }

    public void Start() => _hook.Start();

    /// <summary>Registers or releases the manual hotkey. Returns false if the hotkey is taken.</summary>
    public bool ApplyMode()
    {
        if (Settings.Mode == ConversionMode.Manual)
        {
            return _hotkey.Register(Settings.HotkeyModifiers, Settings.HotkeyKey);
        }

        _hotkey.Unregister();
        return true;
    }

    public void Dispose()
    {
        _hook.Dispose();
        _hotkey.Dispose();
    }

    private bool OnKeyPressed(int virtualKey)
    {
        if (Settings.Mode is ConversionMode.Off or ConversionMode.Manual || IsModifierDown())
        {
            _buffer.Clear();
            return false;
        }

        var language = _layout.GetCurrent();
        if (language == InputLanguage.Other)
        {
            _buffer.Clear();
            return false;
        }

        if (virtualKey == VkSpace)
        {
            return HandleSpace(language);
        }

        if (virtualKey == VkBack)
        {
            if (_buffer.Count > 0)
            {
                _buffer.RemoveAt(_buffer.Count - 1);
            }

            return false;
        }

        if (KeyMap.IsMapped(virtualKey, language == InputLanguage.Persian))
        {
            _buffer.Add(virtualKey);
        }
        else
        {
            _buffer.Clear();
        }

        return false;
    }

    private bool HandleSpace(InputLanguage language)
    {
        var keys = _buffer.ToArray();
        _buffer.Clear();
        if (keys.Length < 2)
        {
            return false;
        }

        var plan = CreatePlan(keys, language);
        if (plan is null)
        {
            return false;
        }

        Task.Run(() => Execute(plan));
        return true;
    }

    private ConversionPlan? CreatePlan(int[] keys, InputLanguage current)
    {
        var english = Build(keys, KeyMap.ToEnglish);
        var persian = Build(keys, KeyMap.ToPersian);

        if (current == InputLanguage.English
            && Settings.Mode is ConversionMode.ToPersian or ConversionMode.Auto
            && _detector.ShouldFlip(english, persian, current))
        {
            return new ConversionPlan(english.Length, persian, InputLanguage.Persian);
        }

        if (current == InputLanguage.Persian
            && Settings.Mode is ConversionMode.ToEnglish or ConversionMode.Auto
            && _detector.ShouldFlip(persian, english, current))
        {
            return new ConversionPlan(persian.Length, english, InputLanguage.English);
        }

        return null;
    }

    private void Execute(ConversionPlan plan)
    {
        lock (_sendLock)
        {
            _replacer.Replace(plan.WordLength, plan.Replacement + " ");
            if (Settings.SwitchKeyboardLayout)
            {
                _layout.Switch(plan.Target);
            }
        }
    }

    private static string Build(IEnumerable<int> keys, Func<int, char> selector)
    {
        var builder = new StringBuilder();
        foreach (var key in keys)
        {
            builder.Append(selector(key));
        }

        return builder.ToString();
    }

    private static bool IsModifierDown() =>
        IsDown(NativeMethods.VK_CONTROL) || IsDown(NativeMethods.VK_MENU)
        || IsDown(NativeMethods.VK_LWIN) || IsDown(NativeMethods.VK_RWIN);

    private static bool IsDown(int key) => (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0;

    private sealed record ConversionPlan(int WordLength, string Replacement, InputLanguage Target);
}
