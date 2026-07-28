// Futures Rewritten (Ultimate). Times and ability ids come from the community
// futures_rewritten timeline; severity and buster flags are measured from kills;
// every call is the official FMBG mit sheet's five phase tabs, placed on the
// mechanic each one actually names rather than matched by proximity.
//
// The sheet keeps its own clock per tab, running behind this one by 0s in P1,
// 46s in P2, 513s in P3 and P4 and 619s in P5. Every row below was checked
// against that, which is what caught the calls the old pass had landed on the
// wrong Cyclonic Break, the wrong Burnished Glory, the wrong Dark Water and a
// Path of Light three hundred seconds from the one the sheet meant.
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

public static class FruData
{
    public static readonly string[] Slots = { "T1", "T2", "SCH", "SGE", "WHM", "AST", "M1", "M2", "R", "Caster" };

    // Hurt: 0 unknown, 1 light, 2 hurts, 3 deadly. Buster: lands on a tank rather
    // than the party. Both measured - see the note above CustomRows.
    public sealed record Entry(int Time, string Phase, string Mechanic, uint Sync, string[] Actions,
        int Hurt = 0, bool Buster = false);

    public static readonly Entry[] Timeline =
    {
        new(15, "P1", "Cyclonic Break 1", 0x9CD1, new[]{"Rep/Party Mit","Party Mit*","Spread-Lo","EukProg/Kera","","CU","Feint","","Party Mit",""}, 1),
        new(24, "P1", "Powder Mark Trail", 0x9CE8, new[]{"","","","","","","","","",""}, 3, true),
        new(35, "P1", "Utopian Sky", 0x9CDA, new[]{"","","Concit","EukProg","Confession","","","","",""}),
        new(40, "P1", "Burn Mark", 0x9CE9, new[]{"","","","","","","","","",""}, 2, true),
        new(56, "P1", "Cyclonic Break 2", 0x9CD1, new[]{"","","Concit/Soil/Exp","Zoe EukProg/Kera","","","","","",""}, 2),
        new(78, "P1", "Sinsmoke", 0x9CE7, new[]{"","","","","","","","","",""}, 2),
        new(86, "P1", "Burnished Glory 1", 0x9CEA, new[]{"Rep","Party Mit","(Early) Soil/Spread-Lo","(Early) Kera/Holos","Bell","Macro/CU","","Feint","","Addle"}, 2),
        new(106, "P1", "Fall of Faith (1/2)", 0x9CDC, new[]{"","","Fey/Seraph/ism","Panhaima/Sophia","Temp","Neutral","","","",""}, 2),
        new(111, "P1", "Fall of Faith (3/4)", 0, new[]{"Party Mit","","Fey/Consolation/Soil","Kera","Temp","Neutral/Sun","","","Party Mit",""}, 2),
        new(121, "P1", "Burnished Glory 2", 0x9CEA, new[]{"Party Mit","(Late) Rep","Fey/Concit/Soil","EukProg/Kera","Temp/Confession/Caress","Sun","(Late) Feint","","Party Mit",""}, 2),
        new(130, "P1", "Powder Mark Trail", 0x9CE8, new[]{"","","","","","","","","",""}, 3, true),
        // Usurper's opening pair. Ungraded on purpose: the logs record no unmitigated
        // figure for either, because a tank is invulning or eating them behind a full
        // kit every time. They land on one person, which is what the shield says, and
        // they are the pair every tank tab plans for.
        new(215, "P2", "Quadruple Slap", 0x9CFF, new[]{"","","","","","","","","",""}, 0, true),
        new(219, "P2", "Quadruple Slap", 0x9D00, new[]{"","","","","","","","","",""}, 0, true),
        new(236, "P2", "Diamond Dust", 0x9D05, new[]{"Rep","Party Mit","Concit/Soil","EukProg/Kera","Confession","CU","","Feint*","","Addle*"}, 3),
        new(246, "P2", "Frigid Stone", 0x9D07, new[]{"","","","","","","","","",""}, 2),
        new(246, "P2", "The House of Light", 0x9D0E, new[]{"","","","","","","","","",""}, 2),
        // 0x9D10 is a Sinbound Holy the boss never casts; 0x9D11 is the one logs
        // records, in every kill.
        new(255, "P2", "Sinbound Holy", 0x9D11, new[]{"","","Spread-Lo/Exp**","Zoe EukProg/Holos","","","","","Party Mit**",""}, 2),
        new(283, "P2", "Hallowed Ray", 0x9D12, new[]{"Party Mit","Rep","Concit/Soil","EukProg/Kera","","","Feint","","",""}, 3),
        new(293, "P2", "Mirror Mirror", 0x9CF3, new[]{"","","Fey/Seraph","Panhaima","Confession/Temp","Neutral/Sun","","","",""}),
        new(307, "P2", "The House of Light", 0x9D0E, new[]{"","","","","","","","","",""}, 2),
        new(317, "P2", "The House of Light", 0x9D0E, new[]{"","","","","","","","","",""}, 2),
        new(323, "P2", "Banish III", 0x9D1C, new[]{"Rep Short CD on M1","Short CD on M2","Consolation/Soil","Kera","Caress","Sun","","Feint","","Addle"}),
        new(333, "P2", "Light Rampant", 0x9D14, new[]{"Rep","","Concit/Soil","EukProg/Kera","","CU","","Feint","","Addle"}, 3),
        new(340, "P2", "Luminous Hammer", 0x9D1A, new[]{"","","","","","","","","",""}, 1, true),
        new(343, "P2", "Bright Hunger", 0x9D15, new[]{"","","","","","","","","",""}, 1),
        new(350, "P2", "Powerful Light", 0x9D19, new[]{"","Party Mit","Spread-Lo/Seraphism","Zoe EukProg/Sophia","Bell","Macro","","","Party Mit",""}, 2),
        new(358, "P2", "Bright Hunger", 0x9D15, new[]{"","","","","","","","","",""}, 1),
        new(362, "P2", "Banish III", 0x9D1C, new[]{"","","Concit/Exp/(Early) Soil*","EukProg/(Early) Kera*","","","","","",""}),
        new(370, "P2", "The House of Light", 0x9CFC, new[]{"","Rep","Concit/Exp","Holos","","","","","",""}, 2),
        new(390, "P2", "Absolute Zero", 0x9D8D, new[]{"Rep/Party Mit","","Concit/Soil","EukProg/Kera","Confession","CU","Feint","","",""}, 3),
        new(431, "P2", "Hiemal Ray", 0x9D41, new[]{"","","","","","","","","",""}, 1, true),
        new(500, "P2", "Junction (Transition)", 0x9D22, new[]{"","Party Mit","Concit/Soil","EukProg/Kera","","CU","","","",""}, 3),
        new(532, "P3", "Ultimate Relativity", 0x9D4A, new[]{"Rep*/Party Mit","","Concit/Soil*","EukProg/Kera*","","","Feint","","","Addle"}, 3),
        new(544, "P3", "Dark Fire III", 0x9D54, new[]{"","","","","","","","","",""}, 1),
        new(544, "P3", "Fire/Dark Set 1/2", 0, new[]{"","","Spread-Lo/Seraph/Soil","EukProg/Holos/Panhaima/Kera","Temp/Confession","Neutral","","","Party Mit",""}),
        new(544, "P3", "Unholy Darkness", 0x9D55, new[]{"","","","","","","","","",""}, 2),
        new(549, "P3", "Sinbound Meltdown", 0x9D2B, new[]{"","","","","","","","","",""}, 1),
        new(554, "P3", "Dark Fire III", 0x9D54, new[]{"","","","","","","","","",""}, 1),
        new(554, "P3", "Unholy Darkness", 0x9D55, new[]{"","","","","","","","","",""}, 2),
        // Unanchored on purpose. 0x9D64 is the version of Sinbound Meltdown that runs
        // as a sixteen-second channel rather than a single cast, so anchoring it held
        // the clock at 559 for that whole channel and the next fourteen anchors -
        // every Dark Fire, Dark Water, Shockwave Pulsar and Memory's End in the phase
        // - then sat outside their windows and never fired. The two casts either side
        // of it (0x9D2B at 549 and 570) carry the resync for this stretch.
        new(559, "P3", "Sinbound Meltdown", 0, new[]{"","","","","","","","","",""}, 1),
        new(564, "P3", "Dark Fire III", 0x9D54, new[]{"","","","","","","","","",""}, 1),
        new(564, "P3", "Set 3 and Rewind", 0, new[]{"","Rep","Concit/Seraphism","EukProg/Philosophia","Caress/Bell","Macro/Sun","","","",""}),
        new(564, "P3", "Unholy Darkness", 0x9D55, new[]{"","","","","","","","","",""}, 2),
        new(570, "P3", "Sinbound Meltdown", 0x9D2B, new[]{"","","","","","","","","",""}, 1),
        new(576, "P3", "Dark Eruption", 0x9D52, new[]{"","","","","","","","","",""}, 1),
        new(576, "P3", "Dark Water III", 0x9D4F, new[]{"","","","","","","","","",""}, 1),
        new(580, "P3", "Shell Crusher", 0x9D5E, new[]{"Rep","Party Mit","Soil","Kera","","CU","","Feint","",""}, 2),
        new(588, "P3", "Shockwave Pulsar", 0x9D5A, new[]{"Rep","","Concit/Soil","EukProg/Kera","","","","Feint","",""}, 3),
        new(597, "P3", "Black Halo", 0x9D62, new[]{"","","","","","","","","",""}, 3, true),
        new(625, "P3", "Dark Water 1", 0x9D4F, new[]{"","","Exped/Soil","Kera","","","","","",""}, 1),
        new(627, "P3", "Spirit Taker", 0x9D60, new[]{"","","","","","","","","",""}, 1, true),
        new(638, "P3", "Dark Eruption", 0x9D52, new[]{"","Rep","Exped/Spread-Lo","EukProg","Confession","","","","",""}, 1),
        new(644, "P3", "Dark Water 2", 0x9D4F, new[]{"","","","","","","","","",""}, 1),
        new(646, "P3", "Darkest Dance", 0x9CF6, new[]{"","","","","","","","","",""}, 3),
        new(653, "P3", "Dark Water 3", 0x9D4F, new[]{"","","","","","","","","",""}, 1),
        new(658, "P3", "Shockwave Pulsar", 0x9D5A, new[]{"Rep**/Party Mit","","Concit/Soil","EukProg/Kera","","","Feint**","","Party Mit**","Addle**"}, 3),
        new(672, "P3", "Memory's End", 0x9D6C, new[]{"Rep","","Concit/Soil","(Early) Holos/Kera","","CU","Feint","","Party Mit","Addle"}, 3),
        new(705, "P4", "Edge of Oblivion", 0x9CEE, new[]{"","","","","","","","","",""}, 1),
        new(715, "P4", "Darklit Dragonsong", 0x9D6D, new[]{"","Rep/Party Mit","Recit Concit/Soil","Zoe EukProg/Kera","Temp","Neutral","","","",""}, 3),
        new(726, "P4", "Bright Hunger", 0x9D15, new[]{"","","","","","","","","",""}, 1),
        new(726, "P4", "The Path of Light", 0x9CFE, new[]{"","Rep","Soil","Kera","Temp/Caress/Confession","Neutral/Sun","","","",""}, 2),
        new(729, "P4", "Spirit Taker", 0x9D60, new[]{"","","","","","","","","",""}, 1, true),
        new(733, "P4", "Dark Water III", 0x9D4F, new[]{"","","","","","","","","",""}, 1),
        new(738, "P4", "Somber Dance", 0x9D5C, new[]{"","","","","","","","","",""}, 3, true),
        new(744, "P4", "Edge of Oblivion", 0x9CEE, new[]{"","","","","","","","","",""}, 1),
        new(751, "P4", "Akh Morn", 0x9D6F, new[]{"Rep","","Concit/Seraph/Soil*","EukProg/Panhaima/Kera*","","","Feint (Shiva)","","Party Mit",""}, 2),
        new(761, "P4", "Morn Afah", 0x9D3A, new[]{"","","","","","","","","",""}, 3),
        new(776, "P4", "Crystallize Time", 0x9D6A, new[]{"Party Mit","Rep","Concit/Soil","EukProg/Kera","Confession","CU","","Feint (Gaia)","","Addle (Gaia)"}, 3),
        new(782, "P4", "Edge of Oblivion", 0x9CEE, new[]{"","","","","","","","","",""}, 1),
        new(789, "P4", "Dark Water III", 0x9D4F, new[]{"","","","","","","","","",""}, 1),
        new(790, "P4", "Longing of the Lost", 0x9D31, new[]{"","","","","","","","","",""}, 2),
        new(791, "P4", "Crystallize Mech", 0, new[]{"","","Spread-Lo/Exped/Seraphism","EukProg/Holos/Sophia","Bell**","Macro**","","","",""}),
        new(791, "P4", "Dark Aero III", 0x9D58, new[]{"","","","","","","","","",""}, 1),
        new(791, "P4", "Dark Eruption", 0x9D52, new[]{"","","","","","","","","",""}, 1),
        new(794, "P4", "Unholy Darkness", 0x9D55, new[]{"","","","","","","","","",""}, 2),
        new(808, "P4", "Quietus", 0x9D59, new[]{"","","","","","","","","",""}, 1),
        new(814, "P4", "Spirit Taker", 0x9D60, new[]{"","","","","","","","","",""}, 1, true),
        new(820, "P4", "Hallowed Wings", 0x9D8C, new[]{"","Party Mit","Concit/Soil***","EukProg/Kera***","","","","","Party Mit",""}, 3),
        new(834, "P4", "Akh Morn", 0x9D6E, new[]{"Rep","","Concit/Soil","EukProg/Kera","Confession/Temp/Caress","Neutral/Sun/CU","Feint (Shiva)","","Party Mit",""}, 2),
        new(841, "P4", "Edge of Oblivion", 0x9CEE, new[]{"","","","","","","","","",""}, 1),
        new(844, "P4", "Morn Afah", 0x9D70, new[]{"","","","","","","","","",""}, 3),
        new(1041, "P5", "Fulgent Blade 1", 0x9D72, new[]{"Rep","","Concit/Soil","EukProg/Kera","Confession","CU","Feint","","",""}, 3),
        new(1068, "P5", "Akh Morn 1", 0x9D76, new[]{"Party Mit","Rep","Spread-Lo/Soil","(Early) Holos*/EukProg/Kera","","","","Feint","","Addle"}, 3),
        new(1086, "P5", "Explosion", 0x9D80, new[]{"","","","","","","","","",""}, 1),
        new(1086, "P5", "Wings Dark and Light", 0x9D29, new[]{"","","","","","","","","",""}, 2, true),
        new(1107, "P5", "Polarizing Strikes 1", 0x9D7C, new[]{"Rep","Party Mit","Seraph/Fey/Exped/Soil","Panhaima/Kera","Temp/Caress","Neutral/Sun","","","Party Mit",""}),
        new(1108, "P5", "Cruel Path of Darkness", 0x9D7E, new[]{"","","","","","","","","",""}, 2),
        new(1108, "P5", "Cruel Path of Light", 0x9D7D, new[]{"","","","","","","","","",""}, 2),
        new(1142, "P5", "Pandora's Box", 0x9D86, new[]{"TANK LB","Rep**","Concit/Soil**","EukProg/Kera**","","","Feint**","","",""}, 3),
        new(1154, "P5", "Fulgent Blade 2", 0x9D72, new[]{"","Rep","Concit/Soil","EukProg/Kera","Confession","CU","Feint","","",""}, 3),
        new(1181, "P5", "Akh Morn 2", 0x9D76, new[]{"Rep/Party Mit","","Spread-Lo/Soil","Holos/EukProg/Kera","","","","Feint","","Addle"}, 3),
        new(1203, "P5", "Explosion", 0x9D80, new[]{"","","","","","","","","",""}, 1),
        new(1204, "P5", "Wings Dark and Light", 0x9D79, new[]{"","","","","","","","","",""}, 2, true),
        // Unanchored: Cruel Path of Light and Cruel Path of Darkness land in the same
        // instant, so the pair only needs one anchor, and keeping both had this one
        // snapping the clock backward onto its own second volley.
        new(1219, "P5", "Cruel Path of Darkness", 0, new[]{"","","","","","","","","",""}, 2),
        new(1219, "P5", "Cruel Path of Light", 0x9D7D, new[]{"","","","","","","","","",""}, 2),
        new(1220, "P5", "Polarizing Strikes 2", 0x9D7C, new[]{"","Party Mit","Seraph/ism/Exped/Fey","Panhaima/Sophia","Temp/Bell/Caress","Neutral/Sun/Macro","","","Party Mit",""}),
        new(1244, "P5", "Fulgent Blade 3", 0x9D72, new[]{"","Rep","Concit/Soil","EukProg/Kera","Temp/Confession","Neutral/CU","Feint","","",""}, 3),
        new(1272, "P5", "Akh Morn 3", 0x9D76, new[]{"Rep/Party Mit","","Spread-Lo/Soil","Zoe EukProg/Kera","","","","Feint","","Addle"}, 3),
    };

