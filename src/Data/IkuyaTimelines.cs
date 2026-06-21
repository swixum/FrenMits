// AUTO-GENERATED from Ikuya's ultimate mitigation sheets, timed against the
// cactbot timelines. The sheets list mit assignments per mechanic but carry no
// timestamps, so each mechanic is aligned by name to its cactbot ability to get
// a pull-relative time (and, where the name matched cleanly, a resync anchor).
// A handful of rows cactbot doesn't name are interpolated between neighbours, so
// fine-tune in the editor (or /fm sync) if one drifts. Slots and layout match
// the other baked fights.
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

public static class IkuyaTimelines
{
    public static readonly string[] Slots = { "MT", "OT", "WHM", "AST", "SCH", "SGE", "D1", "D2", "D3", "D4", "Extras" };

    public sealed record Entry(int Time, string Phase, string Mechanic, string[] Actions);

    public static bool Has(uint territory) => territory is 733 or 777 or 887 or 968 or 1122;

    public static Entry[] Timeline(uint territory) => territory switch
    {
        733 => Ucob,
        777 => Uwu,
        887 => Tea,
        968 => Dsr,
        1122 => Top,
        _ => Array.Empty<Entry>(),
    };

    // First time each phase appears, for the practice phase-jump.
    public static List<(string Name, float Time)> PhaseStarts(uint territory)
        => Timeline(territory).GroupBy(e => e.Phase)
                              .Select(g => (g.Key, (float)g.Min(e => e.Time)))
                              .OrderBy(p => p.Item2)
                              .ToList();

    public static List<MitLine> BuildLines(uint territory, string slot)
    {
        var idx = Array.IndexOf(Slots, slot);
        var list = new List<MitLine>();
        if (idx < 0) return list;
        foreach (var e in Timeline(territory))
        {
            var action = e.Actions[idx];
            if (string.IsNullOrWhiteSpace(action)) continue;
            list.Add(new MitLine { Time = e.Time, Mechanic = e.Mechanic, Action = action, Enabled = true });
        }
        return list;
    }

    public static List<SyncPoint> SyncPoints(uint territory) => territory switch
    {
        733 => UcobSync(),
        777 => UwuSync(),
        887 => TeaSync(),
        968 => DsrSync(),
        1122 => TopSync(),
        _ => new(),
    };

    public static List<BossAnchor> BossAnchors(uint territory) => new();

    // ===== Unending Coil of Bahamut (UCOB) (territory 733) =====
    static readonly Entry[] Ucob =
    {
        new(16, "P1", "Fireball I", new[]{"Party Mit (WAR/PLD)","","Asylum","","Spreadlo + Soil (If No Critical Hit)","Zoe Shields + Kerachole","","","","",""}),
        new(227, "P1", "Fireball II", new[]{"","","Plenary Indulgence","Collective Unconsciousness","Fey Illumination + Sacrd Soil","Kerachole","Feint","","","Addle",""}),
        new(408, "P2", "Heavensfall + Meteor Stream", new[]{"","","","","Sacred Soil","Kerachole","","","Party Mit","",""}),
        new(471, "P2", "Thermionic Beam (1st Quote)", new[]{"Reprisal (If Applicable)","","","","Sacred Soil","Kerachole","","Feint","","","Dismantle (If Applicable)"}),
        new(667, "P2", "Thermionic Beam (2nd Quote)", new[]{"Party Mit","Reprisal + Party Mit","Plenary Indulgence","Collective Unconsciousness","Spreadlo + Soil","Kerachole","Feint","","","Addle","Dismantle"}),
        new(852, "P3", "Calamatous Flame + Blaze", new[]{"","","Plenary Indulgence","Collective Unconsciousness","Sacred Soil","Kerachole","","","Party Mit (LB2)","",""}),
        new(1037, "P3", "Quickmarch Trio", new[]{"Party Mit","","","","Spreadlo + Soil","Zoe Shields + Kerachole","Feint","","","",""}),
        new(1078, "P3", "Blackfire Trio", new[]{"Reprisal","Party Mit","Plenary Indulgence (Thermionic Beam)","Collective Unconsciousness (Thermionic Beam)","Sacred Soil","Kerachole","","Feint","Party Mit (If Available)","Addle",""}),
        new(1129, "P3", "Fellruin Trio", new[]{"Party Mit","Reprisal","","","Sacred Soil","Kerachole","Feint","","","",""}),
        new(1181, "P3", "Heavensfall Trio", new[]{"Reprisal","Party Mit (Fireball + Gigaflare)","Plenary Indulgence (Fireball + Gigaflare)","Collective Unconsciousness (Fireball + Gigaflare)","Spreadlo + Fey Illumination + Sacred Soil","Zoe Shields + Kerachole","","","Party Mit (If Used During Transition)","Addle","Dismantle"}),
        new(1242, "P3", "Tenstrike Trio", new[]{"Party Mit","Reprisal","Plenary Indulgence","Collective Unconsciousness","Sacred Soil (Earthshakers + Gigaflare)","Kerachole (Earthshakers + Gigaflare)","","Feint","","",""}),
        new(1298, "P3", "Grand Octet", new[]{"LB3 (Towers Spawn)","","","","","","","","","",""}),
        new(1608, "P4", "Thermionic Beam (1st Quote)", new[]{"Reprisal (If Applicable)","","","","Fey Illumination + Sacred Soil","Kerachole","Feint","","","",""}),
        new(1814, "P4", "Megaflare I", new[]{"Reprisal + Party Mit","Party Mit","Plenary Indulgence","Collective Unconsciousness","Carry Over + Spreadlo","Zoe Shields + Kerachole (If Available)","","","Party Mit","Addle","Dismantle"}),
        new(2008, "P4", "Thermionic Beam (2nd Quote)", new[]{"","Reprisal (If Applicable)","","","Sacred Soil","Kerachole","","Feint","","",""}),
        new(2228, "P4", "Megaflare II", new[]{"","Reprisal","Plenary Indulgence","Collective Unconsciousness","Carry Over","Kerachole (If Available)","Feint","","","",""}),
        new(2367, "P5", "Morn Afah I", new[]{"Party Mit","Reprisal","Plenary Indulgence","Collective Unconsciousness","Fey Illumination + Sacred Soil","Kerachole","Feint","","Party Mit","Addle",""}),
        new(2422, "P5", "Morn Afah II", new[]{"Reprisal","Party Mit","","","Spreadlo + Sacred Soil","Zoe Shields + Kerachole","","Feint","","","Dismantle"}),
        new(2456, "P5", "Morn Afah III", new[]{"Party Mit","Reprisal","Plenary Indulgence","Collective Unconsciousness","Sacred Soil","Kerachole","Feint","","","Addle",""}),
        new(2506, "P5", "Morn Afah IV", new[]{"Reprisal","Party Mit","","","Fey Illumination + Sacred Soil","Kerachole","","Feint","Party Mit","",""}),
        new(2556, "P5", "Morn Afah V", new[]{"Party Mit","Reprisal","Plenary Indulgence","Collective Unconsciousness","Spreadlo + Sacred Soil","Zoe Shields + Kerachole","Feint","","","Addle","Dismantle"}),
    };

