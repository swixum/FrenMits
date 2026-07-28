// AUTO-GENERATED from the Ikuya "Dancing Mad (Ultimate)" mit sheet v5.0
// (2026-07-16), with resync ability ids cross-referenced from the cactbot
// dancing_mad timeline so the anchors snap the clock onto each real cast and
// keep the authored mit times aligned through all five phases (times = seconds
// from the pull, continuous).
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

public static class DmuData
{
    public static readonly string[] Slots = { "MT", "OT", "WHM", "AST", "SCH", "SGE", "D1", "D2", "D3", "D4" };

    // Hurt: 0 unknown, 1 light, 2 hurts, 3 deadly. Buster: lands on a tank rather
    // than the party. Measured - see the note above CustomRows.
    public sealed record Entry(int Time, string Phase, string Mechanic, uint Sync, string[] Actions,
        int Hurt = 0, bool Buster = false);

    public static readonly Entry[] Timeline =
    {
        new(16, "P1", "Revolting Ruin III", 0xC403, new[]{"Reprisal (Optional First GCD)", "Buddy Mit", "Assist MT", "", "", "", "", "", "", ""}, 3, true),
        new(38, "P1", "Mystery Magic", 0xBA94, new[]{"Party Mit (GNB/DRK)", "Reprisal + Party Mit", "Temperance", "Neutral Sect + Sun Sign", "Spreadlo + Sacred Soil", "Kerachole + Zoe Shields", "Feint", "", "Party Mit", ""}),
        new(43, "P1", "Wave Cannon", 0xBAA8, new[]{"Party Mit (WAR/PLD)", "", "Divine Caress + Asylum", "", "Seraph", "Holos", "", "", "", ""}, 2),
        new(47, "P1", "Explosion", 0, new[]{"","","","","","","","","",""}, 2),
        new(50, "P1", "Double-Trouble Trap", 0xBAA7, new[]{"", "", "", "", "Seraph", "", "", "", "", "Addle"}, 2),
        new(63, "P1", "Light of Judgment", 0xC622, new[]{"Reprisal", "", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil", "Kerachole", "", "Feint", "", ""}, 2),
        new(67, "P1", "Hyperdrive", 0, new[]{"","","","","","","","","",""}, 2, true),
        new(88, "P1", "Gravitas II (Part I)", 0xBAAC, new[]{"", "", "", "Macrocosmos", "Seraphism", "", "", "", "", ""}, 1),
        new(93, "P1", "Vitrophyre", 0, new[]{"","","","","","","","","",""}, 1),
        new(98, "P1", "Revolting Ruin III", 0, new[]{"","","","","","","","","",""}, 3, true),
        new(106, "P1", "Gravitas II (Part II)", 0xBAAC, new[]{"", "", "Liturgy of the Bell", "", "Expedient + Fey Illumination", "Kerachole + Philosophia", "", "", "", ""}, 1),
        new(111, "P1", "Vitrophyre", 0, new[]{"","","","","","","","","",""}, 1),
        new(118, "P1", "Double-Trouble Trap", 0xBAA7, new[]{"", "Reprisal + Party Mit", "", "", "Spreadlo + Sacred Soil", "Panhaima + Zoe Shields", "", "", "Party Mit", ""}, 1),
        new(121, "P1", "Gravity III", 0, new[]{"","","","","","","","","",""}, 1),
        new(132, "P1", "Light of Judgment", 0xC622, new[]{"Reprisal + Party Mit", "", "Plenary Indulgence + Asylum", "Collective Unconscious", "", "Kerachole", "Feint", "", "", ""}, 2),
        new(135, "P1", "Hyperdrive", 0, new[]{"","","","","","","","","",""}, 1, true),
        new(165, "P1", "Double-Trouble Trap", 0xBCF2, new[]{"", "", "Temperance", "Neutral Sect", "", "", "", "", "", ""}, 1),
        new(173, "P1", "Indulgent Will", 0xBAB5, new[]{"", "", "Divine Caress", "Sun Sign", "Sacred Soil", "Kerachole", "", "", "", ""}, 2),
        new(174, "P1", "Indulgent Will", 0, new[]{"","","","","","","","","",""}, 2),
        new(187, "P1", "Mystery Magic", 0xBA94, new[]{"", "Reprisal", "", "", "", "", "", "", "", ""}),
        new(221, "P2", "Ultimate Embrace", 0xC24C, new[]{"", "", "Assist Tanks", "", "Spreadlo", "Holos", "", "Feint", "", ""}, 3, true),
        new(236, "P2", "Forsaken", 0xBABC, new[]{"Reprisal", "Party Mit", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil", "Kerachole + Zoe Shields", "Feint", "", "Party Mit", "Addle"}, 3),
        new(250, "P2", "Towers I", 0xBAC0, new[]{"", "", "Asylum", "", "Seraph + Fey Illumination", "Panhaima", "", "", "", ""}, 2),
        new(260, "P2", "Towers II (Past/Future's End)", 0, new[]{"", "", "", "", "Seraph", "", "", "", "", ""}, 1, true),
        new(271, "P2", "Towers III (All Things Ending)", 0, new[]{"Party Mit", "Reprisal", "", "Macrocosmos", "Sacred Soil", "Kerachole", "", "", "", ""}, 2),
        new(281, "P2", "Towers IV (Past/Future's End)", 0xBABE, new[]{"", "", "", "", "", "", "", "", "", ""}, 1, true),
        new(292, "P2", "Towers V (All Things Ending)", 0xBABE, new[]{"", "", "Liturgy of the Bell", "", "Expedient", "", "", "", "", ""}, 2),
        new(302, "P2", "Towers VI (Past/Future's End)", 0, new[]{"Reprisal", "", "Plenary Indulgence + Temperance", "Collective Unconscious + Neutral Sect", "Seraphism", "Philosophia", "", "", "", ""}, 1, true),
        new(312, "P2", "Towers VII (All Things Ending)", 0, new[]{"", "", "Divine Caress", "Sun Sign", "Sacred Soil", "Kerachole", "", "", "", ""}, 2),
        new(323, "P2", "Towers VIII (Past/Future's End)", 0, new[]{"", "", "", "", "", "", "", "", "", ""}, 1, true),
        new(343, "P2", "Light of Judgement", 0xBABD, new[]{"", "Reprisal + Party Mit", "Asylum", "", "Spreadlo + Sacred Soil", "Kerachole + Holos + Zoe Shields", "Feint", "", "Party Mit", "Addle"}, 3),
        new(364, "P2", "Wings of Destruction", 0, new[]{"","","","","","","","","",""}, 2, true),
        new(371, "P2", "Wings of Destruction", 0xC24C, new[]{"Reprisal + Party Mit", "", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil + Fey Illumination + Seraph", "Kerachole + Panhaima", "", "Feint", "", ""}, 3, true),
        new(378, "P2", "Ultimate Embrace", 0xC24C, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(450, "P3", "Bowels of Agony (Chaos)", 0xBAF2, new[]{"Reprisal", "", "Plenary Indulgence + Asylum", "Collective Unconscious", "Sacred Soil", "Kerachole", "Feint (Chaos)", "", "", ""}, 1),
        new(470, "P3", "Stray Flames/Tsunami", 0, new[]{"Party Mit", "Reprisal", "", "", "Spreadlo + Sacred Soil", "Zoe Shields + Kerachole", "", "", "", ""}, 1, true),
        new(478, "P3", "Thunder III (1st Set)", 0, new[]{"", "", "", "", "Expedient", "Holos", "", "", "", "Addle (Exdeath)"}, 2, true),
        new(493, "P3", "Tsunami", 0, new[]{"","","","","","","","","",""}, 1, true),
        new(497, "P3", "Stray Flames/Tsunami", 0, new[]{"", "Party Mit", "Temperance", "Neutral Sect", "Seraph", "", "", "", "", ""}),
        new(503, "P3", "Ultima Blaster", 0, new[]{"","","","","","","","","",""}, 1),
        new(507, "P3", "Ultima Blaster", 0xBB00, new[]{"Reprisal", "", "", "Sun Sign", "Sacred Soil + Fey Illumination + Seraphism", "Kerachole + Panhaima", "", "Feint (Chaos)", "Party Mit", ""}, 1),
        new(514, "P3", "Vacuum Wave", 0, new[]{"LB3", "LB3", "Plenary Indulgence", "Collective Unconscious", "", "", "", "", "", ""}),
        new(518, "P3", "Cyclone", 0xBAF8, new[]{"", "", "Divine Caress", "", "", "", "", "", "", ""}, 3),
        new(529, "P3", "Ultima Blaster", 0, new[]{"","","","","","","","","",""}, 1),
        new(537, "P3", "Thunder III (2nd Set)", 0xBB09, new[]{"", "", "", "", "", "", "", "", "", ""}, 2, true),
        new(545, "P3", "The Decisive Battle", 0, new[]{"", "Reprisal (Exdeath)", "", "", "Spreadlo", "Zoe Shields", "", "", "", ""}),
        new(554, "P3", "Thunder III", 0, new[]{"","","","","","","","","",""}, 3, true),
        new(559, "P3", "Earthquake", 0, new[]{"Party Mit (GNB/DRK)", "", "Asylum", "Macrocosmos", "Sacred Soil", "Kerachole + Philosophia", "Feint (Chaos)", "", "", ""}, 1),
        new(578, "P3", "Shocking Impact/Shockwave", 0xBAE9, new[]{"Reprisal + Party Mit (WAR/PLD)", "", "Plenary Indulgence", "Collective Unconscious", "", "", "", "", "", ""}, 2),
        new(586, "P3", "Nothingness", 0, new[]{"","","","","","","","","",""}, 1, true),
        new(592, "P3", "Nothingness", 0, new[]{"","","","","","","","","",""}, 1, true),
        new(595, "P3", "Earthquake", 0, new[]{"","","","","","","","","",""}, 1),
        new(595, "P3", "Thunder III", 0, new[]{"","","","","","","","","",""}, 1, true),
        new(609, "P3", "Shocking Impact/Shockwave", 0xBAE9, new[]{"", "Party Mit (GNB/DRK)", "Liturgy of the Bell", "", "Expedient + Seraph + Sacred Soil", "Holos + Kerachole", "", "", "", ""}, 2),
        new(616, "P3", "Black Holes II (3rd Tether Set)", 0, new[]{"", "Party Mit (WAR/PLD)", "Temperance + Divine Caress", "Neutral Sect + Sun Sign", "Seraph", "", "", "", "", ""}, 3, true),
        new(621, "P3", "Black Holes II (4th Tether Set)", 0, new[]{"", "", "", "", "", "", "", "", "", ""}, 3, true),
        new(626, "P3", "Black Holes II (5th Tether Set)", 0, new[]{"", "", "", "", "Fey Illumination", "", "", "", "", ""}),
        new(635, "P3", "Earthquake", 0, new[]{"","","","","","","","","",""}, 1),
        new(637, "P3", "Thunder III (5th Set)", 0, new[]{"Reprisal", "", "Asylum", "", "Spreadlo + Sacred Soil", "Zoe Shields + Kerachole", "", "Feint (Chaos)", "", "Addle (Exdeath)"}, 2, true),
        new(650, "P3", "Black Holes III (6th Tether Set)", 0, new[]{"", "", "", "", "", "Panhaima", "", "", "Party Mit", ""}, 3, true),
        new(655, "P3", "Nothingness", 0, new[]{"","","","","","","","","",""}, 3, true),
        new(661, "P3", "Nothingness", 0, new[]{"","","","","","","","","",""}, 3, true),
        new(668, "P3", "Earthquake", 0, new[]{"","","","","","","","","",""}, 1),
        new(677, "P3", "Shocking Impact/Shockwave", 0xBAE9, new[]{"", "", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil", "Kerachole", "", "", "", ""}),
        new(689, "P3", "Earthquake", 0, new[]{"","","","","","","","","",""}, 1),
        new(705, "P3", "Stomp-a-Mole", 0xBAF0, new[]{"Reprisal + Party Mit", "Reprisal", "", "", "Seraphism + Sacred Soil", "Kerachole", "Feint (Chaos)", "", "", ""}, 2),
        new(705, "P3", "Stomp-a-Mole", 0, new[]{"","","","","","","","","",""}, 2),
        new(745, "P4", "Kefka Returns (phase enter)", 0xC2DC, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(763, "P4", "Grand Cross", 0xBB14, new[]{"", "", "Plenary Indulgence + Asylum", "Collective Unconscious", "Spreadlo + Sacred Soil", "Kerachole + Philosophia + Holos", "", "Feint", "", ""}, 2),
        new(769, "P4", "Inferno/Tsunami", 0, new[]{"", "", "", "", "", "", "", "", "Party Mit", ""}, 2),
        new(778, "P4", "Grand Cross", 0xBB14, new[]{"", "", "Temperance", "Neutral Sect", "Expedient + Fey Illumination", "Panhaima", "", "", "", ""}, 2),
        new(783, "P4", "Inferno/Tsunami", 0, new[]{"", "Party Mit (GNB/DRK)", "", "Sun Sign", "Seraph", "", "", "", "", ""}, 2),
        new(793, "P4", "Grand Cross", 0xBB14, new[]{"", "Party Mit (WAR/PLD)", "Divine Caress", "", "Seraph", "Zoe Shields", "", "", "", ""}, 2),
        new(805, "P4", "Flood of Naught", 0xC393, new[]{"", "", "Liturgy of the Bell", "Macrocosmos", "Sacred Soil", "Kerachole", "", "", "", ""}, 1),
        new(806, "P4", "White Antilight", 0, new[]{"","","","","","","","","",""}, 1),
        new(815, "P4", "Death Bolt/Wave", 0, new[]{"Party Mit", "", "", "", "", "", "", "", "", ""}, 2),
        new(817, "P4", "Death Wave", 0, new[]{"","","","","","","","","",""}, 1, true),
        new(833, "P4", "Ultima Upsurge", 0xC24A, new[]{"Reprisal", "", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil", "Kerachole", "Feint", "", "", "Addle"}, 2),
        new(840, "P4", "Death Bolt/Wave", 0xBB1B, new[]{"", "", "Asylum", "", "", "", "", "", "", ""}),
        new(872, "P4", "Ultima Upsurge", 0, new[]{"", "Reprisal", "", "", "Sacred Soil", "Kerachole", "", "", "", ""}, 2),
        new(911, "P5", "Ultima Repeater", 0xBB40, new[]{"Reprisal", "Party Mit", "Plenary Indulgence", "Collective Unconscious", "Spreadlo + Sacred Soil", "Zoe Shields + Holos + Kerachole", "", "", "Party Mit", ""}, 1),
        new(916, "P5", "Fell Forces (3x)", 0xC654, new[]{"", "", "", "", "", "", "", "", "", ""}, 3),
        new(928, "P5", "Chaotic Flood", 0xC13F, new[]{"", "", "Temperance", "Neutral Sect", "Expedient", "Panhaima", "", "", "", ""}, 1),
        new(940, "P5", "Maddening Orchestra", 0xBB50, new[]{"", "Reprisal", "Divine Caress", "Sun Sign", "Sacred Soil + Fey Illumination", "Kerachole", "", "Feint", "", ""}, 1, true),
        new(942, "P5", "Holy", 0, new[]{"","","","","","","","","",""}, 2),
        new(945, "P5", "Chaotic Flare", 0, new[]{"","","","","","","","","",""}, 1, true),
        new(949, "P5", "Chaotic Holy", 0, new[]{"","","","","","","","","",""}, 3, true),
        new(949, "P5", "Flare Diffusion", 0, new[]{"","","","","","","","","",""}, 1, true),
        new(953, "P5", "Fell Forces (2x)", 0xC654, new[]{"", "", "", "", "", "", "", "", "", ""}, 3),
        new(969, "P5", "Fire III", 0, new[]{"","","","","","","","","",""}, 1, true),
        new(969, "P5", "Thunder III", 0, new[]{"","","","","","","","","",""}, 1, true),
        new(971, "P5", "Celestriad", 0xBB42, new[]{"Party Mit", "", "Asylum", "", "Seraph", "", "", "", "", ""}, 1, true),
        new(978, "P5", "Blizzard III", 0, new[]{"","","","","","","","","",""}, 1),
        new(978, "P5", "Fire III", 0, new[]{"","","","","","","","","",""}, 1, true),
        new(978, "P5", "Thunder III", 0, new[]{"","","","","","","","","",""}, 1, true),
        new(984, "P5", "Blizzard III", 0, new[]{"","","","","","","","","",""}, 1, true),
        new(984, "P5", "Fire III", 0, new[]{"","","","","","","","","",""}, 1),
        new(984, "P5", "Thunder III", 0, new[]{"","","","","","","","","",""}, 1, true),
        new(993, "P5", "Ultima Repeater", 0, new[]{"Reprisal", "Party Mit", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil", "Kerachole", "Feint", "", "Party Mit", "Addle"}, 1),
        new(998, "P5", "Fell Forces (2x)", 0xC654, new[]{"", "", "", "", "", "", "", "", "", ""}, 3),
        new(1024, "P5", "Stray Entropy", 0xBB3F, new[]{"", "Reprisal", "", "", "", "", "", "", "", ""}, 2),
        new(1032, "P5", "Holy", 0, new[]{"","","","","","","","","",""}, 2),
        new(1033, "P5", "Maddening Orchestra", 0xBB51, new[]{"", "", "", "", "Sacred Soil", "Kerachole", "", "", "", ""}, 1, true),
        new(1034, "P5", "Chaotic Flare", 0, new[]{"","","","","","","","","",""}, 1, true),
        new(1038, "P5", "Flare Diffusion", 0, new[]{"","","","","","","","","",""}, 1, true),
        new(1045, "P5", "Fell Forces (3x)", 0, new[]{"", "", "Assist Tanks", "", "", "", "", "", "", ""}, 3),
        new(1062, "P5", "Forsaken (1st Hit)", 0xBB35, new[]{"Reprisal + Party Mit", "", "Temperance + Asylum", "Neutral Sect", "Spreadlo + Fey Illumination + Sacred Soil", "Zoe Shields + Holos + Kerachole", "", "Feint", "", ""}, 3),
        new(1067, "P5", "Forsaken Bonds (2nd Hit)", 0, new[]{"", "", "Liturgy of the Bell", "", "Seraphism", "Philosophia", "", "", "", ""}, 2),
        new(1070, "P5", "Forsaken (3rd Hit)", 0xBB36, new[]{"", "", "", "Macrocosmos", "Expedient", "Panhaima", "", "", "", ""}, 3),
        new(1076, "P5", "Forsaken Bonds (4th Hit)", 0, new[]{"", "", "", "", "", "", "", "", "", ""}, 2),
        new(1079, "P5", "Forsaken (5th Hit)", 0xBB36, new[]{"", "Reprisal + Party Mit (GNB/DRK)", "Divine Caress + Plenary Indulgence", "Sun Sign + Collective Unconscious", "", "", "Feint", "", "Party Mit", "Addle"}, 3),
        new(1084, "P5", "Forsaken Bonds (6th Hit)", 0, new[]{"", "Party Mit (WAR/PLD)", "", "", "", "", "", "", "", ""}, 2),
        new(1087, "P5", "Forsaken (7th Hit)", 0xBB36, new[]{"", "", "", "", "Seraph", "", "", "", "", ""}),
        new(1092, "P5", "Forsaken Bonds (8th Hit)", 0, new[]{"", "", "", "", "Seraph + Sacred Soil", "Kerachole", "", "", "", ""}),
        new(1126, "P5", "Forsaken Null", 0, new[]{"Enrage!", "", "", "", "", "", "", "", "", ""}),
    };

    // First time each phase appears, for the practice phase-jump.
    public static List<(string Name, float Time)> PhaseStarts()
        => Timeline.GroupBy(e => e.Phase)
                   .Select(g => (g.Key, (float)g.Min(e => e.Time)))
                   .OrderBy(p => p.Item2)
                   .ToList();

    public static string PhaseTitle(string phase) => phase switch
    {
        "P1" => "Phase 1: Kefka",
        "P2" => "Phase 2: Forsaken Kefka",
        "P3" => "Phase 3: Chaos & Exdeath",
        "P4" => "Phase 4: Kefka Says",
        "P5" => "Phase 5: Ultima Kefka",
        _ => phase,
    };

    // The sheet's per-phase "Notes" footer (shown under the Sheet View) plus
    // short per-mechanic play guidance shown under a board row when no sheet
    // note applies.
    public static string PressNote(string mechanic)
    {
        var m = (mechanic ?? "").Trim();
        foreach (var (key, note) in PressNotes)
            if (m.Contains(key, StringComparison.OrdinalIgnoreCase)) return note;
        return "";
    }

    private static readonly (string Key, string Note)[] PressNotes =
    {
        ("Ultimate Embrace", "Shield the tank just before it - OT early, MT during the cast."),
        ("Bowels of Agony", "Prep right after the textbox clears; this covers the autos into Stray Flames/Tsunami too."),
        ("The Decisive Battle", "Holding Exdeath? Reprisal BOTH bosses before this ends."),
        ("Stray Apocalypse", "Re-press two GCDs after it so everything is back for Forsaken."),
        ("Celestriad", "Press during the castbar; the towers after need the coverage."),
        ("Forsaken", "The wall. Everything the party has goes here."),
    };

    public static string PhaseNotes(string phase) => phase switch
    {
        "P1" => "All mechanics require shields!\n"
            + "Mitigation for the first Mystery Magic should carry over till the first Double-Trouble Trap unless there is a different usage timing below. "
            + "Targeted mitigation does not work on Wave Cannon, but does apply to Double-Trouble Trap.\n"
            + "Use mitigation for Light of Judgement late into the castbar so it will cover Hyperdrive.\n"
            + "\n"
            + "1) Use your 90s party mitigation as Kefka re-centers to cast the first Graven Image (WAR/PLD can use after Revolting Ruin III finishes).\n"
            + "2) Use your 30s mitigation for the first Mystery Magic after the Graven Image castbar.\n"
            + "3) You can alternatively use Bell just before the first set of puddles which will provide an immediate heal when the second set of puddles occurs, as the Bell will expire shortly after.\n"
            + "4) If you plan to use Dissipation in your opener, use it before Aetherflow. If you use the first Spreadlo earlier, you will get it back for the Double-Trouble Trap in the second Graven Image and be able to use Seraphism earlier/later.",

        "P2" => "All mechanics require shields!\n"
            + "\n"
            + "1) Provide single target mitigation and GCD shield both tanks in the phase transition for Ultimate Embrace. Also assist tanks with the last Ultimate Embrace.\n"
            + "2) Prepare Spreadlo either on the OT shortly beforehand or the MT during Ultimate Embrace to assist the tanks.\n"
            + "3) Use Holos during the first Ultimate Embrace so it is back for Light of Judgement and provides mitigation to the tanks. Alternatively, you can use Holos for the Wings of Destruction + Ultimate Embrace.\n"
            + "4) Use early to avoid shaking off mitigation if playing WAR.",

        "P3" => "All mechanics require shields!\n"
            + "Targeted mitigation must be on your firewalled target unless the firewall is down. For the most part, most targeted mitigation is mostly filler and does not work on raidwides. It is mainly used for minimizing tank autos and/or busters.\n"
            + "Both tanks will get attacked for moderately high damage throughout the entire phase, ensure you are rolling mitigation and heals on them.\n"
            + "\n"
            + "1) At the beginning of the phase, use 30s mitigation after (when the textbox disappears) Kefka says, \"Oh! What other toys can I throw in here...\" to get tank autos and the raidwide + an additional usage for Stray Flames/Tsunami.\n"
            + "2) There is a very small period where you can cover both hits of Thunder III and the next Stray Flames/Tsunami; if you miss the timing, you can use it next GCD.\n"
            + "3) Use if holding Chaos, otherwise use at the beginning of P4 for autos.\n"
            + "\n"
            + "4) Non-healers should avoid using any healing abilities that may cause their Accretion to pop early such as Second Wind, Curing Waltz or Divine Veil. If both Accretions are activated in a short amount of time, it will cause a wipe.\n"
            + "Healers will need to manage HP burst accordingly to ensure that Accretions are not popped together. The H1 and H2 can throw single target heals at whoever has the Accretion between them.\n"
            + "If playing AST, ensure the vulnerability has expired before popping Macrocosmos. WHM can use Benediction (if not used earlier) to instantly pop the healer Accretion.\n"
            + "\n"
            + "5) If you are holding Exdeath instead of Chaos at the beginning, use Reprisal on both before The Decisive Battle finishes.\n"
            + "6) Use LB3 at the W of Vacuum Wave. Either tank can press it, discuss beforehand.\n"
            + "7) Seraphism can be shifted to P4 if you feel you have sufficient mitigation.\n"
            + "8) Prepare Spreadlo on the tanks, prioritizing WAR > DRK > GNB/PLD.\n"
            + "9) Prepare immediately after Bowels of Agony.",

        "P4" => "All mechanics require shields!\n"
            + "Targeted mitigation (Reprisal, Addle, etc) only works on Ultima Upsurge; the rest is used to assist in mitigating tank auto attacks.\n"
            + "\n"
            + "1) Use at the beginning of the phase for autos.",

        "P5" => "All mechanics require shields!\n"
            + "For Forsaken, use any timed mitigation as late as possible unless otherwise noted.\n"
            + "\n"
            + "1) Use when Kefka brings his staff down to his right side (the sheet links a video example). The subsequent usages should be pressed immediately off cooldown.\n"
            + "2) Healers should monitor the tanks during Maddening Orchestra (especially the Flare tank) and Fell Forces. For WAR/DRK, you will need to have single target burst healing prepared after their invulnerability expires so they can survive the 3rd auto.\n"
            + "3) Use two GCDs after the Stray Apocalypse castbar is completed so it is back for Forsaken.\n"
            + "4) Use during the Celestriad castbar.\n"
            + "5) Use after the third towers in Celestriad resolves.",

        _ => "",
    };

    // Build mit lines for a sheet slot (MT/OT/WHM/AST/SCH/SGE/D1..D4/Extras).
    // Severity and tank-buster flags, measured from six kills: every damaging cast
    // paired with what it lands for unmitigated, graded against the 90th percentile
    // of the fight's own raidwides.
    public static List<CustomRow> CustomRows()
    {
        var rows = new List<CustomRow>();
        foreach (var e in Timeline)
            if (e.Hurt > 0 || e.Buster)
                rows.Add(new CustomRow { Time = e.Time, Mechanic = e.Mechanic, Hurt = e.Hurt, Buster = e.Buster });
        return rows;
    }

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
            // at the same time + ability, so take only the first or the call (and
            // its audio) fires twice or more.
            if (!seen.Add((e.Time, e.Sync))) continue;
            list.Add(new MitLine { Time = e.Time, Mechanic = e.Mechanic, Action = action.Replace("*", "").Trim(), Enabled = true });
        }
        return list;
    }

    // Resync anchors (ability id -> expected resolve time): the earliest synced
    // cast in each phase is a phase anchor that re-bases the whole clock so every
    // following call in that phase stays accurate.
    public static List<SyncPoint> SyncPoints()
    {
        return SyncAnchors.Build(
            Timeline.Select(e => (e.Sync, (float)e.Time, e.Phase, e.Mechanic)));
    }

    // No boss-appearance anchors, because the old Chaos@451 one fired the "Bowels
    // of Agony" call the moment Chaos appeared (before the real cast), so P3 now
    // re-bases on the real Bowels cast (BAF2, a phase anchor at 451s).
    public static List<BossAnchor> BossAnchors() => new();
}
