using System.Text;

namespace KeyFlip.Core;

public sealed class ConversionEngine : IDisposable
{
    private const int VkSpace = 0x20;
    private const int VkBack = 0x08;

    private readonly KeyboardHook _hook = new();
    private readonly LayoutService _layout = new();
    private readonly WordValidator _validator = new();
    private readonly TextReplacer _replacer = new();
    private readonly List<int> _buffer = new();
    private readonly SynchronizationContext _context;

    public ConversionEngine(AppSettings settings)
    {
        Settings = settings;
        _context = SynchronizationContext.Current ?? new SynchronizationContext();
        _hook.KeyPressed += OnKeyPressed;
    }

    public AppSettings Settings { get; }

    public void Start() => _hook.Start();

    public void Dispose() => _hook.Dispose();

    private void OnKeyPressed(int virtualKey)
    {
        if (Settings.Mode == ConversionMode.Off || IsModifierDown())
        {
            _buffer.Clear();
            return;
        }

        var language = _layout.GetCurrent();
        if (language == InputLanguage.Other)
        {
            _buffer.Clear();
            return;
        }

        if (virtualKey == VkSpace)
        {
            var word = _buffer.ToArray();
            _buffer.Clear();
            if (word.Length > 1)
            {
                _context.Post(_ => TryConvert(word, language), null);
            }

            return;
        }

        if (virtualKey == VkBack)
        {
            if (_buffer.Count > 0)
            {
                _buffer.RemoveAt(_buffer.Count - 1);
            }

            return;
        }

        if (KeyMap.IsMapped(virtualKey, language == InputLanguage.Persian))
        {
            _buffer.Add(virtualKey);
        }
        else
        {
            _buffer.Clear();
        }
    }

    private void TryConvert(int[] keys, InputLanguage current)
    {
        var english = Build(keys, KeyMap.ToEnglish);
        var persian = Build(keys, KeyMap.ToPersian);

        var canToPersian = Settings.Mode is ConversionMode.ToPersian or ConversionMode.Auto;
        var canToEnglish = Settings.Mode is ConversionMode.ToEnglish or ConversionMode.Auto;

        if (current == InputLanguage.English && canToPersian
            && !_validator.IsEnglishWord(english) && _validator.IsPersianWord(persian))
        {
            Apply(english.Length, persian, InputLanguage.Persian);
        }
        else if (current == InputLanguage.Persian && canToEnglish
                 && !_validator.IsPersianWord(persian) && _validator.IsEnglishWord(english))
        {
            Apply(persian.Length, english, InputLanguage.English);
        }
    }

    private void Apply(int wordLength, string replacement, InputLanguage target)
    {
        _replacer.Replace(wordLength + 1, replacement + " ");
        if (Settings.SwitchKeyboardLayout)
        {
            _layout.Switch(target);
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
}