    // Severity and tank-buster flags, measured from six kills: every damaging cast
    // paired with what it actually lands for, unmitigated.
    //
    // Graded against the 90th percentile of the fight's raidwides rather than the
    // single hardest one. FRU needs that difference - Pandora's Box lands for four
    // times any other raidwide, so the hardest-hit yardstick called all sixty of the
    // real ones "light". Somber Dance's second hit is worse still at nearly nine
    // million, which is the punish for failing it rather than a hit anyone mitigates.
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

    public static List<SyncPoint> SyncPoints()
    {
        return SyncAnchors.Build(
            Timeline.Select(e => (e.Sync, (float)e.Time, e.Phase, e.Mechanic)));
    }

    // Phase bosses resolved by name (phase times); unresolved names are skipped.
    public static List<BossAnchor> BossAnchors()
    {
        var list = new List<BossAnchor>();
        // These re-base the clock when each boss APPEARS, sitting before that
        // phase's first call so they correct the clock without firing a call
        // early (Pandora was dropped because it would have fired P5's first call
        // the instant it spawned).
        BossNames.Add(list, "Fatebreaker", 0f, "P1 Fatebreaker");
        BossNames.Add(list, "Usurper of Frost", 215.3f, "P2 Shiva");
        BossNames.Add(list, "Oracle of Darkness", 500.0f, "P3 Gaia");
        return list;
    }

    // Phase starts on this file's own clock, read off the same community timeline
    // the rows above are matched to. P2 and P3 are the anchors' times exactly,
    // which is what confirms the two share a clock.
    //
    // Written out rather than folded up from the rows' Phase tags, the way every
    // other fight's is, because P4 and P5 both start during downtime - the duo
    // materialises well before its first mechanic, and Pandora spends nearly two
    // minutes in a cutscene - so the first tagged row in each sits far later than
    // the phase itself does.
    public static List<(string Name, float Time)> PhaseStarts() => new()
    {
        ("P1", 0f),
        ("P2", 215.3f),
        ("P3", 500.0f),
        ("P4", 680.8f),
        ("P5", 1041.0f),
    };
}
