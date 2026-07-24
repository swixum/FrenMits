using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// ATTRIBUTION: the untargetable/targetable window times below are adapted from the
// cactbot project's timeline files (github.com/OverlayPlugin/cactbot, Apache License
// 2.0, Copyright the cactbot authors), anchored to fight mechanics and converted
// onto FrenMits' compressed sheet clock.
//
// Hardcoded downtime / DPS-gate knowledge per fight - the plugin KNOWS where a
// boss goes untargetable and what HP it must be pushed below by then, instead of
// learning it off pulls.
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

    // Effective() results per territory, kept because the board asks for them once
    // per row per frame (plus the downtime tick) and each ask used to rebuild the
    // whole list. Invalidated by a fingerprint of the learned refinements, which
    // only change when a pull finishes measuring one.
    private static readonly Dictionary<uint, (int Stamp, List<DowntimeWindow> Windows)> _effective = new();

    // Territory -> its config key, so the per-frame lookups below don't allocate a
    // fresh string on every ask just to index the learned table.
    private static readonly Dictionary<uint, string> _keys = new();

    private static string KeyFor(uint territory)
    {
        if (_keys.TryGetValue(territory, out var k)) return k;
        return _keys[territory] = territory.ToString();
    }

    private static int LearnedStamp(List<DowntimeWindow>? seen)
    {
        if (seen == null) return 0;
        var stamp = seen.Count;
        unchecked
        {
            foreach (var w in seen)
                stamp = stamp * 31 + BitConverter.SingleToInt32Bits(w.Start) * 7
                        + BitConverter.SingleToInt32Bits(w.Duration);
        }
        return stamp;
    }

    // The hardcoded windows with any learnable ones (Learn=true, where cactbot
    // couldn't pin the time) refined by a measured Start/Duration recorded from a
    // pull.
    public static List<DowntimeWindow> Effective(uint territory, Dictionary<string, List<DowntimeWindow>>? learned)
    {
        var baseWins = For(territory);
        List<DowntimeWindow>? learnedHere = null;
        learned?.TryGetValue(KeyFor(territory), out learnedHere);
        var stamp = LearnedStamp(learnedHere);
        if (_effective.TryGetValue(territory, out var cached) && cached.Stamp == stamp)
            return cached.Windows;
        var built = BuildEffective(baseWins, learnedHere);
        _effective[territory] = (stamp, built);
        return built;
    }

    private static List<DowntimeWindow> BuildEffective(
        IReadOnlyList<DowntimeWindow> baseWins, List<DowntimeWindow>? seen)
    {
        var result = new List<DowntimeWindow>(baseWins.Count);
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

    // Dancing Mad (UMAD); the untargetable/targetable TIMES are the median across
    // six top logs kills, and the gate %s are the fight's design (P1 15%, P2 0%,
    // P4 25%).
    private static readonly List<DowntimeWindow> Dmu = new()
    {
        new() { Start = 199, Duration = 10, TargetHp = 0.15f }, // P1 -> P2 (targetable 209)
        new() { Start = 383, Duration = 46, TargetHp = 0.00f, Cutscene = true }, // P2 -> P3 cutscene (targetable 429)
        new() { Start = 857, Duration = 31, TargetHp = 0.25f }, // P4 -> P5 (targetable 888)
    };

    // Futures Rewritten (FRU); these times are on the SHEET clock (forcejump-
    // inflated, not real time), so windows are placed by anchoring to nearby sheet
    // mechanics rather than a fixed offset.
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
    // was converted by anchoring the cactbot marker to the nearest same-phase sheet
    // mechanic and subtracting that phase's offset.

    // Unending Coil (UCOB); every window log-verified across two independent 6-kill
    // sets, converted onto the sheet clock through the fight's own anchor casts.
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

    // Weapon's Refrain (UWU); log-verified across two independent 6-kill sets.
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

    // Epic of Alexander (TEA); log-verified across two independent 6-kill sets, with
    // big per-phase clock jumps (+82/+188/+234) so seams straddling a jump are best
    // estimates.
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

    // Dragonsong's Reprise (DSR); a 7-phase nonlinear clock, log-verified across two
    // independent 6-kill sets (the P6/P7 dragon phase is left off as a run of brief
    // merged flickers with no stable single window).
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

    // The Omega Protocol (TOP); log-verified across two independent 6-kill sets, and
    // the clock is nearly 1:1 so it converts cleanly.
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
