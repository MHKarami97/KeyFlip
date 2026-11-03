using System.Text;

namespace KeyFlip.Core;

/// <summary>Converts arbitrary text between the English and Persian keyboard layouts.</summary>
public static class TextConverter
{
    private static readonly Dictionary<char, char> ToPersianMap = new();
    private static readonly Dictionary<char, char> ToEnglishMap = new();

    static TextConverter()
    {
        foreach (var key in KeyMap.AllKeys)
        {
            ToPersianMap[KeyMap.ToEnglish(key)] = KeyMap.ToPersian(key);
            ToEnglishMap[KeyMap.ToPersian(key)] = KeyMap.ToEnglish(key);
        }
    }

    public static string Convert(string text, out InputLanguage target)
    {
        var latinCount = text.Count(char.IsAsciiLetter);
        var persianCount = text.Count(ToEnglishMap.ContainsKey);
        target = latinCount >= persianCount ? InputLanguage.Persian : InputLanguage.English;

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            builder.Append(target == InputLanguage.Persian ? ToPersianChar(text, i) : ToEnglishChar(text[i]));
        }

        return builder.ToString();
    }

    private static char ToEnglishChar(char c) => ToEnglishMap.GetValueOrDefault(c, c);

    private static char ToPersianChar(string text, int index)
    {
        var c = text[index];
        if (!ToPersianMap.TryGetValue(char.ToLowerInvariant(c), out var persian))
        {
            return c;
        }

        // Punctuation keys (; ' [ ] ,) are letters in Persian; convert them only inside words.
        return char.IsAsciiLetter(c) || IsInsideWord(text, index) ? persian : c;
    }

    private static bool IsInsideWord(string text, int index) =>
        index > 0 && index < text.Length - 1
        && char.IsAsciiLetter(text[index - 1]) && char.IsAsciiLetter(text[index + 1]);
}
