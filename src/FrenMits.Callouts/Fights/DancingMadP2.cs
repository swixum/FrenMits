using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Callouts.Fights;

// Dancing Mad, phase 2: Forsaken and Trines.
//
// Forsaken is eight sets of towers with a mechanic on top of each, and the
// thing that makes it hard is that the mechanic you are handed and the tower
// pattern you have to do are announced at different moments. Even sets come
// with a cast that says whether the baits go between the towers or away from
// them, odd sets inherit the answer from the set before, and the last set has
// no marker at all. So this is one loop that counts, not eight triggers.
//
// The wording is copied exactly from where these calls came from.
public static class DancingMadP2
{
    public const uint Territory = 1363;

    private const uint UltimateEmbrace = 49740;
    private const uint ForsakenCast = 47804;

    // The debuff the whole phase is measured in, whose stacks are the only
    // thing this call shows.
    private const uint ForsakenDebuff = 5083;

    // Which mechanic landed on you, written as a head marker.
    private const uint MarkStack = 715;
    private const uint MarkCircle = 716;
    private const uint MarkCone = 717;

    // The towers going off, which is what moves the loop on a set.
    private const uint TowerResolve = 47806;

    // The cast that says which way the next baits go.
    private const uint Future = 47826;
    private const uint Past = 47827;

    // The towers that come up on an odd set, once the baits are done.
    private const uint TowersA = 47836;
    private const uint TowersB = 47837;

    private const uint TrinesCast = 47839;

    // The phase running out, which only happens when the towers were not fed.
    private const uint FailedEnrage = 47841;

    // The last stack count said out loud, so a refresh that changes nothing
    // says nothing.
    private const string StacksNote = "dmu-forsaken-stacks";

    // The wings, which cleave a half and leave a quarter turn either side.
    private const uint WingsLeft = 47822;
    private const uint WingsRight = 47821;

    // The game pointing at an actor. The trines announce themselves this way
    // rather than by casting anything.
    private const uint Control = 413;
    private const uint TrineFirst = 16;
    private const uint TrineSecond = 32;

    // The trines only ever land on these seven, so the third set is whatever
    // the first two left alone. East and west are not among them.
    private static readonly Way[] TrineSpots =
        [Way.Middle, Way.N, Way.NE, Way.SE, Way.S, Way.SW, Way.NW];

    // The middle of the room, and how close to it still counts as the middle
    // for a trine. Both are the reference's own numbers.
    private const float CenterX = 100f;
    private const float CenterY = 100f;
    private const float TrineBand = 4f;
    private const float Reach = 20f;

    private enum Mech { None, Cone, Circle, Stack }

    public static FightModule Module()
    {
        var runner = new ScriptRunner(Sequences());

        var triggers = new List<Trigger>
        {
            new()
            {
                Key = "DMU P2 Failed Enrage",
                About = "The phase running out because the towers went unfed.",
                On = new TriggerMatch { Kind = EventKind.CastStart, Id = FailedEnrage },
                Text = "Failed",
                Severity = CallSeverity.Danger,
            },
            new()
            {
                Key = "DMU P2 Ultimate Embrace",
                About = "Tank buster, named for whoever it is on.",
                On = new TriggerMatch { Kind = EventKind.CastStart, Id = UltimateEmbrace },
                Text = "Buster on {target}",
                Severity = CallSeverity.Warn,
            },
            // Text only where it came from, because the game refreshes this
            // debuff constantly and a voice would read it out every time. There
            // is no way to ask for a silent banner here, so it is held back
            // instead of repeating.
            new()
            {
                Key = "DMU P2 Forsaken Stacks",
                About = "How many stacks you are carrying, said only when the number changes.",
                On = new TriggerMatch
                {
                    Kind = EventKind.StatusGain,
                    Id = ForsakenDebuff,
                    Target = ActorScope.Me,
                },
                // Only when the number changes. The game refreshes this debuff
                // constantly, and where this call came from it is text with no
                // voice for exactly that reason. Saying it again while it still
                // reads the same is the repeat that made that rule.
                Says = c =>
                {
                    var now = c.Event.Extra.ToString();
                    if (c.State.Noted(StacksNote) == now) return null;
                    c.State.Note(StacksNote, now);
                    return new Say($"{now} Stacks", Severity: CallSeverity.Info, Duration: 4f);
                },
            },
            // Whoever is in the room and what they play, which is the only way
            // to say whether the cones went to the supports or to the dps.
            new()
            {
                Key = "DMU P2 Party",
                On = new TriggerMatch { Kind = EventKind.ActorAdd },
                Note = c =>
                {
                    if (!c.Event.Source.IsPlayer || c.Event.Flags == 0) return;
                    c.State.Note(JobNote(c.Event.Source.Id), RoleOf(c.Event.Flags));
                },
            },
        };

        triggers.AddRange(ScriptRunner.Drivers("DMU P2", runner,
            EventKind.CastStart, EventKind.Ability, EventKind.StatusGain,
            EventKind.HeadMarker, EventKind.ActorControl));

        return new FightModule
        {
            Name = "Dancing Mad P2",
            Territory = Territory,
            Triggers = triggers,
        };
    }

