using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FrenMits.Ui;

// The plugin's shared display text: m:ss clocks, and numerals as digits.
public static class Fmt
{
    // Roman numerals read as digits everywhere: "Physis II" shows as "Physis 2".
    // Names keep their roman spelling in the data, so lookups and plan codes
    // still match; only what a person reads gets swapped.
    public static string Numerals(string? text)
    {
        if (string.IsNullOrEmpty(text) || text!.IndexOfAny(RomanLetters) < 0) return text ?? "";
        if (_numerals.TryGetValue(text, out var hit)) return hit;
        var swapped = RomanToken.Replace(text, m => Swap(text, m));
        if (_numerals.Count >= NumeralCacheMax) _numerals.Clear();
        _numerals[text] = swapped;
        return swapped;
    }

    // The words of `text` whose shown form is `shown`, or null. Lets a box that
    // searches by what's on screen ("Physis 2") edit the stored spelling under it.
    public static string? StoredFragment(string text, string shown)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(shown)) return null;
        var words = text.Split(' ');
        for (var i = 0; i < words.Length; i++)
        {
            var span = words[i];
            for (var j = i; j < words.Length; j++)
            {
                if (j > i) span += " " + words[j];
                if (string.Equals(Numerals(span), shown, StringComparison.OrdinalIgnoreCase)) return span;
            }
        }
        return null;
    }

    private static readonly char[] RomanLetters = { 'I', 'V', 'X' };

    // I/V/X only, so a lone "C" or "D" (a waymark, a role) is never a number.
    private static readonly Regex RomanToken =
        new(@"(?<![\p{L}\p{N}'])(?=[IVX])X{0,3}(?:IX|IV|V?I{0,3})(?![\p{L}\p{N}'])", RegexOptions.Compiled);

    private const int NumeralCacheMax = 4096;
    private static readonly Dictionary<string, string> _numerals = new(StringComparer.Ordinal);

    private static string Swap(string text, Match m)
    {
        // A single I, V or X is a letter as often as a number, so it only counts
        // when it trails a capitalized word and nothing wordy follows: "Towers V",
        // "Gravitas II (Part I)", but not "I Crave Violence".
        if (m.Length == 1 && !(TrailsAName(text, m.Index) && EndsThePhrase(text, m.Index + 1)))
            return m.Value;
        return Value(m.Value).ToString();
    }

    private static bool TrailsAName(string s, int at)
    {
        var i = at - 1;
        while (i >= 0 && s[i] == ' ') i--;
        if (i < 0 || i == at - 1) return false; // nothing before it, or no gap
        var end = i;
        while (i >= 0 && (char.IsLetterOrDigit(s[i]) || s[i] == '\'')) i--;
        return end > i && char.IsUpper(s[i + 1]);
    }

    private static bool EndsThePhrase(string s, int at)
    {
        while (at < s.Length && s[at] == ' ') at++;
        return at >= s.Length || !char.IsLetterOrDigit(s[at]);
    }

    private static int Value(string roman)
    {
        var total = 0;
        for (var i = 0; i < roman.Length; i++)
        {
            var v = roman[i] == 'X' ? 10 : roman[i] == 'V' ? 5 : 1;
            var next = i + 1 < roman.Length ? (roman[i + 1] == 'X' ? 10 : roman[i + 1] == 'V' ? 5 : 1) : 0;
            total += v < next ? -v : v;
        }
        return total;
    }

    // Nearest-second m:ss, the overlay countdown style.
    public static string MmssRound(float seconds)
    {
        var s = (int)MathF.Round(seconds);
        return $"{s / 60}:{s % 60:00}";
    }

    // Floor m:ss, so 1:59.9 still reads 1:59.
    public static string MmssFloor(float seconds)
    {
        var s = (int)seconds;
        return $"{s / 60}:{s % 60:00}";
    }

    // Nearest-second m:ss with a minus for pre-pull rows.
    public static string MmssSigned(float seconds)
    {
        var s = (int)MathF.Round(seconds);
        var sign = s < 0 ? "-" : "";
        s = Math.Abs(s);
        return $"{sign}{s / 60}:{s % 60:00}";
    }
}
