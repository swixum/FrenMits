using System;
using System.Collections.Generic;

namespace FrenMits;

// Scheduled boss-reposition calls per fight, on the pull clock.
public static class Positions
{
    public readonly record struct Spot(float Time, string Where);

    private static readonly IReadOnlyList<Spot> None = Array.Empty<Spot>();

    public static IReadOnlyList<Spot> For(uint territory) => territory switch
    {
        Builtin.DmuTerritory => Dmu,
        _ => None,
    };

    // Dancing Mad: Kefka's five return-to-middle casts, median across six kills.
    private static readonly Spot[] Dmu =
    {
        new(25,  "Middle"),
        new(77,  "Middle"),
        new(181, "Middle"),
        new(739, "Middle"),
        new(851, "Middle"),
    };
}
