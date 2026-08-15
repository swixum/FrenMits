using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Callouts;

// Decides what actually gets said. The rules mirror the plugin's cue engine:
// no repeating yourself, no talking over yourself, and one line for things that
// land together.
public sealed class CallScheduler
{
    // The same words inside this window are the same call said twice.
    public float DuplicateWindow { get; init; } = 2f;

    // Spoken lines need room, or they queue up and land late.
    public float MinGap { get; init; } = 0.2f;

    // Calls this close together are one thought, so they get joined.
    public float MergeWindow { get; init; } = 0.1f;

    public List<Call> Apply(IEnumerable<Call> calls)
    {
        var ordered = calls.OrderBy(c => c.At).ToList();
        var merged = Merge(ordered);

        var kept = new List<Call>(merged.Count);
        var lastSaid = new Dictionary<string, float>(StringComparer.Ordinal);
        var lastAt = float.NegativeInfinity;

        foreach (var c in merged)
        {
            if (lastSaid.TryGetValue(c.Spoken, out var when) && c.At - when < DuplicateWindow) continue;

            // Two calls landing together: the one that matters more wins the slot.
            if (kept.Count > 0 && c.At - lastAt < MinGap)
            {
                if (!Outranks(c, kept[^1])) continue;
                kept[^1] = c;
                lastSaid[c.Spoken] = c.At;
                continue;
            }

            kept.Add(c);
            lastSaid[c.Spoken] = c.At;
            lastAt = c.At;
        }
        return kept;
    }

    // Yours beats the party's, then louder beats quieter.
    private static bool Outranks(Call a, Call b)
        => a.Personal != b.Personal ? a.Personal : a.Severity > b.Severity;

    // A personal call is never merged: it would lose the point of being yours.
    private List<Call> Merge(List<Call> ordered)
    {
        var result = new List<Call>(ordered.Count);
        foreach (var c in ordered)
        {
            if (result.Count == 0) { result.Add(c); continue; }

            var last = result[^1];
            var together = c.At - last.At <= MergeWindow && !c.Personal && !last.Personal;
            if (!together) { result.Add(c); continue; }

            // One call already saying the other is not two calls, and a pile of
            // them joined together is unreadable, so merging stops at a pair.
            if (Overlaps(last.Spoken, c.Spoken)) continue;
            if (last.Text.Contains(" + ", StringComparison.Ordinal)) continue;

            result[^1] = last with
            {
                Text = Join(last.Text, c.Text),
                Tts = Join(last.Spoken, c.Spoken),
                Severity = (CallSeverity)Math.Max((int)last.Severity, (int)c.Severity),
            };
        }
        return result;
    }

    private static bool Overlaps(string a, string b)
        => a.Contains(b, StringComparison.OrdinalIgnoreCase)
        || b.Contains(a, StringComparison.OrdinalIgnoreCase);

    private static string Join(string a, string b)
        => a.Length == 0 ? b : b.Length == 0 ? a : $"{a} + {b}";
}
