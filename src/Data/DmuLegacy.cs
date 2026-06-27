// FROZEN SNAPSHOT of the DMU timeline as it was baked into users' configs before
// the live-sheet resync (v1.0.0.118). The smart re-bake migration diffs against
// this to tell sheet-baked lines (replace) from custom lines people added (keep).
// Do not edit — it must match what shipped previously.
// cactbot cast in fight order, gated to its phase's timeline offset, and stamped
// with that cast's id at the sheet's (real, pull-relative) time. Mit times are
// left as authored — the anchors snap the clock onto each cast, so the calls
// stay aligned to the real timeline through all five phases. cactbot's absolute
// times are NOT used directly (its phase transitions use forcejumps that inflate
// later-phase times by 110s/225s/390s vs the live clock). Times = seconds from
// the pull (continuous).
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

public static class DmuLegacy
{
    public static readonly string[] Slots = { "MT", "OT", "WHM", "AST", "SCH", "SGE", "D1", "D2", "D3", "D4" };

    public sealed record Entry(int Time, string Phase, string Mechanic, uint Sync, string[] Actions);

    public static readonly Entry[] Timeline =
    {
        new(16, "P1", "Revolting Ruin III", 0xC403, new[]{"Reprisal (Optional First GCD)", "Buddy Mit", "Assist MT", "", "", "", "", "", "", "", ""}),
        new(38, "P1", "Mystery Magic", 0xBA94, new[]{"Party Mit (GNB/DRK)", "Reprisal + Party Mit (GNB/DRK)", "Temperance", "Neutral Sect + Sun Sign", "Spreadlo + Sacred Soil", "Kerachole + Zoe Shields", "Feint", "", "Party Mit", "", ""}),
        new(43, "P1", "Wave Cannon", 0xBAA8, new[]{"Party Mit (WAR/PLD)", "", "Divine Caress + Asylum", "", "Seraph", "Holos", "", "", "", "", ""}),
        new(50, "P1", "Double-Trouble Trap", 0xBAA7, new[]{"", "", "", "", "Seraph", "", "", "", "", "Addle", ""}),
        new(63, "P1", "Light of Judgment", 0xC622, new[]{"Reprisal", "", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil", "Kerachole", "", "Feint", "", "", "✔"}),
        new(88, "P1", "Gravitas II (Part I)", 0xBAAC, new[]{"", "", "", "Macrocosmos", "Seraphism", "", "", "", "", "", ""}),
        new(106, "P1", "Gravitas II (Part II)", 0xBAAC, new[]{"", "", "Liturgy of the Bell", "", "Sacred Soil + Expedient + Fey Illumination", "Kerachole + Philosophia", "", "", "", "", ""}),
        new(118, "P1", "Double-Trouble Trap", 0xBAA7, new[]{"", "Reprisal + Party Mit (GNB/DRK)", "", "", "Spreadlo + Sacred Soil", "Panhaima + Zoe Shields", "", "", "Party Mit", "", ""}),
        new(133, "P1", "Light of Judgment", 0xC622, new[]{"Reprisal + Party Mit", "", "Plenary Indulgence + Asylum", "Collective Unconscious", "", "Kerachole", "Feint", "", "", "", ""}),
        new(168, "P1", "Double-Trouble Trap", 0xBCF2, new[]{"", "", "Temperance", "Neutral Sect", "", "", "", "", "", "", ""}),
        new(174, "P1", "Indulgent Will", 0xBAB5, new[]{"", "", "Divine Caress", "Sun Sign", "Sacred Soil", "Kerachole", "", "", "", "", ""}),
        new(187, "P1", "Mystery Magic", 0xBA94, new[]{"", "Reprisal", "", "", "", "", "", "", "", "", ""}),
        new(220, "P2", "Ultimate Embrance", 0xC24C, new[]{"", "", "Assist Tanks (PLD/GNB & PLD/DRK)", "", "", "", "", "Feint", "", "", ""}),
        new(237, "P2", "Forsaken", 0xBABC, new[]{"Reprisal", "Party Mit", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil + Spreadlo", "Kerachole + Holos + Zoe Shields", "Feint", "", "Party Mit", "Addle", "✔"}),
        new(249, "P2", "Towers I", 0xBABE, new[]{"Party Mit", "", "Asylum", "", "Seraph + Fey Illumination", "Panhaima", "", "", "", "", ""}),
        new(259, "P2", "Towers II (Past/Future's End)", 0xBABE, new[]{"", "", "", "", "Seraph", "", "", "", "", "", ""}),
        new(270, "P2", "Towers III (All Things Ending)", 0xBABE, new[]{"", "Reprisal", "", "Macrocosmos", "Sacred Soil", "Kerachole", "", "", "", "", ""}),
        new(280, "P2", "Towers IV (Past/Future's End)", 0xBABE, new[]{"", "", "", "", "", "", "", "", "", "", ""}),
        new(291, "P2", "Towers V (All Things Ending)", 0xBABE, new[]{"", "", "Liturgy of the Bell", "", "Expedient", "", "", "", "", "", ""}),
        new(301, "P2", "Towers VI (Past/Future's End)", 0xBABE, new[]{"Reprisal", "", "Plenary Indulgence + Temperance", "Collective Unconscious + Neutral Sect", "Seraphism", "Philosophia", "", "", "", "", ""}),
        new(312, "P2", "Towers VII (All Things Ending)", 0xBABE, new[]{"", "", "Divine Caress", "Sun Sign", "Sacred Soil", "Kerachole", "", "", "", "", ""}),
        new(322, "P2", "Towers VIII (Past/Future's End)", 0xBABE, new[]{"", "", "", "", "", "", "", "", "", "", ""}),
        new(342, "P2", "Light of Judgement", 0xBABD, new[]{"Party Mit", "Reprisal", "Asylum", "", "Spreadlo + Sacred Soil", "Kerachole + Holos + Zoe Shields", "Feint", "", "Party Mit", "Addle", ""}),
        new(371, "P2", "Wings of Destruction", 0xC24C, new[]{"Reprisal", "Party Mit (WAR/GNB/DRK)", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil + Fey Illumination + Seraph", "Kerachole + Panhaima", "", "Feint", "", "", ""}),
        new(378, "P2", "Ultimate Embrace", 0xC24C, new[]{"", "Party Mit (PLD)", "", "", "", "", "", "", "", "", ""}),
        new(451, "P3", "Bowels of Agony (Chaos)", 0xBAF2, new[]{"Reprisal", "", "Plenary Indulgence + Asylum", "Collective Unconscious", "Sacred Soil", "Kerachole", "Feint (Chaos)", "", "", "Addle (The Decisive Battle)", ""}),
        new(470, "P3", "Stray Flames/Tsunami", 0xBAF8, new[]{"Party Mit", "Reprisal", "", "", "Spreadlo + Sacred Soil", "Zoe Shields + Kerachole", "", "", "", "", "✔"}),
        new(479, "P3", "Thunder III (1st Set)", 0, new[]{"", "", "", "", "Expedient", "Holos", "", "", "", "", "✔"}),
        new(498, "P3", "Stray Flames/Tsunami", 0, new[]{"", "Party Mit", "Temperance", "Neutral Sect", "Seraph", "", "", "", "", "", ""}),
        new(508, "P3", "Ultima Blaster + Umbra Smash", 0xBB00, new[]{"Reprisal + LB3 (Vacuum Wave)", "", "", "Sun Sign", "Sacred Soil + Fey Illumination + Seraphism", "Kerachole + Panhaima", "", "Feint (Chaos)", "Party Mit", "", ""}),
        new(513, "P3", "Vacuum Wave", 0, new[]{"", "LB3", "Plenary Indulgence", "Collective Unconscious", "", "", "", "", "", "", ""}),
        new(519, "P3", "Cyclone", 0xBAF8, new[]{"", "", "Divine Caress", "", "", "", "", "", "", "", ""}),
        new(536, "P3", "Thunder III (2nd Set)", 0xBAE2, new[]{"", "Reprisal", "", "", "", "", "", "", "", "", ""}),
        new(544, "P3", "The Decisive Battle", 0, new[]{"", "", "", "", "Spreadlo", "Zoe Shields", "", "", "", "Addle (Exdeath)", ""}),
        new(559, "P3", "Earthquake (HP to 1)", 0, new[]{"Party Mit (GNB/DRK)", "Reprisal", "Asylum", "Macrocosmos", "Sacred Soil", "Kerachole + Philosophia", "Feint (Chaos)", "", "", "", ""}),
        new(579, "P3", "Shocking Impact", 0xBAF4, new[]{"Party Mit (WAR/PLD)", "", "Plenary Indulgence", "Collective Unconscious", "", "", "", "", "", "", ""}),
        new(609, "P3", "Shocking Impact", 0xBAFD, new[]{"", "Party Mit", "Liturgy of the Bell", "", "Expedient + Seraph + Sacred Soil", "Holos + Kerachole", "", "", "", "", "✔"}),
        new(621, "P3", "Black Holes (4th Tether Set)", 0xC571, new[]{"", "", "Temperance + Divine Caress", "Neutral Sect + Sun Sign", "Seraph", "", "", "", "", "", ""}),
        new(626, "P3", "Black Holes (5th Tether Set)", 0, new[]{"", "", "", "", "Fey Illumination", "Panhaima", "", "", "", ""}),
        new(637, "P3", "Thunder III (5th Set)", 0, new[]{"Reprisal", "", "Asylum", "", "Spreadlo + Sacred Soil", "Zoe Shields + Kerachole", "", "Feint (Chaos)", "", "Addle (Exdeath)", ""}),
        new(677, "P3", "Shockwave", 0, new[]{"", "", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil", "Kerachole", "", "", "Party Mit", "", ""}),
        new(709, "P3", "Stomp-a-Mole", 0xBAF0, new[]{"Reprisal + Party Mit", "", "", "", "Seraphism + Sacred Soil", "Kerachole", "Feint (Chaos)", "", "", "", ""}),
        new(745, "P4", "Kefka Returns (phase enter)", 0xC2DC, new[]{"", "", "", "", "", "", "", "", "", "", ""}),
        new(759, "P4", "Grand Cross + Inferno/Tsunami", 0xBB14, new[]{"", "", "Temperance + Asylum", "Neutral Sect", "Spreadlo + Expedient + Sacred Soil", "Kerachole + Holos + Philosophia", "", "", "", "", ""}),
        new(765, "P4", "Inferno/Tsunami", 0, new[]{"", "Party Mit (GNB/DRK)", "", "", "", "", "", "", "", "", ""}),
        new(774, "P4", "Grand Cross + Inferno/Tsunami", 0xBB14, new[]{"", "Party Mit (WAR/PLD)", "Plenary Indulgence", "Collective Unconscious", "Fey Illumination", "Panhaima", "", "", "", "", "✔"}),
        new(779, "P4", "Inferno/Tsunami", 0, new[]{"", "", "", "Sun Sign", "Seraph", "", "", "", "Party Mit", "", ""}),
        new(789, "P4", "Grand Cross", 0xBB14, new[]{"", "", "Divine Caress", "", "Seraph", "Zoe Shields", "", "", "", "", ""}),
        new(803, "P4", "Flood of Naught", 0xC393, new[]{"", "", "Liturgy of the Bell", "Macrocosmos", "Sacred Soil", "Kerachole", "", "", "", "", ""}),
        new(807, "P4", "Death Bolt/Wave", 0, new[]{"Party Mit", "", "", "", "", "", "", "", "", "", ""}),
        new(827, "P4", "Ultima Upsurge", 0xC24A, new[]{"Reprisal", "", "", "", "Sacred Soil", "Kerachole", "Feint", "", "", "Addle", ""}),
        new(833, "P4", "Death Bolt/Wave", 0xBB1B, new[]{"", "", "Plenary Indulgence + Asylum", "Collective Unconscious", "", "", "", "", "", "", ""}),
        new(867, "P4", "Ultima Upsurge", 0, new[]{"", "Reprisal", "", "", "Sacred Soil", "Kerachole", "", "", "", "", ""}),
        new(905, "P5", "Ultima Repeater", 0xBB40, new[]{"Reprisal", "Party Mit", "Plenary Indulgence", "Collective Unconscious", "Spreadlo + Sacred Soil", "Zoe Shields + Holos + Kerachole", "", "", "Party Mit", "", "✔"}),
        new(922, "P5", "Chaotic Flood", 0xC13F, new[]{"", "", "Temperance", "Neutral Sect", "Expedient", "Panhaima", "", "", "", "", ""}),
        new(935, "P5", "Maddening Orchestra", 0xBB50, new[]{"", "Reprisal", "Divine Caress", "Sun Sign", "Sacred Soil + Fey Illumination", "Kerachole", "", "Feint", "", "", ""}),
        new(946, "P5", "Fell Forces (2x)", 0xC654, new[]{"", "", "", "", "", "", "", "", "", "", ""}),
        new(966, "P5", "Celestriad", 0xBB42, new[]{"Party Mit", "", "Asylum", "", "Seraph + Sacred Soil", "Kerachole", "", "", "", "", ""}),
        new(987, "P5", "Ultima Repeater", 0, new[]{"Reprisal", "Party Mit", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil", "Kerachole", "Feint", "", "Party Mit", "Addle", ""}),
        new(994, "P5", "Fell Forces (2x)", 0xC654, new[]{"", "", "", "", "", "", "", "", "", "", ""}),
        new(1026, "P5", "Stray Entropy", 0xBB3F, new[]{"", "Reprisal", "", "", "Sacred Soil", "Kerachole", "", "", "", "", ""}),
        new(1036, "P5", "Maddening Orchestra", 0xBB51, new[]{"", "", "", "", "", "", "", "", "", "", ""}),
        new(1046, "P5", "Fell Forces (3x)", 0xC654, new[]{"", "", "", "", "", "", "", "", "", "", ""}),
        new(1064, "P5", "Forsaken + Forsaken Bonds", 0xBB35, new[]{"Reprisal + Party Mit (GNB/DRK)", "", "Temperance + Asylum", "Neutral Sect", "Spreadlo + Fey Illumination + Sacred Soil", "Zoe Shields + Holos + Philosophia + Kerachole", "", "Feint", "", "", "✔"}),
        new(1069, "P5", "Forsaken Bonds", 0, new[]{"Party Mit (WAR/PLD)", "", "", "", "Seraphism", "", "", "", "", "", ""}),
        new(1072, "P5", "Forsaken + Forsaken Bonds", 0xBB36, new[]{"", "", "Liturgy of the Bell", "Macrocosmos", "Expedient", "Panhaima", "", "", "", "", ""}),
        new(1080, "P5", "Forsaken + Forsaken Bonds", 0xBB36, new[]{"", "Reprisal + Party Mit (GNB/DRK)", "Divine Caress + Plenary Indulgence", "Sun Sign + Collective Unconscious", "Seraph", "", "Feint", "", "Party Mit", "Addle", ""}),
        new(1085, "P5", "Forsaken Bonds", 0, new[]{"", "Party Mit (WAR/PLD)", "", "", "", "", "", "", "", "", ""}),
        new(1088, "P5", "Forsaken + Forsaken Bonds", 0xBB36, new[]{"", "", "", "", "Seraph + Sacred Soil", "Kerachole", "", "", "", "", ""}),
    };

    // First time each phase appears, for the practice phase-jump.
    public static List<(string Name, float Time)> PhaseStarts()
        => Timeline.GroupBy(e => e.Phase)
                   .Select(g => (g.Key, (float)g.Min(e => e.Time)))
                   .OrderBy(p => p.Item2)
                   .ToList();

    // Build mit lines for a sheet slot (MT/OT/WHM/AST/SCH/SGE/D1..D4/Extras).
    public static List<MitLine> BuildLines(string slot)
    {
        var idx = Array.IndexOf(Slots, slot);
        var list = new List<MitLine>();
        if (idx < 0) return list;
        var seen = new HashSet<(int Time, uint Sync)>();
        foreach (var e in Timeline)
        {
            var action = e.Actions[idx];
            if (string.IsNullOrWhiteSpace(action)) continue;
            // Some mechanics are listed across several note-rows (group / alt-strat)
            // at the same time + ability. For one player that's a single action — take
            // only the first, or the call (and its audio) fires twice or more.
            if (!seen.Add((e.Time, e.Sync))) continue;
            list.Add(new MitLine { Time = e.Time, Mechanic = e.Mechanic, Action = action.Replace("*", "").Trim(), Enabled = true });
        }
        return list;
    }

    // Resync anchors (ability id -> expected resolve time).
    // The earliest synced cast in each phase is flagged as a phase anchor: it
    // gets a wide match window and re-bases the whole clock, so a phase that
    // starts far from the sheet's "standard" time (faster/slower kill) still
    // snaps into place and every following call in that phase stays accurate.
    public static List<SyncPoint> SyncPoints()
    {
        var points = new List<SyncPoint>();
        var phaseSeen = new HashSet<string>();
        var prevTime = float.NegativeInfinity;
        foreach (var e in Timeline.Where(e => e.Sync != 0).OrderBy(e => e.Time))
        {
            // Re-base (wide-forward) anchor at each phase start AND after any
            // downtime/cutscene gap (>90s) — so the clock can jump back on across a
            // transition even if it drifted while nothing was casting.
            var isPhaseAnchor = phaseSeen.Add(e.Phase) || (e.Time - prevTime) > 90f;
            points.Add(new SyncPoint
            {
                Ability = e.Sync,
                Time = e.Time,
                IsPhase = isPhaseAnchor,
                Label = $"{e.Phase} {e.Mechanic}"
            });
            prevTime = e.Time;
        }

        return points;
    }

    // No boss-appearance anchors. The old Chaos@451 one fired the moment Chaos
    // *appeared* (right when the P2->P3 cutscene ended), snapping the clock to 451
    // and firing the "Bowels of Agony" call several seconds before Bowels actually
    // cast — then everything after drifted. P3 now re-bases on the real Bowels cast
    // (BAF2, a phase anchor at 451s), which fires when the mechanic truly happens.
    public static List<BossAnchor> BossAnchors() => new();
}
