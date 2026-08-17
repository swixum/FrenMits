namespace FrenAlerts.Engine;

public sealed class TyrantCometPull
{
    // Whether this player is carrying Atomic Impact, which changes the meteor call
    // from "get middle" to a pair of corners.
    public bool HasAtomic;

    // How many maelstroms have appeared. The fourth is the one worth calling.
    public int Maelstroms;

    // Dance of Domination. Each explosion takes one outer cardinal away, and the
    // call is made the moment a single one is left. Bounded at the four it starts
    // with, and emptied once it has spoken so the rest of the volley stays quiet.
    public readonly HashSet<int> OuterSafe = [0, 1, 2, 3];
    public int VertCount;
    public int HorizCount;
}

// M11S, The Tyrant & Comet.
public static class TyrantComet
{
    public const ushort Territory = 1325;

    private const uint AtomicImpact = 0x001E;

    // Maelstrom's name id, not its base id: upstream matches this add by name and
    // no base id for it appears in any kill. 14307 sits in this tier's own block,
    // right after the Tyrant on 14305 and Vamp Fatale on 14300.
    private const uint Maelstrom = 14307;

    // How many are up before the gust needs baiting.
    private const int GustAfter = 4;
    private const uint MammothMeteor = 0xB453;

    private const uint Explosion = 0xB7BC;

    // How far off the middle a charge has to sit before it counts as taking a side
    // away, rather than being one of the ones through the middle. Upstream's number.
    private const float OffCenter = 5f;

    private static TyrantCometPull Pull(in TriggerContext ctx) =>
        ctx.State.Remember<TyrantCometPull>();

    public static IEnumerable<Trigger> Triggers()
    {
        // Dance of Domination, carried over as upstream reads it: each explosion is
        // a charge across the arena, and which side it runs down is read from the
        // caster's own heading and where it stands. Only the four square-on ones
        // count; a diagonal is not taking a cardinal away.
        yield return new Trigger
        {
            Id = "m11s-dance-of-domination-safe",
            Says = "N/S Mid / east Outer + Partner Stacks",
            On = EventKind.CastStart,
            MatchId = Explosion,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                var at = ctx.Event.Source;
                if (!at.Known || pull.OuterSafe.Count == 0) return null;

                var facing = Compass.Facing8(at.Heading);
                if (facing % 2 != 0) return null;

                // North or south facing runs the charge up and down, which is what
                // takes an east or west outer away, and the other way round.
                var upDown = facing % 4 == 0;
                int? danger = upDown
                    ? at.X < Compass.Middle - OffCenter ? 3
                        : at.X > Compass.Middle + OffCenter ? 1 : null
                    : at.Y < Compass.Middle - OffCenter ? 0
                        : at.Y > Compass.Middle + OffCenter ? 2 : null;

                if (upDown) pull.VertCount++;
                else pull.HorizCount++;

                if (danger is { } gone) pull.OuterSafe.Remove(gone);
                if (pull.OuterSafe.Count != 1) return null;

                var safe = Compass.Name4(pull.OuterSafe.First());
                var mid = pull.VertCount == 1 ? "N/S" : pull.HorizCount == 1 ? "E/W" : "";
                // Said once and then never again this volley.
                pull.OuterSafe.Clear();
                if (mid.Length == 0) return null;

                return new Call
                {
                    Text = $"{mid} Mid / {safe} Outer + Partner Stacks",
                    Time = ctx.Event.Time,
                    Key = "m11s-dance-of-domination-safe",
                    Level = CallLevel.Info,
                };
            },
        };

        yield return new Trigger
        {
            Id = "m11s-atomic-impact-mine",
            On = EventKind.HeadMarker,
            MatchId = AtomicImpact,
            OnlyMe = true,
            Claims = true,
            Make = ctx =>
            {
                Pull(ctx).HasAtomic = true;
                return null;
            },
        };

        // The fourth maelstrom is the cue to go and bait the gust.
        yield return new Trigger
        {
            Id = "m11s-bait-gust",
            Says = "bait the gust",
            On = EventKind.ActorSpawn,
            OncePerBurst = false,
            Make = ctx =>
            {
                if (ctx.Event.Arg2 != Maelstrom) return null;

                var pull = Pull(ctx);
                return ++pull.Maelstroms != GustAfter ? null : new Call
                {
                    Text = "bait the gust",
                    Time = ctx.Event.Time,
                    Key = "m11s-bait-gust",
                    Level = CallLevel.Info,
                };
            },
        };

        // The meteors land on an opposite pair of corners, so seeing one is enough
        // to know both, and the other pair is where you go.
        yield return new Trigger
        {
            Id = "m11s-mammoth-meteor",
            Says = "get middle / someone or someone",
            On = EventKind.CastStart,
            MatchId = MammothMeteor,
            // Two meteors, one line: upstream says it once per set.
            Hush = 1f,
            OncePerBurst = false,
            Make = ctx =>
            {
                if (!ctx.Event.Source.Known) return null;

                var pull = Pull(ctx);
                if (!pull.HasAtomic)
                    return new Call
                    {
                        Text = "get middle",
                        Time = ctx.Event.Time,
                        Key = "m11s-mammoth-meteor",
                        Level = CallLevel.Info,
                    };

                var safe = SafeCorners(Compass.Dir8(ctx.Event.Source));
                return new Call
                {
                    Text = $"{Compass.Name8(safe.A)} or {Compass.Name8(safe.B)}",
                    Time = ctx.Event.Time,
                    Key = "m11s-mammoth-meteor",
                    Level = CallLevel.Info,
                };
            },
        };
    }

    // The corners the meteors are not on. They always take one intercardinal pair,
    // which leaves the other pair open.
    public static (int A, int B) SafeCorners(int meteorDir8) =>
        meteorDir8 is 1 or 5 ? (7, 3) : (1, 5);
}