    static List<SyncPoint> UcobSync() => new()
    {
        new() { Ability = 0x26AC, Time = 16f, IsPhase = true , Label = "P1 Fireball I" },
        new() { Ability = 0x26AC, Time = 227f, IsPhase = true , Label = "P1 Fireball II" },
        new() { Ability = 0x26B8, Time = 408f, IsPhase = true , Label = "P2 Heavensfall + Meteor Stream" },
        new() { Ability = 0x26BD, Time = 471f, IsPhase = false, Label = "P2 Thermionic Beam (1st Quote)" },
        new() { Ability = 0x26BD, Time = 667f, IsPhase = true , Label = "P2 Thermionic Beam (2nd Quote)" },
        new() { Ability = 0x26E2, Time = 1037f, IsPhase = true , Label = "P3 Quickmarch Trio" },
        new() { Ability = 0x26E3, Time = 1078f, IsPhase = false, Label = "P3 Blackfire Trio" },
        new() { Ability = 0x26E4, Time = 1129f, IsPhase = false, Label = "P3 Fellruin Trio" },
        new() { Ability = 0x26E5, Time = 1181f, IsPhase = false, Label = "P3 Heavensfall Trio" },
        new() { Ability = 0x26E6, Time = 1242f, IsPhase = false, Label = "P3 Tenstrike Trio" },
        new() { Ability = 0x26E7, Time = 1298f, IsPhase = false, Label = "P3 Grand Octet" },
        new() { Ability = 0x26BD, Time = 1608f, IsPhase = true , Label = "P4 Thermionic Beam (1st Quote)" },
        new() { Ability = 0x26BA, Time = 1814f, IsPhase = true , Label = "P4 Megaflare I" },
        new() { Ability = 0x26BD, Time = 2008f, IsPhase = true , Label = "P4 Thermionic Beam (2nd Quote)" },
        new() { Ability = 0x26BA, Time = 2228f, IsPhase = true , Label = "P4 Megaflare II" },
        new() { Ability = 0x26EC, Time = 2367f, IsPhase = true , Label = "P5 Morn Afah I" },
        new() { Ability = 0x26EC, Time = 2422f, IsPhase = false, Label = "P5 Morn Afah II" },
        new() { Ability = 0x26EC, Time = 2456f, IsPhase = false, Label = "P5 Morn Afah III" },
        new() { Ability = 0x26EC, Time = 2506f, IsPhase = false, Label = "P5 Morn Afah IV" },
        new() { Ability = 0x26EC, Time = 2556f, IsPhase = false, Label = "P5 Morn Afah V" },
    };

