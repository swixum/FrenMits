using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// Cooldown-aware offset solver, timed in snapshot terms like the recap.
public static class TimingSolver
{
    // Keep equal to the recap, or plan and grade disagree.
    private const float SnapshotLead = MitRecap.SnapshotLead;
    // A press lands as a status about this much later.
    private const float ApplyDelay = 0.6f;
    // How much buff must be left at the last covered snapshot.
    private const float Grace = 0.8f;
    // The least a press may precede a hit and still apply.
    private const float MinLead = SnapshotLead + ApplyDelay;

    // Time the active-slot lines against the hit times, in place.
    public static int Solve(FightProfile fight, IReadOnlyList<float> hitTimes, float lead = 5f,
        Func<string, IEnumerable<Cooldowns.PlanMit>>? mitsFor = null)
    {
        mitsFor ??= Cooldowns.PlanMits;
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
            var mits = mitsFor(line.Action).ToList();
            if (mits.Count == 0) continue;

            // Only something with a real buff can be pressed early.
            var covering = mits.Where(m => m.Duration > 0f).ToList();
            if (covering.Count == 0)
            {
                foreach (var m in mits) readyAt[m.Name] = line.Time + (m.Recast > 0f ? m.Recast : 60f);
                continue;
            }

            var dur = covering.Min(m => m.Duration);        // shortest buff bounds the reach
            // How far past the press the last covered hit may land.
            var reach = dur + ApplyDelay + SnapshotLead - Grace;
            // How far apart a run's first and last hits may be.
            var span = MathF.Max(reach - MinLead, dur * 0.5f);
            var ready = mits.Max(m => readyAt.GetValueOrDefault(m.Name, -9999f)); // all its abilities must be up

            // Leave a hand-timed press, but book the hits it covers.
            if (line.OffsetManual)
            {
                var press0 = line.Time - line.OffsetSeconds;
                MarkCovered(press0, MathF.Max(press0 + reach, line.CoverUntil));
                foreach (var m in mits) readyAt[m.Name] = press0 + (m.Recast > 0f ? m.Recast : 60f);
                continue;
            }

            var iT = Nearest(line.Time);
            var T = hits[iT];

            // Grow the run back to the earliest hit, then forward.
            int lo = iT, hi = iT;
            while (lo - 1 >= 0 && !covered[lo - 1]
                   && hits[hi] - hits[lo - 1] <= span
                   && hits[lo - 1] >= ready - 0.01f) lo--;
            while (hi + 1 < n && !covered[hi + 1]
                   && hits[hi + 1] - hits[lo] <= span) hi++;

            var last = hits[hi];
            var readyFloor = MathF.Max(ready, 0f);

            // Press as early as the cooldown allows, keeping margin.
            var margin = MathF.Min(lead, dur * 0.5f);
            var press = MathF.Max(readyFloor, last - reach + margin - Grace);
            // But never so late it misses the front hit's snapshot.
            if (press > hits[lo] - MinLead) press = MathF.Max(readyFloor, hits[lo] - MinLead);

            // Write the offset when the press can cover its own hit.
            if (press <= T + 0.01f && press + reach >= T - 0.01f)
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
            MarkCovered(press, press + reach);
            foreach (var m in mits) readyAt[m.Name] = press + (m.Recast > 0f ? m.Recast : 60f);
        }

        return changed;
    }
}
