using System.Text;
using System.Text.RegularExpressions;

namespace KeyFlip.Core;

/// <summary>Converts arbitrary text between the English and Persian keyboard layouts.</summary>
public static partial class TextConverter
{
    [GeneratedRegex(@"(\s+)")]
    private static partial Regex WhitespaceSplit();

    /// <summary>Picks one direction for the whole text, based on which script dominates.</summary>
    public static string Convert(string text, out InputLanguage target)
    {
        var maps = KeyMap.GetSnapshot();
        target = DetectDirection(text, maps);
        return ConvertSegment(text, target, maps);
    }

    /// <summary>Swaps every word to the opposite script on its own.</summary>
    public static string ConvertEachWord(string text, out InputLanguage target)
    {
        var maps = KeyMap.GetSnapshot();
        target = InputLanguage.Persian;
        var builder = new StringBuilder(text.Length);

        foreach (var token in WhitespaceSplit().Split(text))
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

    private static InputLanguage DetectDirection(string text, KeyMap.Snapshot maps)
    {
        var latinCount = 0;
        var mappedPunctCount = 0;
        var persianCount = 0;

        foreach (var raw in text)
        {
            if (char.IsAsciiLetter(raw))
            {
                latinCount++;
                continue;
            }

            if (maps.ToPersianMap.ContainsKey(char.ToLowerInvariant(raw)))
            {
                mappedPunctCount++;
            }

            var normalized = NormalizePersianChar(raw);
            if (maps.ToEnglishMap.ContainsKey(normalized) || normalized is 'آ' or 'ژ')
            {
                persianCount++;
            }
        }

        if (latinCount > 0)
        {
            latinCount += mappedPunctCount;
        }

        return latinCount >= persianCount ? InputLanguage.Persian : InputLanguage.English;
    }

    private static string ConvertSegment(string text, InputLanguage target, KeyMap.Snapshot maps)
    {
        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (target == InputLanguage.Persian)
            {
                builder.Append(ToPersianChar(text, i, maps.ToPersianMap));
            }
            else
            {
                var c = NormalizePersianChar(text[i]);
                builder.Append(maps.ToEnglishMap.GetValueOrDefault(c, text[i]));
            }
        }

        return builder.ToString();
    }

    private static char ToPersianChar(string text, int index, IReadOnlyDictionary<char, char> toPersian)
    {
        var c = text[index];
        if (!toPersian.TryGetValue(char.ToLowerInvariant(c), out var persian))
        {
            return c;
        }

        // Preserve decimal/thousands commas embedded inside numbers such as "1,000".
        if (c == ',' && index > 0 && index < text.Length - 1
            && char.IsDigit(text[index - 1]) && char.IsDigit(text[index + 1]))
        {
            return c;
        }

        return persian;
    }

    private static char NormalizePersianChar(char c) => c switch
    {
        'ي' => 'ی',
        'ك' => 'ک',
        _ => c
    };
}
