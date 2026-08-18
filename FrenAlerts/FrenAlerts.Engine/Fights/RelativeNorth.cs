namespace FrenAlerts.Engine;

// Naming a spot by where the boss is looking rather than by where the arena's north
// is.
//
// A group that reads a mechanic off the boss wants "the one in front of him", and
// the arena's north is no use for that: the boss turns between sets, so the same
// true-north spot is a different job each time. Measured on Dancing Mad's phase 3
// black holes, Kefka stands dead centre for every set and only his heading changes,
// which is why this reads the heading and not the position. Eleven sets across three
// pulls (2026-06-27, 06-25, 06-21 logs): position 100,100 every time, heading one of
// 0, +-0.79, +-1.57, +-2.36, 3.14.
//
// A boss standing in the middle has no direction from the middle, so a convention
// built on its position would name every set the same and be silently wrong.
public static class RelativeNorth
{
    // Which way the group is calling north, taken from the boss's own facing.
    public static int Facing(float heading) => Compass.Facing8(heading);

    // The other reading of the same convention: some groups put north at the boss's
    // back rather than its face. One call away, so switching costs a line.
    public static int Back(float heading) => Compass.Opposite8(Compass.Facing8(heading));

    // A spot named against that north, in the same eight words as every other
    // direction call.
    //
    // Unknown rather than a guess when either end is missing: north is a real place
    // on the arena and sending somebody to it on a default would be worse than
    // saying nothing. The caller drops the direction and keeps the mechanic's name.
    public static string Name(Position spot, int north,
        float centerX = Compass.Middle, float centerY = Compass.Middle)
    {
        if (!spot.Known || north < 0 || north >= 8) return Compass.Unknown;
        var dir = Compass.Dir8(spot, centerX, centerY);
        return Compass.Name8(Compass.ClockwiseGap(north, dir, 8));
    }

    // The whole thing for a boss that turns on the spot: where the thing is, and
    // which way the boss was looking when it appeared.
    public static string Name(Position spot, Position boss,
        float centerX = Compass.Middle, float centerY = Compass.Middle)
        => spot.Known && boss.Known
            ? Name(spot, Facing(boss.Heading), centerX, centerY)
            : Compass.Unknown;

    // The same, in the four a mechanic sitting on the cardinals is read in.
    //
    // The reference is still given in the eight, because it comes off a heading and
    // a boss can face a diagonal: rounding it to a quadrant first throws away half
    // the turn and names the spot next door.
    public static string Name4(Position spot, int north8,
        float centerX = Compass.Middle, float centerY = Compass.Middle)
    {
        if (!spot.Known || north8 < 0 || north8 >= 8) return Compass.Unknown;
        var dir = Compass.Dir8(spot, centerX, centerY);
        var turned = Compass.ClockwiseGap(north8, dir, 8);
        // Only a spot that lands back on a cardinal has a four-way word for it. A
        // boss facing a diagonal turns the cardinals into diagonals, and there is no
        // honest way to say that in four.
        return turned % 2 == 0 ? Compass.Name4(Compass.EightToFour(turned)) : Compass.Name8(turned);
    }

    // The same, for a spot already reduced to one of the four rather than left as a
    // place on the floor.
    //
    // A mechanic that has already been sorted into an order holds directions, not
    // positions, and naming the nth of those means turning a direction rather than
    // looking one up. Going back to a position to do it names whichever prop the
    // packet happened to carry instead of the one the order picked.
    public static string Name4(int dir4, int north8)
    {
        if (dir4 < 0 || dir4 >= 4 || north8 < 0 || north8 >= 8) return Compass.Unknown;
        var turned = Compass.ClockwiseGap(north8, Compass.FourToEight(dir4), 8);
        return turned % 2 == 0 ? Compass.Name4(Compass.EightToFour(turned)) : Compass.Name8(turned);
    }

    // The same spot in the arena's own words, for the groups that call true north.
    public static string True(Position spot,
        float centerX = Compass.Middle, float centerY = Compass.Middle)
        => spot.Known ? Compass.Name8(Compass.Dir8(spot, centerX, centerY)) : Compass.Unknown;

    // Whether a direction could be worked out at all, so a caller can fall back to
    // the mechanic's bare name rather than putting "unknown" on screen.
    public static bool Known(string named) => named != Compass.Unknown;
}
