// AUTO-GENERATED from Ikuya's ultimate mitigation sheets, which carry no
// timestamps, so each mechanic is aligned by name to its boss ability id and
// timed from real logs clears (median pull-relative cast time) with the resync
// engine correcting the DPS-variable phase offsets live.
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

public static class IkuyaTimelines
{
    public static readonly string[] Slots = { "MT", "OT", "WHM", "AST", "SCH", "SGE", "D1", "D2", "D3", "D4" };

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
        var seen = new HashSet<(int Time, string Action)>();
        foreach (var e in Timeline(territory))
        {
            var action = e.Actions[idx];
            if (string.IsNullOrWhiteSpace(action)) continue;
            // Alt-strat rows can repeat one call at the same instant (e.g. UWU's
            // Primal I and III both list Sacred Soil for SCH); fire it once.
            if (!seen.Add((e.Time, action))) continue;
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
        new(16, "P1", "Fireball I", new[]{"Party Mit (WAR/PLD)","","Asylum","","Spreadlo + Soil (If No Critical Hit)","Zoe Shields + Kerachole","","","",""}),
        new(97, "P1", "Fireball II", new[]{"","","Plenary Indulgence","Collective Unconsciousness","Fey Illumination + Sacrd Soil","Kerachole","Feint","","","Addle"}),
        new(145, "P2", "Heavensfall + Meteor Stream", new[]{"","","","","Sacred Soil","Kerachole","","","Party Mit",""}),
        new(184, "P2", "Thermionic Beam (1st Quote)", new[]{"Reprisal (If Applicable)","","","","Sacred Soil","Kerachole","","Feint","",""}),
        new(209, "P2", "Thermionic Beam (2nd Quote)", new[]{"Party Mit","Reprisal + Party Mit","Plenary Indulgence","Collective Unconsciousness","Spreadlo + Soil","Kerachole","Feint","","","Addle"}),
        new(260, "P3", "Calamatous Flame + Blaze", new[]{"","","Plenary Indulgence","Collective Unconsciousness","Sacred Soil","Kerachole","","","Party Mit (LB2)",""}),
        new(311, "P3", "Quickmarch Trio", new[]{"Party Mit","","","","Spreadlo + Soil","Zoe Shields + Kerachole","Feint","","",""}),
        new(352, "P3", "Blackfire Trio", new[]{"Reprisal","Party Mit","Plenary Indulgence (Thermionic Beam)","Collective Unconsciousness (Thermionic Beam)","Sacred Soil","Kerachole","","Feint","Party Mit (If Available)","Addle"}),
        new(403, "P3", "Fellruin Trio", new[]{"Party Mit","Reprisal","","","Sacred Soil","Kerachole","Feint","","",""}),
        new(456, "P3", "Heavensfall Trio", new[]{"Reprisal","Party Mit (Fireball + Gigaflare)","Plenary Indulgence (Fireball + Gigaflare)","Collective Unconsciousness (Fireball + Gigaflare)","Spreadlo + Fey Illumination + Sacred Soil","Zoe Shields + Kerachole","","","Party Mit (If Used During Transition)","Addle"}),
        new(517, "P3", "Tenstrike Trio", new[]{"Party Mit","Reprisal","Plenary Indulgence","Collective Unconsciousness","Sacred Soil (Earthshakers + Gigaflare)","Kerachole (Earthshakers + Gigaflare)","","Feint","",""}),
        new(574, "P3", "Grand Octet", new[]{"LB3 (Towers Spawn)","","","","","","","","",""}),
        new(693, "P4", "Thermionic Beam (1st Quote)", new[]{"Reprisal (If Applicable)","","","","Fey Illumination + Sacred Soil","Kerachole","Feint","","",""}),
        new(682, "P4", "Megaflare I", new[]{"Reprisal + Party Mit","Party Mit","Plenary Indulgence","Collective Unconsciousness","Carry Over + Spreadlo","Zoe Shields + Kerachole (If Available)","","","Party Mit","Addle"}),
        new(730, "P4", "Thermionic Beam (2nd Quote)", new[]{"","Reprisal (If Applicable)","","","Sacred Soil","Kerachole","","Feint","",""}),
        new(785, "P4", "Megaflare II", new[]{"","Reprisal","Plenary Indulgence","Collective Unconsciousness","Carry Over","Kerachole (If Available)","Feint","","",""}),
        new(817, "P5", "Morn Afah I", new[]{"Party Mit","Reprisal","Plenary Indulgence","Collective Unconsciousness","Fey Illumination + Sacred Soil","Kerachole","Feint","","Party Mit","Addle"}),
        new(872, "P5", "Morn Afah II", new[]{"Reprisal","Party Mit","","","Spreadlo + Sacred Soil","Zoe Shields + Kerachole","","Feint","",""}),
        new(906, "P5", "Morn Afah III", new[]{"Party Mit","Reprisal","Plenary Indulgence","Collective Unconsciousness","Sacred Soil","Kerachole","Feint","","","Addle"}),
        new(956, "P5", "Morn Afah IV", new[]{"Reprisal","Party Mit","","","Fey Illumination + Sacred Soil","Kerachole","","Feint","Party Mit",""}),
        new(1040, "P5", "Morn Afah V", new[]{"Party Mit","Reprisal","Plenary Indulgence","Collective Unconsciousness","Spreadlo + Sacred Soil","Zoe Shields + Kerachole","Feint","","","Addle"}),
    };