    private static IEnumerable<Sequence> Sequences() =>
    [
        new()
        {
            Key = "DMU P2 Forsaken",
            Starts = (e, _) => e.Kind == EventKind.CastStart && e.Id == ForsakenCast,
            Body = Forsaken,
        },
        new()
        {
            Key = "DMU P2 Trines",
            Starts = (e, _) => e.Kind == EventKind.CastStart && e.Id == TrinesCast,
            Body = Trines,
        },
    ];

    // Eight sets. The first is announced on its own, the six after it each
    // carry a mechanic and a tower pattern, and the last has neither.
    private static IEnumerator<Step> Forsaken(Run s)
    {
        yield return s.Say("Raidwide");

        yield return s.WaitAll(8, e => e.Kind == EventKind.HeadMarker && IsMech(e.Id));
        var opening = MineIn(s, s.Got);
        var who = SupportsHaveCone(s, s.Got) ? "Supports" : "DPS";

        yield return s.Say(opening switch
        {
            Mech.Cone => $"Cone, {who} have cone",
            Mech.Circle => $"Circle, {who} have cone",
            Mech.Stack => $"Stack, {who} have cone",
            _ => $"Error, {who} have cone",
        });

        // Which way the baits go, learned on an even set and used on the odd
        // set after it.
        var away = false;

        for (var set = 2; set <= 8; set++)
        {
            yield return s.Hit(TowerResolve);

            var mine = Mech.None;
            var buddy = "";

            // The eighth set hands out nothing: the towers are the whole of it.
            if (set < 8)
            {
                yield return s.WaitAll(4, e => e.Kind == EventKind.HeadMarker && IsMech(e.Id));
                mine = MineIn(s, s.Got);
                buddy = BuddyIn(s, s.Got, mine);
            }

            if (set % 2 == 0)
            {
                yield return s.Say(mine switch
                {
                    Mech.Cone => $"Cone with {buddy}, Baits",
                    Mech.Circle => $"Circle with {buddy}, Baits",
                    Mech.Stack => "Stack, Baits",
                    _ => "Nothing, Baits",
                });

                yield return s.Cast(Future, Past);
                away = s.First.Id == Future;
                continue;
            }

            yield return s.Say(mine switch
            {
                Mech.Cone => away ? "Cone, Bait Away" : "Cone, Bait Between",
                Mech.Circle => away ? "Circle, Bait Away" : "Circle, Bait Between",
                Mech.Stack => away ? $"Stack with {buddy}, Bait Away" : $"Stack with {buddy}, Bait Between",
                _ => away ? "Nothing, Bait Away" : "Nothing, Bait Between",
            });

            yield return s.Cast(TowersA, TowersB);
            yield return s.Say(mine switch
            {
                Mech.Cone => "Cone",
                Mech.Circle => "Circle",
                Mech.Stack => $"Stack with {buddy}",
                _ => "Nothing",
            });
        }

        yield return s.Say(away ? "Bait Away" : "Bait Between");
    }

