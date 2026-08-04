using System;
using System.Collections.Generic;

namespace FrenMits.Encounters;

// A mit a sheet asks for while it is already running.
public static class CoveredRepeats
{
    private const float Slop = 0.01f;

    // Strip covered repeats from one slot's lines, in place.
    public static int Strip(List<MitLine>? lines,
        Func<string, IEnumerable<(string Name, float Duration)>>? buffsIn = null)
    {
        if (lines == null || lines.Count < 2) return 0;
        buffsIn ??= AbilityBook.BuffsIn;

        var order = new List<MitLine>(lines);
        order.Sort((a, b) => a.Time.CompareTo(b.Time));

        var upUntil = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var drop = new List<MitLine>();
        var changed = 0;

        foreach (var line in order)
        {
            // A disabled line neither covers nor needs cleaning.
            if (!line.Enabled || string.IsNullOrWhiteSpace(line.Action)) continue;
            var ours = !line.Custom && line.Jobs.Count == 0;

            // Each line is now a single action — check if it's already covered.
            var mit = ours ? BareMit(line.Action, buffsIn) : null;
            if (mit != null && !AbilityBook.HasCharges(mit)
                && upUntil.TryGetValue(mit, out var end) && end > line.Time + Slop)
            {
                drop.Add(line);
                changed++;
                continue;
            }

            // Only a press the sheet states outright can cover.
            if (!Conditional(line.Action))
            {
                foreach (var (name, dur) in buffsIn(line.Action))
                    upUntil[name] = line.Time + dur;
            }
        }

        foreach (var d in drop) lines.Remove(d);
        return changed;
    }

    // A part the sheet hedges on, like "If Available".
    private static bool Conditional(string text)
    {
        var i = text.IndexOf("if", StringComparison.OrdinalIgnoreCase);
        while (i >= 0)
        {
            var before = i == 0 ? ' ' : text[i - 1];
            var after = i + 2 >= text.Length ? ' ' : text[i + 2];
            if (!char.IsLetter(before) && !char.IsLetter(after)) return true;
            i = text.IndexOf("if", i + 1, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    // The mit this part names, when it names nothing else.
    private static string? BareMit(string text,
        Func<string, IEnumerable<(string Name, float Duration)>> buffsIn)
    {
        // Stars are a footnote marker, not part of a name.
        var clean = text.Replace("*", "").Trim();
        if (clean.Length == 0) return null;
        string? only = null;
        foreach (var (name, _) in buffsIn(clean))
        {
            if (only != null) return null;
            only = name;
        }
        if (only == null) return null;
        // Either spelling counts, "Soil" or "Sacred Soil".
        return string.Equals(only, clean, StringComparison.OrdinalIgnoreCase)
               || string.Equals(only, AbilityBook.Canonical(clean), StringComparison.OrdinalIgnoreCase)
            ? only : null;
    }
}
