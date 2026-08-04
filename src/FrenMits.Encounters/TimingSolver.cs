using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Encounters;

// Cooldown-aware solver that produces individual MitPress usage windows
// for each mitigation in a fight plan.
public static class TimingSolver
{
    // Keep equal to the recap, or plan and grade disagree.
    // Damage is decided a beat before the hit, so plan and grade from there.
    public const float SnapshotLead = 0.7f;
    // A press lands as a status about this much later.
    private const float ApplyDelay = 0.6f;
    // How much buff must be left at the last covered snapshot.
    private const float Grace = 0.8f;
    // The least a press may precede a hit and still apply.
    private const float MinLead = SnapshotLead + ApplyDelay;
    // The least room to press in that reaching for a bonus hit may leave.
    private const float MinWindow = 2.5f;

    // Which tracked mits an action cell names, supplied by the host.
    public static Func<string, IEnumerable<AbilityBook.PlanMit>> MitsFor { get; set; } =
        _ => Array.Empty<AbilityBook.PlanMit>();

    // Which (row, mit) pairs only restate a press still running from an earlier
    // row. Lines must be in time order.
    private static HashSet<(MitLine, string)> Lingering(
        List<MitLine> lines, Func<string, IEnumerable<AbilityBook.PlanMit>> mitsFor)
    {
        var keys = new List<(MitLine, string)>();
        var uses = new List<(string Name, float Time, float Duration, float CoverUntil)>();
        foreach (var l in lines)
            foreach (var m in mitsFor(l.Action))
            {
                keys.Add((l, m.Name));
                uses.Add((m.Name, l.Time, m.Duration, l.CoverUntil));
            }

        var carried = CarryOver.Mark(uses);
        var set = new HashSet<(MitLine, string)>();
        for (var i = 0; i < carried.Length; i++)
            if (carried[i]) set.Add(keys[i]);
        return set;
    }

    public static IReadOnlyList<MitPress> Solve(FightProfile fight, IReadOnlyList<float> hitTimes,
        bool showUseWindows = true, float maxUseWindowSeconds = 7.5f,
        Func<string, IEnumerable<AbilityBook.PlanMit>>? mitsFor = null)
    {
        mitsFor ??= MitsFor;
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

        // Rows that only restate a mit still running from an earlier press. They
        // are not a second use, so they must not cap how late that press may go
        // and must not claim the cooldown on their own.
        var lingering = Lingering(lines, mitsFor);

        foreach (var line in lines)
        {
            var mits = mitsFor(line.Action).ToList();
            if (mits.Count == 0)
            {
                // Fallback for custom actions so they still appear on the timeline.
                mits.Add(new AbilityBook.PlanMit(line.Action, 0f, 1, line.Action, 1, 0f));
            }

            var iT = Nearest(line.Time);
            var T = hits[iT];

            foreach (var m in mits)
            {
                var dur = m.Duration;
                var ready = readyAt.GetValueOrDefault(m.Name, -9999f);
                var readyFloor = MathF.Max(ready, 0f);
                // The earlier press already spent this cooldown; the row still
                // produces a press so the call keeps its place on the boards.
                var restated = lingering.Contains((line, m.Name));

                if (dur <= 0f || !showUseWindows)
                {
                    // Instant/no-duration or disabled dynamic windows
                    var wStart = line.Time;
                    var wEnd = line.Time;
                    if (line.OffsetManual) { wStart -= line.OffsetSeconds; wEnd -= line.OffsetSeconds; }
                    result.Add(new MitPress(line, m.Name, wStart, wEnd, line.Time, dur));
                    if (!restated) readyAt[m.Name] = wEnd + (m.Recast > 0f ? m.Recast : 60f);
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
                    if (lingering.Contains((nextLine, m.Name))) continue;
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
                if (!restated) readyAt[m.Name] = windowStart + (m.Recast > 0f ? m.Recast : 60f);
            }
        }

        return result;
    }
}
