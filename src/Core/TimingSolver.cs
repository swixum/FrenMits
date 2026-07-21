using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// Cooldown-aware offset solver, shared by Sheet View's "Solve timing" button and
// the automatic on-load pass (AutoCooldownTiming). For the active slot's presses
// it times each mit so one press blankets the run of hits its buff can reach
// (pressed at the front of that run) and its cooldown is back in time for the next
// mechanic. Reads real durations/recasts from the game data. Respects offsets set
// by hand (OffsetManual). Reruns cleanly - it recomputes from the rows each time,
// so it never freezes its own last output, and re-solving an already-solved sheet
// reports no change.
public static class TimingSolver
{
    // Time the fight's active-slot lines against the given mechanic hit times.
    // Mutates line.OffsetSeconds / CoverUntil in place and returns how many lines
    // actually changed. Does NOT persist - the caller saves.
    public static int Solve(FightProfile fight, IReadOnlyList<float> hitTimes)
    {
        if (fight == null || hitTimes == null) return 0;
        var hits = hitTimes.OrderBy(t => t).ToArray();
        var n = hits.Length;
        if (n == 0) return 0;

        var covered = new bool[n];
        var readyAt = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        int Nearest(float t)
        {
            var best = 0; var bd = float.MaxValue;
            for (var k = 0; k < n; k++) { var d = MathF.Abs(hits[k] - t); if (d < bd) { bd = d; best = k; } }
            return best;
        }
        void MarkCovered(float from, float to)
        {
            for (var k = 0; k < n; k++) if (hits[k] >= from - 0.01f && hits[k] <= to + 0.01f) covered[k] = true;
        }

        var lines = fight.Lines
            .Where(l => l.Enabled && !string.IsNullOrWhiteSpace(l.Action))
            .OrderBy(l => l.Time).ToList();

        var changed = 0;
        foreach (var line in lines)
        {
            var mits = Cooldowns.PlanMits(line.Action).ToList();
            if (mits.Count == 0) continue;
            var dur = mits.Min(m => m.Duration > 0f ? m.Duration : 15f);   // shortest buff bounds the reach
            var ready = mits.Max(m => readyAt.GetValueOrDefault(m.Name, -9999f)); // all its abilities must be up

            // A press the user timed by hand: leave it, but book the hits its buff
            // already covers so other presses don't double up on them.
            if (line.OffsetManual)
            {
                var press0 = line.Time - line.OffsetSeconds;
                MarkCovered(press0, MathF.Max(press0 + dur, line.CoverUntil));
                foreach (var m in mits) readyAt[m.Name] = press0 + (m.Recast > 0f ? m.Recast : 60f);
                continue;
            }

            var iT = Nearest(line.Time);
            var T = hits[iT];

            // Grow the run: back to the earliest still-uncovered hit the buff can
            // reach (and the cooldown allows), then forward within the buff window.
            int lo = iT, hi = iT;
            while (lo - 1 >= 0 && !covered[lo - 1]
                   && hits[hi] - hits[lo - 1] <= dur + 0.01f
                   && hits[lo - 1] >= ready - 0.01f) lo--;
            while (hi + 1 < n && !covered[hi + 1]
                   && hits[hi + 1] - hits[lo] <= dur + 0.01f) hi++;

            var press = MathF.Max(hits[lo], MathF.Max(ready, 0f)); // front of run, but never before it's ready
            var last = hits[hi];

            // Only pull the press earlier if it can still be up for its own hit.
            if (press <= T + 0.01f)
            {
                var newOff = MathF.Round((T - press) * 10f) / 10f;
                var newCover = last > T + 0.5f ? last : 0f;
                if (MathF.Abs(line.OffsetSeconds - newOff) > 0.001f
                    || MathF.Abs(line.CoverUntil - newCover) > 0.001f)
                {
                    line.OffsetSeconds = newOff;
                    line.CoverUntil = newCover;
                    changed++;
                }
            }
            MarkCovered(press, press + dur);
            foreach (var m in mits) readyAt[m.Name] = press + (m.Recast > 0f ? m.Recast : 60f);
        }

        return changed;
    }
}
