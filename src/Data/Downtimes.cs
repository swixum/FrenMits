using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// Hardcoded downtime / DPS-gate knowledge per fight - the plugin now KNOWS where a
// boss goes untargetable and what HP it must be pushed below by then, instead of
// learning it off pulls. Each window is on the FrenMits pull clock (the same axis
// the baked sheet uses), so its Untargetable / Targetable rows line up with the
// timeline through the clock's resync anchors.
//
//  Start     - seconds from pull when the boss goes untargetable
//  Duration  - how long the lull lasts (Targetable = Start + Duration)
//  TargetHp  - the phase's DPS check: the HP fraction (0..1) you must push it below
//              by Start, or -1 for a plain lull (cutscene) with no push check
public static class Downtimes
{
    private static readonly List<DowntimeWindow> None = new();

    public static IReadOnlyList<DowntimeWindow> For(uint territory) => territory switch
    {
        Builtin.DmuTerritory => Dmu,
        _ => None,
    };

    // The hardcoded windows with any learnable ones (Learn=true, where cactbot
    // couldn't pin the time) refined by a measured Start/Duration recorded from a
    // pull. A learned entry within 25s of the hardcoded Start replaces its TIME;
    // the gate % is always the hardcoded design value. Everything else is verbatim.
    public static List<DowntimeWindow> Effective(uint territory, Dictionary<string, List<DowntimeWindow>>? learned)
    {
        var baseWins = For(territory);
        var result = new List<DowntimeWindow>(baseWins.Count);
        List<DowntimeWindow>? seen = null;
        learned?.TryGetValue(territory.ToString(), out seen);
        foreach (var w in baseWins)
        {
            var refined = w;
            if (w.Learn && seen != null)
            {
                var m = seen.FirstOrDefault(x => MathF.Abs(x.Start - w.Start) < 25f);
                if (m != null)
                    refined = new DowntimeWindow { Start = m.Start, Duration = m.Duration, TargetHp = w.TargetHp, Learn = true };
            }
            result.Add(refined);
        }
        return result;
    }

    // Dancing Mad (UMAD). Gate %s from the fight's design: P1 push to 15%, P2 to
    // 0%, P3 to 0% on both bosses, P4 to 25%, P5 is the hard enrage (not a lull, so
    // it isn't listed). Untargetable/targetable TIMES are cross-referenced from the
    // cactbot dancing_mad timeline: each cactbot marker is anchored to the nearest
    // sheet mechanic by ability id and converted onto this (pull-relative) clock,
    // since cactbot's own times are forcejump-inflated in the later phases (P3 ~+113s,
    // P4/P5 ~+249s) and can't be used directly.
    //   P1->P2  cactbot 197.3 untarget / 207.6 target  (anchor Mystery Magic BA94)
    //   P2->P3  cactbot 380.1 untarget / 540.3 target   (anchor Ultimate Embrace C24C, Bowels BAF2)
    //   P3->P4  cactbot 840.8 untarget / Kefka Says C2DC (anchor Stomp-a-Mole BAF0)
    //   P4->P5  cactbot 1112.8 untarget / Ultima Repeater BB40 (anchor Ultima Upsurge C24A)
    private static readonly List<DowntimeWindow> Dmu = new()
    {
        new() { Start = 198, Duration = 10, TargetHp = 0.15f }, // P1 -> P2 transition
        new() { Start = 381, Duration = 46, TargetHp = 0.00f }, // P2 -> P3 cutscene
        new() { Start = 728, Duration = 17, TargetHp = 0.00f }, // P3 -> P4 transition
        // cactbot marks this untargetable "?" and the final Ultima Upsurge straddles
        // it, so the time is a best estimate - Learn refines it from live pulls.
        new() { Start = 876, Duration = 31, TargetHp = 0.25f, Learn = true }, // P4 -> P5 transition
    };
}
