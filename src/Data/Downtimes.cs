using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// ATTRIBUTION: the untargetable/targetable window times below are adapted from
// the cactbot project's timeline files (github.com/OverlayPlugin/cactbot,
// Apache License 2.0, Copyright the cactbot authors), anchored to fight
// mechanics and converted onto FrenMits' compressed sheet clock.
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
        Builtin.DoomtrainTerritory => Doomtrain,
        Builtin.EnuoTerritory => Enuo,
        Builtin.ZeleniaTerritory => Zelenia,
        Builtin.M1sTerritory => M1s,
        Builtin.M2sTerritory => M2s,
        Builtin.M3sTerritory => M3s,
        Builtin.M4sTerritory => M4s,
        Builtin.M5sTerritory => M5s,
        Builtin.M6sTerritory => M6s,
        Builtin.M7sTerritory => M7s,
        Builtin.M8sTerritory => M8s,
        _ => None,
    };

    // Cached: the board asks once per row per frame.
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

    // The hardcoded windows, with learnable ones refined by a measured pull.
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

    // Dancing Mad (UMAD): times are the median across six top logs kills.
    private static readonly List<DowntimeWindow> Doomtrain = new()
    {
        new() { Start = 156, Duration = 18, TargetHp = -1f }, // logs agree 8/8
        new() { Start = 207, Duration = 22, TargetHp = -1f }, // top kills reach it at ~194 (7/8)
    };

    // The Unmaking (Enuo EX).
    private static readonly List<DowntimeWindow> Zelenia = new()
    {
        new() { Start = 46.9f, Duration = 16.4f, TargetHp = -1f },
    };

    // The Light-heavyweight tier.
    private static readonly List<DowntimeWindow> M1s = new()
    {
        new() { Start = 125.0f, Duration = 19.4f, TargetHp = -1f },
        new() { Start = 148.4f, Duration = 17.1f, TargetHp = -1f },
        new() { Start = 302.1f, Duration = 22.5f, TargetHp = -1f },
        new() { Start = 349.9f, Duration = 19.6f, TargetHp = -1f },
        new() { Start = 373.5f, Duration = 19.4f, TargetHp = -1f },
        new() { Start = 396.9f, Duration = 17.0f, TargetHp = -1f },
    };

    private static readonly List<DowntimeWindow> M2s = new()
    {
        new() { Start = 211.8f, Duration = 34.2f, TargetHp = -1f },
        new() { Start = 369.4f, Duration = 17.4f, TargetHp = -1f },
        new() { Start = 464.6f, Duration = 18.8f, TargetHp = -1f },
    };

    private static readonly List<DowntimeWindow> M3s = new()
    {
        new() { Start = 112.2f, Duration = 29.5f, TargetHp = -1f },
        new() { Start = 374.4f, Duration = 16.3f, TargetHp = -1f },
    };

    private static readonly List<DowntimeWindow> M4s = new()
    {
        new() { Start = 18.0f, Duration = 18.6f, TargetHp = -1f },
        new() { Start = 294.4f, Duration = 21.0f, TargetHp = -1f },
        new() { Start = 390.0f, Duration = 20.3f, TargetHp = -1f },
        new() { Start = 596.0f, Duration = 17.2f, TargetHp = -1f },
        new() { Start = 634.3f, Duration = 21.3f, TargetHp = -1f },
    };

    // The Cruiserweight tier, measured as the gaps where twelve logged kills
    // recorded no cast at all.
    private static readonly List<DowntimeWindow> M5s = new()
    {
        new() { Start = 253.3f, Duration = 21.8f, TargetHp = -1f },
    };

    private static readonly List<DowntimeWindow> M6s = new()
    {
        new() { Start = 37.9f, Duration = 30.8f, TargetHp = -1f },
        new() { Start = 366.8f, Duration = 21.9f, TargetHp = -1f },
        new() { Start = 432.6f, Duration = 22.6f, TargetHp = -1f },
    };

    private static readonly List<DowntimeWindow> M7s = new()
    {
        new() { Start = 355f, Duration = 25.5f, TargetHp = -1f },
        new() { Start = 457.4f, Duration = 18.6f, TargetHp = -1f },
        new() { Start = 554.2f, Duration = 25.8f, TargetHp = -1f },
    };

    // The first one is the P1/P2 cutscene, which is why M8S is the only fight in
    // the tier with a phase anchor - see Builtin.PhaseStarts.
    private static readonly List<DowntimeWindow> M8s = new()
    {
        new() { Start = 375.2f, Duration = 66.5f, TargetHp = -1f },
        new() { Start = 691f, Duration = 17.6f, TargetHp = -1f },
    };

    private static readonly List<DowntimeWindow> Enuo = new()
    {
        new() { Start = 156, Duration = 21, TargetHp = -1f },                // 18/18, to within a second
        new() { Start = 242, Duration = 10, TargetHp = -1f, Learn = true },  // 18/18, start moves with pace
    };

    private static readonly List<DowntimeWindow> Dmu = new()
    {
        new() { Start = 199, Duration = 10, TargetHp = 0.15f }, // P1 -> P2 (targetable 209)
        new() { Start = 383, Duration = 46, TargetHp = 0.00f, Cutscene = true }, // P2 -> P3 cutscene (targetable 429)
        // The lull opens on the second Ultima Upsurge, which the sheet clock reads
        // as 872 (the P4 Upsurge at 833 is the last anchor before it), not the 857
        // of a log's own clock.
        new() { Start = 871.7f, Duration = 31.2f, TargetHp = 0.25f }, // P4 -> P5 (targetable 903)
    };

    // Futures Rewritten (FRU): these times are on the sheet clock, not real time.
    private static readonly List<DowntimeWindow> Fru = new()
    {
        new() { Start = 35,  Duration = 45,  Learn = true }, // P1 Utopian Sky intermission
        new() { Start = 239, Duration = 37,  Learn = true }, // P2 Diamond Dust
        new() { Start = 336, Duration = 29,  Learn = true }, // P2 Light Rampant
        new() { Start = 389, Duration = 31,  Learn = true }, // P2 Absolute Zero
        new() { Start = 481, Duration = 33,  Learn = true }, // P2 -> P3 Junction transition
        new() { Start = 780, Duration = 49,  Learn = true }, // P4 Crystallize Time
        new() { Start = 857, Duration = 173, Learn = true, Cutscene = true }, // P4 -> P5 Pandora cutscene
    };

    // The five older ultimates: each window was converted by anchoring to the nearest
    // same-phase sheet mechanic.

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

    // Epic of Alexander (TEA): log-verified across two independent 6-kill sets.
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

    // Dragonsong's Reprise (DSR): a 7-phase nonlinear clock; P6/P7 is left off.
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