    // ===== Weapon's Refrain (UWU) (territory 777) =====
    static readonly Entry[] Uwu =
    {
        new(21, "P1", "Spiny Plume", new[]{"","Reprisal (Grab Spiny)","","","Spreadlo (OT)","Haima (OT)","","","","",""}),
        new(42, "P1", "Mistral Shriek", new[]{"Reprisal","","","","Sacred Soil","Zoe Shield","","","","Addle",""}),
        new(52, "P1", "Friction I", new[]{"Party Mit (GNB/DRK)","","Plenary Indulgence","Collective Unconsious","Fey Illumination","Kerachole","Feint","","Party Mit","",""}),
        new(58, "P1", "Friction II", new[]{"Party Mit (WAR/PLD)","","","","","","","Feint","","","Dismantle"}),
        new(77, "P1", "Aerial Blast", new[]{"","","GCD Heal and Shield after Feather Rain","","","","","","","",""}),
        new(193, "P1", "Mistrel Song", new[]{"","Reprisal","","","","","","","","",""}),
        new(252, "P1", "Super Cyclone", new[]{"Reprisal","","Plenary Indulgence","Collective Unconsious","Sacred Soil","Kerachole","","","","",""}),
        new(310, "P2", "Hellfire", new[]{"","Reprisal","","","","","Feint","","","Addle","Dismantle"}),
        new(318, "P2", "Vulcan Burst", new[]{"","","","","GCD Shield","","","","","",""}),
        new(334, "P2", "Infernal Nails", new[]{"Party Mit (WAR/PLD)","","","","Spreadlo + Fey Illumination","Zoe Shields","","","","",""}),
        new(341, "P2", "Searing Wind and Chains", new[]{"Buddy Mit: Searing Wind Healer","","","","Excognition (Searing Wind Healer)","Haima (Searing Wind Healer)","Second Wind + Bloodbath","","","",""}),
        new(349, "P2", "Infernal Surge (Eruption)", new[]{"Reprisal (Nails) + Party Mit (GNB/DRK)","Party Mit (GNB/DRK)","Plenary Indulgence","Earthly Star + Collective Unconsious","Sacred Soil","Kerachole","","Feint","Party Mit","",""}),
        new(373, "P2", "Hellfire", new[]{"Do Not Mitigate!","","GCD Heal and Shield (Do Not Mitigate!)","","","","Do Not Mitigate!","","","",""}),
        new(410, "P2", "Flaming Crush", new[]{"","Reprisal","","","","","Feint","","","",""}),
        new(603, "P3", "Geocrush", new[]{"","","GCD Heal","","Sacred Soil","Kerachole","","","","",""}),
        new(610, "P3", "Earthern Fury", new[]{"Reprisal","","","","","","","","","",""}),
        new(616, "P3", "Rock Slide + Mountain Buster", new[]{"","","","","","","","Feint","","",""}),
        new(618, "P3", "Tumult (x8)", new[]{"Party Mit","Reprisal + Party Mit","","","Sacred Soil","Kerachole","Feint","","","Addle",""}),
        new(619, "P3", "Tumult (x6)", new[]{"Reprisal","","Plenary Indulgence","Collective Unconsious","Sacred Soil + Fey Ilumination","Kerachole","","","Party Mit","","Dismantle"}),
        new(620, "P3", "Rock Slide + Mountain Buster", new[]{"","","","","","Haima (MT)","","Feint","","",""}),
        new(716, "P3", "Rock Slide + Mountain Buster", new[]{"","","","","","","Feint","","","",""}),
        new(850, "P4", "Ultima", new[]{"LB3","","Do Not Heal Until After Tank Purge!","","Shields Only","","","","","",""}),
        new(1004, "P4", "Tank Purge", new[]{"Read notes and communicate with your party!","","","","","","","","","",""}),
        new(1017, "P4", "Viscous Aetheroplasm", new[]{"Kitchen Sink (Optional)","","","","Sacred Soil","Kerachole","","","","",""}),
        new(1068, "P4", "Tumult (x7)", new[]{"Party Mit (GNB/DRK)","","Plenary Indulgence","Collective Unconsious","Sacred Soil + Fey Illumination","Kerachole","","","Party Mit","",""}),
        new(1093, "P4", "Mistrel Shriek", new[]{"Party Mit (WAR/PLD)","","","","","","","","Carry Over","",""}),
        new(1119, "P4", "Mesohigh", new[]{"Buddy Mit: D3","","","","Spreadlo","","","","Second Wind","",""}),
        new(1127, "P4", "Searing Wind", new[]{"","","","","","Haima: Searing Wind Healer","","","","",""}),
        new(1136, "P4", "Mesohigh", new[]{"Buddy Mit: Searing Wind Healer","","Heal Yourself!","","","","","","","",""}),
        new(1139, "P4", "Tank Purge", new[]{"Reprisal","Party Mit","Plenary Indulgence","Collective Unconsious","Sacred Soil","Kerachole","Feint","","","","Dismantle"}),
        new(1228, "P4", "Flaming Crush", new[]{"Party Mit (GNB/DRK)","","","","Sacred Soil","Kerachole","","","","",""}),
        new(1233, "P4", "Tank Purge", new[]{"Party Mit (WAR/PLD)","","GCD Heal and Shield!","","","","","","","",""}),
        new(1244, "P4", "Ultima", new[]{"Reprisal + LB3","","","","Fey Illumination","","","Feint","","Addle",""}),
        new(1255, "P4", "Aetheric Boom", new[]{"LB1","Reprisal","","","Sacred Soil","Kerachole","","","","",""}),
        new(1520, "P4", "Primal I", new[]{"","Party Mit","","","Sacred Soil","Kerachole","","","","",""}),
        new(1522, "P4", "Primal II", new[]{"","","Plenary Indulgence","Collective Unconsious","Spreadlo","Zoe Shields","","","Party Mit","",""}),
        new(1524, "P4", "Primal III", new[]{"Party Mit","","","","Sacred Soil","Kerachole","","","","",""}),
    };

    static List<SyncPoint> UwuSync() => new()
    {
        new() { Ability = 0x2B54, Time = 42f, IsPhase = true , Label = "P1 Mistral Shriek" },
        new() { Ability = 0x2B48, Time = 52f, IsPhase = false, Label = "P1 Friction I" },
        new() { Ability = 0x2B48, Time = 58f, IsPhase = false, Label = "P1 Friction II" },
        new() { Ability = 0x2B55, Time = 77f, IsPhase = false, Label = "P1 Aerial Blast" },
        new() { Ability = 0x2B5E, Time = 310f, IsPhase = true , Label = "P2 Hellfire" },
        new() { Ability = 0x2B57, Time = 318f, IsPhase = false, Label = "P2 Vulcan Burst" },
        new() { Ability = 0x2B5A, Time = 349f, IsPhase = false, Label = "P2 Infernal Surge (Eruption)" },
        new() { Ability = 0x2B5E, Time = 373f, IsPhase = false, Label = "P2 Hellfire" },
        new() { Ability = 0x2B5D, Time = 410f, IsPhase = false, Label = "P2 Flaming Crush" },
        new() { Ability = 0x2CFD, Time = 603f, IsPhase = true , Label = "P3 Geocrush" },
        new() { Ability = 0x2B62, Time = 616f, IsPhase = false, Label = "P3 Rock Slide + Mountain Buster" },
        new() { Ability = 0x2B63, Time = 620f, IsPhase = false, Label = "P3 Rock Slide + Mountain Buster" },
        new() { Ability = 0x2B62, Time = 716f, IsPhase = true , Label = "P3 Rock Slide + Mountain Buster" },
        new() { Ability = 0x2B8B, Time = 850f, IsPhase = true , Label = "P4 Ultima" },
        new() { Ability = 0x2B87, Time = 1004f, IsPhase = true , Label = "P4 Tank Purge" },
        new() { Ability = 0x2B7A, Time = 1017f, IsPhase = false, Label = "P4 Viscous Aetheroplasm" },
        new() { Ability = 0x2B49, Time = 1119f, IsPhase = true , Label = "P4 Mesohigh" },
        new() { Ability = 0x2B49, Time = 1136f, IsPhase = false, Label = "P4 Mesohigh" },
        new() { Ability = 0x2B87, Time = 1139f, IsPhase = false, Label = "P4 Tank Purge" },
        new() { Ability = 0x2B5D, Time = 1228f, IsPhase = false, Label = "P4 Flaming Crush" },
        new() { Ability = 0x2B87, Time = 1233f, IsPhase = false, Label = "P4 Tank Purge" },
        new() { Ability = 0x2B8B, Time = 1244f, IsPhase = false, Label = "P4 Ultima" },
        new() { Ability = 0x2B88, Time = 1255f, IsPhase = false, Label = "P4 Aetheric Boom" },
        new() { Ability = 0x2CD4, Time = 1520f, IsPhase = true , Label = "P4 Primal I" },
    };

