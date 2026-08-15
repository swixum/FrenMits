using System;
using System.Collections.Generic;

namespace FrenMits.Callouts;

// One direction word for the whole library. The ring runs clockwise from north
// so a rotation is arithmetic on the ordinal, and Middle and Unknown sit past
// the ring so they can never be rotated into a real direction by accident.
public enum Way
{
    N = 0,
    NNE,
    NE,
    ENE,
    E,
    ESE,
    SE,
    SSE,
    S,
    SSW,
    SW,
    WSW,
    W,
    WNW,
    NW,
    NNW,

    // The floor's middle, which is an answer a mechanic can want.
    Middle,

    // Nothing knowable. Never spoken.
    Unknown,
}

// How many points to answer with. A fight that only has four safe spots must
// never be told an intercardinal.
public enum Ring
{
    Sixteen = 0,
    Eight,
    Cardinal,
    Intercard,
}

// Which side of a facing actor something is on, from that actor's own point of
// view: a boss's left is the boss's left, not the watcher's.
public enum Side
{
    Front = 0,
    FrontLeft,
    Left,
    BackLeft,
    Back,
    BackRight,
    Right,
    FrontRight,
    Unknown,
}

// The compass itself: positions to directions, headings to facings, and the
// arithmetic on top of both.
//
// Two conventions the whole library rests on, both read off real logs rather
// than assumed. North is negative on the log's second axis, so a bearing is
// atan2(east, south). Heading zero faces south and turns clockwise as it goes
// negative, so a facing vector is (sin h, cos h).
public static class Compass
{
    // Ring members, in clockwise order from north.
    public const int Points = 16;

    private static readonly string[] Shorts =
    [
        "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
        "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW",
    ];

    private static readonly string[] Names =
    [
        "north", "north northeast", "northeast", "east northeast",
        "east", "east southeast", "southeast", "south southeast",
        "south", "south southwest", "southwest", "west southwest",
        "west", "west northwest", "northwest", "north northwest",
    ];

    private static readonly string[] SideNames =
    [
        "front", "front left", "left", "back left",
        "back", "back right", "right", "front right",
    ];

    public static readonly IReadOnlyList<Way> Cardinals = [Way.N, Way.E, Way.S, Way.W];

    public static readonly IReadOnlyList<Way> Intercards = [Way.NE, Way.SE, Way.SW, Way.NW];

    public static readonly IReadOnlyList<Way> Eighths =
        [Way.N, Way.NE, Way.E, Way.SE, Way.S, Way.SW, Way.W, Way.NW];

    public static readonly IReadOnlyList<Way> Sixteenths =
    [
        Way.N, Way.NNE, Way.NE, Way.ENE, Way.E, Way.ESE, Way.SE, Way.SSE,
        Way.S, Way.SSW, Way.SW, Way.WSW, Way.W, Way.WNW, Way.NW, Way.NNW,
    ];

    // On the ring, so it can be rotated and measured. Middle is not.
    public static bool IsWay(this Way w) => w >= Way.N && w <= Way.NNW;

    public static bool IsCardinal(this Way w) => w.IsWay() && (int)w % 4 == 0;

    public static bool IsIntercard(this Way w) => w.IsWay() && (int)w % 4 == 2;

    // Banner text: short enough to read mid-pull.
    public static string Short(this Way w) => w switch
    {
        Way.Middle => "Mid",
        Way.Unknown => "",
        _ => w.IsWay() ? Shorts[(int)w] : "",
    };

    // Spoken text: a word a person would actually say.
    public static string Name(this Way w) => w switch
    {
        Way.Middle => "middle",
        Way.Unknown => "",
        _ => w.IsWay() ? Names[(int)w] : "",
    };

    public static string Name(this Side s)
        => s == Side.Unknown ? "" : SideNames[(int)s];

    // The heading that faces this way, in the log's own frame.
    public static float Angle(this Way w)
        => w.IsWay() ? Wrap(MathF.PI - (int)w * MathF.PI / 8f) : 0f;

    // A unit step in this direction, north being negative on the second axis.
    public static (float X, float Y) Step(this Way w)
    {
        if (!w.IsWay()) return (0f, 0f);
        var a = w.Angle();
        return (MathF.Sin(a), MathF.Cos(a));
    }

