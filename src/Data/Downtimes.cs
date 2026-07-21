using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// ATTRIBUTION: the untargetable/targetable window times below are adapted from the
// cactbot project's timeline files (github.com/OverlayPlugin/cactbot, Apache License
// 2.0, Copyright the cactbot authors). Each cactbot marker was anchored to a fight
// mechanic by ability id and converted onto FrenMits' compressed sheet clock; the
// gate %s are FrenMits' own. Thanks to the cactbot authors.
//
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
        Builtin.FruTerritory => Fru,
        Builtin.UcobTerritory => Ucob,
        Builtin.UwuTerritory => Uwu,
        Builtin.TeaTerritory => Tea,
        Builtin.DsrTerritory => Dsr,
        Builtin.TopTerritory => Top,
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
                    refined = new DowntimeWindow { Start = m.Start, Duration = m.Duration, TargetHp = w.TargetHp, Learn = true, Cutscene = w.Cutscene };
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
        new() { Start = 381, Duration = 46, TargetHp = 0.00f, Cutscene = true }, // P2 -> P3 cutscene
        new() { Start = 728, Duration = 17, TargetHp = 0.00f }, // P3 -> P4 transition
        // cactbot marks this untargetable "?" and the final Ultima Upsurge straddles
        // it, so the time is a best estimate - Learn refines it from live pulls.
        new() { Start = 876, Duration = 31, TargetHp = 0.25f, Learn = true }, // P4 -> P5 transition
    };

    // Futures Rewritten (FRU). FRU has no forcejumps, so its FruData sheet clock is
    // ~1:1 with the cactbot futures_rewritten timeline (Utopian Sky 35, Junction 500,
    // Memory's End 672 all match), and the cactbot untargetable/targetable markers are
    // used directly. TargetHp is -1 for now (no DPS-check skull until the per-phase
    // push %s are provided); every window is Learn=true so its exact time self-corrects
    // from live pulls. The P1->P2 window (Fatebreaker despawns, Usurper appears ~204)
    // is a despawn-based estimate; the rest bracket explicit cactbot markers.
    private static readonly List<DowntimeWindow> Fru = new()
    {
        new() { Start = 35,  Duration = 45,  Learn = true }, // P1 Utopian Sky intermission (cactbot 35.2 -> 79.8)
        new() { Start = 130, Duration = 74,  Learn = true }, // P1 -> P2 (Usurper targetable cactbot 204.1) [estimate]
        new() { Start = 239, Duration = 37,  Learn = true }, // P2 Diamond Dust / Shiva (cactbot 239.0 -> 276.2)
        new() { Start = 336, Duration = 29,  Learn = true }, // P2 Light Rampant (cactbot 335.7 -> 364.9)
        new() { Start = 675, Duration = 6,   Learn = true }, // P3 Memory's End enrage check (cactbot 675.4 -> 680.8)
        new() { Start = 779, Duration = 50,  Learn = true }, // P4 Crystallize Time (cactbot 779.3 -> 829.4)
        new() { Start = 857, Duration = 173, Learn = true, Cutscene = true }, // P4 -> P5 Pandora cutscene (cactbot ~856 -> 1029.6)
    };

    // The five older ultimates share the same story: their Ikuya sheet clock is
    // compressed vs the cactbot timeline (nonlinearly, per phase), so each window
    // was converted by anchoring the cactbot untargetable/targetable marker to the
    // nearest same-phase sheet mechanic (matched by ability id) and subtracting that
    // phase's offset. No gate %s (no DPS-check skull); all Learn=true so the exact
    // times self-correct from pulls. Sub-5s mechanic flickers and unmappable phase
    // seams were dropped. Times are seconds from pull on the sheet clock.

    // Unending Coil (UCOB). The Nael->Bahamut transition and the five Bahamut Prime
    // trios convert cleanly (one linear cactbot block); the phase seams are estimates.
    private static readonly List<DowntimeWindow> Ucob = new()
    {
        new() { Start = 143, Duration = 15, Learn = true }, // P1 -> P2 Nael entrance [estimate]
        new() { Start = 218, Duration = 5,  Learn = true }, // P2 Divebomb 1
        new() { Start = 226, Duration = 5,  Learn = true }, // P2 Divebomb 2
        new() { Start = 236, Duration = 54, Learn = true }, // P2 -> P3 Nael despawn -> Bahamut Prime
        new() { Start = 313, Duration = 11, Learn = true }, // P3 Quickmarch Trio
        new() { Start = 354, Duration = 16, Learn = true }, // P3 Blackfire Trio
        new() { Start = 405, Duration = 16, Learn = true }, // P3 Fellruin Trio
        new() { Start = 457, Duration = 27, Learn = true }, // P3 Heavensfall Trio
        new() { Start = 518, Duration = 19, Learn = true }, // P3 Tenstrike Trio
        new() { Start = 574, Duration = 52, Learn = true }, // P3 -> P4 Grand Octet -> adds phase
        new() { Start = 795, Duration = 16, Learn = true }, // P4 -> P5 Golden Bahamut [estimate]
    };

    // Weapon's Refrain (UWU). Primal-swap fade-ins are near-instant on the sheet
    // clock (dropped); the meaningful lulls are the Titan geocrush windows and P5.
    private static readonly List<DowntimeWindow> Uwu = new()
    {
        new() { Start = 240, Duration = 16, Learn = true }, // P2 Ifrit Crimson Cyclone [nudged]
        new() { Start = 272, Duration = 8,  Learn = true }, // P3 Titan Geocrush
        new() { Start = 325, Duration = 8,  Learn = true }, // P3 Titan Geocrush
        new() { Start = 429, Duration = 40, Learn = true, Cutscene = true }, // P4 -> P5 Ultima cinematic
        new() { Start = 496, Duration = 20, Learn = true }, // P5 Ultimate Predation
        new() { Start = 668, Duration = 40, Learn = true }, // P5 Ultimate Suppression
    };

    // Epic of Alexander (TEA). Clean per-phase offsets (+0.4/+82.4/+188.3/+234.2).
    private static readonly List<DowntimeWindow> Tea = new()
    {
        new() { Start = 114, Duration = 33, Learn = true }, // P1 -> P2 Living Liquid -> BJCC
        new() { Start = 312, Duration = 11, Learn = true }, // P2 -> P3 Temporal Stasis
        new() { Start = 353, Duration = 59, Learn = true }, // P3 Inception Formation
        new() { Start = 444, Duration = 42, Learn = true }, // P3 Wormhole Formation
        new() { Start = 553, Duration = 58, Learn = true }, // P3 -> P4 Perfect Alexander (Down for the Count)
        new() { Start = 751, Duration = 13, Learn = true }, // P4 Fate Calibration Alpha
        new() { Start = 839, Duration = 25, Learn = true }, // P4 Fate Calibration Beta
    };

    // Dragonsong's Reprise (DSR). The roughest: a 7-phase nonlinear clock. Only the
    // confidently-anchored lulls are kept; the phase-entry seams (Nidhogg, Eyes,
    // DKT) had no untargetable marker to anchor and the sub-5s dive swaps were dropped.
    private static readonly List<DowntimeWindow> Dsr = new()
    {
        new() { Start = 33,  Duration = 31, Learn = true }, // P2 Strength of the Ward adds
        new() { Start = 101, Duration = 51, Learn = true }, // P2 Sanctity of the Ward / Meteors
        new() { Start = 386, Duration = 52, Learn = true }, // P4 Eyes -> Intermission
        new() { Start = 496, Duration = 23, Learn = true }, // P5 Wrath of the Heavens
        new() { Start = 638, Duration = 32, Learn = true }, // P5 Death of the Heavens / Meteors
    };

    // The Omega Protocol (TOP). P1->P2 seam compresses to ~0 (dropped); P4 has no
    // sheet anchor so its two windows use an interpolated offset.
    private static readonly List<DowntimeWindow> Top = new()
    {
        new() { Start = 157, Duration = 28, Learn = true }, // P1 -> P2 Party Synergy (M/F appear)
        new() { Start = 271, Duration = 23, Learn = true }, // P2 -> P3 Final Omega [estimate]
        new() { Start = 429, Duration = 6,  Learn = true }, // P4 Blue Screen intro [interpolated]
        new() { Start = 478, Duration = 38, Learn = true }, // P4 -> P5 Run: Dynamis (Delta) [interpolated]
        new() { Start = 544, Duration = 48, Learn = true }, // P5 Delta -> Sigma
        new() { Start = 625, Duration = 61, Learn = true }, // P5 Sigma -> Omega
        new() { Start = 736, Duration = 32, Learn = true }, // P5 Omega -> Blind Faith
        new() { Start = 800, Duration = 56, Learn = true }, // P5 -> P6 Alpha Omega [estimate]
    };
}
