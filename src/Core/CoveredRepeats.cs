using System;
using System.Collections.Generic;
using System.Text;

namespace FrenMits;

// A mit a sheet asks for while it is already running.
public static class CoveredRepeats
{
    private const float Slop = 0.01f;

    // Strip covered repeats from one slot's lines, in place.
    public static int Strip(List<MitLine>? lines,
        Func<string, IEnumerable<(string Name, float Duration)>>? buffsIn = null)
    {
        if (lines == null || lines.Count < 2) return 0;
        buffsIn ??= Cooldowns.BuffsIn;

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

            var parts = Split(line.Action);
            var kept = new List<Part>(parts.Count);
            foreach (var p in parts)
            {
                var mit = ours ? BareMit(p.Text, buffsIn) : null;
                // A second charge is a second press.
                if (mit != null && !Cooldowns.HasCharges(mit)
                    && upUntil.TryGetValue(mit, out var end) && end > line.Time + Slop)
                    continue;
                kept.Add(p);
            }

            var pressed = kept.Count == parts.Count ? line.Action : Join(kept);
            if (kept.Count != parts.Count)
            {
                if (pressed.Length == 0) drop.Add(line);
                else line.Action = pressed;
                changed++;
            }

            // Only a press the sheet states outright can cover.
            foreach (var p in kept)
            {
                if (Conditional(p.Text)) continue;
                foreach (var (name, dur) in buffsIn(p.Text))
                    upUntil[name] = line.Time + dur;
            }
        }

        foreach (var d in drop) lines.Remove(d);
        return changed;
    }

    // One piece of an action text, with its separator.
    private readonly record struct Part(char Sep, string Text);

    // Split at the top level, so a job gate stays whole.
    private static List<Part> Split(string action)
    {
        var parts = new List<Part>();
        var depth = 0;
        var start = 0;
        var sep = '\0';
        for (var i = 0; i < action.Length; i++)
        {
            var c = action[i];
            if (c == '(') depth++;
            else if (c == ')') { if (depth > 0) depth--; }
            else if (depth == 0 && (c == '/' || c == '+'))
            {
                parts.Add(new Part(sep, action.Substring(start, i - start)));
                sep = c;
                start = i + 1;
            }
        }
        parts.Add(new Part(sep, action.Substring(start)));
        return parts;
    }

    // Put survivors back behind the separator they arrived with.
    private static string Join(List<Part> parts)
    {
        var sb = new StringBuilder();
        foreach (var p in parts)
        {
            var text = p.Text.Trim();
            if (text.Length == 0) continue;
            if (sb.Length > 0) sb.Append(p.Sep == '+' ? " + " : "/");
            sb.Append(text);
        }
        return sb.ToString();
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
               || string.Equals(only, Cooldowns.Canonical(clean), StringComparison.OrdinalIgnoreCase)
            ? only : null;
    }
}
