using System;
using System.Collections.Generic;

namespace FrenMits;

// Scheduled boss-reposition calls per fight: the moments the boss moves to a known
// spot (e.g. returns to Middle), on the FrenMits pull clock - the same axis the
// sheet and downtime windows use. The next-mits board shows each as its own cyan
// countdown row, so you get a heads-up before the boss repositions.
//
// Times are the same casts cactbot marks on its timeline, converted onto FrenMits'
// compressed clock the way Downtimes.cs does. `Where` is the spot shown on the row.
public static class Positions
{
    public readonly record struct Spot(float Time, string Where);

    private static readonly IReadOnlyList<Spot> None = Array.Empty<Spot>();

    public static IReadOnlyList<Spot> For(uint territory) => territory switch
    {
        Builtin.DmuTerritory => Dmu,
        _ => None,
    };

    // Dancing Mad - Kefka's "return to middle" casts (cactbot C3FD --middle--). The
    // three P1 times convert 1:1; the P4 and P4->P5 ones are anchored to the sheet's
    // P4/P5 mechanics (Death Bolt ~815, P5 resume ~911), so they're best estimates.
    private static readonly Spot[] Dmu =
    {
        new(25,  "Middle"),
        new(76,  "Middle"),
        new(180, "Middle"),
        new(816, "Middle"),
        new(908, "Middle"),
    };
}
