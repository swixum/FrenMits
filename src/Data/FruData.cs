// AUTO-GENERATED: Futures Rewritten (Ultimate) mit sheet matched to
// Community futures_rewritten timeline (continuous times + ability ids).
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

public static class FruData
{
    public static readonly string[] Slots = { "T1", "T2", "SCH", "SGE", "WHM", "AST", "M1", "M2", "R", "Caster", "Extras" };

    public sealed record Entry(int Time, string Phase, string Mechanic, uint Sync, string[] Actions);

    public static readonly Entry[] Timeline =
    {
        new(15, "P1", "Cyclonic Break 2", 0x9CD1, new[]{"","","Concit/Soil/Exp","Zoe EukProg/Kera","","","","","","",""}),
        new(35, "P1", "Utopian Sky", 0x9CDA, new[]{"","","Concit","EukProg","Confession","","","","","",""}),
        new(86, "P1", "Burnished Glory 1", 0x9CEA, new[]{"Rep","Party Mit","(Early) Soil/Spread-Lo","(Early) Kera/Holos","Bell","Macro/CU","","Feint","","Addle","Barrier/Dismantle"}),
        new(86, "P1", "Burnished Glory 2", 0x9CEA, new[]{"Party Mit","(Late) Rep","Fey/Concit/Soil","EukProg/Kera","Temp/Confession/Caress","Sun","(Late) Feint","","Party Mit","",""}),
        new(101, "P1", "Fall of Faith (1/2)", 0x9CC9, new[]{"","","Fey/Seraph/ism","Panhaima/Sophia","Temp","Neutral","","","","",""}),
        new(101, "P1", "Fall of Faith (3/4)", 0x9CC9, new[]{"Party Mit","","Fey/Consolation/Soil","Kera","Temp","Neutral/Sun","","","Party Mit","",""}),
        new(246, "P2", "House of Light", 0x9D0E, new[]{"","Rep","Concit/Exp","Holos","","","","","","",""}),
        new(255, "P2", "Sinbound Holy", 0x9D10, new[]{"","","Spread-Lo/Exp**","Zoe EukProg/Holos","","","","","Party Mit**","","Barrier"}),
        new(283, "P2", "Hallowed Ray", 0x9D12, new[]{"Party Mit","Rep","Concit/Soil","EukProg/Kera","","","Feint","","","",""}),
        new(293, "P2", "Mirror Mirror", 0x9CF3, new[]{"","","Fey/Seraph","Panhaima","Confession/Temp","Neutral/Sun","","","","",""}),
        new(323, "P2", "Banish III", 0x9D1C, new[]{"Rep Short CD on M1","Short CD on M2","Consolation/Soil","Kera","Caress","Sun","","Feint","","Addle",""}),
        new(323, "P2", "Banish III", 0x9D1C, new[]{"","","Concit/Exp/(Early) Soil*","EukProg/(Early) Kera*","","","","","","",""}),
        new(333, "P2", "Light Rampant", 0x9D14, new[]{"Rep","","Concit/Soil","EukProg/Kera","","CU","","Feint","","Addle","Dismantle"}),
        new(350, "P2", "Powerful Light", 0x9D19, new[]{"","Party Mit","Spread-Lo/Seraphism","Zoe EukProg/Sophia","Bell","Macro","","","Party Mit","",""}),
        new(390, "P2", "Absolute Zero", 0x9D8D, new[]{"Rep/Party Mit","","Concit/Soil","EukProg/Kera","Confession","CU","Feint","","","","Barrier"}),
        new(500, "P2", "Junction (Transition)", 0x9D22, new[]{"","Party Mit","Concit/Soil","EukProg/Kera","","CU","","","","",""}),
        new(576, "P3", "Dark Water 1", 0x9D56, new[]{"","","Exped/Soil","Kera","","","","","","",""}),
        new(580, "P3", "Shell Crusher", 0x9D5E, new[]{"Rep","Party Mit","Soil","Kera","","CU","","Feint","","",""}),
        new(588, "P3", "Shockwave Pulsar", 0x9D5A, new[]{"Rep","","Concit/Soil","EukProg/Kera","","","","Feint","","","Dismantle"}),
        new(588, "P3", "Shockwave Pulsar", 0x9D5A, new[]{"Rep**/Party Mit","","Concit/Soil","EukProg/Kera","","","Feint**","","Party Mit**","Addle**",""}),
        new(638, "P3", "Dark Eruption", 0x9D52, new[]{"","Rep","Exped/Spread-Lo","EukProg","Confession","","","","","",""}),
        new(672, "P3", "Memory's End", 0x9D6C, new[]{"Rep","","Concit/Soil","(Early) Holos/Kera","","CU","Feint","","Party Mit","Addle",""}),
        new(1041, "P5", "Fulgent Blade 2", 0x9D72, new[]{"","Rep","Concit/Soil","EukProg/Kera","Confession","CU","Feint","","","",""}),
        new(1041, "P5", "Fulgent Blade 3", 0x9D72, new[]{"","Rep","Concit/Soil","EukProg/Kera","Temp/Confession","Neutral/CU","Feint","","","","Dismantle"}),
        new(1052, "P4", "The Path of Light", 0x9CB6, new[]{"","Rep","Soil","Kera","Temp/Caress/Confession","Neutral/Sun","","","","",""}),
        new(1068, "P4", "Akh Morn Afah", 0x9D76, new[]{"Rep","","Concit/Seraph/Soil*","EukProg/Panhaima/Kera*","","","Feint (Shiva)","","Party Mit","",""}),
        new(1068, "P4", "Akh Morn Afah", 0x9D76, new[]{"Rep","","Concit/Soil","EukProg/Kera","Confession/Temp/Caress","Neutral/Sun/CU","Feint (Shiva)","","Party Mit","","Dismantle/Barrier"}),
        new(1068, "P5", "Akh Morn 1", 0x9D76, new[]{"Party Mit","Rep","Spread-Lo/Soil","(Early) Holos*/EukProg/Kera","","","","Feint","","Addle","Dismantle/Barrier"}),
        new(1068, "P5", "Akh Morn 2", 0x9D76, new[]{"Rep/Party Mit","","Spread-Lo/Soil","Holos/EukProg/Kera","","","","Feint","","Addle",""}),
        new(1068, "P5", "Akh Morn 3", 0x9D76, new[]{"Rep/Party Mit","","Spread-Lo/Soil","Zoe EukProg/Kera","","","","Feint","","Addle",""}),
        new(1107, "P5", "Polarizing Strikes 1", 0x9D7C, new[]{"Rep","Party Mit","Seraph/Fey/Exped/Soil","Panhaima/Kera","Temp/Caress","Neutral/Sun","","","Party Mit","",""}),
        new(1107, "P5", "Polarizing Strikes 2", 0x9D7C, new[]{"","Party Mit","Seraph/ism/Exped/Fey","Panhaima/Sophia","Temp/Bell/Caress","Neutral/Sun/Macro","","","Party Mit","","Barrier"}),
        new(1142, "P5", "Pandora's Box", 0x9D86, new[]{"TANK LB","Rep**","Concit/Soil**","EukProg/Kera**","","","Feint**","","","",""}),
    };

