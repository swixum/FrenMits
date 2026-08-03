using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// Cooldown-aware solver that produces individual MitPress usage windows
// for each mitigation in a fight plan.
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
    // The least room to press in that reaching for a bonus hit may leave.
    private const float MinWindow = 2.5f;

    public static IReadOnlyList<MitPress> Solve(FightProfile fight, IReadOnlyList<float> hitTimes,
        bool showUseWindows = true, float maxUseWindowSeconds = 7.5f,
        Func<string, IEnumerable<Cooldowns.PlanMit>>? mitsFor = null)
    {
        mitsFor ??= Cooldowns.PlanMits;
        var result = new List<MitPress>();
        if (fight == null || hitTimes == null) return result;
        var hits = hitTimes.OrderBy(t => t).ToArray();
        var n = hits.Length;
        if (n == 0) return result;

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

        foreach (var line in lines)
        {
            var mits = mitsFor(line.Action).ToList();
            if (mits.Count == 0)
            {
                // Fallback for custom actions so they still appear on the timeline.
                mits.Add(new Cooldowns.PlanMit(line.Action, 0f, 1, line.Action, 1, 0f));
            }

            var iT = Nearest(line.Time);
            var T = hits[iT];

            foreach (var m in mits)
            {
                var dur = m.Duration;
                var ready = readyAt.GetValueOrDefault(m.Name, -9999f);
                var readyFloor = MathF.Max(ready, 0f);

                if (dur <= 0f || !showUseWindows)
                {
                    // Instant/no-duration or disabled dynamic windows
                    var wStart = line.Time;
                    var wEnd = line.Time;
                    if (line.OffsetManual) { wStart -= line.OffsetSeconds; wEnd -= line.OffsetSeconds; }
                    result.Add(new MitPress(line, m.Name, wStart, wEnd, line.Time, dur));
                    readyAt[m.Name] = wEnd + (m.Recast > 0f ? m.Recast : 60f);
                    continue;
                }

                // How far past the press the last covered hit may land.
                var reach = dur + ApplyDelay + SnapshotLead - Grace;
                // How far apart a run's first and last hits may be.
                var span = MathF.Max(reach - MinLead, dur * 0.5f);
                
                // Find next explicitly assigned use of this mitigation
                float nextHitTime = float.MaxValue;
                foreach (var nextLine in lines.Where(l => l.Time > line.Time))
                {
                    if (mitsFor(nextLine.Action).Any(nm => nm.Name == m.Name))
                    {
                        nextHitTime = nextLine.Time;
                        break;
                    }
                }
                var latestByNext = nextHitTime != float.MaxValue ? nextHitTime - (m.Recast > 0f ? m.Recast : 60f) - 3f : float.MaxValue;

                // lo stays at the mechanic's own hit — sheet lines are explicitly assigned to
                // their target; reaching back to earlier mechanics creates incorrect early calls.
                var lo = iT;
                var hi = iT;

                // Window end: latest you can press and still cover the assigned mechanic.
                var windowEnd = hits[lo] - MinLead;
                if (windowEnd > latestByNext) windowEnd = latestByNext;

                // Never demand more room than the window a user allowed, or a
                // tight Max window duration would block every expansion.
                var keepOpen = MathF.Min(MinWindow, maxUseWindowSeconds);

                // Expand hi forward: catch additional hits AFTER the mechanic within
                // the buff. Each extra hit drags the earliest press later, since the
                // buff has to still be up when that one lands - so a hit near the far
                // end of `span` squeezes the window down onto the mechanic this line
                // was actually assigned to. A bonus hit is not worth leaving no room
                // to press in, so stop while a usable window remains.
                while (hi + 1 < n && !covered[hi + 1]
                       && hits[hi + 1] - hits[lo] <= span
                       && MathF.Max(readyFloor, hits[hi + 1] - reach) <= latestByNext - 3f
                       && windowEnd - MathF.Max(readyFloor, hits[hi + 1] - reach) >= keepOpen) hi++;

                var last = hits[hi];

                // Window start: earliest press that still covers all expanded hits.
                var absoluteEarliest = MathF.Max(readyFloor, last - reach);
                if (absoluteEarliest > windowEnd) absoluteEarliest = windowEnd; // clamp: can't be later than end

                var windowStart = MathF.Max(absoluteEarliest, windowEnd - maxUseWindowSeconds);
                if (windowStart > windowEnd) windowStart = windowEnd; // sanity clamp

                if (line.OffsetManual) 
                { 
                    windowStart -= line.OffsetSeconds; 
                    windowEnd -= line.OffsetSeconds; 
                }

                result.Add(new MitPress(line, m.Name, windowStart, windowEnd, T, dur));
                
                MarkCovered(windowStart, windowStart + reach);
                readyAt[m.Name] = windowStart + (m.Recast > 0f ? m.Recast : 60f);
            }
        }

        return result;
    }
}