    static List<SyncPoint> UcobSync() => new()
    {
        new() { Ability = 0x26AC, Time = 16.3f, IsPhase = true , Label = "P1 Fireball I" },
        new() { Ability = 0x26AC, Time = 96.6f, IsPhase = true , Label = "P1 Fireball II" },
        new() { Ability = 0x26B8, Time = 144.6f, IsPhase = true , Label = "P2 Heavensfall + Meteor Stream" },
        new() { Ability = 0x26BD, Time = 184.3f, IsPhase = false, Label = "P2 Thermionic Beam (1st Quote)" },
        new() { Ability = 0x26BD, Time = 209.0f, IsPhase = true , Label = "P2 Thermionic Beam (2nd Quote)" },
        new() { Ability = 0x26E2, Time = 310.7f, IsPhase = true , Label = "P3 Quickmarch Trio" },
        new() { Ability = 0x26E3, Time = 351.8f, IsPhase = false, Label = "P3 Blackfire Trio" },
        new() { Ability = 0x26E4, Time = 403.1f, IsPhase = false, Label = "P3 Fellruin Trio" },
        new() { Ability = 0x26E5, Time = 455.5f, IsPhase = false, Label = "P3 Heavensfall Trio" },
        new() { Ability = 0x26E6, Time = 516.7f, IsPhase = false, Label = "P3 Tenstrike Trio" },
        new() { Ability = 0x26E7, Time = 573.7f, IsPhase = false, Label = "P3 Grand Octet" },
        new() { Ability = 0x26BD, Time = 692.8f, IsPhase = true , Label = "P4 Thermionic Beam (1st Quote)" },
        new() { Ability = 0x26BA, Time = 682.2f, IsPhase = true , Label = "P4 Megaflare I" },
        new() { Ability = 0x26BD, Time = 730.3f, IsPhase = true , Label = "P4 Thermionic Beam (2nd Quote)" },
        new() { Ability = 0x26BA, Time = 784.9f, IsPhase = true , Label = "P4 Megaflare II" },
        new() { Ability = 0x26EC, Time = 817.4f, IsPhase = true , Label = "P5 Morn Afah I" },
        new() { Ability = 0x26EC, Time = 872.5f, IsPhase = false, Label = "P5 Morn Afah II" },
        new() { Ability = 0x26EC, Time = 905.8f, IsPhase = false, Label = "P5 Morn Afah III" },
        new() { Ability = 0x26EC, Time = 955.5f, IsPhase = false, Label = "P5 Morn Afah IV" },
        new() { Ability = 0x26EC, Time = 1040.0f, IsPhase = false, Label = "P5 Morn Afah V" },
    };

    // ===== Weapon's Refrain (UWU) (territory 777) =====
    static readonly Entry[] Uwu =
    {
        new(21, "P1", "Spiny Plume", new[]{"","Reprisal (Grab Spiny)","","","Spreadlo (OT)","Haima (OT)","","","",""}),
        new(42, "P1", "Mistral Shriek", new[]{"Reprisal","","","","Sacred Soil","Zoe Shield","","","","Addle"}),
        new(51, "P1", "Friction I", new[]{"Party Mit (GNB/DRK)","","Plenary Indulgence","Collective Unconsious","Fey Illumination","Kerachole","Feint","","Party Mit",""}),
        new(58, "P1", "Friction II", new[]{"Party Mit (WAR/PLD)","","","","","","","Feint","",""}),
        new(77, "P1", "Aerial Blast", new[]{"","","GCD Heal and Shield after Feather Rain","","","","","","",""}),
        new(113, "P1", "Mistrel Song", new[]{"","Reprisal","","","","","","","",""}),
        new(132, "P1", "Super Cyclone", new[]{"Reprisal","","Plenary Indulgence","Collective Unconsious","Sacred Soil","Kerachole","","","",""}),
        new(150, "P2", "Hellfire", new[]{"","Reprisal","","","","","Feint","","","Addle"}),
        new(158, "P2", "Vulcan Burst", new[]{"","","","","GCD Shield","","","","",""}),
        new(174, "P2", "Infernal Nails", new[]{"Party Mit (WAR/PLD)","","","","Spreadlo + Fey Illumination","Zoe Shields","","","",""}),
        new(181, "P2", "Searing Wind and Chains", new[]{"Buddy Mit: Searing Wind Healer","","","","Excognition (Searing Wind Healer)","Haima (Searing Wind Healer)","Second Wind + Bloodbath","","",""}),
        new(189, "P2", "Infernal Surge (Eruption)", new[]{"Reprisal (Nails) + Party Mit (GNB/DRK)","Party Mit (GNB/DRK)","Plenary Indulgence","Earthly Star + Collective Unconsious","Sacred Soil","Kerachole","","Feint","Party Mit",""}),
        new(213, "P2", "Hellfire", new[]{"Do Not Mitigate!","","GCD Heal and Shield (Do Not Mitigate!)","","","","Do Not Mitigate!","","",""}),
        new(219, "P2", "Flaming Crush", new[]{"","Reprisal","","","","","Feint","","",""}),
        new(247, "P3", "Geocrush", new[]{"","","GCD Heal","","Sacred Soil","Kerachole","","","",""}),
        new(255, "P3", "Earthern Fury", new[]{"Reprisal","","","","","","","","",""}),
        new(261, "P3", "Rock Slide + Mountain Buster", new[]{"","","","","","","","Feint","",""}),
        new(262, "P3", "Tumult (x8)", new[]{"Party Mit","Reprisal + Party Mit","","","Sacred Soil","Kerachole","Feint","","","Addle"}),
        new(263, "P3", "Tumult (x6)", new[]{"Reprisal","","Plenary Indulgence","Collective Unconsious","Sacred Soil + Fey Ilumination","Kerachole","","","Party Mit",""}),
        new(264, "P3", "Rock Slide + Mountain Buster", new[]{"","","","","","Haima (MT)","","Feint","",""}),
        new(361, "P3", "Rock Slide + Mountain Buster", new[]{"","","","","","","Feint","","",""}),
        new(429, "P4", "Ultima", new[]{"LB3","","Do Not Heal Until After Tank Purge!","","Shields Only","","","","",""}),
        new(472, "P4", "Tank Purge", new[]{"Read notes and communicate with your party!","","","","","","","","",""}),
        new(486, "P4", "Viscous Aetheroplasm", new[]{"Kitchen Sink (Optional)","","","","Sacred Soil","Kerachole","","","",""}),
        new(537, "P4", "Tumult (x7)", new[]{"Party Mit (GNB/DRK)","","Plenary Indulgence","Collective Unconsious","Sacred Soil + Fey Illumination","Kerachole","","","Party Mit",""}),
        new(562, "P4", "Mistrel Shriek", new[]{"Party Mit (WAR/PLD)","","","","","","","","Carry Over",""}),
        new(587, "P4", "Mesohigh", new[]{"Buddy Mit: D3","","","","Spreadlo","","","","Second Wind",""}),
        new(596, "P4", "Searing Wind", new[]{"","","","","","Haima: Searing Wind Healer","","","",""}),
        new(605, "P4", "Mesohigh", new[]{"Buddy Mit: Searing Wind Healer","","Heal Yourself!","","","","","","",""}),
        new(608, "P4", "Tank Purge", new[]{"Reprisal","Party Mit","Plenary Indulgence","Collective Unconsious","Sacred Soil","Kerachole","Feint","","",""}),
        new(661, "P4", "Flaming Crush", new[]{"Party Mit (GNB/DRK)","","","","Sacred Soil","Kerachole","","","",""}),
        new(667, "P4", "Tank Purge", new[]{"Party Mit (WAR/PLD)","","GCD Heal and Shield!","","","","","","",""}),
        new(678, "P4", "Ultima", new[]{"Reprisal + LB3","","","","Fey Illumination","","","Feint","","Addle"}),
        new(688, "P4", "Aetheric Boom", new[]{"LB1","Reprisal","","","Sacred Soil","Kerachole","","","",""}),
        new(730, "P4", "Primal I", new[]{"","Party Mit","","","Sacred Soil","Kerachole","","","",""}),
        new(730, "P4", "Primal II", new[]{"","","Plenary Indulgence","Collective Unconsious","Spreadlo","Zoe Shields","","","Party Mit",""}),
        new(730, "P4", "Primal III", new[]{"Party Mit","","","","Sacred Soil","Kerachole","","","",""}),
    };

