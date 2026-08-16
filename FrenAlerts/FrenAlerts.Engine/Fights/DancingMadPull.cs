namespace FrenAlerts.Engine;

// What one pull of Dancing Mad has told us so far.
//
// Almost every call in this fight needs something an earlier event said: which
// element was real, who else has your debuff, how many black holes have gone off,
// where the boss teleported to. None of that is in the event making the call.
//
// It lives on the fight state rather than in a static, so it dies when the pull
// does. Carrying a wipe's answers into the next pull is the bug where the second
// pull of the night is called perfectly for the first one.
public sealed class DancingMadPull
{
    // A full party, which is the most any of these lists can honestly hold. Past it
    // the entry is dropped rather than the list growing: eight is the fight.
    public const int Party = 8;

    // ---- phase 1 ----

    // How many sets of statues have been summoned, which is what tells three
    // identical tethers apart.
    public int Statues;

    // Which statue this player is tethered to: pulse, gravitas, vitrophyre,
    // indulgent or idyllic. Empty until one lands.
    public string MyTether = "";

    // Whether each element's tell is the real one. Null until its marker lands,
    // because "not yet known" and "known to be fake" are opposite calls.
    public bool? FireReal;
    public bool? IceReal;
    public bool? ThunderReal;

    // Which of the two markers this player got, which flips what the element means.
    public uint MyMark;

    // Whether the eye tower is the one to look away from.
    public bool? LookAway;

    public string Portent1 = "";
    public string Portent2 = "";

    public readonly List<uint> WaveCannoned = [];
    public readonly List<uint> Trapped = [];

    // The first statue's id in a set. The rest of the set is this one minus a fixed
    // amount each, which is the only thing that says which statue does what.
    public uint StatueBase;

    public bool StatuesKnown => StatueBase != 0;

    // ---- phase 2 ----

    // Which set of towers is being resolved, counting from one.
    public int PathSet = 1;

    public readonly List<string> MyPaths = [];
    public readonly Dictionary<uint, string> PathMark = new(Party);
    public readonly List<uint> PathStacks = [];
    public readonly List<uint> PathCones = [];
    public readonly List<uint> PathSpreads = [];

    // Which half of the party this player is in for the tower rotation.
    public bool GroupA;

    // Locked by the first set only. Later sets can match by chance, and letting
    // that change the answer swaps a player's job halfway through the mechanic.
    public string Buddy = "";

    // Which of the two halves the 3/4/1 rotation puts this player in: "a" takes the
    // first three towers and the last, "b" takes the middle four. Settled by who
    // held the stack on the first set, and never revisited.
    public string StackSide = "";

    public readonly List<int> TrineDirs = [];

    // Which way the middle trine sweeps, for the tanks who have to stand off it.
    public string MiddleTrine = "";

    // ---- phase 3 ----

    // Whether fire is the short debuff this pull, which decides the order the
    // crystals are taken in.
    public bool? FireShort;

    public string MyElement = "";
    public readonly List<uint> FirePlayers = [];
    public readonly List<uint> WaterPlayers = [];

    // head or tail, which is whether to face the boss or look away from it.
    public string MyWind = "";

    public int FireCrystal = Nowhere;
    public int WaterCrystal = Nowhere;
    public int WindCrystal = Nowhere;

    public bool WindNext;

    public readonly Dictionary<uint, int> InLine = new(Party);

    public uint FirstAccretion;
    public uint SecondAccretion;
    public bool HadAccretion;

    // Everyone it landed on this time, in the order it arrived, so the group's own
    // reading of the order has something to sort rather than two fixed slots.
    public readonly List<uint> Accretions = [];

    // How many times Nothingness has gone off, counting from one, which is what
    // says which of the six black hole sets is on screen.
    public int Nothingness = 1;

    public readonly List<int> HoleDirs = [];
    public bool HolesCalled;

    // The boss's own id, learned from a cast, so a teleport can be recognised as
    // this boss moving rather than any actor in the zone.
    public uint KefkaId;

    // Where the boss teleported to, which is where the black hole order starts.
    public int KefkaDir = Nowhere;

    public Position FirstBlaster = Position.None;
    public int BlasterDir = Nowhere;

    // Which way the blaster sweeps: -1 clockwise, 1 counterclockwise, 0 unknown.
    public int BlasterTurn;

    public bool SecondKnockDown;

    // ---- phase 4 ----

    // Whether each pair of debuffs means what it says, in the order they arrive.
    public bool? Debuffs1;
    public bool? Debuffs2;
    public bool? Debuffs3;
    public bool? Debuffs4;

    public bool? FirstDebuffShort;

    public readonly List<uint> ShortShriek = [];
    public readonly List<uint> LongShriek = [];
    public readonly List<uint> ShortForked = [];
    public readonly List<uint> LongForked = [];
    public readonly List<uint> ShortCompressed = [];
    public readonly List<uint> LongCompressed = [];
    public readonly List<uint> FirstShortBomb = [];
    public readonly List<uint> FirstLongBomb = [];
    public readonly List<uint> SecondShortBomb = [];
    public readonly List<uint> SecondLongBomb = [];

    public string Wound = "";
    public string DeathOrField = "";

    public int GrandCrosses;

    // Whether each boss is telling the truth this time, which is what the shotcall
    // lines read. The two wear different numbers for it, so they are kept apart.
    public bool? NeoReal;
    public bool? ChaosReal;

    public bool? EntropyReal;
    public bool? FluidReal;
    public bool? ThunderCharged;
    public bool? BlizzardCharged;

    // ---- phase 5 ----

    // Three elements and a spare, because a tower can be counted twice before the
    // old one despawns and dropping the extra is better than growing.
    public const int Towers = 6;

    public readonly List<(string Element, Position At)> CeleTowers = [];

    // When each element's resistance runs out, measured on the fight's own clock so
    // a replay reads the same as the pull did.
    public readonly Dictionary<string, double> CeleUntil = new(3);

    public bool CeleCalled;
    public bool? CeleNoDebuff;

    // No direction at all, which is not the same as north.
    public const int Nowhere = -1;

    // Adds without letting a list grow past the party it describes. A duplicate is
    // dropped too: the same status polled twice is one player, not two.
    public static void Note(List<uint> into, uint who, int cap = Party)
    {
        if (who == 0 || into.Count >= cap || into.Contains(who)) return;
        into.Add(who);
    }

    public static void Note(List<int> into, int what, int cap)
    {
        if (into.Count >= cap || into.Contains(what)) return;
        into.Add(what);
    }

    public static void Note<TKey, TValue>(Dictionary<TKey, TValue> into, TKey key, TValue value, int cap)
        where TKey : notnull
    {
        if (into.Count >= cap && !into.ContainsKey(key)) return;
        into[key] = value;
    }
}
