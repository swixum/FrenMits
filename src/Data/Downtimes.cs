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

    // Dancing Mad (UMAD). Gate %s are the fight's design (P1 push to 15%, P2 to 0%,
    // P4 to 25%; P5 is the hard enrage, not a lull). The untargetable/targetable
    // TIMES are the median across six top FFLogs kills: "targetable" is the exact
    // phase-transition start FFLogs records, and "untargetable" is where damage to
    // the boss stops - the two cross-validate to ~1s. Fight-relative seconds ARE
    // this pull clock, so no conversion. The P3 -> P4 transition is NOT listed: the
    // boss only goes away for ~5s there (720 -> 725), too brief to be a real lull,
    // so a bar would just be noise.
    private static readonly List<DowntimeWindow> Dmu = new()
    {
        new() { Start = 199, Duration = 10, TargetHp = 0.15f }, // P1 -> P2 (targetable 209)
        new() { Start = 383, Duration = 46, TargetHp = 0.00f, Cutscene = true }, // P2 -> P3 cutscene (targetable 429)
        new() { Start = 857, Duration = 31, TargetHp = 0.25f }, // P4 -> P5 (targetable 888)
    };

    // Futures Rewritten (FRU). These times are on the SHEET clock (what the resync
    // anchors align the board to), NOT real time - and that clock is FORCEJUMP-
    // INFLATED, not the "~1:1" an earlier note claimed. Measured across 8 FFLogs
    // kills, the sheet runs ~+50 ahead by early P2, ~+87 by Junction (P2->P3), and
    // ~+190 by P5, and the drift varies with kill speed (so windows are placed by
    // anchoring to nearby sheet mechanics, not a fixed offset).
    // P1/P2 windows are log-verified (converted through the sheet's own anchor casts
    // across all 8 kills). The P3/P4 stretch is deliberately bare: the logs show the
    // boss STAYS targetable through Crystallize Time (damage is continuous, only two
    // ~8s flickers), so the old 50s "Crystallize" lull was wrong, and the P3->P4
    // Memory's End gap is a sub-6s flicker (dropped like DMU's). That leaves Pandora
    // as the only real cutscene here (the one window where players also go idle).
    // All Learn=true (self-correct live). No gate %s.
    private static readonly List<DowntimeWindow> Fru = new()
    {
        new() { Start = 35,  Duration = 45,  Learn = true }, // P1 Utopian Sky intermission
        new() { Start = 236, Duration = 40,  Learn = true }, // P1 -> P2 (Fatebreaker -> Usurper)
        new() { Start = 336, Duration = 29,  Learn = true }, // P2 Light Rampant
        new() { Start = 389, Duration = 31,  Learn = true }, // P2 Absolute Zero
        new() { Start = 481, Duration = 33,  Learn = true }, // P2 -> P3 Junction transition
        new() { Start = 857, Duration = 173, Learn = true, Cutscene = true }, // P4 -> P5 Pandora cutscene
    };

    // The five older ultimates share the same story: their Ikuya sheet clock is
    // compressed vs the cactbot timeline (nonlinearly, per phase), so each window
    // was converted by anchoring the cactbot untargetable/targetable marker to the
    // nearest same-phase sheet mechanic (matched by ability id) and subtracting that
    // phase's offset. No gate %s (no DPS-check skull); all Learn=true so the exact
    // times self-correct from pulls. Sub-5s mechanic flickers and unmappable phase
    // seams were dropped. Times are seconds from pull on the sheet clock.

    // Unending Coil (UCOB). Every window log-verified across two independent 6-kill
    // sets, converted onto the sheet clock through the fight's own anchor casts. The
    // P3 Bahamut Prime trios were already right; the P2->P3 seam (was 236/54) and the
    // P4->P5 seam (was 795/16) were the estimates that needed the fix. The two 5s Nael
    // divebomb flickers are dropped (too brief). All Learn=true.
    private static readonly List<DowntimeWindow> Ucob = new()
    {
        new() { Start = 135, Duration = 24, Learn = true }, // P1 -> P2 Nael entrance
        new() { Start = 273, Duration = 17, Learn = true }, // P2 -> P3 Nael despawn -> Bahamut Prime
        new() { Start = 313, Duration = 11, Learn = true }, // P3 Quickmarch Trio
        new() { Start = 354, Duration = 17, Learn = true }, // P3 Blackfire Trio
        new() { Start = 405, Duration = 17, Learn = true }, // P3 Fellruin Trio
        new() { Start = 457, Duration = 28, Learn = true }, // P3 Heavensfall Trio
        new() { Start = 519, Duration = 20, Learn = true }, // P3 Tenstrike Trio
        new() { Start = 576, Duration = 54, Learn = true }, // P3 -> P4 Grand Octet -> adds phase
        new() { Start = 724, Duration = 58, Learn = true }, // P4 -> P5 Golden Bahamut
    };

    // Weapon's Refrain (UWU). Log-verified across two independent 6-kill sets. The
    // primal-swap transitions (P1->P2 at 135, P3->P4 at 380) are real ~12s lulls, not
    // "near-instant", so they're added back. The old 429 "Ultima cinematic" is NOT a
    // cutscene - players cast all through it (Ultima is a raidwide you mit), so the
    // flag is dropped. All Learn=true.
    private static readonly List<DowntimeWindow> Uwu = new()
    {
        new() { Start = 135, Duration = 13, Learn = true }, // P1 -> P2 (Garuda -> Ifrit)
        new() { Start = 242, Duration = 8,  Learn = true }, // P2 Ifrit Crimson Cyclone
        new() { Start = 272, Duration = 8,  Learn = true }, // P3 Titan Geocrush
        new() { Start = 326, Duration = 8,  Learn = true }, // P3 Titan Geocrush
        new() { Start = 380, Duration = 11, Learn = true }, // P3 -> P4 (Titan -> Ultima)
        new() { Start = 434, Duration = 35, Learn = true }, // P4 Ultima
        new() { Start = 496, Duration = 20, Learn = true }, // P5 Ultimate Predation
        new() { Start = 642, Duration = 19, Learn = true }, // P5 Ultimate Suppression
    };

    // Epic of Alexander (TEA). Log-verified across two independent 6-kill sets. Fixed
    // the P2->P3 Temporal Stasis (was 59s, really ~22s), split out the P3 Judgment/
    // Super Jump lull that was missing, and re-placed the Down-for-the-Count seam. Its
    // clock has big per-phase jumps (+82/+188/+234), so the seams that straddle a jump
    // are best estimates; all Learn=true so they self-correct.
    private static readonly List<DowntimeWindow> Tea = new()
    {
        new() { Start = 114, Duration = 32, Learn = true }, // P1 -> P2 Living Liquid -> BJCC
        new() { Start = 304, Duration = 22, Learn = true }, // P2 -> P3 Temporal Stasis
        new() { Start = 353, Duration = 26, Learn = true }, // P3 Inception Formation
        new() { Start = 397, Duration = 16, Learn = true }, // P3 Judgment Crystal / Super Jump
        new() { Start = 444, Duration = 42, Learn = true }, // P3 Wormhole Formation
        new() { Start = 588, Duration = 72, Learn = true }, // P3 -> P4 Perfect Alexander (Down for the Count)
        new() { Start = 751, Duration = 15, Learn = true }, // P4 Fate Calibration Alpha
        new() { Start = 839, Duration = 27, Learn = true }, // P4 Fate Calibration Beta
    };

    // Dragonsong's Reprise (DSR). A 7-phase nonlinear clock - now log-verified across
    // two independent 6-kill sets, which filled in the many seams the old data missed
    // (Nidhogg, the Eyes intermission in/out, the P5 seams) and fixed two mistimed
    // ones (old 386/52 and 638/32). The P6/P7 dragon phase is a run of brief merged
    // flickers with no stable single window, so it's left off. All Learn=true.
    private static readonly List<DowntimeWindow> Dsr = new()
    {
        new() { Start = 33,  Duration = 31, Learn = true }, // P2 Strength of the Ward adds
        new() { Start = 101, Duration = 51, Learn = true }, // P2 Sanctity of the Ward / Meteors
        new() { Start = 174, Duration = 12, Learn = true }, // P2 -> P3 Nidhogg
        new() { Start = 294, Duration = 17, Learn = true }, // P3 Nidhogg dives
        new() { Start = 378, Duration = 22, Learn = true }, // P3 -> Intermission (Eyes)
        new() { Start = 449, Duration = 16, Learn = true }, // Intermission -> P5
        new() { Start = 496, Duration = 24, Learn = true }, // P5 Wrath of the Heavens
        new() { Start = 557, Duration = 32, Learn = true }, // P5 Death of the Heavens / Meteors
        new() { Start = 622, Duration = 12, Learn = true }, // P5 -> P6 Wyrmsbreath
    };

    // The Omega Protocol (TOP). Log-verified across two independent 6-kill sets; the
    // clock is nearly 1:1 so it converts cleanly. Fixed the P2->P3 (was 271/23) and
    // the interpolated P4->P5 (was 478/38, really ~500/17), and confirmed Alpha Omega
    // is a true cutscene (zero player casts in all 12 kills). All Learn=true.
    private static readonly List<DowntimeWindow> Top = new()
    {
        new() { Start = 158, Duration = 29, Learn = true }, // P1 -> P2 Party Synergy (M/F appear)
        new() { Start = 260, Duration = 34, Learn = true }, // P2 -> P3 Final Omega
        new() { Start = 437, Duration = 8,  Learn = true }, // P4 Blue Screen intro
        new() { Start = 500, Duration = 17, Learn = true }, // P4 -> P5 Run: Dynamis (Delta)
        new() { Start = 544, Duration = 48, Learn = true }, // P5 Delta -> Sigma
        new() { Start = 626, Duration = 61, Learn = true }, // P5 Sigma -> Omega
        new() { Start = 738, Duration = 32, Learn = true }, // P5 Omega -> Blind Faith
        new() { Start = 801, Duration = 56, Learn = true, Cutscene = true }, // P5 -> P6 Alpha Omega (log-confirmed cutscene)
    };
}
