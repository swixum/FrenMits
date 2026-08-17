namespace FrenAlerts.Engine;

// Turning a spot on the arena into a word.
//
// Every direction call in this plugin comes through here, so the convention is
// written down once rather than re-derived at each call site and got backwards at
// one of them. North is negative Y, angles grow clockwise, and a number is only
// ever compared against another number produced the same way.
public static class Compass
{
    // Where the middle is on most arenas, and where every fight so far measures from.
    public const float Middle = 100f;

    private static readonly string[] Eight =
        ["north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest"];

    private static readonly string[] Four = ["north", "east", "south", "west"];

    // The in-between spots spoken rather than lettered, because the voice reads
    // "NNE" as three letters and the overlay is read at a glance either way.
    private static readonly string[] Sixteen =
    [
        "north", "north northeast", "northeast", "east northeast",
        "east", "east southeast", "southeast", "south southeast",
        "south", "south southwest", "southwest", "west southwest",
        "west", "west northwest", "northwest", "north northwest",
    ];

    // Clockwise from north in radians, which is the one angle everything else is
    // built on. X grows east and Y grows south, so north is negative Y.
    public static float Angle(float x, float y, float centerX = Middle, float centerY = Middle) =>
        MathF.Atan2(x - centerX, centerY - y);

    public static int Dir4(float x, float y, float centerX = Middle, float centerY = Middle) =>
        Wrap(Round(Angle(x, y, centerX, centerY), 4), 4);

    public static int Dir8(float x, float y, float centerX = Middle, float centerY = Middle) =>
        Wrap(Round(Angle(x, y, centerX, centerY), 8), 8);

    public static int Dir16(float x, float y, float centerX = Middle, float centerY = Middle) =>
        Wrap(Round(Angle(x, y, centerX, centerY), 16), 16);

    public static int Dir4(Position p, float centerX = Middle, float centerY = Middle) =>
        Dir4(p.X, p.Y, centerX, centerY);

    public static int Dir8(Position p, float centerX = Middle, float centerY = Middle) =>
        Dir8(p.X, p.Y, centerX, centerY);

    public static int Dir16(Position p, float centerX = Middle, float centerY = Middle) =>
        Dir16(p.X, p.Y, centerX, centerY);

    // Which way an actor is facing, in the same eight as everything else.
    //
    // A heading of zero is south in this game and grows the opposite way round from
    // the angles above, so this is the one place the sign flips. Getting it the
    // other way up turns every boss-facing call into its mirror image.
    public static int Facing8(float heading) =>
        Wrap((int)MathF.Round(4f - 4f * heading / MathF.PI), 8);

    // The same thing in four, for a mechanic that only ever cleaves a half.
    // Rounded in four rather than folded down from the eight, or a heading sitting
    // between two cardinals lands a whole quarter out.
    public static int Facing4(float heading) =>
        Wrap((int)MathF.Round(2f - 2f * heading / MathF.PI), 4);

    // The opposite side, which is what a boss standing at the edge and facing the
    // middle is asked for: it faces in, so where it stands is the way it is not
    // looking.
    public static int Opposite8(int dir8) => Wrap(dir8 + 4, 8);

    public static int Opposite4(int dir4) => Wrap(dir4 + 2, 4);

    // The eight and the four share the cardinals, and this is the only place that
    // conversion happens: 8-dir 2 is east and so is 4-dir 1.
    //
    // An intercardinal lands exactly halfway and has to go somewhere. It goes to the
    // next one round rather than to the nearest even, which is what the default
    // rounding would do and would send half of them backwards.
    public static int EightToFour(int dir8) =>
        Wrap((int)MathF.Round(dir8 / 2f, MidpointRounding.AwayFromZero), 4);

    public static int FourToEight(int dir4) => Wrap(dir4 * 2, 8);

    public static int EightToSixteen(int dir8) => Wrap(dir8 * 2, 16);

    public static string Name4(int dir4) => In(Four, dir4);

    public static string Name8(int dir8) => In(Eight, dir8);

    public static string Name16(int dir16) => In(Sixteen, dir16);

    // A spot nobody could work out is said as unknown rather than as north, which
    // is a real place on the arena and would send somebody to it.
    public const string Unknown = "unknown";

    private static string In(string[] names, int n) =>
        n >= 0 && n < names.Length ? names[n] : Unknown;

    // How far clockwise it is from one spot to another, counted in whichever ring
    // is being used. Never negative, so sorting by it reads clockwise.
    public static int ClockwiseGap(int from, int to, int of)
    {
        var gap = Wrap(to - from, of);
        return gap;
    }

    // The same spots, ordered clockwise, with a spot standing exactly on `from`
    // taken last.
    //
    // There are two of these on purpose, because the source has two. The towers in
    // the last phase are walked with the one you are standing on taken last; the
    // holes are counted with the one on the boss taken first. Using either rule for
    // both walks that mechanic's order round by one.
    public static IReadOnlyList<int> ClockwiseFrom(int from, IEnumerable<int> dirs, int of)
    {
        var list = dirs.ToList();
        list.Sort((a, b) => Distance(a).CompareTo(Distance(b)));
        return list;

        int Distance(int d)
        {
            var gap = ClockwiseGap(from, d, of);
            return gap == 0 ? of : gap;
        }
    }

    // The same, with a spot standing exactly on `from` taken first instead of last.
    //
    // This is the one the black holes are counted with. They were being read with
    // the rule above, which moves the whole order round by one on any set where a
    // hole sits on the boss's own side.
    public static IReadOnlyList<int> ClockwiseFromIncluding(int from, IEnumerable<int> dirs, int of)
    {
        var list = dirs.ToList();
        list.Sort((a, b) => ClockwiseGap(from, a, of).CompareTo(ClockwiseGap(from, b, of)));
        return list;
    }

    // Away from zero rather than to even, so a spot sitting exactly on the line
    // between two names always gets the same one of them.
    private static int Round(float angle, int of) =>
        (int)MathF.Round(angle / (MathF.Tau / of), MidpointRounding.AwayFromZero);

    // Negative inputs included, so a difference of directions is safe to wrap.
    public static int Wrap(int n, int of) => ((n % of) + of) % of;
}
