using System.Collections.Concurrent;
using System.Text;

namespace KeyFlip.Core;

public sealed class ConversionEngine : IDisposable
{
    private const int VkBack = 0x08;
    private const int VkSpace = 0x20;
    private const int UndoWindowMs = 4000;
    private const int PendingWindowMs = 6000;

    private readonly KeyboardHook _hook = new();
    private readonly LayoutService _layout = new();
    private readonly LanguageDetector _detector;
    private readonly LanguageContext _context = new();
    private readonly ForegroundInspector _inspector = new();
    private readonly UserDictionary _user;
    private readonly TextReplacer _replacer = new();
    private readonly HotkeyWindow _hotkey = new();
    private readonly ManualConverter _manual;
    private readonly List<KeyStroke> _buffer = new();
    private readonly BlockingCollection<Action> _dispatchQueue = new();
    private readonly Thread _dispatchThread;

    private PendingWord? _pending;
    private LastConversion? _lastConversion;
    private (IntPtr Window, IntPtr FocusWindow) _lastFocus;
    private bool _skipWord;

    public ConversionEngine(AppSettings settings, UserDictionary user)
    {
        Settings = settings;
        _user = user;
        _detector = new LanguageDetector(user);
        _manual = new ManualConverter(settings);
        _hook.KeyPressed = OnKeyPressed;
        _hotkey.Pressed += _manual.Run;

        _dispatchThread = new Thread(DispatchLoop)
        {
            IsBackground = true,
            Name = "KeyFlip.Dispatch",
            Priority = ThreadPriority.AboveNormal
        };
        _dispatchThread.Start();
    }

    public AppSettings Settings { get; }

    public void Start() => ApplyMode();

    /// <summary>Applies hook and hotkey registration for the current conversion mode. Returns false if the manual hotkey is taken.</summary>
    public bool ApplyMode()
    {
        if (Settings.Mode == ConversionMode.Manual)
        {
            _hook.Stop();
            ResetWord();
            return _hotkey.Register(Settings.HotkeyModifiers, Settings.HotkeyKey);
        }

        _hotkey.Unregister();
        if (Settings.Mode == ConversionMode.Off)
        {
            _hook.Stop();
            ResetWord();
        }
        else
        {
            _hook.Start();
        }

        return true;
    }

    public void Dispose()
    {
        _hook.Dispose();
        _hotkey.Dispose();
        _manual.Dispose();
        _dispatchQueue.CompleteAdding();
        _dispatchThread.Join(500);
        _dispatchQueue.Dispose();
    }

    private void DispatchLoop()
    {
        foreach (var work in _dispatchQueue.GetConsumingEnumerable())
        {
            work();
        }
    }

    private bool OnKeyPressed(int virtualKey)
    {
        if (Settings.Mode is ConversionMode.Off or ConversionMode.Manual)
        {
            ResetWord();
            return false;
        }

        if (IsIgnorable(virtualKey))
        {
            return false;
        }

        var focus = _inspector.GetFocusHandles();
        if (focus != _lastFocus)
        {
            _lastFocus = focus;
            ResetWord();
            AcceptLastConversion();
        }

        if (IsModifierDown() || _inspector.ShouldIgnore(Settings.ExcludedApps))
        {
            ResetWord();
            return false;
        }

        var language = _layout.GetCurrent();
        if (language == InputLanguage.Other)
        {
            ResetWord();
            return false;
        }

        if (virtualKey == VkBack)
        {
            return HandleBackspace();
        }

        AcceptLastConversion();

        if (virtualKey == VkSpace)
        {
            return HandleSpace(language, focus);
        }

        if (IsNavigation(virtualKey))
        {
            ResetWord();
            return false;
        }

        var isLetter = KeyMap.IsLetterKey(virtualKey);
        if (KeyMap.IsMapped(virtualKey) && (isLetter || !InputState.IsShiftDown()))
        {
            _buffer.Add(new KeyStroke(virtualKey, IsUpper(language, isLetter)));
            return false;
        }

        // Digits, symbols and shifted punctuation glue the current word to something that is not plain text.
        _buffer.Clear();
        _pending = null;
        _skipWord = true;
        return false;
    }

    private bool HandleBackspace()
    {
        if (TryUndo())
        {
            return true;
        }

        if (_buffer.Count > 0)
        {
            _buffer.RemoveAt(_buffer.Count - 1);
        }

        _pending = null;
        return false;
    }

    private bool HandleSpace(InputLanguage language, (IntPtr Window, IntPtr FocusWindow) focus)
    {
        var strokes = _buffer.ToArray();
        var skip = _skipWord;
        _buffer.Clear();
        _skipWord = false;

        var structural = strokes.Length > 0 && LooksStructural(strokes, language);
        DebugLog.Write($"space: layout={language} keys={strokes.Length} skip={skip} structural={structural}");

        if (skip || strokes.Length < 2 || structural)
        {
            _pending = null;
            return false;
        }

        var keys = strokes.Select(stroke => stroke.VirtualKey).ToArray();
        var english = Build(keys, KeyMap.ToEnglish);
        var persian = Build(keys, KeyMap.ToPersian);
        var typed = language == InputLanguage.English ? english : persian;
        var alternative = language == InputLanguage.English ? persian : english;
        var target = language == InputLanguage.English ? InputLanguage.Persian : InputLanguage.English;

        var pending = TakePending(language, focus);
        if (!IsDirectionAllowed(target))
        {
            return false;
        }

        var bias = Settings.UseContext ? _context.BiasToward(target) : 0;
        var evaluation = _detector.Evaluate(typed, alternative, language, bias);
        DebugLog.Write($"typed='{typed}' alt='{alternative}' bias={bias:F2} verdict={evaluation.Verdict} typedKnown={evaluation.TypedKnown}");

        switch (evaluation.Verdict)
        {
            case Verdict.Flip:
                return StartFlip(pending, typed, alternative, language, target);
            case Verdict.Maybe when pending is not null:
                return StartFlip(pending, typed, alternative, language, target);
            case Verdict.Maybe:
                _pending = new PendingWord(typed, alternative, language, focus.Window, focus.FocusWindow, Environment.TickCount64);
                return false;
            default:
                if (evaluation.TypedKnown)
                {
                    _context.Record(language);
                }

                return false;
        }
    }