    static List<SyncPoint> UwuSync() => new()
    {
        new() { Ability = 0x2B54, Time = 42.0f, IsPhase = true , Label = "P1 Mistral Shriek" },
        new() { Ability = 0x2B48, Time = 51.4f, IsPhase = false, Label = "P1 Friction I" },
        new() { Ability = 0x2B48, Time = 57.5f, IsPhase = false, Label = "P1 Friction II" },
        new() { Ability = 0x2B55, Time = 76.9f, IsPhase = false, Label = "P1 Aerial Blast" },
        new() { Ability = 0x2B5E, Time = 150.2f, IsPhase = true , Label = "P2 Hellfire" },
        new() { Ability = 0x2B57, Time = 158.4f, IsPhase = false, Label = "P2 Vulcan Burst" },
        new() { Ability = 0x2B5A, Time = 188.9f, IsPhase = false, Label = "P2 Infernal Surge (Eruption)" },
        new() { Ability = 0x2B5E, Time = 213.1f, IsPhase = false, Label = "P2 Hellfire" },
        new() { Ability = 0x2B5D, Time = 219.0f, IsPhase = false, Label = "P2 Flaming Crush" },
        new() { Ability = 0x2CFD, Time = 247.2f, IsPhase = true , Label = "P3 Geocrush" },
        new() { Ability = 0x2B62, Time = 260.9f, IsPhase = false, Label = "P3 Rock Slide + Mountain Buster" },
        new() { Ability = 0x2B63, Time = 264.0f, IsPhase = false, Label = "P3 Rock Slide + Mountain Buster" },
        new() { Ability = 0x2B62, Time = 361.4f, IsPhase = true , Label = "P3 Rock Slide + Mountain Buster" },
        new() { Ability = 0x2B8B, Time = 428.7f, IsPhase = true , Label = "P4 Ultima" },
        new() { Ability = 0x2B87, Time = 472.5f, IsPhase = true , Label = "P4 Tank Purge" },
        new() { Ability = 0x2B7A, Time = 485.9f, IsPhase = false, Label = "P4 Viscous Aetheroplasm" },
        new() { Ability = 0x2B49, Time = 587.4f, IsPhase = true , Label = "P4 Mesohigh" },
        new() { Ability = 0x2B49, Time = 604.8f, IsPhase = false, Label = "P4 Mesohigh" },
        new() { Ability = 0x2B87, Time = 607.6f, IsPhase = false, Label = "P4 Tank Purge" },
        new() { Ability = 0x2B5D, Time = 661.3f, IsPhase = false, Label = "P4 Flaming Crush" },
        new() { Ability = 0x2B87, Time = 666.6f, IsPhase = false, Label = "P4 Tank Purge" },
        new() { Ability = 0x2B8B, Time = 678.0f, IsPhase = false, Label = "P4 Ultima" },
        new() { Ability = 0x2B88, Time = 688.2f, IsPhase = false, Label = "P4 Aetheric Boom" },
        new() { Ability = 0x2CD4, Time = 729.6f, IsPhase = true , Label = "P4 Primal I" },
    };

