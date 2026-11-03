using System.Text;
using System.Text.RegularExpressions;

namespace KeyFlip.Core;

/// <summary>Converts arbitrary text between the English and Persian keyboard layouts.</summary>
public static class TextConverter
{
    /// <summary>Picks one direction for the whole text, based on which script dominates.</summary>
    public static string Convert(string text, out InputLanguage target)
    {
        var maps = BuildMaps();
        target = DetectDirection(text, maps);
        return ConvertSegment(text, target, maps);
    }

    /// <summary>Swaps every word to the opposite script on its own.</summary>
    public static string ConvertEachWord(string text, out InputLanguage target)
    {
        var maps = BuildMaps();
        target = InputLanguage.Persian;
        var builder = new StringBuilder(text.Length);

        foreach (var token in Regex.Split(text, @"(\s+)"))
        {
            if (token.Length == 0 || char.IsWhiteSpace(token[0]))
            {
                builder.Append(token);
                continue;
            }

            target = DetectDirection(token, maps);
            builder.Append(ConvertSegment(token, target, maps));
        }

        return builder.ToString();
    }

    private static InputLanguage DetectDirection(string text, Maps maps)
    {
        var latinCount = text.Count(char.IsAsciiLetter);
        var persianCount = text.Count(maps.ToEnglish.ContainsKey);
        return latinCount >= persianCount ? InputLanguage.Persian : InputLanguage.English;
    }

    private static string ConvertSegment(string text, InputLanguage target, Maps maps)
    {
        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            builder.Append(target == InputLanguage.Persian ? ToPersianChar(text, i, maps) : maps.ToEnglish.GetValueOrDefault(text[i], text[i]));
        }

        return builder.ToString();
    }

    private static char ToPersianChar(string text, int index, Maps maps)
    {
        var c = text[index];
        if (!maps.ToPersian.TryGetValue(char.ToLowerInvariant(c), out var persian))
        {
            return c;
        }

        // Punctuation keys are letters in Persian; convert them only inside words.
        return char.IsAsciiLetter(c) || IsInsideWord(text, index) ? persian : c;
    }

    private static bool IsInsideWord(string text, int index) =>
        index > 0 && index < text.Length - 1
        && char.IsAsciiLetter(text[index - 1]) && char.IsAsciiLetter(text[index + 1]);

    private static Maps BuildMaps()
    {
        var toPersian = new Dictionary<char, char>();
        var toEnglish = new Dictionary<char, char>();
        foreach (var key in KeyMap.AllKeys)
        {
            var english = KeyMap.ToEnglish(key);
            var persian = KeyMap.ToPersian(key);
            toPersian[english] = persian;
            toEnglish[persian] = english;
        }

        return new Maps(toPersian, toEnglish);
    }

    private sealed record Maps(Dictionary<char, char> ToPersian, Dictionary<char, char> ToEnglish);
}