    // ===== Epic of Alexander (TEA) (territory 887) =====
    static readonly Entry[] Tea =
    {
        new(6, "P1", "Countdown", new[]{"Rampart (-6s) + Party Mit (WAR/PLD)","","Asylum (-10s)","","","","","","","",""}),
        new(11, "P1", "Fluid Swing", new[]{"","","","","Sacred Soil","Kerachole","","","","",""}),
        new(20, "P1", "Cascade", new[]{"Reprisal","","","Collective Unconscious","Carry Over","Carry Over","Feint","","","",""}),
        new(38, "P1", "Fluid Swing/Strike", new[]{"Invulnerability + Provoke (Hand)","","","","","","","","","",""}),
        new(43, "P1", "Hand of Pain", new[]{"120s","120s + Party Mit","Plenary Indulgence","Neutral Sect","Sprealdo + Sacred Soil","Holos + Kerachole","","","Party Mit + Personals","Personals",""}),
        new(57, "P1", "Fluid Swing/Strike", new[]{"Short Mit","Short Mit + Reprisal","","","","","","","","",""}),
        new(87, "P1", "Splash (x6) + Drainage", new[]{"Kitchen Sink + Reprisal + Party Mit (DRK/GNB)","Invulnerability (DRK Only) or Kitchen Sink","Asylum + Temperance","","Seraph + Fey Illumination + Soil","Panhaima + Kerachole","","Feint (Living Liquid)","","Addle (Living Liquid)","Dismantle (Living Liquid)"}),
        new(92, "P1", "Cascade", new[]{"Party Mit (WAR/PLD)","Rampart + 90s (DRK Only)","","Collective Unconscious","Carry Over","Carry Over","","","","",""}),
        new(96, "P1", "Throttles", new[]{"","","Esuna (Top Down)","Esuna (Top Down)","Esuna (Bottom Up)","Esuna (Bottom Up)","","","The Warden's Paean (Bard)","",""}),
        new(107, "P1", "Protean Wave", new[]{"Short Mit + Reprisal","Short Mit","","","Sacred Soil","Kerachole","","","","",""}),
        new(226, "P2", "J Kick", new[]{"Party Mit","","","Collective Unconscious","Sacred Soil","Kerachole","","","Party Mit","",""}),
        new(238, "P2", "Whirlwind", new[]{"Short Mit (Autos After) + Party Mit","Short Mit (Autos After) + Reprisal","","","Carry Over","Carry Over","Feint (Cruise Chaser)","","","Addle (Cruise Chaser)",""}),
        new(262, "P2", "Photon", new[]{"","","Plenary Indulgence","","Spreadlo + Sacred Soil","Holos + Kerachole","","","","",""}),
        new(277, "P2", "Water/Thunder I", new[]{"Rampart + Short Mit","Rampart (After Water Resolves)","","","Carry Over","Carry Over","","Feint (Brute Justice)","","","Dismantle (Brute Justice)"}),
        new(284, "P2", "Missile Command", new[]{"Reprisal","","","","Excognition (OT)","Haima (OT)","","","","",""}),
        new(291, "P2", "Hidden Minefield", new[]{"","Kitchen Sink","","","Sacred Soil","Kerachole","","","","",""}),
        new(307, "P2", "Water/Thunder II", new[]{"120s + Short Mit","Party Mit","Temperance","Neutral Sect","Seraph + Fey Illumination","Panhaima","","","","",""}),
        new(328, "P2", "Whirlwind", new[]{"Party Mit (GNB/DRK)","Reprisal","","","Seraph + Sacred Soil","Kerachole","Feint (Cruise Chaser)","","Party Mit","Addle (Cruise Chaser)",""}),
        new(336, "P2", "Water/Thunder III", new[]{"90s + Short Mit + Party Mit (WAR/PLD)","Short Mit","Plenary Indulgence","Collective Unconscious","","","","","Carry Over","",""}),
        new(367, "P2", "Photon", new[]{"","","Benediction (MT)","Essential Dignity (MT)","Excognition (OT)","Taurochole (OT)","","","","",""}),
        new(375, "P2", "Double Rocket Punch", new[]{"Rampart + Short Mit + Reprisal","Rampart + Short Mit","","","Sacred Soil","Kerachole","","Feint (Brute Justice)","","",""}),
        new(395, "P2", "Whirlwind", new[]{"","Reprisal","","","","","Feint (Cruise Chaser)","","","Addle (Cruise Chaser)","Dismantle (Cruise Chaser)"}),
        new(522, "P3", "Chastening Heat + Divine Spear", new[]{"Provoke + Invulnerability (End of Castbar)","Provoke (After 3rd Divine Spear)","","","","","","","","",""}),
        new(552, "P3", "Judgment Crystal + True Heart", new[]{"Party Mit","","Everything","","","","","","Party Mit","",""}),
        new(575, "P3", "Flamethrower", new[]{"Short Mit","","","","","","","","","",""}),
        new(598, "P3", "Super Jump", new[]{"Kitchen Sink","","","","","","","","","",""}),
        new(611, "P3", "Chastening Heat + Divine Spear", new[]{"Provoke (After 3rd Divine Spear)","Kitchen Sink (Chastening Heat) + Invulnerability","","","","","","","","","Dismantle"}),
        new(670, "P3", "Incinerating Heat", new[]{"Party Mit","","","","","","","","","",""}),
        new(683, "P3", "Mega Holy I", new[]{"Reprisal","","Plenary Indulgence","","Sacred Soil","Kerachole","Feint","","","Addle",""}),
        new(690, "P3", "Mega Holy II", new[]{"Carry Over","","","","Carry Over","Carry Over","Carry Over","","","Carry Over",""}),
        new(706, "P3", "J Storm", new[]{"","","","Collective Unconscious","Spreadlo","","","","Party Mit","",""}),
        new(857, "P3", "J Waves (0-10s)", new[]{"","","","","","Panhaima","","","","",""}),
        new(932, "P3", "J Waves (10-20s)", new[]{"","Reprisal","Temperance","Neutral Sect","Seraph + Fey Illumination + Sacred Soil","Holos + Kerachole","","Feint (Brute Justice)","","",""}),
        new(970, "P3", "J Waves (20-30s)", new[]{"","Party Mit","Cure III","Aspected Helios","Succor","Eukrasian Prognosis","","","","","Dismantle"}),
        new(989, "P3", "Divine Judgement", new[]{"Party Mit","LB3","Medica II (When Z Appears) + Plenary Indulgence","Aspected Helios (When Z Appears)","Sacred Soil","Kerachole","","","","",""}),
        new(999, "P4", "Optical Sight", new[]{"Short Mit + Reprisal + Party Mit","Party Mit","Everything","","","","Feint","","Party Mit","Addle",""}),
        new(1008, "P4", "Ordained Capital Punishment", new[]{"Kitchen Sink","Kitchen Sink + Reprisal","","","","","","Feint","","","Dismantle"}),
        new(1011, "P4", "Ordained Punishment", new[]{"","Provoke (Castbar)","","","","","","","","",""}),
        new(1070, "P4", "Optical Sight (Fate Calibration β)", new[]{"Party Mit","Party Mit","Plenary Indulgence","Collective Unconscious","Sacred Soil","Kerachole","","","Party Mit","",""}),
        new(1107, "P4", "Ordained Capital Punishment", new[]{"","Invulnerability (Use Late)","","","","","","","","",""}),
        new(1110, "P4", "Ordained Punishment", new[]{"Provoke (Castbar)","","","","","","","","","",""}),
        new(1142, "P4", "Irresistible Grace", new[]{"Reprisal","","Temperance","Neutral Sect","Seraph + Spreadlo + Soil","Holos + Kerachole","Feint","","","Addle",""}),
        new(1153, "P4", "Ordained Capital Punishment", new[]{"Invulnerability (Use Late)","","","","","","","","","",""}),
        new(1156, "P4", "Ordained Punishment", new[]{"","Provoke (Castbar)","","","","","","","","",""}),
        new(1188, "P4", "Irresistible Grace", new[]{"Party Mit","Reprisal + Party Mit","Plenary Indulgence","Neutral Sect + Collective Unconscious","Seraph + Fey Illumination + Soil","Panhaima + Kerachole","","Feint","Party Mit","","Dismantle"}),
    };

