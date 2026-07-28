using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// One place that decides which timeline rows become resync anchors, and which of
// those may re-base a phase.
//
// Every built-in sheet used to answer that inline, and every one of them answered
// it the same way: the first row of a phase re-bases, and so does any row more
// than 90s after the last anchor, to get the clock back after downtime.
//
// The part that was missing is the guard below. A phase anchor is given a 2000s
// forward window so a clock still sitting in an earlier segment can land on the
// next phase the moment its boss casts. The boss then only has to use that same
// ability once more, earlier, somewhere the table says nothing about, and the
// clock leaps forward the whole gap - and stays there until something drags it
// back. Measured against six kills apiece that none of the data came from, that
// was costing UCOB more than thirty seconds of accuracy on 96 readings out of 168
// and UWU on 120 out of 150, with median errors of five and a half and seven and a
// half minutes.
//
// So only an ability's FIRST anchor may re-base. A later one still corrects fine
// drift, within the tight mechanic window, where being wrong costs seconds instead
// of phases.
//
// A phase that would have re-based on a repeat doesn't just lose the re-base: it
// hands it to the next row whose ability hasn't been anchored yet, so the clock
// still gets back on, a few seconds later and on something unambiguous.
public static class SyncAnchors
{
    // How far past a phase start to look for an ability that can carry its re-base.
    private const float HandoffReach = 60f;

    public static List<SyncPoint> Build(IEnumerable<(uint Sync, float Time, string Phase, string Mechanic)> rows)
    {
        var ordered = rows.Where(r => r.Sync != 0).OrderBy(r => r.Time).ToList();

        // Which rows would re-base under the old rule: the first of each phase, and
        // anything more than 90s after the last anchor (downtime, cutscenes).
        var wants = new bool[ordered.Count];
        var phaseSeen = new HashSet<string>();
        var prevTime = float.NegativeInfinity;
        for (var i = 0; i < ordered.Count; i++)
        {
            wants[i] = phaseSeen.Add(ordered[i].Phase) || (ordered[i].Time - prevTime) > 90f;
            prevTime = ordered[i].Time;
        }

        // First anchor per ability, which is the only one allowed to re-base.
        var firstFor = new Dictionary<uint, int>();
        for (var i = 0; i < ordered.Count; i++)
            if (!firstFor.ContainsKey(ordered[i].Sync)) firstFor[ordered[i].Sync] = i;

        var isPhase = new bool[ordered.Count];
        for (var i = 0; i < ordered.Count; i++)
        {
            if (!wants[i]) continue;
            var take = firstFor[ordered[i].Sync] == i ? i : -1;
            for (var j = i + 1; take < 0 && j < ordered.Count
                                && ordered[j].Time - ordered[i].Time <= HandoffReach; j++)
                if (firstFor[ordered[j].Sync] == j && !isPhase[j]) take = j;
            if (take >= 0) isPhase[take] = true;
        }

        return ordered.Select((e, i) => new SyncPoint
        {
            Ability = e.Sync,
            Time = e.Time,
            IsPhase = isPhase[i],
            Label = $"{e.Phase} {e.Mechanic}",
        }).ToList();
    }

    // The same rule applied to a list that already knows which anchors re-base:
    // the baked duty timelines, whose IsPhase flags come out of the bake rather
    // than from Build above.
    //
    // Those needed it more than anything else does. A duty packs each boss into
    // its own 1000-second block and a phase anchor is given a 20000-second forward
    // window to reach them, so an ability that re-bases a later boss and is ALSO
    // cast by an earlier one drags the clock a whole boss or more forward, mid
    // fight. Across the 310 shipped duties, 355 anchors were shaped that way, the
    // worst able to jump 8792 seconds - the board would sit on the last boss's
    // mechanics while the party fought the first.
    //
    // A denied re-base is handed on, first to a neighbour within HandoffReach as
    // Build does, then to anywhere in the same block, because a block that loses
    // its last re-base is never reached at all and shows no timeline whatsoever.
    // Where a block has nothing safe to offer - every ability in it is cast
    // earlier too - the risky anchor is KEPT: a wrong clock beats no clock, and
    // that is the honest trade rather than a silent regression.
    // Where one encounter in a baked duty ends and the next begins. NOT time/1000:
    // a segment can run long enough to cross the next thousand, so the boundary is
    // the gap itself.
    private const float EncounterGap = 150f;

    // Where each encounter of a baked duty starts, taken from the ROW times, which
    // is how the bake splits them. Anchor times alone give different boundaries and
    // hand a re-base to the wrong encounter.
    public static List<float> EncounterStarts(IEnumerable<float> rowTimes)
    {
        var times = rowTimes.OrderBy(t => t).ToList();
        var starts = new List<float>();
        if (times.Count == 0) return starts;
        starts.Add(times[0]);
        for (var i = 1; i < times.Count; i++)
            if (times[i] - times[i - 1] > EncounterGap) starts.Add(times[i]);
        return starts;
    }

    public static void Guard(List<SyncPoint> points, IReadOnlyList<float>? encounterStarts = null)
    {
        if (points.Count == 0) return;
        var order = points.Select((p, i) => (p, i)).OrderBy(x => x.p.Time).Select(x => x.i).ToList();

        // Which encounter each anchor belongs to, and which encounters could re-base
        // before any of this ran. Whatever else happens, none of them may lose that.
        var segment = new int[points.Count];
        if (encounterStarts is { Count: > 0 })
        {
            foreach (var i in order)
            {
                var s = 0;
                for (var k = 0; k < encounterStarts.Count; k++)
                    if (points[i].Time >= encounterStarts[k] - 5f) s = k;
                segment[i] = s;
            }
        }
        else
        {
            var seg = 0;
            for (var k = 1; k < order.Count; k++)
            {
                if (points[order[k]].Time - points[order[k - 1]].Time > EncounterGap) seg++;
                segment[order[k]] = seg;
            }
        }
        var couldEnter = new HashSet<int>();
        foreach (var i in order)
            if (points[i].IsPhase) couldEnter.Add(segment[i]);

        var firstFor = new Dictionary<uint, int>();
        foreach (var i in order)
            if (!firstFor.ContainsKey(points[i].Ability)) firstFor[points[i].Ability] = i;

        var denied = new List<int>();
        foreach (var i in order)
        {
            if (!points[i].IsPhase || firstFor[points[i].Ability] == i) continue;

            var take = -1;
            foreach (var j in order)
            {
                if (points[j].Time <= points[i].Time || points[j].Time - points[i].Time > HandoffReach) continue;
                if (firstFor[points[j].Ability] == j && !points[j].IsPhase) { take = j; break; }
            }
            if (take < 0)                       // anywhere in this encounter, either side
                foreach (var j in order)
                    if (segment[j] == segment[i] && firstFor[points[j].Ability] == j
                        && !points[j].IsPhase) { take = j; break; }

            points[i].IsPhase = false;
            denied.Add(i);
            if (take >= 0) points[take].IsPhase = true;
        }

        // The invariant, enforced rather than hoped for: an encounter that could be
        // entered before must still be enterable. Where nothing safe was found, the
        // risky anchor goes back - a wrong clock beats a boss with no timeline.
        var canEnter = new HashSet<int>();
        foreach (var i in order)
            if (points[i].IsPhase) canEnter.Add(segment[i]);
        foreach (var i in denied)
            if (!canEnter.Contains(segment[i]))
            {
                points[i].IsPhase = true;
                canEnter.Add(segment[i]);
            }
    }
}
