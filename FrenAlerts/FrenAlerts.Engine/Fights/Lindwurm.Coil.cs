namespace FrenAlerts.Engine;

// M12S's cursed coil, and the chain calls that are read off where it has turned to.
//
// The coil starts on a cardinal and turns counterclockwise one place every
// skinsplitter, so where it is now is the start minus how many have gone by. That
// running answer is the exit for the chain calls.
public static class LindwurmCoil
{
    // Which cardinal each coil cast starts on.
    private static readonly Dictionary<uint, int> CoilStart = new()
    {
        [0xB4BA] = 0, // north
        [0xB4B8] = 1, // east
        [0xB4BB] = 2, // south
        [0xB4B9] = 3, // west
    };

    // Skinsplitter, one every five seconds, each turning the coil one place.
    private const uint Skinsplitter = 0xB4BC;

    // The two unbreakable flesh statuses, alpha and beta.
    private const uint UnbreakableAlpha = 0x1291;
    private const uint UnbreakableBeta = 0x1293;

    // The blob prop, whose spawn spots say which corner survives Curtain Call.
    private const uint Blob = 0x1EBF29;

    private static LindwurmPull Pull(in TriggerContext ctx) =>
        ctx.State.Remember<LindwurmPull>();

    // Where the coil has turned to, counting the skinsplitters that have gone by.
    //
    // It turns counterclockwise, so this subtracts. Eight rather than four because
    // the subtraction goes negative and eight is the next multiple of four above
    // the number of turns the mechanic has.
    public static int? ExitFrom(int? start, int turns) =>
        start is not { } from ? null : ((from - turns) + 8) % 4;

    public static IEnumerable<Trigger> Triggers()
    {
        // Which way the coil started.
        foreach (var (cast, dir) in CoilStart)
            yield return new Trigger
            {
                Id = $"m12s-coil-{cast:X}",
                On = EventKind.CastStart,
                MatchId = cast,
                Claims = true,
                Make = ctx =>
                {
                    Pull(ctx).CoilStart = dir;
                    return null;
                },
            };

        // Each one turns it a place.
        yield return new Trigger
        {
            Id = "m12s-skinsplitter",
            On = EventKind.AbilityHit,
            MatchId = Skinsplitter,
            Claims = true,
            Hush = 1f,
            Make = ctx =>
            {
                Pull(ctx).Skinsplitters++;
                return null;
            },
        };

        // Where the blobs land says which corner Curtain Call leaves open. Only the
        // one on that row counts; the rest of the pattern is a different mechanic.
        yield return new Trigger
        {
            Id = "m12s-curtain-corner",
            On = EventKind.ActorSpawn,
            MatchId = Blob,
            Claims = true,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.CurtainCorner >= 0 || !ctx.Event.Source.Known) return null;

                var at = ctx.Event.Source;
                if (at.Y is <= 86.5f or >= 87.5f) return null;

                if (at.X < 92f) pull.CurtainCorner = 1;        // northeast
                else if (at.X > 109f) pull.CurtainCorner = 7;  // northwest
                return null;
            },
        };

        yield return Chains("m12s-unbreakable-alpha", UnbreakableAlpha, alpha: true);
        yield return Chains("m12s-unbreakable-beta", UnbreakableBeta, alpha: false);
    }

    // What to do with your chain, which pair you are, and where to go after.
    private static Trigger Chains(string id, uint status, bool alpha) => new()
    {
        Id = id,
        Says = "break chains, northwest or south",
        On = EventKind.StatusGain,
        MatchId = status,
        OnlyMe = true,
        Make = ctx =>
        {
            var pull = Pull(ctx);
            if (pull.MyNumber == 0) return null;

            // Curtain Call gives everyone alpha, and there the call is a pair of
            // corners rather than a chain number and an exit.
            if (alpha && pull.CurtainCorner >= 0)
            {
                var other = pull.CurtainCorner == 7 ? 3 : 5;
                return new Call
                {
                    Text = $"break chains, {Compass.Name8(pull.CurtainCorner)} "
                         + $"or {Compass.Name8(other)}",
                    Time = ctx.Event.Time,
                    Key = id,
                    Level = CallLevel.Alert,
                    Personal = true,
                    Hold = 8f,
                };
            }

            var exit = ExitFrom(pull.CoilStart, pull.Skinsplitters);
            var where = exit is { } e ? Compass.Name4(e) : Compass.Unknown;
            var num = pull.MyNumber;

            return new Call
            {
                Text = alpha ? Alpha(num, where, pull) : Beta(num, where),
                Time = ctx.Event.Time,
                Key = id,
                Level = CallLevel.Alert,
                Personal = true,
                Hold = 8f,
            };
        },
    };

    // Ones and twos take the outer pair of blob towers; threes and fours get out.
    private static string Alpha(int num, string exit, LindwurmPull pull)
    {
        if (num > 2) return $"break chains {num} ({exit}), then get out";

        var index = num + 1;
        var tower = index < pull.BlobTowers.Count
            ? $", outer {Compass.Name8(pull.BlobTowers[index])}"
            : "";
        return $"break chains {num} ({exit}), then blob tower {index + 1}{tower}";
    }

    private static string Beta(int num, string exit) => num switch
    {
        1 or 2 => $"break chains {num} ({exit}), then get middle",
        3 => $"break chains {num} ({exit}), then wait for the last pair",
        _ => $"break chains {num} ({exit}), then get out",
    };
}
