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
        },
    };

    public static IReadOnlyList<Extra> For(uint territory)
        => ByZone.TryGetValue(territory, out var e) ? e : Array.Empty<Extra>();

    public static Extra? For(uint territory, string? job)
        => string.IsNullOrEmpty(job)
            ? null
            : For(territory).FirstOrDefault(e => string.Equals(e.Job, job, StringComparison.OrdinalIgnoreCase));
}
