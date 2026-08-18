namespace FrenAlerts.Engine;

// One strat choice a group makes, and the answers it can take.
//
// A mechanic with several accepted solutions has no single right call. Which
// tower you take, which way the rotation reads, where the healers plant: the
// mechanic is identical and the call is not. Hardcoding one answer is picking a
// group's strat for them and being wrong for everybody else.
public sealed record Strategy(
    ushort Territory,
    string Key,
    string Name,
    string Hint,
    IReadOnlyList<StrategyOption> Options)
{
    public string Default => Options.Count > 0 ? Options[0].Value : "";
}

public sealed record StrategyOption(string Value, string Label);

public static class Strategies
{
    public static IReadOnlyList<Strategy> For(ushort territory) =>
        All.Where(s => s.Territory == territory).ToList();

    public static Strategy? Find(ushort territory, string key) =>
        All.FirstOrDefault(s => s.Territory == territory && s.Key == key);

    // "none" everywhere it exists, so nothing invents a call for a strat the group
    // has not told us about. Silence beats confidently naming the wrong tower.
    private static StrategyOption Off => new("none", "Off");

    // The same answer written two ways.
    //
    // An imported fight asks several of these questions on its own page, and where it
    // does, that page is the one with the row on it. The answers line up by value for
    // all but one: their Kefka-north mode is a patch beside their file and calls
    // itself "kefka", ours calls it "kefkaNorth", and one row cannot drive both
    // unless something knows they are the same thing.
    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["kefka"] = "kefkaNorth",
    };

    // Which of a setting's own answers an imported one means, or empty when it means
    // none of them. Empty is a real answer here: a fight that adds an option we have
    // never heard of must fall back to what was picked here rather than be forced
    // onto whichever of ours happens to sort first.
    public static string Matching(Strategy setting, string theirs)
    {
        if (setting is null || string.IsNullOrWhiteSpace(theirs)) return "";
        var want = Aliases.GetValueOrDefault(theirs, theirs);
        return setting.Options.Any(o => o.Value == want) ? want : "";
    }

    // Which answer a setting takes, given what an imported fight says on its own page
    // and what was left here.
    //
    // One row per question. Both engines run in a fight like Dancing Mad and both ask
    // which Forsaken order the group runs; two rows for that is two ways to be half
    // set, and only one of the two is on screen. Theirs is the one with the row on it,
    // so theirs is the one that answers, and an answer of theirs this setting has
    // never heard of falls back to what was picked here rather than to a guess.
    public static string Answer(Strategy setting, string theirs, string ours)
    {
        if (setting is null) return "";
        if (Matching(setting, theirs) is { Length: > 0 } same) return same;

        return setting.Options.Any(o => o.Value == ours) ? ours : setting.Default;
    }

    public static readonly IReadOnlyList<Strategy> All =
    [
        // Dancing Mad
        new(1363, "teleportent", "Tele-Portents", "How the portent pair reads",
            [Off, new("clockwise", "Clockwise"), new("filipino", "Filipino")]),
        new(1363, "forsaken", "Forsaken", "Which side each group takes",
            [Off, new("kroxy-rinon", "Kroxy Rinon 3/4/1"), new("buddy", "Buddy")]),
        new(1363, "boa", "Bowels of Agony", "What answers it",
            [Off, new("lb3", "Tank limit break"), new("sg3k", "SG3K")]),
        new(1363, "accretion", "Accretion", "How the order is read",
            [new("line", "By line"), new("role", "By role")]),
        new(1363, "blackHole", "Black Holes", "Which order they are taken in",
            [Off, new("dsa", "DSA"), new("sda", "SDA"), new("modified", "Modified")]),
        // Kefka is relative north for the whole of that phase, so this names the hand
        // attack's safe side as well as the tethers.
        new(1363, "blackHoleTether", "Black Hole tethers", "How directions are called",
            [new("true", "True north"), new("clock", "Clock spots"),
             new("kefkaNorth", "Kefka is north")]),

        // M12S
        // Named the way wtfdig.info names them, so the picker reads the same as the
        // page the group planned off. Swix runs Modified with Rep 1 DN and Rep 2
        // Clone Zone/Caro, which is why those are the ones listed first.
        new(1327, "strat", "Strat", "Which plan the group runs",
            [Off, new("modified", "Modified (3VJ0)"), new("caroZenith", "Caro/Zenith (a3V)")]),
        new(1327, "mortalSlayer", "Mortal Slayer", "How the sides are assigned",
            [Off, new("role", "Role"), new("position", "Position")]),
        new(1327, "curtainCallStrat", "Curtain Call", "Which axis it uses",
            [Off, new("ns", "North south")]),
        new(1327, "portentStrategy", "Portents", "How they are assigned",
            [Off, new("dn", "DN"), new("zenith", "Zenith"), new("nukemaru", "Nukemaru")]),
        new(1327, "idyllic", "Idyllic", "How it is assigned",
            [Off, new("dn", "DN"), new("caro", "Caro")]),
        new(1327, "replication2Strategy", "Rep 1", "How it is assigned",
            [Off, new("dn", "DN"), new("banana", "Banana"), new("nukemaru", "Nukemaru")]),
        new(1327, "replication4Strategy", "Rep 2", "How it is assigned",
            [Off, new("cloneZoneCaro", "Clone Zone/Caro (CJ4)"), new("dn", "DN"),
             new("em", "EM"), new("nukemaru", "Nukemaru")]),

        // M11S. The plan names are the site's own; swix runs Hector with No Buddies
        // and Fixed. The two below it are the ones the calls actually read, and
        // neither is written on the page, so they stay off until somebody picks.
        new(1325, "strat", "Strat", "Which plan the group runs",
            [Off, new("hector", "Hector, Toxic/No Buddies"),
             new("usbWdz", "USB + WdZ + Fixed Toxic Friends")]),
        // No "off" on these two: the tethers turn one way or the other and the
        // baits take one axis or the other, so there is no third state that could
        // be true. Swix's answers are first, which makes them the default.
        new(1325, "majesticMeteowrathTetherDir", "Meteowrath tethers", "Which way they turn",
            [new("cw", "Clockwise"), new("ccw", "Counterclockwise")]),
        new(1325, "twoWayFireballBaitDir", "Fireball baits", "Which axis baits",
            [new("ew", "East west"), new("ns", "North south")]),

        // The Unending Coil. Eight towers go up in Heavensfall and which one is
        // yours is a seat counted round from Nael. R1 takes the tower Nael is
        // standing on, which is seat one, and the rest count round from there.
        // Positions are the raid plan's own, not the eight the sheets use: this
        // fight splits into two light parties and numbers outward from Nael, so
        // its "R1" is Right 1 and has nothing to do with Ranged 1.
        //
        // Clockwise from the tower under Nael: R1 R2 R3 R4 L4 L3 L2 L1. That comes
        // straight off the plan, which puts L1 and R1 at Nael, L2/L3 and R2/R3 to
        // either side, and L4/R4 opposite her.
        new(733, "heavensfallTower", "Heavensfall tower", "Your spot in the plan",
            [Off, new("0", "R1, under Nael"), new("1", "R2"), new("2", "R3"),
             new("3", "R4, opposite"), new("4", "L4, opposite"), new("5", "L3"),
             new("6", "L2"), new("7", "L1, under Nael")]),
    ];
}
