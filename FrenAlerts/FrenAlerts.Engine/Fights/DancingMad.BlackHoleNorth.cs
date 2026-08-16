namespace FrenAlerts.Engine;

// Black hole tethers named against Kefka instead of against the arena.
//
// A group running this calls the side Kefka is on north and reads every hole from
// there, so the same true-north hole is a different call on every set. The reference
// is the pull's own KefkaDir, which phase three already works out from his heading:
// he stands dead centre for the black hole sets and only turns, so his position says
// nothing and his facing says everything.
//
// Measured on three pulls (2026-06-27, 06-25, 06-21 network logs, zone 553): the
// phase three boss actor is at 100,100 for every set, heading one of the eight.
public static partial class DancingMad
{
    // The tethered hole's direction, counted round from wherever Kefka is.
    //
    // Unknown when either end is missing, which is what makes the call fall back to
    // naming the mechanic rather than sending somebody to a real place on a guess.
    internal static string HoleNameFromKefka(in TriggerContext ctx)
    {
        var north = Pull(ctx).KefkaDir;
        if (north == DancingMadPull.Nowhere) return Compass.Unknown;

        var id = HoleEnd(ctx);
        return id == 0 ? Compass.Unknown : RelativeNorth.Name4(ctx.Actors.Where(id), north);
    }
}
