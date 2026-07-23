// AUTO-GENERATED from the M12S (Lindwurm) mitigation sheet, with Phase 2 shifted
// by Phase2Offset onto the single continuous clock and no resync ability ids yet,
// so the clock free-runs (tune Phase2Offset or use /fm sync if Phase 2 drifts).
using System;
using System.Collections.Generic;

namespace FrenMits;

public static class M12sData
{
    public static readonly string[] Slots = { "MT", "OT", "WHM", "AST", "SCH", "SGE", "D1", "D2", "D3", "D4" };

    // Seconds from pull that Phase 2's 0:00 lands on (estimate, adjust to taste).
    public const int Phase2Offset = 420;

    public sealed record Entry(int Time, string Phase, string Mechanic, uint Sync, string[] Actions);

    public static readonly Entry[] Timeline =
    {
        // ---- Phase 1: Lindwurm ----
        new(16, "P1", "The Fixer", 0, new[]{"Reprisal","Party Mit","Plenary Indulgence","Collective Unconsious","Sacred Soil + Spreadlo","Zoe Shields + Kerachole","Feint","","Party Mit","Addle"}),
        new(44, "P1", "Mortal Slayer", 0, new[]{"","","","","Sacred Soil + Fey Illumination","Kerachole","","","",""}),
        new(88, "P1", "Ravenous Reach", 0, new[]{"Party Mit","Reprisal","Temperance + Divine Caress","Neutral Sect + Sun Sign","Expedient + Seraph + Seraphism","Holos + Panhaima + Philosophia","","Feint","",""}),
        new(97, "P1", "Fourth-wall Fusion", 0, new[]{"Reprisal","Party Mit (DRK/GNB)","Liturgy of the Bell","Macrocosmos","Sacred Soil + Spreadlo (The Fixer)","Zoe Shields (The Fixer) + Kerachole","Feint","","Party Mit","Addle"}),
        new(108, "P1", "The Fixer", 0, new[]{"","Party Mit (WAR/PLD)","Plenary Indulgence","Collective Unconsious","","","","","",""}),
        new(188, "P1", "Splattershed", 0, new[]{"","Reprisal","","","Sacred Soil","Kerachole","","Feint","",""}),
        new(231, "P1", "Venomous Scourge", 0, new[]{"Reprisal","Party Mit (DRK/GNB)","","","Sacred Soil + Fey Illumination+ Spreadlo (The Fixer)","Panhaima + Kerachole + Zoe Shields (The Fixer)","Feint","","Party Mit","Addle"}),
        new(241, "P1", "The Fixer", 0, new[]{"","Party Mit (WAR/PLD)","Plenary Indulgence","Collective Unconsious","","","","","",""}),
        new(268, "P1", "Ravenous Reach", 0, new[]{"Party Mit","Reprisal","Temperance + Divine Caress","Neutral Sect + Sun Sign","Expedient + Seraph","Holos","","","",""}),
        new(290, "P1", "Splattershed", 0, new[]{"Reprisal","","","","Sacred Soil","Kerachole","","Feint","",""}),
        new(315, "P1", "Mortal Slayer", 0, new[]{"","","","","Succor","Eukrasian Prognosis","","","",""}),
        new(342, "P1", "Slaughtershed I", 0, new[]{"","Reprisal","Plenary Indulgence","Collective Unconsious","Sacred Soil + Fey Illumination + Seraphism","Kerachole + Philosophia","Feint","","","Addle"}),
        new(371, "P1", "Slaughtershed II", 0, new[]{"Reprisal","Party Mit","Liturgy of the Bell","Macrocosmos","Sacred Soil + Spreadlo","Kerachole + Panhaima + Zoe Shields","","","Party Mit",""}),
        new(400, "P1", "Slaughtershed III", 0, new[]{"Party Mit","Reprisal","Plenary Indulgence + Temperance + Divine Caress","Collective Unconsious + Neutral Sect + Sun Sign","Sacred Soil + Expediant + Seraph","Kerachole + Holos","","Feint","",""}),

        // ---- Phase 2: Lindwurm (+Phase2Offset) ----
        new(437, "P2", "Arcadia Aflame", 0, new[]{"Reprisal","Party Mit","Plenary Indulgence","Collective Unconsious","Spreadlo + Sacred Soil","Holos + Kerachole","Feint","","Party Mit","Addle"}),
        new(460, "P2", "Mighty Magic / Top-tier Slam I", 0, new[]{"","","Temperance","Neutral Sect","Seraph + Expedient","Panhaima + Zoe Shields","","","",""}),
        new(481, "P2", "Mighty Magic / Top-tier Slam II", 0, new[]{"Party Mit","Reprisal","Divine Caress","Sun Sign","Fey Illumination + Seraph + Sacred Soil","Kerachole","","Feint","",""}),
        new(548, "P2", "Firefall Splash", 0, new[]{"Reprisal","Party Mit","Liturgy of the Bell","Macrocosmos","Seraphism + Sacred Soil + Spreadlo","Philosophia + Holos + Kerachole","Feint","","Party Mit","Addle"}),
        new(581, "P2", "Reenactment", 0, new[]{"Party Mit","Reprisal","Plenary Indulgence + Temperance + Divine Caress","Collective Unconsious + Neutral Sect + Sun Sign","Expedient + Seraph + Sacred Soil","Zoe Shields + Panhaima + Kerachole","","Feint","",""}),
        new(622, "P2", "Blood Mana", 0, new[]{"","","","","Sacred Soil","Kerachole","","","",""}),
        new(650, "P2", "Netherworld Near/Far", 0, new[]{"Reprisal","Party Mit (DRK/GNB)","Plenary Indulgence","Collective Unconsious","Sacred Soil","Kerachole","Feint","","Party Mit","Addle"}),
        new(658, "P2", "Arcadia Aflame", 0, new[]{"","Party Mit (WAR/PLD)","","","","","","","",""}),
        new(690, "P2", "Idyllic Dream", 0, new[]{"Party Mit","Reprisal","","","Fey Illumination (Use Early) + Spreadlo + Sacred Soil","Holos (Use Early) + Zoe Shields + Kerachole","","Feint","",""}),
        new(765, "P2", "Lindwurm's Meteor", 0, new[]{"Reprisal","Party Mit","Plenary Indulgence","Collective Unconsious","Sacred Soil","Kerachole","Feint","","Party Mit","Addle"}),
        new(792, "P2", "Twisted Vision", 0, new[]{"Party Mit","","Everything","","","","Use Personals!","","",""}),
        new(860, "P2", "Reenactment + Twisted Vision", 0, new[]{"Reprisal","Party Mit","","","Sacred Soil","Kerachole","","Feint","Party Mit",""}),
        new(893, "P2", "Idyllic Dream", 0, new[]{"Party Mit","Reprisal","Plenary Indulgence","Collective Unconsious","Sacred Soil","Kerachole","Feint","","","Addle"}),
        new(936, "P2", "Arcadian Hell I", 0, new[]{"Reprisal","","Temperance","Neutral Sect","Expedient + Seraph + Fey Illumination + Sacred Soil","Holos","","","",""}),
        new(952, "P2", "Arcadian Hell II", 0, new[]{"","Reprisal + Party Mit","Plenary Indulgence + Divine Caress","Collective Unconsious + Sun Sign","Spreadlo + Seraph + Sacred Soil","Zoe Shields + Panhaima + Kerachole","","Feint","Party Mit",""}),
    };

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
        new() { Ability = 0xB4D7, Time = 107.3f, Label = "P1 The Fixer" },
        new() { Ability = 0xB4C2, Time = 176.7f, Label = "P1 Constrictor" },
        new() { Ability = 0xB9C6, Time = 188.2f, Label = "P1 Splattershed" },
        new() { Ability = 0xB4A8, Time = 230.4f, Label = "P1 Venomous Scourge" },
        new() { Ability = 0xB4D7, Time = 239.6f, Label = "P1 The Fixer" },
        new() { Ability = 0xB49D, Time = 266.5f, Label = "P1 Ravenous Reach" },
        new() { Ability = 0xB9C6, Time = 288.5f, Label = "P1 Splattershed" },
        new() { Ability = 0xADC9, Time = 340.9f, Label = "P1 Slaughtershed" },

        // ---- Phase 2 (cactbot time - 3000 + Phase2Offset) ----
        new() { Ability = 0xB528, Time = 435.7f, IsPhase = true,  Label = "P2 Arcadia Aflame" },
        new() { Ability = 0xB527, Time = 465.3f, Label = "P2 Snaking Kick" },
        new() { Ability = 0xB4E4, Time = 547.8f, Label = "P2 Firefall Splash" },
        new() { Ability = 0xB4EC, Time = 571.3f, Label = "P2 Reenactment" },
        new() { Ability = 0xB4FB, Time = 610.0f, Label = "P2 Blood Mana" },
        new() { Ability = 0xB528, Time = 655.8f, Label = "P2 Arcadia Aflame" },
        new() { Ability = 0xB509, Time = 688.8f, Label = "P2 Idyllic Dream" },
        new() { Ability = 0xB4F2, Time = 762.8f, Label = "P2 Lindwurm's Meteor" },
        new() { Ability = 0xB4EC, Time = 855.7f, IsPhase = true, Label = "P2 Reenactment" }, // re-base after the ~93s gap
    };

    public static List<BossAnchor> BossAnchors() => new();
}
