// AUTO-GENERATED from the Ikuya "Dancing Mad (Ultimate)" mit sheet
// (Ikuya Kirishima), sheet version 5.0 (2026-07-16). The sheet's grey "arrow"
// cells are carryover indicators (a mit still active from an earlier row), not
// new casts, and are NOT baked.
// EXCEPTION: the WHM Asylum calls are a FrenMits addition timed from an FFLogs
// clear; the sheet never lists Asylum. Keep them when syncing future sheet
// versions (they are the " + Asylum" / "Asylum" entries in the WHM column).
// EXCEPTION: the sheet's P2 row at 221 is misspelled "Ultimate Embrance"; it is
// deliberately baked corrected as "Ultimate Embrace". Keep the correction when
// syncing future sheet versions (a diff will show it as a change; it isn't).
// Resync ability ids are cross-referenced from the cactbot
// dancing_mad timeline (07-dt/ultimate): every sheet mechanic is matched to its
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

public static class DmuData
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
        new(221, "P2", "Ultimate Embrace", 0xC24C, new[]{"", "", "Assist Tanks", "", "Spreadlo", "Holos", "", "Feint", "", ""}),
        new(236, "P2", "Forsaken", 0xBABC, new[]{"Reprisal", "Party Mit", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil", "Kerachole + Zoe Shields", "Feint", "", "Party Mit", "Addle"}),
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
        new(450, "P3", "Bowels of Agony (Chaos)", 0xBAF2, new[]{"Reprisal", "", "Plenary Indulgence + Asylum", "Collective Unconscious", "Sacred Soil", "Kerachole", "Feint (Chaos)", "", "", ""}),
        new(470, "P3", "Stray Flames/Tsunami", 0xBAF8, new[]{"Party Mit", "Reprisal", "", "", "Spreadlo + Sacred Soil", "Zoe Shields + Kerachole", "", "", "", ""}),
        new(478, "P3", "Thunder III (1st Set)", 0, new[]{"", "", "", "", "Expedient", "Holos", "", "", "", "Addle (Exdeath)"}),
        new(497, "P3", "Stray Flames/Tsunami", 0, new[]{"", "Party Mit", "Temperance", "Neutral Sect", "Seraph", "", "", "", "", ""}),
        new(507, "P3", "Ultima Blaster", 0xBB00, new[]{"Reprisal", "", "", "Sun Sign", "Sacred Soil + Fey Illumination + Seraphism", "Kerachole + Panhaima", "", "Feint (Chaos)", "Party Mit", ""}),
        new(514, "P3", "Vacuum Wave", 0, new[]{"LB3", "LB3", "Plenary Indulgence", "Collective Unconscious", "", "", "", "", "", ""}),
        new(518, "P3", "Cyclone", 0xBAF8, new[]{"", "", "Divine Caress", "", "", "", "", "", "", ""}),
        new(537, "P3", "Thunder III (2nd Set)", 0xBAE2, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(545, "P3", "The Decisive Battle", 0, new[]{"", "Reprisal (Exdeath)", "", "", "Spreadlo", "Zoe Shields", "", "", "", ""}),
        new(559, "P3", "Earthquake", 0, new[]{"Party Mit (GNB/DRK)", "", "Asylum", "Macrocosmos", "Sacred Soil", "Kerachole + Philosophia", "Feint (Chaos)", "", "", ""}),
        new(578, "P3", "Shocking Impact/Shockwave", 0xBAF4, new[]{"Reprisal + Party Mit (WAR/PLD)", "", "Plenary Indulgence", "Collective Unconscious", "", "", "", "", "", ""}),
        new(609, "P3", "Shocking Impact/Shockwave", 0xBAFD, new[]{"", "Party Mit (GNB/DRK)", "Liturgy of the Bell", "", "Expedient + Seraph + Sacred Soil", "Holos + Kerachole", "", "", "", ""}),
        new(616, "P3", "Black Holes II (3rd Tether Set)", 0, new[]{"", "Party Mit (WAR/PLD)", "Temperance + Divine Caress", "Neutral Sect + Sun Sign", "Seraph", "", "", "", "", ""}),
        new(621, "P3", "Black Holes II (4th Tether Set)", 0xC571, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(626, "P3", "Black Holes II (5th Tether Set)", 0, new[]{"", "", "", "", "Fey Illumination", "", "", "", "", ""}),
        new(637, "P3", "Thunder III (5th Set)", 0, new[]{"Reprisal", "", "Asylum", "", "Spreadlo + Sacred Soil", "Zoe Shields + Kerachole", "", "Feint (Chaos)", "", "Addle (Exdeath)"}),
        new(650, "P3", "Black Holes III (6th Tether Set)", 0, new[]{"", "", "", "", "", "Panhaima", "", "", "Party Mit", ""}),
        new(677, "P3", "Shocking Impact/Shockwave", 0, new[]{"", "", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil", "Kerachole", "", "", "", ""}),
        new(705, "P3", "Stomp-a-Mole", 0xBAF0, new[]{"Reprisal + Party Mit", "Reprisal", "", "", "Seraphism + Sacred Soil", "Kerachole", "Feint (Chaos)", "", "", ""}),
        new(745, "P4", "Kefka Returns (phase enter)", 0xC2DC, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(763, "P4", "Grand Cross", 0xBB14, new[]{"", "", "Plenary Indulgence + Asylum", "Collective Unconscious", "Spreadlo + Sacred Soil", "Kerachole + Philosophia + Holos", "", "Feint", "", ""}),
        new(769, "P4", "Inferno/Tsunami", 0, new[]{"", "", "", "", "", "", "", "", "Party Mit", ""}),
        new(778, "P4", "Grand Cross", 0xBB14, new[]{"", "", "Temperance", "Neutral Sect", "Expedient + Fey Illumination", "Panhaima", "", "", "", ""}),
        new(783, "P4", "Inferno/Tsunami", 0, new[]{"", "Party Mit (GNB/DRK)", "", "Sun Sign", "Seraph", "", "", "", "", ""}),
        new(793, "P4", "Grand Cross", 0xBB14, new[]{"", "Party Mit (WAR/PLD)", "Divine Caress", "", "Seraph", "Zoe Shields", "", "", "", ""}),
        new(805, "P4", "Flood of Naught", 0xC393, new[]{"", "", "Liturgy of the Bell", "Macrocosmos", "Sacred Soil", "Kerachole", "", "", "", ""}),
        new(815, "P4", "Death Bolt/Wave", 0, new[]{"Party Mit", "", "", "", "", "", "", "", "", ""}),
        new(833, "P4", "Ultima Upsurge", 0xC24A, new[]{"Reprisal", "", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil", "Kerachole", "Feint", "", "", "Addle"}),
        new(840, "P4", "Death Bolt/Wave", 0xBB1B, new[]{"", "", "Asylum", "", "", "", "", "", "", ""}),
        new(872, "P4", "Ultima Upsurge", 0, new[]{"", "Reprisal", "", "", "Sacred Soil", "Kerachole", "", "", "", ""}),
        new(911, "P5", "Ultima Repeater", 0xBB40, new[]{"Reprisal", "Party Mit", "Plenary Indulgence", "Collective Unconscious", "Spreadlo + Sacred Soil", "Zoe Shields + Holos + Kerachole", "", "", "Party Mit", ""}),
        new(916, "P5", "Fell Forces (3x)", 0xC654, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(928, "P5", "Chaotic Flood", 0xC13F, new[]{"", "", "Temperance", "Neutral Sect", "Expedient", "Panhaima", "", "", "", ""}),
        new(940, "P5", "Maddening Orchestra", 0xBB50, new[]{"", "Reprisal", "Divine Caress", "Sun Sign", "Sacred Soil + Fey Illumination", "Kerachole", "", "Feint", "", ""}),
        new(953, "P5", "Fell Forces (2x)", 0xC654, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(971, "P5", "Celestriad", 0xBB42, new[]{"Party Mit", "", "Asylum", "", "Seraph", "", "", "", "", ""}),
        new(993, "P5", "Ultima Repeater", 0, new[]{"Reprisal", "Party Mit", "Plenary Indulgence", "Collective Unconscious", "Sacred Soil", "Kerachole", "Feint", "", "Party Mit", "Addle"}),
        new(998, "P5", "Fell Forces (2x)", 0xC654, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(1024, "P5", "Stray Entropy", 0xBB3F, new[]{"", "Reprisal", "", "", "", "", "", "", "", ""}),
        new(1033, "P5", "Maddening Orchestra", 0xBB51, new[]{"", "", "", "", "Sacred Soil", "Kerachole", "", "", "", ""}),
        new(1045, "P5", "Fell Forces (3x)", 0, new[]{"", "", "Assist Tanks", "", "", "", "", "", "", ""}),
        new(1062, "P5", "Forsaken (1st Hit)", 0xBB35, new[]{"Reprisal + Party Mit", "", "Temperance + Asylum", "Neutral Sect", "Spreadlo + Fey Illumination + Sacred Soil", "Zoe Shields + Holos + Kerachole", "", "Feint", "", ""}),
        new(1067, "P5", "Forsaken Bonds (2nd Hit)", 0, new[]{"", "", "Liturgy of the Bell", "", "Seraphism", "Philosophia", "", "", "", ""}),
        new(1070, "P5", "Forsaken (3rd Hit)", 0xBB36, new[]{"", "", "", "Macrocosmos", "Expedient", "Panhaima", "", "", "", ""}),
        new(1076, "P5", "Forsaken Bonds (4th Hit)", 0, new[]{"", "", "", "", "", "", "", "", "", ""}),
        new(1079, "P5", "Forsaken (5th Hit)", 0xBB36, new[]{"", "Reprisal + Party Mit (GNB/DRK)", "Divine Caress + Plenary Indulgence", "Sun Sign + Collective Unconscious", "", "", "Feint", "", "Party Mit", "Addle"}),
        new(1084, "P5", "Forsaken Bonds (6th Hit)", 0, new[]{"", "Party Mit (WAR/PLD)", "", "", "", "", "", "", "", ""}),
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

    // The "Notes" footer from each phase tab of the sheet, shown at the bottom of
    // the Sheet View. Footnote superscripts are written as "1)" and glyphs the
    // game font can't render are spelled out; otherwise the text is the sheet's.
    // Short play guidance for key mechanics, shown under a board row when it
    // goes gold/green and the sheet itself carries no note there. Hard-won
    // timing knowledge, kept to one line each.
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