    // The spot that far from an origin in this direction.
    public static Spot From(this Way w, Spot origin, float distance)
    {
        if (!w.IsWay() || !origin.Known) return Spot.Nowhere;
        var (dx, dy) = w.Step();
        return new Spot(origin.X + dx * distance, origin.Y + dy * distance, origin.Z);
    }

    // Where a point lies as seen from a middle. This is the whole reason a call
    // can say "north": it is the arena's north, never the player's.
    public static Way Of(Spot at, Spot middle, Ring ring = Ring.Eight)
    {
        if (!at.Known || !middle.Known) return Way.Unknown;
        return Of(at.X - middle.X, at.Y - middle.Y, ring);
    }

    // Same, from an offset that is already relative to the middle.
    public static Way Of(float dx, float dy, Ring ring = Ring.Eight)
    {
        if (float.IsNaN(dx) || float.IsNaN(dy)) return Way.Unknown;
        if (dx == 0f && dy == 0f) return Way.Unknown;

        var turns = MathF.Atan2(dx, dy) / MathF.PI;
        return ring switch
        {
            Ring.Sixteen => Ring16(Round(8f - 8f * turns), 16, 1),
            Ring.Eight => Ring16(Round(4f - 4f * turns), 8, 2),
            Ring.Cardinal => Ring16(Round(2f - 2f * turns), 4, 4),
            Ring.Intercard => Ring16(Round(2f - 2f * (0.25f + turns)), 4, 4, 2),
            _ => Way.Unknown,
        };
    }

    // Which way an actor is looking, from the heading the log carries.
    public static Way Facing(float heading, Ring ring = Ring.Eight)
    {
        if (float.IsNaN(heading)) return Way.Unknown;

        var turns = heading / MathF.PI;
        return ring switch
        {
            Ring.Sixteen => Ring16(Round(8f - 8f * turns), 16, 1),
            Ring.Eight => Ring16(Round(4f - 4f * turns), 8, 2),
            Ring.Cardinal => Ring16(Round(2f - 2f * turns), 4, 4),
            Ring.Intercard => Ring16(Round(2f - 2f * (0.25f + turns)), 4, 4, 2),
            _ => Way.Unknown,
        };
    }

    public static Way Facing(this Actor a, Ring ring = Ring.Eight)
        => a.Known ? Facing(a.Heading, ring) : Way.Unknown;

    // Which side of a facing actor a spot sits on, from that actor's own view.
    // Turning left is the positive way round, which is what the log's heading
    // does as it grows.
    public static Side SideOf(Spot origin, float heading, Spot at)
    {
        if (!origin.Known || !at.Known || float.IsNaN(heading)) return Side.Unknown;

        var dx = at.X - origin.X;
        var dy = at.Y - origin.Y;
        if (dx == 0f && dy == 0f) return Side.Unknown;

        var rel = Wrap(MathF.Atan2(dx, dy) - heading);
        var eighth = (int)MathF.Floor(rel / (MathF.PI / 4f) + 0.5f);
        return (Side)(((eighth % 8) + 8) % 8);
    }

    public static Side SideOf(this Actor a, Spot at) => SideOf(a.At, a.Heading, at);

    // Straight across the floor.
    public static Way Opposite(this Way w) => w.Plus(8);

    // Turn clockwise by that many sixteenths; negative turns the other way.
    public static Way Plus(this Way w, int sixteenths)
        => w.IsWay() ? (Way)((((int)w + sixteenths) % Points + Points) % Points) : w;

    public static Way PlusEighths(this Way w, int eighths) => w.Plus(eighths * 2);

    public static Way PlusQuads(this Way w, int quads) => w.Plus(quads * 4);

    // The turn from one direction to another, clockwise positive, by the short
    // way round. Straight across is always positive eight, never negative.
    public static int SixteenthsTo(this Way from, Way to)
    {
        if (!from.IsWay() || !to.IsWay()) return 0;
        var raw = (int)to - (int)from;
        if (raw > 8) return raw - Points;
        if (raw <= -8) return raw + Points;
        return raw;
    }