    // ===== Epic of Alexander (TEA) (territory 887) =====
    static readonly Entry[] Tea =
    {
        new(6, "P1", "Countdown", new[]{"Rampart (-6s) + Party Mit (WAR/PLD)","","Asylum (-10s)","","","","","","",""}),
        new(11, "P1", "Fluid Swing", new[]{"","","","","Sacred Soil","Kerachole","","","",""}),
        new(19, "P1", "Cascade", new[]{"Reprisal","","","Collective Unconscious","Carry Over","Carry Over","Feint","","",""}),
        new(37, "P1", "Fluid Swing/Strike", new[]{"Invulnerability + Provoke (Hand)","","","","","","","","",""}),
        new(42, "P1", "Hand of Pain", new[]{"120s","120s + Party Mit","Plenary Indulgence","Neutral Sect","Sprealdo + Sacred Soil","Holos + Kerachole","","","Party Mit + Personals","Personals"}),
        new(56, "P1", "Fluid Swing/Strike", new[]{"Short Mit","Short Mit + Reprisal","","","","","","","",""}),
        new(86, "P1", "Splash (x6) + Drainage", new[]{"Kitchen Sink + Reprisal + Party Mit (DRK/GNB)","Invulnerability (DRK Only) or Kitchen Sink","Asylum + Temperance","","Seraph + Fey Illumination + Soil","Panhaima + Kerachole","","Feint (Living Liquid)","","Addle (Living Liquid)"}),
        new(92, "P1", "Cascade", new[]{"Party Mit (WAR/PLD)","Rampart + 90s (DRK Only)","","Collective Unconscious","Carry Over","Carry Over","","","",""}),
        new(96, "P1", "Throttles", new[]{"","","Esuna (Top Down)","Esuna (Top Down)","Esuna (Bottom Up)","Esuna (Bottom Up)","","","The Warden's Paean (Bard)",""}),
        new(107, "P1", "Protean Wave", new[]{"Short Mit + Reprisal","Short Mit","","","Sacred Soil","Kerachole","","","",""}),
        new(144, "P2", "J Kick", new[]{"Party Mit","","","Collective Unconscious","Sacred Soil","Kerachole","","","Party Mit",""}),
        new(156, "P2", "Whirlwind", new[]{"Short Mit (Autos After) + Party Mit","Short Mit (Autos After) + Reprisal","","","Carry Over","Carry Over","Feint (Cruise Chaser)","","","Addle (Cruise Chaser)"}),
        new(180, "P2", "Photon", new[]{"","","Plenary Indulgence","","Spreadlo + Sacred Soil","Holos + Kerachole","","","",""}),
        new(195, "P2", "Water/Thunder I", new[]{"Rampart + Short Mit","Rampart (After Water Resolves)","","","Carry Over","Carry Over","","Feint (Brute Justice)","",""}),
        new(202, "P2", "Missile Command", new[]{"Reprisal","","","","Excognition (OT)","Haima (OT)","","","",""}),
        new(208, "P2", "Hidden Minefield", new[]{"","Kitchen Sink","","","Sacred Soil","Kerachole","","","",""}),
        new(225, "P2", "Water/Thunder II", new[]{"120s + Short Mit","Party Mit","Temperance","Neutral Sect","Seraph + Fey Illumination","Panhaima","","","",""}),
        new(246, "P2", "Whirlwind", new[]{"Party Mit (GNB/DRK)","Reprisal","","","Seraph + Sacred Soil","Kerachole","Feint (Cruise Chaser)","","Party Mit","Addle (Cruise Chaser)"}),
        new(254, "P2", "Water/Thunder III", new[]{"90s + Short Mit + Party Mit (WAR/PLD)","Short Mit","Plenary Indulgence","Collective Unconscious","","","","","Carry Over",""}),
        new(285, "P2", "Photon", new[]{"","","Benediction (MT)","Essential Dignity (MT)","Excognition (OT)","Taurochole (OT)","","","",""}),
        new(293, "P2", "Double Rocket Punch", new[]{"Rampart + Short Mit + Reprisal","Rampart + Short Mit","","","Sacred Soil","Kerachole","","Feint (Brute Justice)","",""}),
        new(317, "P2", "Whirlwind", new[]{"","Reprisal","","","","","Feint (Cruise Chaser)","","","Addle (Cruise Chaser)"}),
        new(333, "P3", "Chastening Heat + Divine Spear", new[]{"Provoke + Invulnerability (End of Castbar)","Provoke (After 3rd Divine Spear)","","","","","","","",""}),
        new(364, "P3", "Judgment Crystal + True Heart", new[]{"Party Mit","","Everything","","","","","","Party Mit",""}),
        new(387, "P3", "Flamethrower", new[]{"Short Mit","","","","","","","","",""}),
        new(410, "P3", "Super Jump", new[]{"Kitchen Sink","","","","","","","","",""}),
        new(423, "P3", "Chastening Heat + Divine Spear", new[]{"Provoke (After 3rd Divine Spear)","Kitchen Sink (Chastening Heat) + Invulnerability","","","","","","","",""}),
        new(482, "P3", "Incinerating Heat", new[]{"Party Mit","","","","","","","","",""}),
        new(495, "P3", "Mega Holy I", new[]{"Reprisal","","Plenary Indulgence","","Sacred Soil","Kerachole","Feint","","","Addle"}),
        new(502, "P3", "Mega Holy II", new[]{"Carry Over","","","","Carry Over","Carry Over","Carry Over","","","Carry Over"}),
        new(518, "P3", "J Storm", new[]{"","","","Collective Unconscious","Spreadlo","","","","Party Mit",""}),
        new(591, "P3", "J Waves (0-10s)", new[]{"","","","","","Panhaima","","","",""}),
        new(628, "P3", "J Waves (10-20s)", new[]{"","Reprisal","Temperance","Neutral Sect","Seraph + Fey Illumination + Sacred Soil","Holos + Kerachole","","Feint (Brute Justice)","",""}),
        new(647, "P3", "J Waves (20-30s)", new[]{"","Party Mit","Cure III","Aspected Helios","Succor","Eukrasian Prognosis","","","",""}),
        new(656, "P3", "Divine Judgement", new[]{"Party Mit","LB3","Medica II (When Z Appears) + Plenary Indulgence","Aspected Helios (When Z Appears)","Sacred Soil","Kerachole","","","",""}),
        new(694, "P4", "Optical Sight", new[]{"Short Mit + Reprisal + Party Mit","Party Mit","Everything","","","","Feint","","Party Mit","Addle"}),
        new(774, "P4", "Ordained Capital Punishment", new[]{"Kitchen Sink","Kitchen Sink + Reprisal","","","","","","Feint","",""}),
        new(777, "P4", "Ordained Punishment", new[]{"","Provoke (Castbar)","","","","","","","",""}),
        new(836, "P4", "Optical Sight (Fate Calibration β)", new[]{"Party Mit","Party Mit","Plenary Indulgence","Collective Unconscious","Sacred Soil","Kerachole","","","Party Mit",""}),
        new(874, "P4", "Ordained Capital Punishment", new[]{"","Invulnerability (Use Late)","","","","","","","",""}),
        new(877, "P4", "Ordained Punishment", new[]{"Provoke (Castbar)","","","","","","","","",""}),
        new(909, "P4", "Irresistible Grace", new[]{"Reprisal","","Temperance","Neutral Sect","Seraph + Spreadlo + Soil","Holos + Kerachole","Feint","","","Addle"}),
        new(919, "P4", "Ordained Capital Punishment", new[]{"Invulnerability (Use Late)","","","","","","","","",""}),
        new(922, "P4", "Ordained Punishment", new[]{"","Provoke (Castbar)","","","","","","","",""}),
        new(954, "P4", "Irresistible Grace", new[]{"Party Mit","Reprisal + Party Mit","Plenary Indulgence","Neutral Sect + Collective Unconscious","Seraph + Fey Illumination + Soil","Panhaima + Kerachole","","Feint","Party Mit",""}),
    };

