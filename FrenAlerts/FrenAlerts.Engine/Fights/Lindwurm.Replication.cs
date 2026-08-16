namespace FrenAlerts.Engine;

// M12S's replications, where the tether you get decides what you do and the one
// person who gets none takes the defamation instead.
public static class LindwurmReplication
{
    // The four tethers that hand out a job.
    private static readonly uint[] AbilityTethers = [0x016F, 0x0170, 0x0171, 0x0176];

    // The tether that locks the set in, which is when the call is made.
    private const uint LockedTether = 0x0175;

    // Where the far defamation goes, per plan. Swix runs DN, which sends it south.
    private static readonly Dictionary<string, string> BaitAt = new()
    {
        ["dn"] = "south",
        ["banana"] = "east",
        ["nukemaru"] = "west",
    };

    // What each clone direction is asked to take, per plan. Upstream's table,
    // straight across: the rows are the eight clone spots and the columns the
    // three plans. It is not a rotation of one another, so it has to be the table.
    private static readonly Dictionary<string, string[]> JobForClone = new()
    {
        ["dn"] =
        [
            "boss tether", "cone tether clockwise", "stack tether clockwise",
            "defamation tether clockwise", "no tether",
            "defamation tether counterclockwise", "stack tether counterclockwise",
            "cone tether counterclockwise",
        ],
        ["banana"] =
        [
            "cone tether clockwise", "defamation tether clockwise", "no tether",
            "defamation tether counterclockwise", "cone tether counterclockwise",
            "stack tether counterclockwise", "boss tether", "stack tether clockwise",
        ],
        ["nukemaru"] =
        [
            "cone tether counterclockwise", "stack tether counterclockwise",
            "boss tether", "stack tether clockwise", "cone tether clockwise",
            "defamation tether clockwise", "no tether",
            "defamation tether counterclockwise",
        ],
    };

    // Which job this clone spot takes under this plan, or empty for a plan nobody
    // picked: naming somebody else's tether is worse than naming none.
    public static string JobFor(string strat, int cloneDir8) =>
        JobForClone.TryGetValue(strat, out var jobs)
        && cloneDir8 >= 0 && cloneDir8 < jobs.Length
            ? jobs[cloneDir8]
            : "";

    // Idyllic's table, which is a different shape again: each clone spot has a
    // side, a quadrant, and a spot that depends on which mechanic goes first.
    // This is swix's plan, Clone Zone/Caro. The others are not written down here
    // because he does not run them and a half-read column is worse than none.
    public readonly record struct Idyllic(
        string Side, int Quad, string OnStacks, string OnDefamations);

    private static readonly Idyllic[] CaroIdyllic =
    [
        new("east", 1, "northeast", "north"),
        new("east", 2, "east", "southeast"),
        new("east", 3, "south", "southwest"),
        new("east", 4, "northwest", "west"),
        new("west", 1, "northeast", "north"),
        new("west", 2, "south", "southeast"),
        new("west", 3, "west", "southwest"),
        new("west", 4, "northwest", "west"),
    ];

    // Where this clone spot goes when stacks come first, or when defamations do.
    public static Idyllic? IdyllicFor(int cloneDir8) =>
        cloneDir8 >= 0 && cloneDir8 < CaroIdyllic.Length ? CaroIdyllic[cloneDir8] : null;

    // The two tethers that open Idyllic, and which mechanic each means comes first.
    private const uint HeavySlamTether = 0x0171;
    private const uint ManaBurstTether = 0x0170;

    private static LindwurmPull Pull(in TriggerContext ctx) =>
        ctx.State.Remember<LindwurmPull>();

    public static IEnumerable<Trigger> Triggers()
    {
        // The staging tether names your clone. Its far end is the clone, so where
        // that is standing is the spot the table is read by.
        yield return new Trigger
        {
            Id = "m12s-my-clone",
            On = EventKind.Tether,
            MatchId = LockedTether,
            OnlyMe = true,
            Claims = true,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.MyCloneDir >= 0) return null;

                var clone = ctx.Actors.Where(ctx.Event.SourceId);
                if (clone.Known) pull.MyCloneDir = Compass.Dir8(clone);
                return null;
            },
        };

        // Idyllic. The first tether thrown says whether stacks or defamations go
        // first, and that flips where every clone spot stands.
        foreach (var (tether, first) in
                 new[] { (HeavySlamTether, "stacks"), (ManaBurstTether, "defamations") })
            yield return new Trigger
            {
                Id = $"m12s-idyllic-{tether:X}",
                On = EventKind.Tether,
                MatchId = tether,
                OnlyMe = true,
                Make = ctx =>
                {
                    var pull = Pull(ctx);
                    if (pull.MyCloneDir < 0) return null;
                    if (ctx.Strat("replication4Strategy") != "cloneZoneCaro") return null;
                    if (IdyllicFor(pull.MyCloneDir) is not { } spot) return null;

                    var where = first == "stacks" ? spot.OnStacks : spot.OnDefamations;

                    return new Call
                    {
                        Text = $"{first} first, {spot.Side} group {spot.Quad}, {where}",
                        Time = ctx.Event.Time,
                        Key = "m12s-idyllic",
                        Level = CallLevel.Alert,
                        Personal = true,
                        Hold = 7f,
                    };
                },
            };

        // What your clone's spot asks you to take.
        yield return new Trigger
        {
            Id = "m12s-replication-job",
            On = EventKind.Tether,
            MatchId = LockedTether,
            OnlyMe = true,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.MyCloneDir < 0) return null;

                var job = JobFor(ctx.Strat("replication2Strategy"), pull.MyCloneDir);
                if (job.Length == 0) return null;

                return new Call
                {
                    Text = $"{Compass.Name8(pull.MyCloneDir)} clone, {job}",
                    Time = ctx.Event.Time,
                    Key = "m12s-replication-job",
                    Level = CallLevel.Alert,
                    Personal = true,
                    Hold = 8f,
                };
            },
        };

        // Whether this player got a job this replication.
        foreach (var tether in AbilityTethers)
            yield return new Trigger
            {
                Id = $"m12s-replication-tether-{tether:X}",
                On = EventKind.Tether,
                MatchId = tether,
                OnlyMe = true,
                Claims = true,
                Make = ctx =>
                {
                    Pull(ctx).GotReplicationTether = true;
                    return null;
                },
            };

        // The locked tether closes the set. Anyone still without one is the far
        // defamation, and where it goes is the group's plan rather than the fight's.
        yield return new Trigger
        {
            Id = "m12s-far-defamation",
            On = EventKind.Tether,
            MatchId = LockedTether,
            Hush = 1f,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.GotReplicationTether) return null;

                var where = BaitAt.GetValueOrDefault(ctx.Strat("replication2Strategy"), "");
                var bait = where.Length > 0
                    ? $"bait far defamation {where}"
                    : "bait far defamation";

                return new Call
                {
                    Text = $"{bait}, then stack groups",
                    Time = ctx.Event.Time,
                    Key = "m12s-far-defamation",
                    Level = CallLevel.Alert,
                    Personal = true,
                    Hold = 8f,
                };
            },
        };
    }
}
