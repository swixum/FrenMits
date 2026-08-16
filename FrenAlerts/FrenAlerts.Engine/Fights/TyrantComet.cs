namespace FrenAlerts.Engine;

public sealed class TyrantCometPull
{
    // Whether this player is carrying Atomic Impact, which changes the meteor call
    // from "get middle" to a pair of corners.
    public bool HasAtomic;

    // How many maelstroms have appeared. The fourth is the one worth calling.
    public int Maelstroms;
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

    private static TyrantCometPull Pull(in TriggerContext ctx) =>
        ctx.State.Remember<TyrantCometPull>();

    public static IEnumerable<Trigger> Triggers()
    {
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