    static List<SyncPoint> TeaSync() => new()
    {
        new() { Ability = 0x49B0, Time = 11.0f, IsPhase = true , Label = "P1 Fluid Swing" },
        new() { Ability = 0x4826, Time = 19.1f, IsPhase = false, Label = "P1 Cascade" },
        new() { Ability = 0x49B0, Time = 37.4f, IsPhase = false, Label = "P1 Fluid Swing/Strike" },
        new() { Ability = 0x482D, Time = 42.5f, IsPhase = false, Label = "P1 Hand of Pain" },
        new() { Ability = 0x49B0, Time = 56.5f, IsPhase = false, Label = "P1 Fluid Swing/Strike" },
        new() { Ability = 0x4827, Time = 86.5f, IsPhase = false, Label = "P1 Splash (x6) + Drainage" },
        new() { Ability = 0x4826, Time = 91.6f, IsPhase = false, Label = "P1 Cascade" },
        new() { Ability = 0x4828, Time = 95.8f, IsPhase = false, Label = "P1 Throttles" },
        new() { Ability = 0x4822, Time = 106.8f, IsPhase = false, Label = "P1 Protean Wave" },
        new() { Ability = 0x4854, Time = 143.7f, IsPhase = true , Label = "P2 J Kick" },
        new() { Ability = 0x49C2, Time = 155.8f, IsPhase = false, Label = "P2 Whirlwind" },
        new() { Ability = 0x4836, Time = 180.1f, IsPhase = false, Label = "P2 Photon" },
        new() { Ability = 0x4841, Time = 194.9f, IsPhase = false, Label = "P2 Water/Thunder I" },
        new() { Ability = 0x4851, Time = 208.3f, IsPhase = false, Label = "P2 Hidden Minefield" },
        new() { Ability = 0x4841, Time = 224.6f, IsPhase = false, Label = "P2 Water/Thunder II" },
        new() { Ability = 0x49C2, Time = 245.6f, IsPhase = false, Label = "P2 Whirlwind" },
        new() { Ability = 0x4841, Time = 254.3f, IsPhase = false, Label = "P2 Water/Thunder III" },
        new() { Ability = 0x4836, Time = 285.0f, IsPhase = false, Label = "P2 Photon" },
        new() { Ability = 0x4847, Time = 292.8f, IsPhase = false, Label = "P2 Double Rocket Punch" },
        new() { Ability = 0x49C2, Time = 316.7f, IsPhase = false, Label = "P2 Whirlwind" },
        new() { Ability = 0x4A80, Time = 333.2f, IsPhase = true , Label = "P3 Chastening Heat + Divine Spear" },
        new() { Ability = 0x485B, Time = 364.2f, IsPhase = false, Label = "P3 Judgment Crystal + True Heart" },
        new() { Ability = 0x484A, Time = 410.4f, IsPhase = false, Label = "P3 Super Jump" },
        new() { Ability = 0x4A80, Time = 422.6f, IsPhase = false, Label = "P3 Chastening Heat + Divine Spear" },
        new() { Ability = 0x4A51, Time = 482.0f, IsPhase = false, Label = "P3 Incinerating Heat" },
        new() { Ability = 0x4A83, Time = 494.9f, IsPhase = false, Label = "P3 Mega Holy I" },
        new() { Ability = 0x4A83, Time = 502.0f, IsPhase = false, Label = "P3 Mega Holy II" },
        new() { Ability = 0x4876, Time = 517.8f, IsPhase = false, Label = "P3 J Storm" },
        new() { Ability = 0x4892, Time = 773.8f, IsPhase = true , Label = "P4 Ordained Capital Punishment" },
        new() { Ability = 0x4893, Time = 776.9f, IsPhase = false, Label = "P4 Ordained Punishment" },
        new() { Ability = 0x4B14, Time = 836.2f, IsPhase = false, Label = "P4 Optical Sight (Fate Calibration β)" },
        new() { Ability = 0x4892, Time = 873.7f, IsPhase = false, Label = "P4 Ordained Capital Punishment" },
        new() { Ability = 0x4893, Time = 876.9f, IsPhase = false, Label = "P4 Ordained Punishment" },
        new() { Ability = 0x4894, Time = 909.2f, IsPhase = false, Label = "P4 Irresistible Grace" },
        new() { Ability = 0x4892, Time = 919.4f, IsPhase = false, Label = "P4 Ordained Capital Punishment" },
        new() { Ability = 0x4893, Time = 922.5f, IsPhase = false, Label = "P4 Ordained Punishment" },
        new() { Ability = 0x4894, Time = 954.1f, IsPhase = false, Label = "P4 Irresistible Grace" },
    };