    // Trines: three land, then three more, and the three nobody used are where
    // the last set goes. The call names somewhere to start from and where the
    // first set was, because that is what you have time to act on.
    private static IEnumerator<Step> Trines(Run s)
    {
        yield return s.Say("Trines");

        yield return s.ControlAll(3, Control, TrineFirst, TrineSecond);
        var first = Sectors(s, s.Got);

        yield return s.Cast(WingsLeft, WingsRight);
        var facing = Compass.Facing(s.First.Source.Heading);
        var safe = facing.PlusQuads(s.First.Id == WingsLeft ? -1 : 1);
        yield return s.Say($"{Name(safe)} Safe");

        yield return s.ControlAll(3, Control, TrineFirst, TrineSecond);
        var second = Sectors(s, s.Got);

        var left = TrineSpots.Where(w => !first.Contains(w) && !second.Contains(w)).ToList();

        // The middle is always worth starting from when it survives. Otherwise
        // stand next to somewhere the first set already went off.
        var start = left.Contains(Way.Middle)
            ? Way.Middle
            : left.FirstOrDefault(c => first.Any(f => Neighbours(f, c)), Way.Unknown);

        yield return s.Say($"{Name(start)} to {string.Join(", ", first.Select(Name))}");
    }

    // Two spots count as neighbours if they sit next to each other on the
    // floor, and two intercardinals count if they share a side of the room.
    private static bool Neighbours(Way one, Way other)
    {
        if (one == Way.Unknown || other == Way.Unknown || one == Way.Middle || other == Way.Middle)
            return false;
        if (one.IsNextTo(other)) return true;
        if (other.IsCardinal() || one.IsCardinal()) return false;
        return (one.IsNextTo(Way.W) && other.IsNextTo(Way.W))
               || (one.IsNextTo(Way.E) && other.IsNextTo(Way.E));
    }

    private static List<Way> Sectors(Run s, List<GameEvent> events)
    {
        var floor = new Floor(Territory, "", CenterX, CenterY, Reach, TrineBand, TrineBand,
            Square: false, Authored: true);

        var found = new List<Way>();
        foreach (var e in events)
        {
            var at = s.At(e.Source.Id);
            if (!at.Known) at = e.Source.At;
            if (!at.Known) continue;
            var way = floor.Sector(at);
            if (way != Way.Unknown && !found.Contains(way)) found.Add(way);
        }
        return found;
    }

    // Capitalised, so a worked out direction reads like the ones written by
    // hand next to it: "Roles North", not "Roles north".
    private static string Name(Way w)
    {
        var word = w == Way.Middle ? "Middle" : w == Way.Unknown ? "Unknown" : w.Name();
        return word.Length > 0 ? char.ToUpperInvariant(word[0]) + word[1..] : word;
    }

    private static bool IsMech(uint id) => id is MarkStack or MarkCircle or MarkCone;

    private static Mech MechOf(uint id) => id switch
    {
        MarkCone => Mech.Cone,
        MarkCircle => Mech.Circle,
        MarkStack => Mech.Stack,
        _ => Mech.None,
    };

    private static Mech MineIn(Run s, List<GameEvent> markers)
    {
        foreach (var m in markers)
            if (s.Mine(m.Target)) return MechOf(m.Id);
        return Mech.None;
    }

    // Whoever else was handed the same thing, which is who you are doing it
    // with. Nobody is a real answer on a set where yours is alone.
    private static string BuddyIn(Run s, List<GameEvent> markers, Mech mine)
    {
        if (mine == Mech.None) return "";
        foreach (var m in markers)
            if (!s.Mine(m.Target) && m.Target.IsPlayer && MechOf(m.Id) == mine)
                return m.Target.Name;
        return "";
    }

    // The four cones go to one role group or the other. Counted from what the
    // room is playing when that is known, and worked out from your own role
    // when it is not: if the cones are on your group you are looking at one.
    private static bool SupportsHaveCone(Run s, List<GameEvent> markers)
    {
        var cones = markers.Where(m => m.Id == MarkCone && m.Target.IsPlayer).ToList();

        var supports = 0;
        var dps = 0;
        foreach (var c in cones)
        {
            var role = s.State.Noted(JobNote(c.Target.Id));
            if (role.Length == 0) continue;
            if (role is "tank" or "healer") supports++; else dps++;
        }

        if (supports + dps > 0) return supports > dps;

        var iAmSupport = s.Me.Role is "tank" or "healer";
        var iHaveCone = cones.Any(c => s.Mine(c.Target));
        return iHaveCone == iAmSupport;
    }

    private static string JobNote(uint actorId) => $"job:{actorId:X}";

    // The plugin's own job table, kept to the roles this fight asks about.
    private static string RoleOf(uint job) => job switch
    {
        19 or 21 or 32 or 37 => "tank",
        24 or 28 or 33 or 40 => "healer",
        0 => "",
        _ => "dps",
    };
}