    static List<SyncPoint> TeaSync() => new()
    {
        new() { Ability = 0x49B0, Time = 11f, IsPhase = true , Label = "P1 Fluid Swing" },
        new() { Ability = 0x4826, Time = 20f, IsPhase = false, Label = "P1 Cascade" },
        new() { Ability = 0x49B0, Time = 38f, IsPhase = false, Label = "P1 Fluid Swing/Strike" },
        new() { Ability = 0x482D, Time = 43f, IsPhase = false, Label = "P1 Hand of Pain" },
        new() { Ability = 0x49B0, Time = 57f, IsPhase = false, Label = "P1 Fluid Swing/Strike" },
        new() { Ability = 0x4827, Time = 87f, IsPhase = false, Label = "P1 Splash (x6) + Drainage" },
        new() { Ability = 0x4826, Time = 92f, IsPhase = false, Label = "P1 Cascade" },
        new() { Ability = 0x4828, Time = 96f, IsPhase = false, Label = "P1 Throttles" },
        new() { Ability = 0x4822, Time = 107f, IsPhase = false, Label = "P1 Protean Wave" },
        new() { Ability = 0x4854, Time = 226f, IsPhase = true , Label = "P2 J Kick" },
        new() { Ability = 0x49C2, Time = 238f, IsPhase = false, Label = "P2 Whirlwind" },
        new() { Ability = 0x4836, Time = 262f, IsPhase = false, Label = "P2 Photon" },
        new() { Ability = 0x4841, Time = 277f, IsPhase = false, Label = "P2 Water/Thunder I" },
        new() { Ability = 0x4851, Time = 291f, IsPhase = false, Label = "P2 Hidden Minefield" },
        new() { Ability = 0x4841, Time = 307f, IsPhase = false, Label = "P2 Water/Thunder II" },
        new() { Ability = 0x49C2, Time = 328f, IsPhase = false, Label = "P2 Whirlwind" },
        new() { Ability = 0x4841, Time = 336f, IsPhase = false, Label = "P2 Water/Thunder III" },
        new() { Ability = 0x4836, Time = 367f, IsPhase = false, Label = "P2 Photon" },
        new() { Ability = 0x4847, Time = 375f, IsPhase = false, Label = "P2 Double Rocket Punch" },
        new() { Ability = 0x49C2, Time = 395f, IsPhase = false, Label = "P2 Whirlwind" },
        new() { Ability = 0x4A80, Time = 522f, IsPhase = true , Label = "P3 Chastening Heat + Divine Spear" },
        new() { Ability = 0x485B, Time = 552f, IsPhase = false, Label = "P3 Judgment Crystal + True Heart" },
        new() { Ability = 0x484A, Time = 598f, IsPhase = false, Label = "P3 Super Jump" },
        new() { Ability = 0x4A80, Time = 611f, IsPhase = false, Label = "P3 Chastening Heat + Divine Spear" },
        new() { Ability = 0x4A51, Time = 670f, IsPhase = false, Label = "P3 Incinerating Heat" },
        new() { Ability = 0x4A83, Time = 683f, IsPhase = false, Label = "P3 Mega Holy I" },
        new() { Ability = 0x4A83, Time = 690f, IsPhase = false, Label = "P3 Mega Holy II" },
        new() { Ability = 0x4876, Time = 706f, IsPhase = false, Label = "P3 J Storm" },
        new() { Ability = 0x4892, Time = 1008f, IsPhase = true , Label = "P4 Ordained Capital Punishment" },
        new() { Ability = 0x4893, Time = 1011f, IsPhase = false, Label = "P4 Ordained Punishment" },
        new() { Ability = 0x4B14, Time = 1070f, IsPhase = false, Label = "P4 Optical Sight (Fate Calibration β)" },
        new() { Ability = 0x4892, Time = 1107f, IsPhase = false, Label = "P4 Ordained Capital Punishment" },
        new() { Ability = 0x4893, Time = 1110f, IsPhase = false, Label = "P4 Ordained Punishment" },
        new() { Ability = 0x4894, Time = 1142f, IsPhase = false, Label = "P4 Irresistible Grace" },
        new() { Ability = 0x4892, Time = 1153f, IsPhase = false, Label = "P4 Ordained Capital Punishment" },
        new() { Ability = 0x4893, Time = 1156f, IsPhase = false, Label = "P4 Ordained Punishment" },
        new() { Ability = 0x4894, Time = 1188f, IsPhase = false, Label = "P4 Irresistible Grace" },
    };