    // ===== Dragonsong's Reprise (DSR) (territory 968) =====
    static readonly Entry[] Dsr =
    {
        new(15, "P2", "Heavenly Heel & Ascalon's Might", new[]{"Reprisal","","","","","","","","",""}),
        new(30, "P2", "Strength of the Ward", new[]{"Party Mit","Party Mit","Temperance + Liturgy of the Bell","Neutral Sect","Fey Illumination + Expedient","Holos","","","Party Mit",""}),
        new(70, "P2", "Ancient Quaga", new[]{"","Reprisal","Plenary Indulgence","Collective Unconsious","Carry Over + Sacred Soil","Carry Over + Kerachole","Feint","","","Addle"}),
        new(80, "P2", "Heavenly Heel & Ascalon's Might", new[]{"Reprisal","","","","","","","Feint","",""}),
        new(123, "P2", "Sancitity of the Ward", new[]{"","","","Macrocosmos","Seraph + Spreadlo","Zoe Shields + Panhaima + Kerachole","","","",""}),
        new(165, "P2", "Ultimate End", new[]{"Party Mit","Reprisal","Plenary Indulgence","Collective Unconsious","Sacred Soil","Kerachole","Feint","","","Addle"}),
        new(186, "P2", "Final Chorus", new[]{"","Party Mit","","Neutral Sect","Spreadlo + Sacred Soil","Zoe Shields + Holos + Kerachole","","","Party Mit",""}),
        new(252, "P3", "First Stack", new[]{"Reprisal","","Temperance","Carry Over (Neutral Sect)","Fey Illumination + Sacred Soil","Kerachole","","Feint","",""}),
        new(285, "P3", "Second Stack", new[]{"Party Mit (If Available)","Reprisal","Plenary Indulgence","Collective Unconscious","Seraph + Expedient + Sacred Soil (If Available","Panhaima + Kerachole (If Available)","Feint (If Available)","","","Addle (If Available)"}),
        new(318, "P3", "Resentment", new[]{"Party Mit (WAR/PLD after bleed expires)","","","Collective Unconscious","Sprealdo + Sacred Soil","Zoe Shields + Kerachole","","","",""}),
        new(351, "P3", "Orb Pops", new[]{"Reprisal","","Plenary Indulgence","Macrocosmos","","","","","Party Mit",""}),
        new(368, "P3", "Mirage Dives", new[]{"","","","","Seraph + Sacred Soil","Panhaima + Kerachole","","","",""}),
        new(384, "P3", "Steep in Rage", new[]{"","Reprisal","","","","","","","",""}),
        new(410, "Int", "Healers", new[]{"LB3 (3rd GCD) | WAR > DRK > PLD > GNB","","","","Spreadlo","Zoe Shields","","","",""}),
        new(423, "Int", "Melee", new[]{"Buddy Mit (D1) + Reprisal","Buddy Mit (D2)","Temperance + Liturgy of the Bell","Neutral Sect","Expedient","Holos","Feint","","",""}),
        new(430, "Int", "Range", new[]{"Party Mit","Party Mit","","","Fey Illumination + Sacred Soil","Kerachole","","","Party Mit",""}),
        new(433, "Int", "Tanks", new[]{"","Reprisal","Plenary Indulgence","Collective Unconscious","","","","Feint","","Addle"}),
        new(436, "Int", "Pure of Heart", new[]{"","","Benediction (Haurchefant)","","","","","","",""}),
        new(493, "P5", "Wrath of the Heavens", new[]{"","","","","Spreadlo + Seraph + Fey Illumination","Zoe Shields + Panhaima + Kerachole","","","",""}),
        new(526, "P5", "Ancient Quaga", new[]{"Party Mit (After Proteans)","Party Mit (After Proteans) + Reprisal","Plenary Indulgence","Collective Unconscious","Sacred Soil","Kerachole","Feint","","Party Mit (After Proteans)","Addle"}),
        new(536, "P5", "Heavenly Heel & Ascalon's Might", new[]{"Reprisal (If Not Warrior)","","","","","","","Feint (Without Warrior)","",""}),
        new(554, "P5", "Death of the Heavens", new[]{"","","Temperance","Neutral Sect","Expedient","Holos","","","",""}),
        new(608, "P5", "Ancient Quaga", new[]{"Party Mit","Party Mit + Reprisal","Plenary Indulgence","Collective Unconscious","Sacred Soil or Spreadlo","Kerachole","","Feint (With Warrior)","Party Mit",""}),
        new(622, "P5", "Heavenly Heel & Ascalon's Might", new[]{"Reprisal","","Assist Tanks","","","","Feint","","","Addle"}),
        new(656, "P6", "Wyrmsbreath I", new[]{"","","Plenary Indulgence","","Seraph","Kerachole","","","",""}),
        new(676, "P6", "Akh Afah", new[]{"","Reprisal","","Collective Unconscious","Seraph + Sacred Soil","Kerachole","","","",""}),
        new(704, "P6", "Wroth Flames", new[]{"Reprisal + Party Mit","Party Mit","Everything","Neutral Sect","Everything","Everything","Feint (Nidhogg)","Feint (Either Hallowed Plume)","Party Mit","Addle (Nidhogg)"}),
        new(745, "P6", "Akh Afah", new[]{"","Reprisal","","Collective Unconscious","Sacred Soil","Kerachole","","","",""}),
        new(774, "P6", "Wyrmsbreath II", new[]{"","","","","Seraph","","","","",""}),
        new(790, "P6", "Cauterize", new[]{"Reprisal","","Plenary Indulgence","Macrocosmos","Seraph + Sacred Soil","Kerachole","","","",""}),
        new(797, "P6", "Touchdown", new[]{"Limit Break (If Hraesvelgr Enrages)","","Benediction (DRK > WAR > GNB)","Microcosmos","Carry Over","Carry Over","","","",""}),
        new(852, "P7", "Alternative End", new[]{"Party Mit","Party Mit","Plenary Indulgence","Collective Unconscious","Spreadlo","Zoe Shields + Holos","","","Party Mit",""}),
        new(885, "P7", "Ahk Morn I", new[]{"Reprisal","","Temperance","Neutral Sect","Fey Illumination","Panhaima","Feint","","","Addle"}),
        new(919, "P7", "Gigaflare I", new[]{"","Reprisal","Plenary Indulgence","Collective Unconscious","Expedient + Seraph","","","Feint","",""}),
        new(959, "P7", "Ahk Morn II", new[]{"Reprisal + Party Mit","Party Mit","Liturgy of the Bell","","","Zoe Shields","","","Party Mit",""}),
        new(999, "P7", "Gigaflare II", new[]{"","Reprisal","Plenary Indulgence","Collective Unconscious + Macrocosmos","Spreadlo","","Feint","","","Addle"}),
        new(1039, "P7", "Akh Morn III", new[]{"Reprisal + Party Mit","Party Mit","Everything","","","","","Feint","Party Mit",""}),
    };

