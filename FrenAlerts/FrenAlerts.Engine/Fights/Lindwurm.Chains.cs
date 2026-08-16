namespace FrenAlerts.Engine;

// What one pull of M12S has told us so far.
public sealed class LindwurmPull
{
    // Which number in the line this player is, from the debuff that hands it out.
    // Zero until one lands, because one is a real answer.
    public int MyNumber;

    // Which set of flesh bonds this player got. Empty until one lands.
    public string MyBonds = "";

    // How many cell chains have been thrown so far this phase.
    public int ChainsThrown;

    // Which side the purple orb is on, which is the side the tanks take. Null
    // until enough orbs have landed to know, because left and right are both real
    // answers and neither is a safe guess.
    public bool? PurpleIsLeft;

    // Green orbs seen on each side. Four on one side settles where purple goes
    // without waiting to see purple itself.
    public int GreenLeft;
    public int GreenRight;

    // Which cardinal the cursed coil started on, and how many skinsplitters have
    // turned it since. Null until the coil casts, because north is a real answer.
    public int? CoilStart;
    public int Skinsplitters;

    // Which corner Curtain Call leaves open, as an intercardinal. Minus one until
    // a blob lands on the row that says it.
    public int CurtainCorner = -1;

    // Which element the replication gave this player. Empty until one lands.
    public string MyElement = "";

    // Which spot this player's clone took, as an eighth. Minus one until the
    // staging tether names it, because north is a real answer.
    public int MyCloneDir = -1;

    // Whether a replication handed this player a job. Without one they are the
    // far defamation.
    public bool GotReplicationTether;

    // The four blob towers in the order they go up, as intercardinals. Bounded at
    // four because that is how many the mechanic has.
    public readonly List<int> BlobTowers = new(4);

    public void NewMortalSlayer()
    {
        PurpleIsLeft = null;
        GreenLeft = 0;
        GreenRight = 0;
    }
}

// M12S's chains, which are called off the number the fight hands each player
// rather than off anything the group agreed beforehand.
//
// The line number arrives as one of four debuffs and the bonds as one of two, so
// all of this is read from the pull itself.
public static class LindwurmChains
{
    // The four in-line debuffs, in order.
    private static readonly uint[] InLine = [0xBBC, 0xBBD, 0xBBE, 0xD7B];

    private const uint FleshAlpha = 0x1290;
    private const uint FleshBeta = 0x1292;

    private const uint CellChainTether = 0x016E;

    // Dramatic Lysis, which is a chain being broken.
    private const uint ChainBroken = 0xB4B4;

    // Which chain tower each number takes. The line numbers and the tower order
    // are not the same thing: one and two take the last two towers.
    private static readonly int[] TowerForNumber = [3, 4, 1, 2];

    // The two orbs, and the cast that ends the mechanic so the second one starts
    // from nothing.
    private const uint PurpleOrb = 19200;
    private const uint GreenOrb = 19201;
    private const uint MortalSlayer = 0xB495;

    // The arena's middle, which is the only thing that decides left from right.
    private const float Middle = 100f;

    // How many greens on one side settle it.
    private const int GreensThatSettleIt = 4;

    private static LindwurmPull Pull(in TriggerContext ctx) =>
        ctx.State.Remember<LindwurmPull>();

    // Which tower this line number takes, or zero for a number nobody has.
    public static int TowerFor(int myNumber) =>
        myNumber >= 1 && myNumber <= TowerForNumber.Length ? TowerForNumber[myNumber - 1] : 0;

    public static IEnumerable<Trigger> Triggers()
    {
        // Every orb that lands, until the side is known.
        yield return new Trigger
        {
            Id = "m12s-mortal-slayer-orbs",
            On = EventKind.ActorSpawn,
            Claims = true,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (!ctx.Event.Source.Known || pull.PurpleIsLeft is not null) return null;

                var left = ctx.Event.Source.X < Middle;

                // Purple itself is the direct answer.
                if (ctx.Event.DataId == PurpleOrb)
                {
                    pull.PurpleIsLeft = left;
                    return null;
                }
                if (ctx.Event.DataId != GreenOrb) return null;

                // Four greens on one side means purple is going to the other.
                if (left && ++pull.GreenLeft == GreensThatSettleIt) pull.PurpleIsLeft = false;
                else if (!left && ++pull.GreenRight == GreensThatSettleIt) pull.PurpleIsLeft = true;
                return null;
            },
        };

