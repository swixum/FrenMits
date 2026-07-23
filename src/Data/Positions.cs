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

    // Dancing Mad - Kefka's "return to middle" casts (C3FD). There are exactly FIVE
    // in a clear, and these times are the median of the C3FD cast across six top
    // FFLogs kills - fight-relative seconds, which IS this pull clock, so no
    // conversion. The last two were previously mis-estimated (816/908); the real
    // ones are 739 (early P4, just after Kefka returns at 725) and 851 (mid-P4, just
    // before the P4->P5 lull). There is NO return-to-middle in P5, so P5 shows none.
    private static readonly Spot[] Dmu =
    {
        new(25,  "Middle"),
        new(77,  "Middle"),
        new(181, "Middle"),
        new(739, "Middle"),
        new(851, "Middle"),
    };
}