    // ===== Dragonsong's Reprise (DSR) (territory 968) =====
    static readonly Entry[] Dsr =
    {
        new(556, "P2", "Heavenly Heel & Ascalon's Might", new[]{"Reprisal","","","","","","","","","",""}),
        new(570, "P2", "Strength of the Ward", new[]{"Party Mit","Party Mit","Temperance + Liturgy of the Bell","Neutral Sect","Fey Illumination + Expedient","Holos","","","Party Mit","","RDM"}),
        new(611, "P2", "Ancient Quaga", new[]{"","Reprisal","Plenary Indulgence","Collective Unconsious","Carry Over + Sacred Soil","Carry Over + Kerachole","Feint","","","Addle","MCH"}),
        new(621, "P2", "Heavenly Heel & Ascalon's Might", new[]{"Reprisal","","","","","","","Feint","","",""}),
        new(664, "P2", "Sancitity of the Ward", new[]{"","","","Macrocosmos","Seraph + Spreadlo","Zoe Shields + Panhaima + Kerachole","","","","",""}),
        new(706, "P2", "Ultimate End", new[]{"Party Mit","Reprisal","Plenary Indulgence","Collective Unconsious","Sacred Soil","Kerachole","Feint","","","Addle",""}),
        new(1002, "P2", "Final Chorus", new[]{"","Party Mit","","Neutral Sect","Spreadlo + Sacred Soil","Zoe Shields + Holos + Kerachole","","","Party Mit","","RDM"}),
        new(1256, "P3", "First Stack", new[]{"Reprisal","","Temperance","Carry Over (Neutral Sect)","Fey Illumination + Sacred Soil","Kerachole","","Feint","","",""}),
        new(1383, "P3", "Second Stack", new[]{"Party Mit (If Available)","Reprisal","Plenary Indulgence","Collective Unconscious","Seraph + Expedient + Sacred Soil (If Available","Panhaima + Kerachole (If Available)","Feint (If Available)","","","Addle (If Available)","MCH"}),
        new(1510, "P3", "Resentment", new[]{"Party Mit (WAR/PLD after bleed expires)","","","Collective Unconscious","Sprealdo + Sacred Soil","Zoe Shields + Kerachole","","","","",""}),
        new(1540, "P3", "Orb Pops", new[]{"Reprisal","","Plenary Indulgence","Macrocosmos","","","","","Party Mit","",""}),
        new(1556, "P3", "Mirage Dives", new[]{"","","","","Seraph + Sacred Soil","Panhaima + Kerachole","","","","",""}),
        new(1571, "P3", "Steep in Rage", new[]{"","Reprisal","","","","","","","","",""}),
        new(1803, "Int", "Healers", new[]{"LB3 (3rd GCD) | WAR > DRK > PLD > GNB","","","","Spreadlo","Zoe Shields","","","","",""}),
        new(1920, "Int", "Melee", new[]{"Buddy Mit (D1) + Reprisal","Buddy Mit (D2)","Temperance + Liturgy of the Bell","Neutral Sect","Expedient","Holos","Feint","","","",""}),
        new(1978, "Int", "Range", new[]{"Party Mit","Party Mit","","","Fey Illumination + Sacred Soil","Kerachole","","","Party Mit","",""}),
        new(2007, "Int", "Tanks", new[]{"","Reprisal","Plenary Indulgence","Collective Unconscious","","","","Feint","","Addle","MCH/RDM"}),
        new(2036, "Int", "Pure of Heart", new[]{"","","Benediction (Haurchefant)","","","","","","","",""}),
        new(3025, "P5", "Wrath of the Heavens", new[]{"","","","","Spreadlo + Seraph + Fey Illumination","Zoe Shields + Panhaima + Kerachole","","","","",""}),
        new(3057, "P5", "Ancient Quaga", new[]{"Party Mit (After Proteans)","Party Mit (After Proteans) + Reprisal","Plenary Indulgence","Collective Unconscious","Sacred Soil","Kerachole","Feint","","Party Mit (After Proteans)","Addle",""}),
        new(3068, "P5", "Heavenly Heel & Ascalon's Might", new[]{"Reprisal (If Not Warrior)","","","","","","","Feint (Without Warrior)","","",""}),
        new(3085, "P5", "Death of the Heavens", new[]{"","","Temperance","Neutral Sect","Expedient","Holos","","","","",""}),
        new(3208, "P5", "Ancient Quaga", new[]{"Party Mit","Party Mit + Reprisal","Plenary Indulgence","Collective Unconscious","Sacred Soil or Spreadlo","Kerachole","","Feint (With Warrior)","Party Mit","",""}),
        new(3218, "P5", "Heavenly Heel & Ascalon's Might", new[]{"Reprisal","","Assist Tanks","","","","Feint","","","Addle",""}),
        new(3535, "P6", "Wyrmsbreath I", new[]{"","","Plenary Indulgence","","Seraph","Kerachole","","","","",""}),
        new(3554, "P6", "Akh Afah", new[]{"","Reprisal","","Collective Unconscious","Seraph + Sacred Soil","Kerachole","","","","",""}),
        new(3582, "P6", "Wroth Flames", new[]{"Reprisal + Party Mit","Party Mit","Everything","Neutral Sect","Everything","Everything","Feint (Nidhogg)","Feint (Either Hallowed Plume)","Party Mit","Addle (Nidhogg)","MCH/RDM"}),
        new(3623, "P6", "Akh Afah", new[]{"","Reprisal","","Collective Unconscious","Sacred Soil","Kerachole","","","","",""}),
        new(3655, "P6", "Wyrmsbreath II", new[]{"","","","","Seraph","","","","","",""}),
        new(3668, "P6", "Cauterize", new[]{"Reprisal","","Plenary Indulgence","Macrocosmos","Seraph + Sacred Soil","Kerachole","","","","",""}),
        new(3675, "P6", "Touchdown", new[]{"Limit Break (If Hraesvelgr Enrages)","","Benediction (DRK > WAR > GNB)","Microcosmos","Carry Over","Carry Over","","","","",""}),
        new(3745, "P7", "Alternative End", new[]{"Party Mit","Party Mit","Plenary Indulgence","Collective Unconscious","Spreadlo","Zoe Shields + Holos","","","Party Mit","","RDM"}),
        new(3954, "P7", "Ahk Morn I", new[]{"Reprisal","","Temperance","Neutral Sect","Fey Illumination","Panhaima","Feint","","","Addle",""}),
        new(4163, "P7", "Gigaflare I", new[]{"","Reprisal","Plenary Indulgence","Collective Unconscious","Expedient + Seraph","","","Feint","","","MCH"}),
        new(4165, "P7", "Ahk Morn II", new[]{"Reprisal + Party Mit","Party Mit","Liturgy of the Bell","","","Zoe Shields","","","Party Mit","","RDM"}),
        new(4167, "P7", "Gigaflare II", new[]{"","Reprisal","Plenary Indulgence","Collective Unconscious + Macrocosmos","Spreadlo","","Feint","","","Addle",""}),
        new(4169, "P7", "Akh Morn III", new[]{"Reprisal + Party Mit","Party Mit","Everything","","","","","Feint","Party Mit","","MCH"}),
    };