    static List<SyncPoint> DsrSync() => new()
    {
        new() { Ability = 0x63C5, Time = 15.3f, IsPhase = true , Label = "P2 Heavenly Heel & Ascalon's Might" },
        new() { Ability = 0x63D3, Time = 29.9f, IsPhase = false, Label = "P2 Strength of the Ward" },
        new() { Ability = 0x63C6, Time = 70.2f, IsPhase = false, Label = "P2 Ancient Quaga" },
        new() { Ability = 0x63C7, Time = 80.3f, IsPhase = false, Label = "P2 Heavenly Heel & Ascalon's Might" },
        new() { Ability = 0x63BE, Time = 165.2f, IsPhase = false, Label = "P2 Ultimate End" },
        new() { Ability = 0x6709, Time = 185.7f, IsPhase = true , Label = "P2 Final Chorus" },
        new() { Ability = 0x68BA, Time = 318.2f, IsPhase = true , Label = "P3 Resentment" },
        new() { Ability = 0x68BD, Time = 383.9f, IsPhase = false, Label = "P3 Steep in Rage" },
        new() { Ability = 0x62E4, Time = 436.3f, IsPhase = true , Label = "Int Pure of Heart" },
        new() { Ability = 0x6B89, Time = 493.3f, IsPhase = true , Label = "P5 Wrath of the Heavens" },
        new() { Ability = 0x63C6, Time = 525.6f, IsPhase = false, Label = "P5 Ancient Quaga" },
        new() { Ability = 0x63C7, Time = 536.1f, IsPhase = false, Label = "P5 Heavenly Heel & Ascalon's Might" },
        new() { Ability = 0x6B92, Time = 553.5f, IsPhase = false, Label = "P5 Death of the Heavens" },
        new() { Ability = 0x63C6, Time = 607.5f, IsPhase = true , Label = "P5 Ancient Quaga" },
        new() { Ability = 0x63C7, Time = 622.5f, IsPhase = false, Label = "P5 Heavenly Heel & Ascalon's Might" },
        new() { Ability = 0x6D35, Time = 656.3f, IsPhase = true , Label = "P6 Wyrmsbreath I" },
        new() { Ability = 0x6D42, Time = 675.6f, IsPhase = false, Label = "P6 Akh Afah" },
        new() { Ability = 0x6D45, Time = 703.8f, IsPhase = false, Label = "P6 Wroth Flames" },
        new() { Ability = 0x6D42, Time = 744.8f, IsPhase = false, Label = "P6 Akh Afah" },
        new() { Ability = 0x6D35, Time = 774.4f, IsPhase = false, Label = "P6 Wyrmsbreath II" },
        new() { Ability = 0x6D3F, Time = 790.0f, IsPhase = false, Label = "P6 Cauterize" },
        new() { Ability = 0x70E7, Time = 797.0f, IsPhase = false, Label = "P6 Touchdown" },
        new() { Ability = 0x7438, Time = 851.8f, IsPhase = true , Label = "P7 Alternative End" },
        new() { Ability = 0x6D99, Time = 918.8f, IsPhase = true , Label = "P7 Gigaflare I" },
    };

