using System;
using System.Collections.Generic;

namespace FrenMits;

// Scheduled boss-reposition calls per fight: the moments the boss moves to a known
// spot (e.g. returns to Middle), on the FrenMits pull clock, shown on the next-mits
// board as a cyan countdown row.
//
// Times are the same casts cactbot marks on its timeline, converted onto FrenMits'
// compressed clock the way Downtimes.cs does.
public static class Positions
{
    public readonly record struct Spot(float Time, string Where);

    private static readonly IReadOnlyList<Spot> None = Array.Empty<Spot>();

    public static IReadOnlyList<Spot> For(uint territory) => territory switch
    {
        Builtin.DmuTerritory => Dmu,
        _ => None,
    };

    // Dancing Mad - Kefka's "return to middle" casts (C3FD); exactly FIVE in a
    // clear, timed as the median of the C3FD cast across six top logs kills
    // (fight-relative seconds, which IS this pull clock).
    private static readonly Spot[] Dmu =
    {
        new(25,  "Middle"),
        new(77,  "Middle"),
        new(181, "Middle"),
        new(739, "Middle"),
        new(851, "Middle"),
    };
}
