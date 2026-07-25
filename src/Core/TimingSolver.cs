using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// Cooldown-aware offset solver (Sheet View's "Solve timing" button and the
// automatic AutoCooldownTiming pass) that times each active-slot press so one
// press blankets the run of hits its buff can reach and its cooldown is back for
// the next mechanic.
public static class TimingSolver
{
    // How much buff has to be LEFT when the last hit of a run lands.
    //
    // Without it the solver treats a buff as reaching exactly one duration ahead,
    // so a 15s Feint stretched back to a hit 15s earlier expires on the very hit
    // it was planned for - M11S solved Feint for Impact at 26s to a press at 11s,
    // fading at 26.0. A press that lands on the boundary is a press that did
    // nothing, and it burns the recast too.
    //
    // 1.5s is the slop worth allowing for: a sheet's row time is the log's damage
    // timestamp, the game snapshots a little before that, and no two pulls run at
    // exactly the same pace.
    private const float Grace = 1.5f;

    // Time the fight's active-slot lines against the given mechanic hit times,
    // mutating line.OffsetSeconds / CoverUntil in place and returning how many
    // lines actually changed.
    //
    // `mitsFor` resolves an action's tracked mits; it defaults to the live game
    // sheets and is only passed explicitly by the tests, which need a fixed mit
    // table to assert the solver's invariants without a game running.
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
            var dur = mits.Min(m => m.Duration > 0f ? m.Duration : 15f);   // shortest buff bounds the reach
            var reach = MathF.Max(dur - Grace, dur * 0.5f);                // how far it can honestly stretch
            var ready = mits.Max(m => readyAt.GetValueOrDefault(m.Name, -9999f)); // all its abilities must be up

            // A press the user timed by hand: leave it, but book the hits its buff
            // already covers so other presses don't double up on them.
            if (line.OffsetManual)
            {
                var press0 = line.Time - line.OffsetSeconds;
                MarkCovered(press0, MathF.Max(press0 + reach, line.CoverUntil));
                foreach (var m in mits) readyAt[m.Name] = press0 + (m.Recast > 0f ? m.Recast : 60f);
                continue;
            }

            var iT = Nearest(line.Time);
            var T = hits[iT];

            // Grow the run: back to the earliest still-uncovered hit the buff can
            // reach (and the cooldown allows), then forward within the buff window.
            // Capped at `reach`, not the full duration, so pressing at the front of
            // the run still leaves buff on the back of it.
            int lo = iT, hi = iT;
            while (lo - 1 >= 0 && !covered[lo - 1]
                   && hits[hi] - hits[lo - 1] <= reach
                   && hits[lo - 1] >= ready - 0.01f) lo--;
            while (hi + 1 < n && !covered[hi + 1]
                   && hits[hi + 1] - hits[lo] <= reach) hi++;

            var last = hits[hi];
            var readyFloor = MathF.Max(ready, 0f);

            // Press as EARLY as the cooldown allows while the buff still reaches the
            // last hit of the run, keeping a margin so the recast starts ASAP and
            // the mit is back for the NEXT mechanic.
            var margin = MathF.Min(lead, dur * 0.5f);
            var press = MathF.Max(readyFloor, last - dur + margin);
            // ...but never so early the buff has faded by the run's FRONT hit.
            if (press > hits[lo]) press = MathF.Max(readyFloor, hits[lo]);

            // Only pull the press earlier if it can still be up for its own hit -
            // properly up, with Grace to spare, not expiring as the damage lands.
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