    // ===== The Omega Protocol (TOP) (territory 1122) =====
    static readonly Entry[] Top =
    {
        new(69, "P1", "Pantokrator", new[]{"Party Mit","","Everything","","","","","","",""}),
        new(104, "P1", "Stack I", new[]{"Reprisal","","","","","","Feint","","Party Mit",""}),
        new(121, "P1", "Stack II", new[]{"","","","","Seraph","","","","",""}),
        new(129, "P1", "Stack III", new[]{"","Reprisal","","","Seraph","","","Feint","","Addle"}),
        new(138, "P2", "Solar Ray", new[]{"","","Assist Tanks","Assist Tanks","Sacred Soil","Kerachole","","","",""}),
        new(214, "P2", "Optomized Meteor (6-1-1)", new[]{"Party Mit","","Everything","","","","","","Party Mit",""}),
        new(290, "P2", "Sniper Cannon Fodder (Transition)", new[]{"","","","","","Kerachole","","","",""}),
        new(303, "P3", "Hello World", new[]{"Reprisal","","Plenary Indulgence","Collective Unconscious","","","Feint","","","Addle"}),
        new(329, "P3", "1st Patch Set", new[]{"Party Mit","","","Earthly Star (Place 2 GCDs after Hello World)","Spreadlo + Sacred Soil","Zoe Shields + Kerachole","","","",""}),
        new(350, "P3", "2nd Patch Set", new[]{"","Reprisal","Temperance","Neutral Sect","Expedient + Seraph","Panhaima","","","",""}),
        new(371, "P3", "3rd Patch Set", new[]{"","","Liturgy of the Bell","Macrocosmos","Fey Illumination + Seraph + Sacred Soil","Kerachole","","Feint","Party Mit",""}),
        new(389, "P3", "4th Patch Set", new[]{"Reprisal","Party Mit","","","","Holos","","","",""}),
        new(411, "P3", "Critical Error", new[]{"","Reprisal","Plenary Indulgence","Collective Unconscious","Sacred Soil","Kerachole","Feint","","","Addle"}),
        new(449, "P4", "Protean I", new[]{"Reprisal","","","","Spreadlo","Zoe Shields","","","",""}),
        new(495, "P4", "Light Party Stacks I", new[]{"","","Plenary Indulgence","Collective Unconscious","Seraph + Sacred Soil","Kerachole","","","",""}),
        new(518, "P4", "Protean II", new[]{"Party Mit","","Temperance","Neutral Sect","Expedient","","","","",""}),
        new(529, "P4", "Light Party Stacks II", new[]{"","Party Mit","","","Seraph","Panhaima","","","",""}),
        new(535, "P4", "Protean III", new[]{"","Reprisal","","","","","","","",""}),
        new(538, "P4", "Critical Error", new[]{"","","","","Sacred Soil","Kerachole","Feint","","",""}),
        new(541, "P5", "Delta", new[]{"Reprisal","","Plenary Indulgence","Collective Unconscious","Spreadlo + Sacred Soil","Zoe Shields + Holos + Kerachole","","Feint","Party Mit","Addle"}),
        new(582, "P5", "Delta (During Mechanic)", new[]{"","","Liturgy of the Bell","Macrocosmos","Fey Illumination + Sacred Soil","Kerachole","","","",""}),
        new(623, "P5", "Sigma", new[]{"Reprisal + Party Mit","Party Mit","Temperance + Plenary Indulgence","Neutral Sect + Collective Unconscious","Seraph + Sacred Soil","Zoe Shields + Panhaima + Kerachole","Feint","","Party Mit",""}),
        new(667, "P5", "Sigma (During Mechanic)", new[]{"","","","","Seraph + Expedient","","","","",""}),
        new(712, "P5", "Omega", new[]{"Reprisal + Party Mit","Party Mit","Plenary Indulgence","Collective Unconscious","Spreadlo + Fey Illumination + Sacred Soil","Zoe Shields + Holos + Kerachole","","Feint","Party Mit","Addle"}),
        new(757, "P5", "Omega (During Mechanic)", new[]{"","","Temperance/Liturgy of the Bell","Macrocosmos + Neutral Sect","Seraph","Panhaima","","","",""}),
        new(801, "P5", "Blind Faith", new[]{"Reprisal","","Plenary Indulgence","Collective Unconscious","Sacred Soil","Kerachole","","","Party Mit",""}),
        new(870, "P6", "Cosmo Memory", new[]{"LB3 + Party Mit (GNB/DRK)","LB3 + Reprisal","","","Sacred Soil","Kerachole","","","",""}),
        new(899, "P6", "Cosmo Dive I", new[]{"Reprisal + Party Mit (WAR/PLD)","","Temperance","Neutral Sect","Seraph + Expedient + Fey Illumination","Panhaima + Kerachole","","","","Addle"}),
        new(908, "P6", "Protean I", new[]{"","Party Mit (GNB/DRK)","","","Sacred Soil","Kerachole + Zoe Shields (Wave Cannon)","","","Party Mit",""}),
        new(917, "P6", "Wave Cannon I", new[]{"","Reprisal + Party Mit (WAR/PLD)","Plenary Indulgence","Collective Unconscious","","","","","",""}),
        new(921, "P6", "Proteans II", new[]{"Party Mit (GNB/DRK)","","","","Spreadlo (Proteans) + Sacred Soil","Holos + Kerachole","","","",""}),
        new(925, "P6", "Wave Cannon II", new[]{"Reprisal + Party Mit (WAR/PLD)","","","Macrocosmos","","","","Feint","",""}),
        new(1013, "P6", "Cosmo Dive II", new[]{"","Reprisal","Plenary Indulgence","Collective Unconscious","Sacred Soil","Kerachole","Feint (D1) + Check That Limit Break III Is Available","","","Addle"}),
        new(1034, "P6", "Cosmo Meteor", new[]{"Reprisal + Buddy Mit (D3)","Party Mit + Buddy Mit (D4)","Temperance + Liturgy of the Bell","Neutral Sect","Seraph + Expedient + Fey Illumination","Zoe Shields + Panhaima","","","Party Mit",""}),
        new(1052, "P6", "Flares", new[]{"","","","","Spreadlo + Sacred Soil","Kerachole","","","",""}),
        new(1070, "P6", "Magic Number", new[]{"First LB3","Second LB3","Second LB3","","First LB3","","","","",""}),
    };

    static List<SyncPoint> TopSync() => new()
    {
        new() { Ability = 0x7B0B, Time = 69.3f, IsPhase = true , Label = "P1 Pantokrator" },
        new() { Ability = 0x7E6A, Time = 137.6f, IsPhase = true , Label = "P2 Solar Ray" },
        new() { Ability = 0x7B53, Time = 289.8f, IsPhase = true , Label = "P2 Sniper Cannon Fodder (Transition)" },
        new() { Ability = 0x7B55, Time = 303.0f, IsPhase = true , Label = "P3 Hello World" },
        new() { Ability = 0x7B63, Time = 329.0f, IsPhase = false, Label = "P3 1st Patch Set" },
        new() { Ability = 0x7B63, Time = 350.1f, IsPhase = false, Label = "P3 2nd Patch Set" },
        new() { Ability = 0x7B63, Time = 371.2f, IsPhase = false, Label = "P3 3rd Patch Set" },
        new() { Ability = 0x7B63, Time = 389.2f, IsPhase = false, Label = "P3 4th Patch Set" },
        new() { Ability = 0x7B64, Time = 411.4f, IsPhase = false, Label = "P3 Critical Error" },
        new() { Ability = 0x7B88, Time = 540.7f, IsPhase = true , Label = "P5 Delta" },
        new() { Ability = 0x8014, Time = 622.7f, IsPhase = false, Label = "P5 Sigma" },
        new() { Ability = 0x8015, Time = 712.2f, IsPhase = false, Label = "P5 Omega" },
        new() { Ability = 0x7B87, Time = 800.8f, IsPhase = false, Label = "P5 Blind Faith" },
        new() { Ability = 0x7BA1, Time = 869.8f, IsPhase = true , Label = "P6 Cosmo Memory" },
        new() { Ability = 0x7BA6, Time = 899.0f, IsPhase = false, Label = "P6 Cosmo Dive I" },
        new() { Ability = 0x7BAC, Time = 917.4f, IsPhase = false, Label = "P6 Wave Cannon I" },
        new() { Ability = 0x7BAF, Time = 925.4f, IsPhase = false, Label = "P6 Wave Cannon II" },
        new() { Ability = 0x7BA6, Time = 1012.9f, IsPhase = false, Label = "P6 Cosmo Dive II" },
        new() { Ability = 0x7BB0, Time = 1034.4f, IsPhase = false, Label = "P6 Cosmo Meteor" },
        new() { Ability = 0x7BB6, Time = 1069.8f, IsPhase = false, Label = "P6 Magic Number" },
    };

}