    public static List<MitLine> BuildLines(string slot)
    {
        var idx = Array.IndexOf(Slots, slot);
        var list = new List<MitLine>();
        if (idx < 0) return list;
        foreach (var e in Timeline)
        {
            var action = e.Actions[idx];
            if (string.IsNullOrWhiteSpace(action)) continue;
            list.Add(new MitLine { Time = e.Time, Mechanic = e.Mechanic, Action = action, Enabled = true });
        }
        return list;
    }

    public static List<SyncPoint> SyncPoints()
    {
        var points = new List<SyncPoint>();
        var phaseSeen = new HashSet<string>();
        foreach (var e in Timeline.Where(e => e.Sync != 0).OrderBy(e => e.Time))
        {
            var isPhase = phaseSeen.Add(e.Phase);
            points.Add(new SyncPoint { Ability = e.Sync, Time = e.Time, IsPhase = isPhase, Label = $"{e.Phase} {e.Mechanic}" });
        }
        return points;
    }

    // Phase bosses resolved by name (phase times). Unresolved names are
    // skipped; capture them from a pull on the Timer tab.
    public static List<BossAnchor> BossAnchors()
    {
        var list = new List<BossAnchor>();
        BossNames.Add(list, "Fatebreaker", 0f, "P1 Fatebreaker");
        BossNames.Add(list, "Usurper of Frost", 215.3f, "P2 Shiva");
        BossNames.Add(list, "Oracle of Darkness", 500.0f, "P3 Gaia");
        BossNames.Add(list, "Pandora", 1041.0f, "P5 Pandora");
        return list;
    }
}