    public static bool IsNextTo(this Way from, Way to)
        => from.IsWay() && to.IsWay() && Math.Abs(from.SixteenthsTo(to)) <= 2 && from != to;

    // The direction sitting halfway between two, for a mechanic that leaves two
    // safe spots: northeast and northwest leave north. Opposites leave nothing.
    public static Way Between(Way a, Way b)
    {
        if (!a.IsWay() || !b.IsWay()) return Way.Unknown;
        if (a == b) return a;

        var turn = a.SixteenthsTo(b);
        if (Math.Abs(turn) == 8 || turn % 2 != 0) return Way.Unknown;
        return a.Plus(turn / 2);
    }

    // Collapse to a coarser ring; a tie goes clockwise, always.
    public static Way Snap(this Way w, Ring ring)
    {
        if (!w.IsWay()) return w;
        var k = (int)w;
        return ring switch
        {
            Ring.Sixteen => w,
            Ring.Eight => (Way)(((int)MathF.Floor(k / 2f + 0.5f) * 2) % Points),
            Ring.Cardinal => (Way)(((int)MathF.Floor(k / 4f + 0.5f) * 4) % Points),
            Ring.Intercard => (Way)((((int)MathF.Floor((k - 2) / 4f + 0.5f) * 4 + 2) % Points + Points) % Points),
            _ => w,
        };
    }

    // The nearest of a set of directions, for a mechanic whose safe spots are
    // known but few. A tie goes clockwise.
    public static Way Nearest(this Way w, IReadOnlyList<Way> options)
    {
        if (!w.IsWay()) return Way.Unknown;

        var best = Way.Unknown;
        var bestTurn = int.MaxValue;
        foreach (var option in options)
        {
            if (!option.IsWay()) continue;
            var turn = w.SixteenthsTo(option);
            var far = Math.Abs(turn) * 2 + (turn < 0 ? 1 : 0);
            if (far >= bestTurn) continue;
            bestTurn = far;
            best = option;
        }
        return best;
    }

    // Sort directions clockwise starting from one of them, which is the order a
    // caller reads a list of spots out loud.
    public static IComparer<Way> ClockwiseFrom(Way start)
        => new Clockwise(start.IsWay() ? (int)start : 0);

    private sealed class Clockwise(int offset) : IComparer<Way>
    {
        public int Compare(Way a, Way b) => Rank(a).CompareTo(Rank(b));

        // Middle and Unknown sort last, in that order.
        private int Rank(Way w)
            => w.IsWay() ? (((int)w - offset) % Points + Points) % Points : Points + (int)w - (int)Way.Middle;
    }

    // A direction somebody typed, in whichever of the two spellings they used,
    // or Unknown for anything else. This is how an authored "northeast" joins
    // the same vocabulary as a worked out one instead of going out as loose
    // text nothing else can reason about.
    public static Way Parse(string text)
    {
        if (text.Length == 0) return Way.Unknown;

        var word = text.Trim();
        if (word.Equals("mid", StringComparison.OrdinalIgnoreCase)
            || word.Equals("middle", StringComparison.OrdinalIgnoreCase))
            return Way.Middle;

        for (var i = 0; i < Points; i++)
            if (word.Equals(Shorts[i], StringComparison.OrdinalIgnoreCase)
                || word.Equals(Names[i], StringComparison.OrdinalIgnoreCase))
                return (Way)i;

        return Way.Unknown;
    }

    // Radians into the half open turn the log uses, so a comparison never has to
    // worry which side of the seam it landed on.
    public static float Wrap(float radians)
    {
        if (float.IsNaN(radians)) return radians;
        while (radians > MathF.PI) radians -= 2f * MathF.PI;
        while (radians <= -MathF.PI) radians += 2f * MathF.PI;
        return radians;
    }

    // Half goes up, the same way every time, so a spot exactly on a boundary
    // always gets the clockwise answer instead of one that depends on the bit
    // pattern. Framework rounding goes to even, which would not.
    private static int Round(float v) => (int)MathF.Floor(v + 0.5f);

    // Index on a coarse ring back onto the sixteen point one.
    private static Way Ring16(int index, int modulo, int stride, int offset = 0)
        => (Way)((((index % modulo) + modulo) % modulo * stride + offset) % Points);
}
