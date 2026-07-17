using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// Optional, job-specific mitigation timers derived from FFLogs clears (raalm.com),
// the same way the WHM Asylum line was. Offered as a one-click add on the fight
// page when you're on that job. Lines are job-restricted (only fire for that job)
// and flagged Custom, so a sheet re-bake keeps them.
//
// Every schedule is spaced to the ability's real recast - no call ever asks you to
// press something that's still on cooldown.
public static class JobExtras
{
    public sealed record Extra(string Job, string Action, float Recast, (int Time, string Mechanic)[] Lines);

    private static readonly Dictionary<uint, Extra[]> ByZone = new()
    {
        [Builtin.DmuTerritory] = new[]
        {
            // Bard - Nature's Minne (120s recast)
            new Extra("BRD", "Nature's Minne", 120f, new[]
            {
                (63, "Light of Judgment"),
                (249, "Towers I"),
                (451, "Bowels of Agony (Chaos)"),
                (637, "Thunder III (5th Set)"),
                (789, "Grand Cross"),
                (922, "Chaotic Flood"),
                (1046, "Fell Forces (3x)"),
            }),
            // Monk - Mantra (90s recast)
            new Extra("MNK", "Mantra", 90f, new[]
            {
                (88, "Gravitas II (Part I)"),
                (237, "Forsaken"),
                (451, "Bowels of Agony (Chaos)"),
                (544, "The Decisive Battle"),
                (650, "Black Holes III (6th Tether Set)"),
                (765, "Inferno/Tsunami"),
                (905, "Ultima Repeater"),
            }),
            // Paladin - Passage of Arms (120s recast)
            new Extra("PLD", "Passage of Arms", 120f, new[]
            {
                (63, "Light of Judgment"),
                (342, "Light of Judgement"),
                (609, "Shocking Impact"),
                (789, "Grand Cross"),
                (922, "Chaotic Flood"),
            }),
            // Dancer - Curing Waltz (60s recast). Windows from raalm.com m-spec
            // top-100 kill logs (phase-normalized cluster medians; % = share of
            // kills pressing there): 89s 81%, 327s 77%, 506s 46%, 760s 84%, 992s 88%.
            new Extra("DNC", "Curing Waltz", 60f, new[]
            {
                (87, "Gravitas II (Part I)"),
                (322, "Towers VIII (Past/Future's End)"),
                (506, "Ultima Blaster"),
                (762, "Grand Cross"),
                (992, "Ultima Repeater"),
            }),
            // Machinist - Dismantle (120s recast). raalm top-100 windows: 60s 97%,
            // 233s 96%, 552s 89%, 906s 61%, 1055s 96%. (Logs split 50/61 between
            // Death Bolt/Wave and Ultima Repeater for the 4th press - 84s apart,
            // can't have both on a 120s recast - so the majority pick ships.)
            new Extra("MCH", "Dismantle", 120f, new[]
            {
                (62, "Light of Judgment"),
                (235, "Forsaken"),
                (558, "Earthquake"),
                (910, "Ultima Repeater"),
                (1061, "Forsaken (1st Hit)"),
            }),
            // Red Mage - Magick Barrier (120s recast). raalm top-100 windows:
            // 38s 72%, 268s 94%, 516s 95%, 771s 76%, 904s 93%, 1056s 97%.
            new Extra("RDM", "Magick Barrier", 120f, new[]
            {
                (37, "Mystery Magic"),
                (270, "Towers III (All Things Ending)"),
                (517, "Cyclone"),
                (768, "Inferno/Tsunami"),
                (910, "Ultima Repeater"),
                (1061, "Forsaken (1st Hit)"),
            }),
        },
    };

    public static IReadOnlyList<Extra> For(uint territory)
        => ByZone.TryGetValue(territory, out var e) ? e : Array.Empty<Extra>();

    public static Extra? For(uint territory, string? job)
        => string.IsNullOrEmpty(job)
            ? null
            : For(territory).FirstOrDefault(e => string.Equals(e.Job, job, StringComparison.OrdinalIgnoreCase));
}
