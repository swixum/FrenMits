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
}
