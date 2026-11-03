namespace KeyFlip.Core;

public static class HotkeyFormatter
{
    public static string Format(uint modifiers, uint key)
    {
        var parts = new List<string>();
        if ((modifiers & 2) != 0) parts.Add("Ctrl");
        if ((modifiers & 1) != 0) parts.Add("Alt");
        if ((modifiers & 4) != 0) parts.Add("Shift");
        if ((modifiers & 8) != 0) parts.Add("Win");
        parts.Add(((Keys)key).ToString());
        return string.Join(" + ", parts);
    }
}