    private bool StartFlip(PendingWord? pending, string typed, string alternative, InputLanguage language, InputLanguage target)
    {
        var plan = pending is null
            ? new ConversionPlan(typed.Length, alternative + " ", typed + " ", new[] { typed }, language, target)
            : new ConversionPlan(
                pending.Typed.Length + 1 + typed.Length,
                pending.Alternative + " " + alternative + " ",
                pending.Typed + " " + typed + " ",
                new[] { pending.Typed, typed },
                language,
                target);

        _context.Record(target);
        if (!_dispatchQueue.IsAddingCompleted)
        {
            _dispatchQueue.TryAdd(() => Execute(plan));
        }

        return true;
    }

    private void Execute(ConversionPlan plan)
    {
        if (Settings.SwitchKeyboardLayout)
        {
            _layout.Switch(plan.Target);
        }

        _replacer.Replace(plan.DeleteLength, plan.Replacement);
        Volatile.Write(ref _lastConversion, new LastConversion(plan, Environment.TickCount64));
    }

    private bool TryUndo()
    {
        var last = Interlocked.Exchange(ref _lastConversion, null);
        if (last is null)
        {
            return false;
        }

        if (!Settings.UndoOnBackspace || Environment.TickCount64 - last.CompletedAt > UndoWindowMs)
        {
            Accept(last);
            return false;
        }

        foreach (var word in last.Plan.TypedWords)
        {
            _user.AddNeverConvert(word);
        }

        _context.Record(last.Plan.Previous);
        if (!_dispatchQueue.IsAddingCompleted)
        {
            _dispatchQueue.TryAdd(() => ExecuteUndo(last.Plan));
        }

        return true;
    }

    private void ExecuteUndo(ConversionPlan plan)
    {
        if (Settings.SwitchKeyboardLayout)
        {
            _layout.Switch(plan.Previous);
        }

        _replacer.Replace(plan.Replacement.Length, plan.Original);
    }

    private void AcceptLastConversion()
    {
        var last = Interlocked.Exchange(ref _lastConversion, null);
        if (last is not null)
        {
            Accept(last);
        }
    }

    private void Accept(LastConversion last)
    {
        foreach (var word in last.Plan.TypedWords)
        {
            _user.RecordAccepted(word);
        }
    }

    private PendingWord? TakePending(InputLanguage language, (IntPtr Window, IntPtr FocusWindow) focus)
    {
        var pending = _pending;
        _pending = null;
        return pending is not null
               && pending.Language == language
               && pending.Window == focus.Window
               && pending.FocusWindow == focus.FocusWindow
               && Environment.TickCount64 - pending.Tick < PendingWindowMs
            ? pending
            : null;
    }

    private void ResetWord()
    {
        _buffer.Clear();
        _pending = null;
        _skipWord = false;
    }

    private bool IsDirectionAllowed(InputLanguage target) => Settings.Mode switch
    {
        ConversionMode.Auto => true,
        ConversionMode.ToPersian => target == InputLanguage.Persian,
        ConversionMode.ToEnglish => target == InputLanguage.English,
        _ => false
    };

    /// <summary>Acronyms (README), camelCase/PascalCase identifiers, and shifted characters in the Persian layout.</summary>
    private static bool LooksStructural(KeyStroke[] strokes, InputLanguage language)
    {
        if (language == InputLanguage.Persian)
        {
            return strokes.Any(stroke => stroke.Upper);
        }

        var uppers = strokes.Count(stroke => stroke.Upper);
        return uppers == strokes.Length || strokes.Skip(1).Any(stroke => stroke.Upper);
    }

    private static bool IsUpper(InputLanguage language, bool isLetterKey)
    {
        var shift = InputState.IsShiftDown();
        return language == InputLanguage.English && isLetterKey ? shift ^ InputState.IsCapsLockOn() : shift;
    }

    private static bool IsIgnorable(int key) =>
        key is 0x10 or 0x11 or 0x12 or 0x14 or 0x5B or 0x5C or 0x5D or 0x90 or 0x91
        || key is >= 0xA0 and <= 0xB7;

    private static bool IsNavigation(int key) =>
        key is 0x09 or 0x0D or 0x13 or 0x1B or 0x2C or 0x2D or 0x2E
        || key is >= 0x21 and <= 0x28
        || key is >= 0x70 and <= 0x87;

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

    private readonly record struct KeyStroke(int VirtualKey, bool Upper);

    private sealed record PendingWord(string Typed, string Alternative, InputLanguage Language, IntPtr Window, IntPtr FocusWindow, long Tick);

    private sealed record ConversionPlan(
        int DeleteLength,
        string Replacement,
        string Original,
        string[] TypedWords,
        InputLanguage Previous,
        InputLanguage Target);

    private sealed record LastConversion(ConversionPlan Plan, long CompletedAt);
}
