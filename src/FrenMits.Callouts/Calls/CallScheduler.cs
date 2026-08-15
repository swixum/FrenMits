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

    // What was said and when, carried between events: two events half a second
    // apart are the repeat these windows exist to catch, and locals could only
    // ever see one event's worth.
    private readonly Dictionary<string, float> _lastSaid = new(StringComparer.Ordinal);
    private float _lastAt = float.NegativeInfinity;
    private Call? _lastCall;

    // Bound on the spoken-line memory, pruned to the duplicate window.
    private const int MaxRemembered = 256;

    // A new pull says everything afresh.
    public void Reset()
    {
        _lastSaid.Clear();
        _lastAt = float.NegativeInfinity;
        _lastCall = null;
    }

    public List<Call> Apply(IEnumerable<Call> calls)
    {
        var ordered = calls.OrderBy(c => c.At).ToList();
        var merged = Merge(ordered);

        var kept = new List<Call>(merged.Count);

        foreach (var c in merged)
        {
            if (_lastSaid.TryGetValue(c.Spoken, out var when) && c.At - when < DuplicateWindow) continue;

            // Two calls landing together: the one that matters more wins the slot.
            if (_lastCall is { } prev && c.At - _lastAt < MinGap)
            {
                if (!Outranks(c, prev)) continue;

                // A call this batch has not handed over yet can still be swapped;
                // one already gone out is only ever talked over.
                if (kept.Count > 0) kept[^1] = c;
                else kept.Add(c);
                _lastSaid[c.Spoken] = c.At;
                _lastCall = c;
                continue;
            }

            kept.Add(c);
            _lastSaid[c.Spoken] = c.At;
            _lastAt = c.At;
            _lastCall = c;
        }

        Prune();
        return kept;
    }

    // Anything past the duplicate window can never match again.
    private void Prune()
    {
        if (_lastSaid.Count <= MaxRemembered) return;

        foreach (var key in _lastSaid.Where(p => _lastAt - p.Value >= DuplicateWindow)
                     .Select(p => p.Key).ToList())
            _lastSaid.Remove(key);

        // Still over means a burst of distinct lines, so the oldest go.
        foreach (var key in _lastSaid.OrderBy(p => p.Value)
                     .Take(Math.Max(0, _lastSaid.Count - MaxRemembered))
                     .Select(p => p.Key).ToList())
            _lastSaid.Remove(key);
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
