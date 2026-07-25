using System;
using System.Collections.Generic;
using System.Text;

namespace FrenMits;

// A mit a sheet asks for while it is already running from an earlier press.
//
// Reference sheets write the name again on every hit one press covers - that is
// how they say "you are still shielded here". Read back as instructions it turns
// into a press nobody can make: FRU lists Feint for Banish III at 5:23 and again
// for Light Rampant at 5:33, ten seconds into a fifteen second buff with eighty
// seconds of recast to go. The same goes for the melee Feint across Shell Crusher
// and Shockwave Pulsar, and the caster Addle beside the first pair.
//
// Taking them out is also what makes the carry-over ghost appear: the cell empties,
// and the grid fills it with a dim "-> Feint" pointing at the press that IS
// covering the hit. Nothing is lost from the sheet, and the call stops firing for
// a button that has not come back.
//
// Nothing with a second charge is ever taken: for those the buff being up says
// nothing about whether you can press it.
//
// Only whole words get taken - a part has to BE the mit's name and nothing else.
// "Zoe EukProg/Holos" loses Holos and keeps the rest; "Rep Short CD on M1" and
// "Party Mit (WAR/PLD)" are never touched. Lines you edited yourself are left
// alone too, though their presses still count against what follows.
public static class CoveredRepeats
{
    private const float Slop = 0.01f;

    // Strip every already-covered repeat from one slot's lines, editing the list
    // in place and returning how many lines changed (a line whose every part goes
    // is removed outright).
    //
    // `buffsIn` resolves an action's mits and their durations; it defaults to the
    // curated table and is only passed by tests wanting a fixed one.
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
            // A disabled line is never pressed, so it neither covers anything nor
            // needs cleaning up. A line you wrote or edited is yours: it still
            // counts as a press, but it is not ours to rewrite. Job-gated lines
            // belong to one job in a shared column, which is a different player's
            // timer from the column's own.
            if (!line.Enabled || string.IsNullOrWhiteSpace(line.Action)) continue;
            var ours = !line.Custom && line.Jobs.Count == 0;

            var parts = Split(line.Action);
            var kept = new List<Part>(parts.Count);
            foreach (var p in parts)
            {
                var mit = ours ? BareMit(p.Text, buffsIn) : null;
                // A second charge is a second press: Consolation runs 30s but comes
                // in twos, so FRU's healer laying one at 0:15 and another at 0:35 is
                // doing it right, not repeating themselves.
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

            // Only what survives is actually pressed, and only a press the sheet
            // states outright can be what covers the next hit.
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

    // One "/"- or "+"-separated piece of an action text, with the separator that
    // preceded it ("" for the first).
    private readonly record struct Part(char Sep, string Text);

    // Split on separators at the top level only, so the "/" inside a job gate like
    // "Party Mit (WAR/PLD)" stays where it belongs.
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

    // Put the survivors back, each behind the separator it arrived with. A "+"
    // gets its spaces back and a "/" stays tight, which is how the sheets that use
    // each one write them.
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

    // A part the sheet is hedging on rather than promising: UCOB's healer gets
    // "Kerachole (If Available)" at 11:22 and a plain Kerachole eleven seconds
    // later, so the second one is the press for anyone the first didn't have. A
    // maybe can't be what covers the next hit, and the line that follows it stays.
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

    // The mit this part names, when the part is nothing BUT that name. Anything
    // carrying extra words ("Feint (Shiva)", "Rep Short CD on M1") means something
    // the sheet is saying beyond the button, so it stays.
    private static string? BareMit(string text,
        Func<string, IEnumerable<(string Name, float Duration)>> buffsIn)
    {
        // The stars are a footnote marker in the source sheets, not part of a name.
        var clean = text.Replace("*", "").Trim();
        if (clean.Length == 0) return null;
        string? only = null;
        foreach (var (name, _) in buffsIn(clean))
        {
            if (only != null) return null;
            only = name;
        }
        if (only == null) return null;
        // Either spelling counts: the sheets write "Soil" as often as "Sacred Soil".
        return string.Equals(only, clean, StringComparison.OrdinalIgnoreCase)
               || string.Equals(only, Cooldowns.Canonical(clean), StringComparison.OrdinalIgnoreCase)
            ? only : null;
    }
}
