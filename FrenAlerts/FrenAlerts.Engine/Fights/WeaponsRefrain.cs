namespace FrenAlerts.Engine;

// What one pull of the Weapon's Refrain has told us so far.
//
// Lives on the fight state, so it dies with the pull. See DancingMadPull for why
// that matters: a wipe's answers carried into the next pull calls the second pull
// of the night perfectly for the first one.
public sealed class WeaponsRefrainPull
{
    // Which boss is up. The fight reuses ids between phases, so almost nothing can
    // be called without knowing this first.
    public string Phase = "garuda";

    // The actor each boss is, learned from the cast that opens its phase.
    public uint TitanId;
    public uint IfritId;

    // Whether Titan has jumped once already. The first jump is the one where he can
    // be read even with no turn seen, because he starts the phase facing north.
    public bool SeenFirstJump;

    // The four nails in the order they died, with where each one stood. Bounded at
    // the four the mechanic has.
    public readonly List<(uint Id, int Dir8)> NailOrder = new(4);

    // Which way round the kills went, and where the first one was. Empty and minus
    // one until four nails have died in an order that reads as a rotation at all.
    public string NailRotation = "";
    public int NailFirstDir = -1;

    // How many times Ifrit has hidden, which is how the two sets of dashes are told
    // apart.
    public int IfritHidden;

    public bool SaidSecondDash;

    // Cardinals the radiant plumes and Ifrit's dash have taken out. Three of them
    // leaves exactly one place to stand.
    public readonly HashSet<int> UnsafeCardinals = new(4);

    public bool SaidInitialDash;
    public bool SaidPlumeSafe;
}

// The Weapon's Refrain.
//
// Only what the call pack could not carry: the calls whose words are worked out
// from where something is standing rather than read off the trigger. Everything
// else in this fight comes from the pack.
//
// The arena constants are upstream's, not invented here: the middle is 100/100 and
// Titan's four jump sites sit 14 units out on the cardinals.
public static class WeaponsRefrain
{
    public const ushort Territory = 777;

    // Titan lands on one of four cardinals and the opposite one is safe.
    private static readonly (int Dir4, float X, float Y)[] JumpSites =
    [
        (3, 86f, 100f),   // west
        (1, 114f, 100f),  // east
        (0, 100f, 86f),   // north
        (2, 100f, 114f),  // south
    ];

    // The casts that hand the fight from one boss to the next.
    private const uint GarudaOpener = 0x2B53;
    private const uint IfritOpener = 0x2B5F;
    private const uint TitanOpener = 0x2CFD;
    private const uint TitanEnd = 0x2CF5;

    // Garuda's add, which the tanks pick up. Upstream matches it by the add's
    // NAME and no source carries the number, so it came out of the actor table on
    // three real kills instead: Spiny Plume is 8726, beside Satin Plume on 8725
    // and Razor Plume on 8724, which is why matching the name loosely would be
    // wrong here.
    private const uint SpinyPlume = 8726;

    private static WeaponsRefrainPull Pull(in TriggerContext ctx) =>
        ctx.State.Remember<WeaponsRefrainPull>();

    public static IEnumerable<Trigger> Triggers()
    {
        foreach (var t in WeaponsRefrainIfrit.Triggers()) yield return t;

        // Phase, learned from the opener each boss casts. Says nothing itself: it
        // exists so everything below knows which boss it is looking at.
        yield return new Trigger
        {
            Id = "uwu-phase",
            On = EventKind.CastStart,
            Claims = true,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                switch (ctx.Event.Id)
                {
                    case IfritOpener when pull.Phase == "garuda":
                        pull.Phase = "ifrit";
                        pull.IfritId = ctx.Event.SourceId;
                        break;
                    case TitanOpener when pull.Phase == "ifrit":
                        pull.Phase = "titan";
                        pull.TitanId = ctx.Event.SourceId;
                        break;
                    case TitanEnd when pull.Phase == "titan":
                        pull.Phase = "intermission";
                        break;
                }
                return null;
            },
        };

        // Only the tanks go and get it.
        yield return new Trigger
        {
            Id = "uwu-spiny-plume",
            On = EventKind.ActorSpawn,
            MatchId = SpinyPlume,
            For = "tank",
            Make = ctx => new Call
            {
                Text = "grab the plume",
                Time = ctx.Event.Time,
                Key = "uwu-spiny-plume",
                Level = CallLevel.Alert,
            },
        };

        // Titan hides to jump. No cast and no marker announces it, so the moment he
        // stops being targetable is the only thing there is to fire on, and his
        // heading at that moment is what says where he is going.
        yield return new Trigger
        {
            Id = "uwu-titan-jump",
            On = EventKind.NameToggle,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.Phase != "titan") return null;
                if (ctx.Event.SourceId != pull.TitanId || pull.TitanId == 0) return null;

                // Becoming targetable again is him landing, not leaving.
                if (ctx.Event.Arg1 != 0) return null;

                var where = ctx.Event.Source;
                var safe = SafeFrom(where, pull.SeenFirstJump);
                pull.SeenFirstJump = true;

                return new Call
                {
                    Text = safe is null ? "safe spot unknown" : $"{Compass.Name4(safe.Value)} is safe",
                    Time = ctx.Event.Time,
                    Key = "uwu-titan-jump",
                    Level = CallLevel.Alert,
                };
            },
        };
    }

    // Which cardinal is safe, given where Titan was standing and which way he faced.
    //
    // He faces the site he is about to jump to, so the site whose bearing back to
    // him is the exact opposite of his own heading is the one he takes. The safe
    // spot is the cardinal across from it.
    //
    // Null when nothing lines up, which is said as unknown rather than guessed: a
    // wrong cardinal here walks the party into the landing.
    public static int? SafeFrom(Position where, bool seenFirstJump)
    {
        // Before the first jump he has not necessarily turned at all, and he opens
        // the phase facing north, which puts the party south.
        if (!where.Known) return seenFirstJump ? null : 2;

        foreach (var (dir4, x, y) in JumpSites)
        {
            var toTitan = Position.Facing(new Position(x, y, 0f, 0f), where);
            var apart = MathF.Abs(where.Heading - toTitan);

            // Should be exactly pi. The window is upstream's, and it is there
            // because a heading off the wire is rounded.
            if (apart is >= 3f and <= 3.28f) return Compass.Opposite4(dir4);
        }
        return null;
    }
}