    static List<SyncPoint> DsrSync() => new()
    {
        new() { Ability = 0x63C5, Time = 556f, IsPhase = true , Label = "P2 Heavenly Heel & Ascalon's Might" },
        new() { Ability = 0x63D3, Time = 570f, IsPhase = false, Label = "P2 Strength of the Ward" },
        new() { Ability = 0x63C6, Time = 611f, IsPhase = false, Label = "P2 Ancient Quaga" },
        new() { Ability = 0x63C7, Time = 621f, IsPhase = false, Label = "P2 Heavenly Heel & Ascalon's Might" },
        new() { Ability = 0x63BE, Time = 706f, IsPhase = false, Label = "P2 Ultimate End" },
        new() { Ability = 0x6709, Time = 1002f, IsPhase = true , Label = "P2 Final Chorus" },
        new() { Ability = 0x68BA, Time = 1510f, IsPhase = true , Label = "P3 Resentment" },
        new() { Ability = 0x68BD, Time = 1571f, IsPhase = false, Label = "P3 Steep in Rage" },
        new() { Ability = 0x62E4, Time = 2036f, IsPhase = true , Label = "Int Pure of Heart" },
        new() { Ability = 0x6B89, Time = 3025f, IsPhase = true , Label = "P5 Wrath of the Heavens" },
        new() { Ability = 0x63C6, Time = 3057f, IsPhase = false, Label = "P5 Ancient Quaga" },
        new() { Ability = 0x63C7, Time = 3068f, IsPhase = false, Label = "P5 Heavenly Heel & Ascalon's Might" },
        new() { Ability = 0x6B92, Time = 3085f, IsPhase = false, Label = "P5 Death of the Heavens" },
        new() { Ability = 0x63C6, Time = 3208f, IsPhase = true , Label = "P5 Ancient Quaga" },
        new() { Ability = 0x63C7, Time = 3218f, IsPhase = false, Label = "P5 Heavenly Heel & Ascalon's Might" },
        new() { Ability = 0x6D35, Time = 3535f, IsPhase = true , Label = "P6 Wyrmsbreath I" },
        new() { Ability = 0x6D42, Time = 3554f, IsPhase = false, Label = "P6 Akh Afah" },
        new() { Ability = 0x6D45, Time = 3582f, IsPhase = false, Label = "P6 Wroth Flames" },
        new() { Ability = 0x6D42, Time = 3623f, IsPhase = false, Label = "P6 Akh Afah" },
        new() { Ability = 0x6D35, Time = 3655f, IsPhase = false, Label = "P6 Wyrmsbreath II" },
        new() { Ability = 0x6D3F, Time = 3668f, IsPhase = false, Label = "P6 Cauterize" },
        new() { Ability = 0x70E7, Time = 3675f, IsPhase = false, Label = "P6 Touchdown" },
        new() { Ability = 0x7438, Time = 3745f, IsPhase = true , Label = "P7 Alternative End" },
        new() { Ability = 0x6D99, Time = 4163f, IsPhase = true , Label = "P7 Gigaflare I" },
    };

