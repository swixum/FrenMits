// FROZEN SNAPSHOT of the DMU timeline as it was baked into users' configs before
// the sheet v5.0 update (the bake that shipped v1.0.0.118 through v1.0.0.167,
// matching the Ikuya sheet ~v4.1). The smart re-bake migration diffs against
// this to tell sheet-baked lines (replace, carrying per-line tweaks over) from
// custom lines people added (keep). Do not edit; it must match what shipped
// previously. When baking a NEW sheet version into DmuData, first copy the
// then-current DmuData.Timeline here so the diff stays correct.
using System;
using System.Collections.Generic;

namespace FrenMits;

public static class DmuLegacy
{
    public static readonly string[] Slots = { "MT", "OT", "WHM", "AST", "SCH", "SGE", "D1", "D2", "D3", "D4" };

    public sealed record Entry(int Time, string Phase, string Mechanic, uint Sync, string[] Actions);

    public static readonly Entry[] Timeline =
    {
        new(16, "P1", "Revolting Ruin III", 0xC403, new[]{"Reprisal (Optional First GCD)", "Buddy Mit", "Assist MT", "", "", "", "", "", "", ""}),
        new(38, "P1", "Mystery Magic", 0xBA94, new[]{"Party Mit (GNB/DRK)", "Reprisal + Party Mit", "Temperance", "Neutral Sect + Sun Sign", "Spreadlo + Sacred Soil", "Kerachole + Zoe Shields", "Feint", "", "Party Mit", ""}),
        new(43, "P1", "Wave Cannon", 0xBAA8, new[]{"Party Mit (WAR/PLD)", "", "Divine Caress + Asylum", "", "Seraph", "Holos", "", "", "", ""}),
        new(50, "P1", "Double-Trouble Trap", 0xBAA7, new[]{"", "", "", "", "Seraph", "", "", "", "", "Addle"}),
        new(63, "P1", "Light of Judgment", 0xC622, new[]{"Reprisal", "", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil", "Kerachole", "", "Feint", "", ""}),
        new(88, "P1", "Gravitas II (Part I)", 0xBAAC, new[]{"", "", "", "Macrocosmos", "Seraphism", "", "", "", "", ""}),
        new(106, "P1", "Gravitas II (Part II)", 0xBAAC, new[]{"", "", "Liturgy of the Bell", "", "Expedient + Fey Illumination", "Kerachole + Philosophia", "", "", "", ""}),
        new(118, "P1", "Double-Trouble Trap", 0xBAA7, new[]{"", "Reprisal + Party Mit", "", "", "Spreadlo + Sacred Soil", "Panhaima + Zoe Shields", "", "", "Party Mit", ""}),
        new(132, "P1", "Light of Judgment", 0xC622, new[]{"Reprisal + Party Mit", "", "Plenary Indulgence + Asylum", "Collective Unconscious", "", "Kerachole", "Feint", "", "", ""}),
        new(165, "P1", "Double-Trouble Trap", 0xBCF2, new[]{"", "", "Temperance", "Neutral Sect", "", "", "", "", "", ""}),
        new(173, "P1", "Indulgent Will", 0xBAB5, new[]{"", "", "Divine Caress", "Sun Sign", "Sacred Soil", "Kerachole", "", "", "", ""}),
        new(187, "P1", "Mystery Magic", 0xBA94, new[]{"", "Reprisal", "", "", "", "", "", "", "", ""}),
        new(221, "P2", "Ultimate Embrance", 0xC24C, new[]{"", "", "Assist Tanks", "", "", "", "", "Feint", "", ""}),
        new(236, "P2", "Forsaken", 0xBABC, new[]{"Reprisal", "Party Mit", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil + Spreadlo", "Kerachole + Holos + Zoe Shields", "Feint", "", "Party Mit", "Addle"}),
        new(250, "P2", "Towers I", 0xBABE, new[]{"", "", "Asylum", "", "Seraph + Fey Illumination", "Panhaima", "", "", "", ""}),
        new(260, "P2", "Towers II (Past/Future's End)", 0xBABE, new[]{"", "", "", "", "Seraph", "", "", "", "", ""}),
        new(271, "P2", "Towers III (All Things Ending)", 0xBABE, new[]{"Party Mit", "Reprisal", "", "Macrocosmos", "Sacred Soil", "Kerachole", "", "", "", ""}),
        new(281, "P2", "Towers IV (Past/Future's End)", 0xBABE, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(292, "P2", "Towers V (All Things Ending)", 0xBABE, new[]{"", "", "Liturgy of the Bell", "", "Expedient", "", "", "", "", ""}),
        new(302, "P2", "Towers VI (Past/Future's End)", 0xBABE, new[]{"Reprisal", "", "Plenary Indulgence + Temperance", "Collective Unconscious + Neutral Sect", "Seraphism", "Philosophia", "", "", "", ""}),
        new(312, "P2", "Towers VII (All Things Ending)", 0xBABE, new[]{"", "", "Divine Caress", "Sun Sign", "Sacred Soil", "Kerachole", "", "", "", ""}),
        new(323, "P2", "Towers VIII (Past/Future's End)", 0xBABE, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(343, "P2", "Light of Judgement", 0xBABD, new[]{"", "Reprisal + Party Mit", "Asylum", "", "Spreadlo + Sacred Soil", "Kerachole + Holos + Zoe Shields", "Feint", "", "Party Mit", "Addle"}),
        new(371, "P2", "Wings of Destruction", 0xC24C, new[]{"Reprisal + Party Mit", "", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil + Fey Illumination + Seraph", "Kerachole + Panhaima", "", "Feint", "", ""}),
        new(378, "P2", "Ultimate Embrace", 0xC24C, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(450, "P3", "Bowels of Agony (Chaos)", 0xBAF2, new[]{"Reprisal", "", "Plenary Indulgence + Asylum", "Collective Unconscious", "Sacred Soil", "Kerachole", "Feint (Chaos)", "", "", "Addle (The Decisive Battle)"}),
        new(470, "P3", "Stray Flames/Tsunami", 0xBAF8, new[]{"Party Mit", "Reprisal", "", "", "Spreadlo + Sacred Soil", "Zoe Shields + Kerachole", "", "", "", ""}),
        new(478, "P3", "Thunder III (1st Set)", 0, new[]{"", "", "", "", "Expedient", "Holos", "", "", "", ""}),
        new(497, "P3", "Stray Flames/Tsunami", 0, new[]{"", "Party Mit", "Temperance", "Neutral Sect", "Seraph", "", "", "", "", ""}),
        new(507, "P3", "Ultima Blaster", 0xBB00, new[]{"Reprisal", "", "", "Sun Sign", "Sacred Soil + Fey Illumination + Seraphism", "Kerachole + Panhaima", "", "Feint (Chaos)", "Party Mit", ""}),
        new(514, "P3", "Vacuum Wave", 0, new[]{"LB3", "LB3", "Plenary Indulgence", "Collective Unconscious", "", "", "", "", "", ""}),
        new(518, "P3", "Cyclone", 0xBAF8, new[]{"", "", "Divine Caress", "", "", "", "", "", "", ""}),
        new(537, "P3", "Thunder III (2nd Set)", 0xBAE2, new[]{"", "Reprisal", "", "", "", "", "", "", "", ""}),
        new(545, "P3", "The Decisive Battle", 0, new[]{"", "", "", "", "Spreadlo", "Zoe Shields", "", "", "", "Addle (Exdeath)"}),
        new(559, "P3", "Earthquake", 0, new[]{"Party Mit (GNB/DRK)", "Reprisal", "Asylum", "Macrocosmos", "Sacred Soil", "Kerachole + Philosophia", "Feint (Chaos)", "", "", ""}),
        new(578, "P3", "Shocking Impact/Shockwave", 0xBAF4, new[]{"Reprisal + Party Mit (WAR/PLD)", "", "Plenary Indulgence", "Collective Unconscious", "", "", "", "", "", ""}),
        new(609, "P3", "Shocking Impact/Shockwave", 0xBAFD, new[]{"", "Party Mit (GNB/DRK)", "Liturgy of the Bell", "", "Expedient + Seraph + Sacred Soil", "Holos + Kerachole", "", "", "", ""}),
        new(616, "P3", "Black Holes II (3rd Tether Set)", 0, new[]{"", "Party Mit (WAR/PLD)", "Temperance + Divine Caress", "Neutral Sect + Sun Sign", "Seraph", "", "", "", "", ""}),
        new(621, "P3", "Black Holes II (4th Tether Set)", 0xC571, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(626, "P3", "Black Holes II (5th Tether Set)", 0, new[]{"", "", "", "", "Fey Illumination", "", "", "", "", ""}),
        new(637, "P3", "Thunder III (5th Set)", 0, new[]{"Reprisal", "", "Asylum", "", "Spreadlo + Sacred Soil", "Zoe Shields + Kerachole", "", "Feint (Chaos)", "", "Addle (Exdeath)"}),
        new(650, "P3", "Black Holes III (6th Tether Set)", 0, new[]{"", "", "", "", "", "Panhaima", "", "", "Party Mit", ""}),
        new(677, "P3", "Shocking Impact/Shockwave", 0, new[]{"", "", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil", "Kerachole", "", "", "", ""}),
        new(705, "P3", "Stomp-a-Mole", 0xBAF0, new[]{"Reprisal + Party Mit", "", "", "", "Seraphism + Sacred Soil", "Kerachole", "Feint (Chaos)", "", "", ""}),
        new(745, "P4", "Kefka Returns (phase enter)", 0xC2DC, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(763, "P4", "Grand Cross", 0xBB14, new[]{"", "Reprisal", "Temperance + Asylum", "Neutral Sect", "Spreadlo + Expedient + Sacred Soil", "Kerachole + Philosophia + Zoe Shields", "", "Feint", "Party Mit", ""}),
        new(769, "P4", "Inferno/Tsunami", 0, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(778, "P4", "Grand Cross", 0xBB14, new[]{"", "", "Plenary Indulgence", "Collective Unconscious", "Fey Illumination", "Holos", "", "", "", ""}),
        new(783, "P4", "Inferno/Tsunami", 0, new[]{"", "Party Mit (GNB/DRK)", "", "Sun Sign", "Seraph", "", "", "", "", ""}),
        new(793, "P4", "Grand Cross", 0xBB14, new[]{"", "Party Mit (WAR/PLD)", "Divine Caress", "", "Seraph", "Kerachole", "", "", "", ""}),
        new(805, "P4", "Flood of Naught", 0xC393, new[]{"", "", "Liturgy of the Bell", "Macrocosmos", "Sacred Soil", "Panhaima", "", "", "", ""}),
        new(815, "P4", "Death Bolt/Wave", 0, new[]{"Party Mit", "", "", "", "", "", "", "", "", ""}),
        new(833, "P4", "Ultima Upsurge", 0xC24A, new[]{"Reprisal", "", "", "", "Sacred Soil", "Kerachole", "Feint", "", "", "Addle"}),
        new(840, "P4", "Death Bolt/Wave", 0xBB1B, new[]{"", "", "Plenary Indulgence + Asylum", "Collective Unconscious", "", "", "", "", "", ""}),
        new(872, "P4", "Ultima Upsurge", 0, new[]{"", "Reprisal", "", "", "Sacred Soil", "Kerachole", "", "", "", ""}),
        new(911, "P5", "Ultima Repeater", 0xBB40, new[]{"Reprisal", "Party Mit", "Plenary Indulgence", "Collective Unconscious", "Spreadlo + Sacred Soil", "Zoe Shields + Holos + Kerachole", "", "", "Party Mit", ""}),
        new(916, "P5", "Fell Forces (3x)", 0xC654, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(928, "P5", "Chaotic Flood", 0xC13F, new[]{"", "", "Temperance", "Neutral Sect", "Expedient", "Panhaima", "", "", "", ""}),
        new(940, "P5", "Maddening Orchestra", 0xBB50, new[]{"", "Reprisal", "Divine Caress", "Sun Sign", "Sacred Soil + Fey Illumination", "Kerachole", "", "Feint", "", ""}),
        new(953, "P5", "Fell Forces (2x)", 0xC654, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(971, "P5", "Celestriad", 0xBB42, new[]{"Party Mit", "", "Asylum", "", "Seraph + Sacred Soil", "Kerachole", "", "", "", ""}),
        new(993, "P5", "Ultima Repeater", 0, new[]{"Reprisal", "Party Mit", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil", "Kerachole", "Feint", "", "Party Mit", "Addle"}),
        new(998, "P5", "Fell Forces (2x)", 0xC654, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(1024, "P5", "Stray Entropy", 0xBB3F, new[]{"", "Reprisal", "", "", "Sacred Soil", "Kerachole", "", "", "", ""}),
        new(1033, "P5", "Maddening Orchestra", 0xBB51, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(1045, "P5", "Fell Forces (3x)", 0, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(1062, "P5", "Forsaken", 0xBB35, new[]{"Reprisal + Party Mit", "", "Temperance + Asylum", "Neutral Sect", "Spreadlo + Fey Illumination + Sacred Soil", "Zoe Shields + Holos + Kerachole", "", "Feint", "", ""}),
        new(1067, "P5", "Forsaken Bonds", 0, new[]{"", "", "", "", "Seraphism", "Philosophia", "", "", "", ""}),
        new(1070, "P5", "Forsaken", 0xBB36, new[]{"", "", "Liturgy of the Bell", "Macrocosmos", "Expedient", "Panhaima", "", "", "", ""}),
        new(1076, "P5", "Forsaken Bonds", 0, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(1079, "P5", "Forsaken", 0xBB36, new[]{"", "Reprisal + Party Mit (GNB/DRK)", "Divine Caress + Plenary Indulgence", "Sun Sign + Collective Unconscious", "Seraph", "", "Feint", "", "Party Mit", "Addle"}),
        new(1084, "P5", "Forsaken Bonds", 0, new[]{"", "Party Mit (WAR/PLD)", "", "", "", "", "", "", "", ""}),
        new(1087, "P5", "Forsaken", 0xBB36, new[]{"", "", "", "", "Seraph", "", "", "", "", ""}),
        new(1092, "P5", "Forsaken Bonds", 0, new[]{"", "", "", "", "Sacred Soil", "Kerachole", "", "", "", ""}),
        new(1126, "P5", "Forsaken Null", 0, new[]{"Enrage!", "", "", "", "", "", "", "", "", ""}),
    };

    // Build mit lines for a sheet slot (MT/OT/WHM/AST/SCH/SGE/D1..D4), exactly as
    // the previous bake did, so the smart re-bake can identity-match old lines.
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
}