        // The side is only worth saying once it is settled, and it is the tanks'
        // call first: everyone else is being told where not to be.
        yield return new Trigger
        {
            Id = "m12s-mortal-slayer",
            On = EventKind.ActorSpawn,
            OncePerBurst = false,
            Hush = 12f,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (ctx.Event.DataId is not (PurpleOrb or GreenOrb)) return null;
                if (pull.PurpleIsLeft is not { } left) return null;

                return new Call
                {
                    Text = left ? "tanks left" : "tanks right",
                    Time = ctx.Event.Time,
                    Key = "m12s-mortal-slayer",
                    Level = ctx.ForMe("tank") ? CallLevel.Alert : CallLevel.Info,
                };
            },
        };

        // The mechanic resolving clears it, so the second one is read fresh.
        yield return new Trigger
        {
            Id = "m12s-mortal-slayer-done",
            On = EventKind.AbilityHit,
            MatchId = MortalSlayer,
            Claims = true,
            Make = ctx =>
            {
                Pull(ctx).NewMortalSlayer();
                return null;
            },
        };

        // The number this player is in the line.
        for (var i = 0; i < InLine.Length; i++)
        {
            var number = i + 1;
            yield return new Trigger
            {
                Id = $"m12s-in-line-{number}",
                On = EventKind.StatusGain,
                MatchId = InLine[i],
                OnlyMe = true,
                Claims = true,
                Make = ctx =>
                {
                    Pull(ctx).MyNumber = number;
                    return null;
                },
            };
        }

        yield return Bonds("m12s-bonds-alpha", FleshAlpha, "alpha");
        yield return Bonds("m12s-bonds-beta", FleshBeta, "beta");

        // Every chain thrown moves the count on, which is what tells one tower
        // from the next.
        yield return new Trigger
        {
            Id = "m12s-chain-count",
            On = EventKind.Tether,
            MatchId = CellChainTether,
            Claims = true,
            Make = ctx =>
            {
                Pull(ctx).ChainsThrown++;
                return null;
            },
        };

        // Each chain thrown is numbered out loud, and when the number reaching the
        // group is the one that opens your tower, both halves are said at once.
        yield return new Trigger
        {
            Id = "m12s-chain-tether",
            On = EventKind.Tether,
            MatchId = CellChainTether,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.MyBonds != "beta") return null;

                // Counted by the tracker above, which runs first.
                var num = pull.ChainsThrown;
                if (num == 0) return null;

                var mine = TowerFor(pull.MyNumber);
                var text = mine != 0 && mine == num
                    ? $"tether {num}, then chain tower {mine}"
                    : $"tether {num}";

                return new Call
                {
                    Text = text,
                    Time = ctx.Event.Time,
                    Key = "m12s-chain-tether",
                    Level = CallLevel.Info,
                };
            },
        };

        // A chain breaking is the cue for the next tower, and it is only your cue
        // when the count has reached yours.
        yield return new Trigger
        {
            Id = "m12s-chain-tower",
            On = EventKind.AbilityHit,
            MatchId = ChainBroken,
            Hush = 1f,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.MyBonds != "beta" || pull.MyNumber == 0) return null;

                var mine = TowerFor(pull.MyNumber);
                if (mine == 0 || mine != pull.ChainsThrown) return null;

                return new Call
                {
                    Text = $"take chain tower {mine}",
                    Time = ctx.Event.Time,
                    Key = "m12s-chain-tower",
                    Level = CallLevel.Alert,
                    Personal = true,
                };
            },
        };
    }

    private static Trigger Bonds(string id, uint status, string which) => new()
    {
        Id = id,
        On = EventKind.StatusGain,
        MatchId = status,
        OnlyMe = true,
        Claims = true,
        Make = ctx =>
        {
            Pull(ctx).MyBonds = which;
            return null;
        },
    };
}
