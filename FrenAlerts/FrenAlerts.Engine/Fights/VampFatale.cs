namespace FrenAlerts.Engine;

// What one pull of M9S has told us so far.
public sealed class VampFatalePull
{
    // Which of the four lanes the coffins are about to hit. Bounded at four,
    // because four lanes is the whole arena.
    public const int Lanes = 4;
    public readonly List<string> Coffins = new(Lanes);

    // Where the two flails landed. Bounded at the two the mechanic drops.
    public const int Flails = 2;
    public readonly List<Position> FlailSpots = new(Flails);
}

// M9S, Vamp Fatale.
//
// Only what the pack could not carry. The lane boundaries are upstream's: the
// arena splits at 95, 100 and 105 across, giving an outer and an inner lane on
// each side.
public static class VampFatale
{
    public const ushort Territory = 1321;

    private const uint Plummet = 0xB38B;

    private const uint HalfMoonRightFirst = 0xB377;
    private const uint BigHalfMoonRight = 0xB379;
    private const uint HalfMoonLeftFirst = 0xB37B;
    private const uint BigHalfMoonLeft = 0xB37D;

    private static readonly uint[] Coffinfillers = [0xB368, 0xB369, 0xB36A];

    private const string OuterWest = "outer west";
    private const string InnerWest = "inner west";
    private const string InnerEast = "inner east";
    private const string OuterEast = "outer east";

    private static readonly string[] AllLanes = [OuterWest, InnerWest, InnerEast, OuterEast];
    private static readonly string[] Inside = [InnerWest, InnerEast];
    private static readonly string[] Outside = [OuterWest, OuterEast];
    private static readonly string[] West = [InnerWest, OuterWest];
    private static readonly string[] East = [InnerEast, OuterEast];

    private static VampFatalePull Pull(in TriggerContext ctx) =>
        ctx.State.Remember<VampFatalePull>();

    // Which lane a coffin is standing in, off where it is across the arena.
    public static string LaneAt(float x) =>
        x < 95f ? OuterWest : x < 100f ? InnerWest : x < 105f ? InnerEast : OuterEast;

    public static IEnumerable<Trigger> Triggers()
    {
        // The coffins going up, which is half of what the cleave call needs.
        foreach (var id in Coffinfillers)
            yield return new Trigger
            {
                Id = $"m9s-coffinfiller-{id:X}",
                On = EventKind.CastStart,
                MatchId = id,
                Claims = true,
                OncePerBurst = false,
                Make = ctx =>
                {
                    var pull = Pull(ctx);
                    if (!ctx.Event.Source.Known || pull.Coffins.Count >= VampFatalePull.Lanes)
                        return null;
                    pull.Coffins.Add(LaneAt(ctx.Event.Source.X));
                    return null;
                },
            };

        // Two flails drop on corners, and each is near or far depending on how far
        // up the arena it is. Nothing is said until both are down.
        yield return new Trigger
        {
            Id = "m9s-plummet",
            Says = "right then left / left then right / near",
            On = EventKind.CastStart,
            MatchId = Plummet,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (!ctx.Event.Source.Known || pull.FlailSpots.Count >= VampFatalePull.Flails)
                    return null;

                pull.FlailSpots.Add(ctx.Event.Source);
                if (pull.FlailSpots.Count < VampFatalePull.Flails) return null;

                var said = Flails(pull.FlailSpots[0], pull.FlailSpots[1]);
                pull.FlailSpots.Clear();

                return new Call
                {
                    Text = said,
                    Time = ctx.Event.Time,
                    Key = "m9s-plummet",
                    Level = CallLevel.Info,
                    Hold = 6f,
                };
            },
        };

        foreach (var id in new[]
                 { HalfMoonRightFirst, BigHalfMoonRight, HalfMoonLeftFirst, BigHalfMoonLeft })
            yield return new Trigger
            {
                Id = $"m9s-half-moon-{id:X}",
                On = EventKind.CastStart,
                MatchId = id,
                OncePerBurst = false,
                Make = ctx =>
                {
                    var pull = Pull(ctx);
                    var text = HalfMoon(pull.Coffins, id, ctx.Event.Source);
                    pull.Coffins.Clear();
                    return text is null ? null : new Call
                    {
                        Text = text,
                        Time = ctx.Event.Time + ctx.Event.CastTime,
                        Key = "m9s-half-moon",
                        Level = CallLevel.Alert,
                        Hold = 6f,
                    };
                },
            };
    }

    // The whole cleave call: which side each half hits, and which lane survives it.
    //
    // With fewer than two coffins up there is no lane to name, so it is only the
    // two sides in the order they land.
    public static string? HalfMoon(IReadOnlyList<string> coffins, uint cast, Position at)
    {
        var rightFirst = cast is HalfMoonRightFirst or BigHalfMoonRight;

        if (coffins.Count < 2)
            return rightFirst ? "right then left" : "left then right";

        if (!at.Known) return null;

        // The cast faces the half it hits second; the first half is across from it.
        var second = Compass.Facing4(at.Heading);
        var first = Compass.Opposite4(second);

        // Whatever is hit in the first round is what is left standing for the
        // second, so the two sets are opposites of each other.
        var safeFirst = AllLanes.Where(l => !coffins.Contains(l)).ToList();
        var safeSecond = AllLanes.Where(coffins.Contains).ToList();

        return $"{Lane(Narrow(safeFirst, first))} {Side(first)} then " +
               $"{Lane(Narrow(safeSecond, second))} {Side(second)}";
    }

    // Where the two flails are, each as a corner and how far up the arena it sits.
    //
    // Upstream reads the second flail's distance off the FIRST one's position,
    // which makes both halves of its own call say the same thing. That is a slip
    // rather than the mechanic, so each one is read from itself here.
    public static string Flails(Position first, Position second) =>
        $"flails {Reach(first)} {Compass.Name8(Corner(first))} "
      + $"and {Reach(second)} {Compass.Name8(Corner(second))}";

    // Snapped to a corner, because these only ever land on the intercardinals.
    private static int Corner(Position at)
    {
        var dir8 = Compass.Dir8(at);
        return dir8 % 2 == 1 ? dir8 : Compass.Wrap(dir8 + 1, 8);
    }

    private static string Reach(Position at) =>
        MathF.Abs(at.Y - Compass.Middle) < 10f ? "near" : "far";

    // A cleave on a side takes that side's lanes out of the reckoning.
    private static List<string> Narrow(List<string> safe, int dir4) => dir4 switch
    {
        3 => safe.Where(West.Contains).ToList(),
        1 => safe.Where(East.Contains).ToList(),
        _ => safe,
    };

    // What is left, said as plainly as it can be: a whole ring if the survivors
    // are all one, otherwise the inner lane by name.
    private static string Lane(List<string> safe) =>
        safe.Count == 0 ? Compass.Unknown
        : safe.All(Inside.Contains) ? "inside"
        : safe.All(Outside.Contains) ? "outside"
        : safe.FirstOrDefault(Inside.Contains) ?? Compass.Unknown;

    // Said as the side you face plus the compass point, because half the raid
    // calls it one way and half the other.
    private static string Side(int dir4) => dir4 switch
    {
        3 => "left (west)",
        1 => "right (east)",
        _ => Compass.Name4(dir4),
    };
}
