// AUTO-GENERATED from the M12S (Lindwurm) mitigation sheet, with Phase 2 shifted
// by Phase2Offset onto the single continuous clock. Resync anchors from the
// cactbot r12s timeline (SyncPoints below) snap the clock on Lindwurm's casts.
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

public static class M12sData
{
    public static readonly string[] Slots = { "MT", "OT", "WHM", "AST", "SCH", "SGE", "D1", "D2", "D3", "D4" };

    // Seconds from pull that Phase 2's 0:00 lands on. Already baked into every
    // P2 row and anchor time below, so never tune it alone - the rows and the
    // runtime offset must move together (regenerate the file instead).
    public const int Phase2Offset = 420;

    // Hurt: 0 unknown, 1 light, 2 hurts, 3 deadly. Buster: lands on a tank rather
    // than the party. Measured - see the note above CustomRows.
    public sealed record Entry(int Time, string Phase, string Mechanic, uint Sync, string[] Actions,
        int Hurt = 0, bool Buster = false);

    public static readonly Entry[] Timeline =
    {
        // ---- Phase 1 ----
        new(16, "P1", "The Fixer", 0, new[]{"Reprisal","Party Mit","Plenary Indulgence","Collective Unconscious","Sacred Soil + Spreadlo","Zoe Shields + Kerachole","Feint","","Party Mit","Addle"}),
        new(44, "P1", "Mortal Slayer", 0, new[]{"","","","","Sacred Soil + Fey Illumination","Kerachole","","","",""}),
        new(88, "P1", "Ravenous Reach", 0, new[]{"Party Mit","Reprisal","Temperance + Divine Caress","Neutral Sect + Sun Sign","Expedient + Seraph + Seraphism","Holos + Panhaima + Philosophia","","Feint","",""}, 3),
        new(97, "P1", "Fourth-wall Fusion", 0, new[]{"Reprisal","Party Mit (DRK/GNB)","Liturgy of the Bell","Macrocosmos","Sacred Soil + Spreadlo (The Fixer)","Zoe Shields (The Fixer) + Kerachole","Feint","","Party Mit","Addle"}, 3),
        new(108, "P1", "The Fixer", 0, new[]{"","Party Mit (WAR/PLD)","Plenary Indulgence","Collective Unconscious","","","","","",""}, 3),
        new(150, "P1", "Dramatic Lysis", 0, new[]{"","","","","","","","","",""}, 1),
        new(153, "P1", "Roiling Mass", 0, new[]{"","","","","","","","","",""}, 1),
        new(154, "P1", "Roiling Mass", 0, new[]{"","","","","","","","","",""}, 1),
        new(188, "P1", "Splattershed", 0, new[]{"","Reprisal","","","Sacred Soil","Kerachole","","Feint","",""}, 2),
        new(219, "P1", "Dramatic Lysis", 0, new[]{"","","","","","","","","",""}, 1),
        new(219, "P1", "Metamitosis", 0, new[]{"","","","","","","","","",""}, 1),
        new(231, "P1", "Venomous Scourge", 0, new[]{"Reprisal","Party Mit (DRK/GNB)","","","Sacred Soil + Fey Illumination+ Spreadlo (The Fixer)","Panhaima + Kerachole + Zoe Shields (The Fixer)","Feint","","Party Mit","Addle"}, 3),
        new(241, "P1", "The Fixer", 0, new[]{"","Party Mit (WAR/PLD)","Plenary Indulgence","Collective Unconscious","","","","","",""}, 3),
        new(268, "P1", "Ravenous Reach", 0, new[]{"Party Mit","Reprisal","Temperance + Divine Caress","Neutral Sect + Sun Sign","Expedient + Seraph","Holos","","","",""}, 3),
        new(290, "P1", "Splattershed", 0, new[]{"Reprisal","","","","Sacred Soil","Kerachole","","Feint","",""}, 2),
        new(315, "P1", "Mortal Slayer", 0, new[]{"","","","","Succor","Eukrasian Prognosis","","","",""}),
        new(342, "P1", "Slaughtershed I", 0, new[]{"","Reprisal","Plenary Indulgence","Collective Unconscious","Sacred Soil + Fey Illumination + Seraphism","Kerachole + Philosophia","Feint","","","Addle"}, 2),
        new(350, "P1", "Dramatic Lysis", 0, new[]{"","","","","","","","","",""}, 2),
        new(350, "P1", "Fourth-Wall Fusion", 0, new[]{"","","","","","","","","",""}, 2),
        new(371, "P1", "Slaughtershed II", 0, new[]{"Reprisal","Party Mit","Liturgy of the Bell","Macrocosmos","Sacred Soil + Spreadlo","Kerachole + Panhaima + Zoe Shields","","","Party Mit",""}, 2),
        new(400, "P1", "Slaughtershed III", 0, new[]{"Party Mit","Reprisal","Plenary Indulgence + Temperance + Divine Caress","Collective Unconscious + Neutral Sect + Sun Sign","Sacred Soil + Expedient + Seraph","Kerachole + Holos","","Feint","",""}),
        // ---- Phase 2 (+Phase2Offset) ----
        new(437, "P2", "Arcadia Aflame", 0, new[]{"Reprisal","Party Mit","Plenary Indulgence","Collective Unconscious","Spreadlo + Sacred Soil","Holos + Kerachole","Feint","","Party Mit","Addle"}, 3),
        new(460, "P2", "Mighty Magic / Top-tier Slam I", 0, new[]{"","","Temperance","Neutral Sect","Seraph + Expedient","Panhaima + Zoe Shields","","","",""}, 2),
        new(481, "P2", "Mighty Magic / Top-tier Slam II", 0, new[]{"Party Mit","Reprisal","Divine Caress","Sun Sign","Fey Illumination + Seraph + Sacred Soil","Kerachole","","Feint","",""}, 2),
        new(498, "P2", "Esoteric Finisher", 0, new[]{"","","","","","","","","",""}, 3, true),
        new(548, "P2", "Firefall Splash", 0, new[]{"Reprisal","Party Mit","Liturgy of the Bell","Macrocosmos","Seraphism + Sacred Soil + Spreadlo","Philosophia + Holos + Kerachole","Feint","","Party Mit","Addle"}, 1),
        new(556, "P2", "Heavy Slam", 0, new[]{"","","","","","","","","",""}, 2),
        new(581, "P2", "Reenactment", 0, new[]{"Party Mit","Reprisal","Plenary Indulgence + Temperance + Divine Caress","Collective Unconscious + Neutral Sect + Sun Sign","Expedient + Seraph + Sacred Soil","Zoe Shields + Panhaima + Kerachole","","Feint","",""}, 2),
        new(622, "P2", "Blood Mana", 0, new[]{"","","","","Sacred Soil","Kerachole","","","",""}, 2),
        new(650, "P2", "Netherworld Near/Far", 0, new[]{"Reprisal","Party Mit (DRK/GNB)","Plenary Indulgence","Collective Unconscious","Sacred Soil","Kerachole","Feint","","Party Mit","Addle"}, 2),
        new(658, "P2", "Arcadia Aflame", 0, new[]{"","Party Mit (WAR/PLD)","","","","","","","",""}, 3),
        new(673, "P2", "Esoteric Finisher", 0, new[]{"","","","","","","","","",""}, 3, true),
        new(690, "P2", "Idyllic Dream", 0, new[]{"Party Mit","Reprisal","","","Fey Illumination (Use Early) + Spreadlo + Sacred Soil","Holos (Use Early) + Zoe Shields + Kerachole","","Feint","",""}, 3),
        new(765, "P2", "Lindwurm's Meteor", 0, new[]{"Reprisal","Party Mit","Plenary Indulgence","Collective Unconscious","Sacred Soil","Kerachole","Feint","","Party Mit","Addle"}, 3),
        new(777, "P2", "Arcadian Arcanum", 0, new[]{"","","","","","","","","",""}, 2),
        new(792, "P2", "Twisted Vision", 0, new[]{"Party Mit","","Everything","","","","Use Personals!","","",""}),
        new(819, "P2", "Cosmic Kiss", 0, new[]{"","","","","","","","","",""}, 1),
        new(819, "P2", "Lindwurm's Dark II", 0, new[]{"","","","","","","","","",""}, 1, true),
        new(829, "P2", "Lindwurm's Glare", 0, new[]{"","","","","","","","","",""}, 1),
        new(829, "P2", "Lindwurm's Thunder II", 0, new[]{"","","","","","","","","",""}, 1),
        new(860, "P2", "Reenactment + Twisted Vision", 0, new[]{"Reprisal","Party Mit","","","Sacred Soil","Kerachole","","Feint","Party Mit",""}),
        new(893, "P2", "Idyllic Dream", 0, new[]{"Party Mit","Reprisal","Plenary Indulgence","Collective Unconscious","Sacred Soil","Kerachole","Feint","","","Addle"}, 3),
        new(907, "P2", "Esoteric Finisher", 0, new[]{"","","","","","","","","",""}, 3, true),
        new(936, "P2", "Arcadian Hell I", 0, new[]{"Reprisal","","Temperance","Neutral Sect","Expedient + Seraph + Fey Illumination + Sacred Soil","Holos","","","",""}),
        new(952, "P2", "Arcadian Hell II", 0, new[]{"","Reprisal + Party Mit","Plenary Indulgence + Divine Caress","Collective Unconscious + Sun Sign","Spreadlo + Seraph + Sacred Soil","Zoe Shields + Panhaima + Kerachole","","Feint","Party Mit",""}),
    };

