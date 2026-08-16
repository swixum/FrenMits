namespace FrenAlerts.Engine;

// Ifrit's dashes, which are called off the order the nails were killed in.
//
// The party kills four nails in a rotation, and that rotation is what decides
// where Ifrit dashes and therefore where it is safe to stand. Nothing about the
// dash itself says any of this, so the nail deaths have to be watched and the
// rotation worked out before the first dash lands.
public static class WeaponsRefrainIfrit
{
    // The nail's death blow.
    private const uint NailDeath = 0x2B58;

    // Crimson Cyclone, which is both the cast that opens Ifrit's phase and the
    // dash itself.
    public const uint CrimsonCyclone = 0x2B5F;

    public const int Nails = 4;

    // The plume cast that marks a cardinal off.
    private const uint RadiantPlume = 0x2B61;

    // How far off the middle counts as being at an edge rather than near it.
    private const float Edge = 5f;

    // Three cardinals gone leaves one, which is the whole call.
    private const int Cardinals = 4;

    private static WeaponsRefrainPull Pull(in TriggerContext ctx) =>
        ctx.State.Remember<WeaponsRefrainPull>();

    public static IEnumerable<Trigger> Triggers()
    {
        // Ifrit's first dash. He jumps to a cardinal well out from the middle, and
        // the dash runs through that whole axis, so both ends of it are gone.
        yield return new Trigger
        {
            Id = "uwu-ifrit-initial-dash",
            Says = "Ifrit someone",
            On = EventKind.ActorMoved,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.Phase != "ifrit" || pull.SaidInitialDash) return null;
                if (pull.IfritId == 0 || ctx.Event.SourceId != pull.IfritId) return null;
                if (!ctx.Event.Source.Known) return null;

                if (CardinalAt(ctx.Event.Source) is not { } dir) return null;
                pull.SaidInitialDash = true;

                // The dash crosses the arena, so the far side goes with it.
                pull.UnsafeCardinals.Add(dir);
                pull.UnsafeCardinals.Add(Compass.Opposite4(dir));

                return new Call
                {
                    Text = $"Ifrit {Compass.Name4(dir)}",
                    Time = ctx.Event.Time,
                    Key = "uwu-ifrit-initial-dash",
                    Level = CallLevel.Info,
                };
            },
        };

        // The plumes take a cardinal each. Once three are gone the last one is the
        // answer, and nothing is said before that because two gone leaves a choice.
        yield return new Trigger
        {
            Id = "uwu-ifrit-plumes",
            Says = "someone is safe",
            On = EventKind.CastStart,
            MatchId = RadiantPlume,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.SaidPlumeSafe || !ctx.Event.Source.Known) return null;

                if (PlumeCardinal(ctx.Event.Source) is { } dir) pull.UnsafeCardinals.Add(dir);
                if (pull.UnsafeCardinals.Count != Cardinals - 1) return null;

                var safe = Enumerable.Range(0, Cardinals)
                    .First(d => !pull.UnsafeCardinals.Contains(d));
                pull.SaidPlumeSafe = true;

                return new Call
                {
                    Text = $"{Compass.Name4(safe)} is safe",
                    Time = ctx.Event.Time,
                    Key = "uwu-ifrit-plumes",
                    Level = CallLevel.Alert,
                };
            },
        };

        // The four nails, in the order they died and where each one stood.
        yield return new Trigger
        {
            Id = "uwu-nail-deaths",
            On = EventKind.AbilityHit,
            MatchId = NailDeath,
            Claims = true,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.NailOrder.Count >= Nails || !ctx.Event.Source.Known) return null;
                if (pull.NailOrder.Any(n => n.Id == ctx.Event.SourceId)) return null;

                pull.NailOrder.Add((ctx.Event.SourceId, Compass.Dir8(ctx.Event.Source)));
                if (pull.NailOrder.Count == Nails) Settle(pull);
                return null;
            },
        };

        // Ifrit hiding is what starts each set of dashes, and which set it is
        // decides which call is made.
        yield return new Trigger
        {
            Id = "uwu-ifrit-dash-1",
            Says = "someone or someone",
            On = EventKind.NameToggle,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.Phase != "ifrit" || ctx.Event.Arg1 != 0) return null;
                if (pull.IfritId == 0 || ctx.Event.SourceId != pull.IfritId) return null;

                pull.IfritHidden++;
                if (pull.IfritHidden != 1) return null;
                if (FirstDashPair(pull) is not var (a, b)) return null;

                return new Call
                {
                    Text = $"{Compass.Name8(a)} or {Compass.Name8(b)}",
                    Time = ctx.Event.Time,
                    Key = "uwu-ifrit-dash-1",
                    Level = CallLevel.Alert,
                    Hold = 5f,
                };
            },
        };

        // The second set. Upstream waits two and a half seconds after Ifrit hides
        // and then asks where he is; this reads the dash's own cast instead, which
        // is the same moment and carries his position on the event rather than
        // needing a poll timed to catch it.
        yield return new Trigger
        {
            Id = "uwu-ifrit-dash-2",
            Says = "cw 90 to north, fast",
            On = EventKind.CastStart,
            MatchId = CrimsonCyclone,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.Phase != "ifrit" || pull.IfritHidden < 2) return null;
                if (pull.IfritId == 0 || ctx.Event.SourceId != pull.IfritId) return null;
                if (pull.SaidSecondDash || !ctx.Event.Source.Known) return null;
                if (pull.NailRotation.Length == 0) return null;

                var move = SecondDash(pull, Compass.Dir8(ctx.Event.Source));
                if (move is not { } m) return null;
                pull.SaidSecondDash = true;

                return new Call
                {
                    Text = $"{m.Turn} {m.Degrees} to {Compass.Name8(m.Dir)}, {m.Pace}",
                    Time = ctx.Event.Time,
                    Key = "uwu-ifrit-dash-2",
                    Level = CallLevel.Alert,
                    Hold = 5f,
                };
            },
        };
    }

    // Which cardinal Ifrit jumped to, or null if he is not out at an edge.
    public static int? CardinalAt(Position at)
    {
        if (at.X < Compass.Middle - Edge) return 3;
        if (at.X > Compass.Middle + Edge) return 1;
        if (at.Y < Compass.Middle - Edge) return 0;
        if (at.Y > Compass.Middle + Edge) return 2;
        return null;
    }

    // Which cardinal a plume takes out. Only the four that sit at the very edge
    // count; the rest of the ring is not a cardinal and takes nothing.
    public static int? PlumeCardinal(Position at)
    {
        const float Slack = 1f;

        if (MathF.Abs(at.X - Compass.Middle) < Slack)
        {
            if (MathF.Abs(at.Y - 83f) < Slack) return 0;
            if (MathF.Abs(at.Y - 118f) < Slack) return 2;
        }
        if (MathF.Abs(at.Y - Compass.Middle) < Slack)
        {
            if (MathF.Abs(at.X - 82f) < Slack) return 3;
            if (MathF.Abs(at.X - 118f) < Slack) return 1;
        }
        return null;
    }

    // Which way the kills went round, and where the first one was.
    //
    // The four nails sit on opposite pairs, so each is read modulo four: a valid
    // order steps one place round every time, in the same direction throughout.
    // Anything else is an order nobody can predict a dash from, and it is left
    // unset rather than guessed.
    public static void Settle(WeaponsRefrainPull pull)
    {
        string rotation = "";
        int? last = null;

        foreach (var (_, dir8) in pull.NailOrder)
        {
            var here = dir8 % 4;
            if (last is not { } was)
            {
                last = here;
                continue;
            }

            var clockwise = here - was == 1 || was - here == 3;
            var counter = was - here == 1 || here - was == 3;
            last = here;

            var step = clockwise ? "cw" : counter ? "ccw" : "";
            if (step.Length == 0) return;
            if (rotation.Length == 0) { rotation = step; continue; }
            if (step != rotation) return;
        }

        pull.NailRotation = rotation;
        pull.NailFirstDir = pull.NailOrder[0].Dir8;
    }

    // The two spots that are safe for the first set of dashes, which are always an
    // opposite pair on the intercardinals.
    public static (int, int)? FirstDashPair(WeaponsRefrainPull pull)
    {
        if (pull.NailRotation.Length == 0 || pull.NailFirstDir < 0) return null;

        var back = pull.NailRotation == "cw" ? 7 : 1;
        var onIntercard = pull.NailFirstDir % 2 == 1;

        // Already on an intercardinal is where the party stops; otherwise it turns
        // one place against the rotation to reach one.
        var first = onIntercard ? pull.NailFirstDir : Compass.Wrap(pull.NailFirstDir + back, 8);
        return (first, Compass.Opposite8(first));
    }

    public readonly record struct DashMove(string Turn, string Degrees, int Dir, string Pace);

    // Where to go for the second set, given where Ifrit actually dashed from.
    //
    // Ifrit takes one of four dashes in the rotation. Which one he is decides both
    // how far the party turns and whether it has time to walk it.
    public static DashMove? SecondDash(WeaponsRefrainPull pull, int ifritDir8)
    {
        if (pull.NailRotation.Length == 0 || pull.NailFirstDir < 0) return null;

        var forward = pull.NailRotation == "cw" ? 1 : -1;
        var start = Compass.Wrap(pull.NailFirstDir - forward, 8);
        var turn = pull.NailRotation == "cw" ? "clockwise" : "counterclockwise";

        for (var i = 1; i <= Nails; i++)
        {
            var dashDir = Compass.Wrap(start + i * forward, 8);

            // Dashes run through a direction and its opposite, so the pair Ifrit is
            // on is what identifies him rather than the exact eighth.
            if (dashDir % 4 != ifritDir8 % 4) continue;

            // First and third are a 45 degree turn, the others 90.
            var swing = i is 1 or 3 ? forward : forward * 2;
            var to = Compass.Wrap(start + swing, 8);

            return new DashMove(
                turn,
                i is 1 or 3 ? "45" : "90",
                to,
                i <= 2 ? "fast" : "slow");
        }
        return null;
    }
}