    // ===== The Omega Protocol (TOP) (territory 1122) =====
    static readonly Entry[] Top =
    {
        new(69, "P1", "Pantokrator", new[]{"Party Mit","","Everything","","","","","","","",""}),
        new(142, "P1", "Stack I", new[]{"Reprisal","","","","","","Feint","","Party Mit","","Magick Barrier"}),
        new(178, "P1", "Stack II", new[]{"","","","","Seraph","","","","","",""}),
        new(196, "P1", "Stack III", new[]{"","Reprisal","","","Seraph","","","Feint","","Addle","Dismantle"}),
        new(214, "P2", "Solar Ray", new[]{"","","Assist Tanks","Assist Tanks","Sacred Soil","Kerachole","","","","",""}),
        new(321, "P2", "Optomized Meteor (6-1-1)", new[]{"Party Mit","","Everything","","","","","","Party Mit","","RDM"}),
        new(428, "P2", "Sniper Cannon Fodder (Transition)", new[]{"","","","","","Kerachole","","","","",""}),
        new(441, "P3", "Hello World", new[]{"Reprisal","","Plenary Indulgence","Collective Unconscious","","","Feint","","","Addle",""}),
        new(467, "P3", "1st Patch Set", new[]{"Party Mit","","","Earthly Star (Place 2 GCDs after Hello World)","Spreadlo + Sacred Soil","Zoe Shields + Kerachole","","","","",""}),
        new(488, "P3", "2nd Patch Set", new[]{"","Reprisal","Temperance","Neutral Sect","Expedient + Seraph","Panhaima","","","","",""}),
        new(509, "P3", "3rd Patch Set", new[]{"","","Liturgy of the Bell","Macrocosmos","Fey Illumination + Seraph + Sacred Soil","Kerachole","","Feint","Party Mit","","MCH"}),
        new(528, "P3", "4th Patch Set", new[]{"Reprisal","Party Mit","","","","Holos","","","","","RDM"}),
        new(550, "P3", "Critical Error", new[]{"","Reprisal","Plenary Indulgence","Collective Unconscious","Sacred Soil","Kerachole","Feint","","","Addle",""}),
        new(645, "P4", "Protean I", new[]{"Reprisal","","","","Spreadlo","Zoe Shields","","","","",""}),
        new(693, "P4", "Light Party Stacks I", new[]{"","","Plenary Indulgence","Collective Unconscious","Seraph + Sacred Soil","Kerachole","","","","",""}),
        new(716, "P4", "Protean II", new[]{"Party Mit","","Temperance","Neutral Sect","Expedient","","","","","",""}),
        new(728, "P4", "Light Party Stacks II", new[]{"","Party Mit","","","Seraph","Panhaima","","","","",""}),
        new(734, "P4", "Protean III", new[]{"","Reprisal","","","","","","","","",""}),
        new(737, "P4", "Critical Error", new[]{"","","","","Sacred Soil","Kerachole","Feint","","","",""}),
        new(740, "P5", "Delta", new[]{"Reprisal","","Plenary Indulgence","Collective Unconscious","Spreadlo + Sacred Soil","Zoe Shields + Holos + Kerachole","","Feint","Party Mit","Addle","MCH/RDM/PCT"}),
        new(781, "P5", "Delta (During Mechanic)", new[]{"","","Liturgy of the Bell","Macrocosmos","Fey Illumination + Sacred Soil","Kerachole","","","","",""}),
        new(822, "P5", "Sigma", new[]{"Reprisal + Party Mit","Party Mit","Temperance + Plenary Indulgence","Neutral Sect + Collective Unconscious","Seraph + Sacred Soil","Zoe Shields + Panhaima + Kerachole","Feint","","Party Mit","",""}),
        new(866, "P5", "Sigma (During Mechanic)", new[]{"","","","","Seraph + Expedient","","","","","",""}),
        new(911, "P5", "Omega", new[]{"Reprisal + Party Mit","Party Mit","Plenary Indulgence","Collective Unconscious","Spreadlo + Fey Illumination + Sacred Soil","Zoe Shields + Holos + Kerachole","","Feint","Party Mit","Addle","MCH/RDM/PCT"}),
        new(955, "P5", "Omega (During Mechanic)", new[]{"","","Temperance/Liturgy of the Bell","Macrocosmos + Neutral Sect","Seraph","Panhaima","","","","",""}),
        new(998, "P5", "Blind Faith", new[]{"Reprisal","","Plenary Indulgence","Collective Unconscious","Sacred Soil","Kerachole","","","Party Mit","",""}),
        new(1168, "P6", "Cosmo Memory", new[]{"LB3 + Party Mit (GNB/DRK)","LB3 + Reprisal","","","Sacred Soil","Kerachole","","","","",""}),
        new(1197, "P6", "Cosmo Dive I", new[]{"Reprisal + Party Mit (WAR/PLD)","","Temperance","Neutral Sect","Seraph + Expedient + Fey Illumination","Panhaima + Kerachole","","","","Addle",""}),
        new(1206, "P6", "Protean I", new[]{"","Party Mit (GNB/DRK)","","","Sacred Soil","Kerachole + Zoe Shields (Wave Cannon)","","","Party Mit","",""}),
        new(1215, "P6", "Wave Cannon I", new[]{"","Reprisal + Party Mit (WAR/PLD)","Plenary Indulgence","Collective Unconscious","","","","","","",""}),
        new(1219, "P6", "Proteans II", new[]{"Party Mit (GNB/DRK)","","","","Spreadlo (Proteans) + Sacred Soil","Holos + Kerachole","","","","",""}),
        new(1223, "P6", "Wave Cannon II", new[]{"Reprisal + Party Mit (WAR/PLD)","","","Macrocosmos","","","","Feint","","",""}),
        new(1310, "P6", "Cosmo Dive II", new[]{"","Reprisal","Plenary Indulgence","Collective Unconscious","Sacred Soil","Kerachole","Feint (D1) + Check That Limit Break III Is Available","","","Addle",""}),
        new(1332, "P6", "Cosmo Meteor", new[]{"Reprisal + Buddy Mit (D3)","Party Mit + Buddy Mit (D4)","Temperance + Liturgy of the Bell","Neutral Sect","Seraph + Expedient + Fey Illumination","Zoe Shields + Panhaima","","","Party Mit","",""}),
        new(1349, "P6", "Flares", new[]{"","","","","Spreadlo + Sacred Soil","Kerachole","","","","",""}),
        new(1367, "P6", "Magic Number", new[]{"First LB3","Second LB3","Second LB3","","First LB3","","","","","",""}),
    };

    static List<SyncPoint> TopSync() => new()
    {
        new() { Ability = 0x7B0B, Time = 69f, IsPhase = true , Label = "P1 Pantokrator" },
        new() { Ability = 0x7E6A, Time = 214f, IsPhase = true , Label = "P2 Solar Ray" },
        new() { Ability = 0x7B53, Time = 428f, IsPhase = true , Label = "P2 Sniper Cannon Fodder (Transition)" },
        new() { Ability = 0x7B55, Time = 441f, IsPhase = true , Label = "P3 Hello World" },
        new() { Ability = 0x7B63, Time = 467f, IsPhase = false, Label = "P3 1st Patch Set" },
        new() { Ability = 0x7B63, Time = 488f, IsPhase = false, Label = "P3 2nd Patch Set" },
        new() { Ability = 0x7B63, Time = 509f, IsPhase = false, Label = "P3 3rd Patch Set" },
        new() { Ability = 0x7B63, Time = 528f, IsPhase = false, Label = "P3 4th Patch Set" },
        new() { Ability = 0x7B64, Time = 550f, IsPhase = false, Label = "P3 Critical Error" },
        new() { Ability = 0x7B88, Time = 740f, IsPhase = true , Label = "P5 Delta" },
        new() { Ability = 0x8014, Time = 822f, IsPhase = false, Label = "P5 Sigma" },
        new() { Ability = 0x8015, Time = 911f, IsPhase = false, Label = "P5 Omega" },
        new() { Ability = 0x7B87, Time = 998f, IsPhase = false, Label = "P5 Blind Faith" },
        new() { Ability = 0x7BA1, Time = 1168f, IsPhase = true , Label = "P6 Cosmo Memory" },
        new() { Ability = 0x7BA6, Time = 1197f, IsPhase = false, Label = "P6 Cosmo Dive I" },
        new() { Ability = 0x7BAC, Time = 1215f, IsPhase = false, Label = "P6 Wave Cannon I" },
        new() { Ability = 0x7BAF, Time = 1223f, IsPhase = false, Label = "P6 Wave Cannon II" },
        new() { Ability = 0x7BA6, Time = 1310f, IsPhase = false, Label = "P6 Cosmo Dive II" },
        new() { Ability = 0x7BB0, Time = 1332f, IsPhase = false, Label = "P6 Cosmo Meteor" },
        new() { Ability = 0x7BB6, Time = 1367f, IsPhase = false, Label = "P6 Magic Number" },
    };

}