    // Severity and tank-buster flags, and every mechanic the sheet never listed,
    // measured from six kills of each half and checked against six more.
    //
    // The logs file this fight as two encounters, Lindwurm and Lindwurm II, each
    // timed from its own zero, so each half was read against its own half of the
    // anchor table above - which is also what puts the second one back on this
    // file's single clock, Phase2Offset and all.
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

    // Resync anchors from the cactbot r12s timeline (Lindwurm casts), with Phase 2
    // times shifted by Phase2Offset so the clock snaps to resolve on time.
    public static List<SyncPoint> SyncPoints() => new()
    {
        // ---- Phase 1 ----
        new() { Ability = 0xB4D7, Time = 15.6f,  IsPhase = true,  Label = "P1 The Fixer" },
        new() { Ability = 0xB49D, Time = 87.7f,  Label = "P1 Ravenous Reach" },
        new() { Ability = 0xB4D7, Time = 107.3f, IsPhase = false, Label = "P1 The Fixer" },
        new() { Ability = 0xB4C2, Time = 176.7f, IsPhase = false, Label = "P1 Constrictor" },
        new() { Ability = 0xB9C6, Time = 188.2f, IsPhase = false, Label = "P1 Splattershed" },
        new() { Ability = 0xB4A8, Time = 230.4f, IsPhase = false, Label = "P1 Venomous Scourge" },
        new() { Ability = 0xB4D7, Time = 239.6f, IsPhase = false, Label = "P1 The Fixer" },
        new() { Ability = 0xB49D, Time = 266.5f, IsPhase = false, Label = "P1 Ravenous Reach" },
        new() { Ability = 0xB9C6, Time = 288.5f, IsPhase = false, Label = "P1 Splattershed" },
        new() { Ability = 0xADC9, Time = 340.9f, IsPhase = false, Label = "P1 Slaughtershed" },

        // ---- Phase 2 (cactbot time - 3000 + Phase2Offset) ----
        new() { Ability = 0xB528, Time = 435.7f, IsPhase = true,  Label = "P2 Arcadia Aflame" },
        new() { Ability = 0xB527, Time = 465.3f, IsPhase = false, Label = "P2 Snaking Kick" },
        new() { Ability = 0xB4E4, Time = 547.8f, IsPhase = false, Label = "P2 Firefall Splash" },
        new() { Ability = 0xB4EC, Time = 571.3f, IsPhase = false, Label = "P2 Reenactment" },
        new() { Ability = 0xB4FB, Time = 610.0f, IsPhase = false, Label = "P2 Blood Mana" },
        new() { Ability = 0xB528, Time = 655.8f, IsPhase = false, Label = "P2 Arcadia Aflame" },
        new() { Ability = 0xB509, Time = 688.8f, IsPhase = false, Label = "P2 Idyllic Dream" },
        new() { Ability = 0xB4F2, Time = 762.8f, IsPhase = false, Label = "P2 Lindwurm's Meteor" },
        // Fine drift only. It used to re-base the phase, to close the ~93s gap above,
        // but Reenactment is already anchored at 9:31 and a phase anchor carries a
        // 2000s forward window: any Reenactment the clock met while more than the
        // 8s mechanic window out would have snapped forward to here instead, four
        // and three quarter minutes on. That is how UCOB and UWU were losing their
        // boards for whole phases. P2 still re-bases, on Arcadia Aflame at 7:16.
        new() { Ability = 0xB4EC, Time = 855.7f, IsPhase = false, Label = "P2 Reenactment" },
    };

    public static List<BossAnchor> BossAnchors() => new();

    // First time each phase appears. P2's rows already carry Phase2Offset, so
    // these land on the same clock the board measures its rows against.
    public static List<(string Name, float Time)> PhaseStarts()
        => Timeline.GroupBy(e => e.Phase)
                   .Select(g => (g.Key, (float)g.Min(e => e.Time)))
                   .OrderBy(p => p.Item2)
                   .ToList();
}
