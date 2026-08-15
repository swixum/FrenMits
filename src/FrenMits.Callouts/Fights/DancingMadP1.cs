using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Callouts.Fights;

// Dancing Mad, phase 1: Graven Image and Tele-trouncing.
//
// Almost nothing here can be said from one event. The phase is built on a lie
// the boss tells with head markers over its own head: fire, ice and thunder
// each come in a real and a fake version, and which one is up decides whether
// the marker on you means what it says. So the shape of every call is the same,
// read the boss, then read yourself, then say the pair.
//
// The wording is copied exactly from where these calls came from. Not
// shortened and not made friendlier: people already know these words.
public static class DancingMadP1
{
    public const uint Territory = 1363;

    // The Kefka these markers appear over. Five actors in this fight answer to
    // the same name, so this is the base id, which is the one that tells them
    // apart.
    private const uint Kefka = 19504;

    // The pair over the boss. Each element has a real and a fake, and only the
    // fake is worth naming: seeing it is what flips the reading.
    private const uint FakeFire = 673;
    private const uint FakeIce = 675;
    private const uint FakeThunder = 677;

    // The pair on the party. Which of the two is showing is the same for
    // everybody; fire being fake is what turns one into the other.
    private const uint Spread = 127;
    private const uint Stack = 128;

    private const uint GravenImage = 48370;
    private const uint RevoltingRuin = 50179;
    private const uint LightOfJudgment = 50722;
    private const uint LightOfJudgmentAlt = 47805;
    private const uint TeleTrouncing = 47801;

    // The laser that picks four people, and the tower left for everybody else.
    private const uint Laser = 47784;
    private const uint Tower = 47786;

    // Graven Image 2: the stone landing, and the hands going up after it.
    private const uint StoneDrop = 47788;
    private const uint AfterStone = 47792;

    private const uint Confetti = 5078;

    // Every tether this phase draws carries the same id.
    private const uint Tether = 45;

    // The game pointing at an actor rather than naming an ability. This is how
    // a hand lighting up is announced, and it is the only way to know which
    // side of the room is about to be safe.
    private const uint Control = 413;
    private const uint HandFirst = 64;
    private const uint HandSecond = 128;

    // The arrows, in both the sets the fight uses them in.
    private const uint ArrowNorth = 4876;
    private const uint ArrowSouth = 4877;
    private const uint ArrowEast = 4878;
    private const uint ArrowWest = 4879;
    private const uint ArrowNorth2 = 5079;
    private const uint ArrowSouth2 = 5080;
    private const uint ArrowEast2 = 5081;
    private const uint ArrowWest2 = 5082;

    // The two arrows land together and differ only in how long they run. Under
    // this is the one that expires first, which is the one called first.
    private const float ShortArrow = 8.5f;

    // Which half of the room a thing is standing in. Both numbers are the
    // reference's own, kept exactly: the tether test and the hand test do not
    // use the same middle.
    private const float StoneSide = 120f;
    private const float RoomMiddle = 100f;

    // Who had confetti when it went out, kept for the call that has to name
    // them a mechanic later.
    private const string ConfettiNote = "dmu-p1-confetti";

    public static FightModule Module()
    {
        var runner = new ScriptRunner(Sequences());

        var triggers = new List<Trigger>
        {
            new()
            {
                Key = "DMU P1 Revolting Ruin III",
                About = "Tank buster, named for whoever it is on.",
                On = new TriggerMatch { Kind = EventKind.CastStart, Id = RevoltingRuin },
                Text = "Buster on {target}",
                Severity = CallSeverity.Warn,
            },
            new()
            {
                Key = "DMU P1 Light of Judgment",
                About = "Raidwide.",
                On = new TriggerMatch { Kind = EventKind.CastStart, Id = LightOfJudgment },
                Text = "Raidwide",
                Severity = CallSeverity.Warn,
            },
            new()
            {
                Key = "DMU P1 Light of Judgment (Second)",
                About = "Raidwide.",
                On = new TriggerMatch { Kind = EventKind.CastStart, Id = LightOfJudgmentAlt },
                Text = "Raidwide",
                Severity = CallSeverity.Warn,
            },
        };

        triggers.AddRange(ScriptRunner.Drivers("DMU P1", runner,
            EventKind.CastStart, EventKind.Ability, EventKind.StatusGain, EventKind.StatusLose,
            EventKind.HeadMarker, EventKind.Tether, EventKind.ActorControl));

        return new FightModule
        {
            Name = "Dancing Mad P1",
            Territory = Territory,
            Triggers = triggers,
        };
    }

    private static IEnumerable<Sequence> Sequences() =>
    [
        new()
        {
            Key = "DMU P1 Graven Image",
            Starts = (e, _) => e.Kind == EventKind.CastStart && e.Id == GravenImage,
            // Twice, and the second one is a different mechanic wearing the same
            // cast. A third cast belongs to Tele-trouncing and must not reopen
            // this.
            Invocations = 2,
            Body = run => run.Invocation == 0 ? GravenOne(run) : GravenTwo(run),
        },
        new()
        {
            Key = "DMU P1 Tele-trouncing",
            Starts = (e, _) => e.Kind == EventKind.CastStart && e.Id == TeleTrouncing,
            Body = TeleTrounce,
        },
    ];

    // Graven Image 1: tethers, then the fire and ice lie, then a laser that
    // decides who takes the tower, then confetti, then the thunder and ice lie.
    private static IEnumerator<Step> GravenOne(Run s)
    {
        yield return s.Say("Graven Image");

        yield return s.WaitAll(4, e => e.Kind == EventKind.Tether);
        yield return s.MineOf(s.Got) is not null ? s.Say("Tether") : s.Say("No Tether");

        yield return s.MarkersOn(2, Kefka);
        var fakeFire = s.Got.Any(m => m.Id == FakeFire);
        var fakeIce = s.Got.Any(m => m.Id == FakeIce);

        yield return s.Markers(1, Spread, Stack);
        var showingSpread = s.First.Id == Spread;

        // The marker says one thing and the boss says whether to believe it.
        var reallySpread = showingSpread != fakeFire;

        yield return s.Say(reallySpread
            ? fakeIce ? "Spread in Cones" : "Spread out of Cones"
            : fakeIce ? "Stacks in Cones" : "Stacks out of Cones");

        // Nothing to read here, the lasers are simply this far off.
        yield return s.Later("Line Spread", 6f);

        yield return s.WaitAll(4, e => e.Kind == EventKind.Ability && e.Id == Laser);
        // The towers begin casting in the same second the lasers land, so this
        // does not wait for that cast: by the time the four lasers have been
        // counted it has often already started, and waiting for one that is
        // already running waits forever.
        yield return s.MineOf(s.Got) is not null
            ? s.Say("Avoid Tower", CallSeverity.Danger)
            : s.Say("Take Tower");

        yield return s.WaitAll(2, e => e.Kind == EventKind.StatusGain && e.Id == Confetti);
        // Remembered because the same confetti is still up in Graven Image 2,
        // which has to name who is carrying it and will never see it land.
        s.State.Note(ConfettiNote, Names(s.Got));
        yield return s.MineOf(s.Got) is not null
            ? s.Say("Confetti", CallSeverity.Danger)
            : s.Say($"Confetti on {Names(s.Got)}");

        yield return s.MarkersOn(2, Kefka);
        var fakeThunder = s.Got.Any(m => m.Id == FakeThunder);
        var fakeIceAgain = s.Got.Any(m => m.Id == FakeIce);

        yield return s.Say(fakeThunder
            ? fakeIceAgain ? "Stand in Both" : "Out of Cones, In Lines"
            : fakeIceAgain ? "In Cones, Out of Lines" : "Avoid Both");
    }

    // Graven Image 2: everyone is tethered to a stone or to a dark, and which
    // one you have is not written anywhere except in where the thing on the
    // other end of your tether is standing.
    private static IEnumerator<Step> GravenTwo(Run s)
    {
        yield return s.Tethers(8, Tether);
        var stone = TetheredToStone(s, StoneSide);

        yield return s.MarkersOn(1, Kefka);
        var iceIsFake = s.First.Id == FakeIce;

        yield return s.Say(iceIsFake
            ? stone ? "Fake Ice, Stone" : "Fake Ice, Dark"
            : stone ? "Avoid Ice, Stone" : "Avoid Ice, Dark");

        yield return s.Hit(StoneDrop);
        yield return s.Say(stone ? "Drop Stone" : "Avoid Stone and Puddle");

        yield return s.Hit(AfterStone);

        yield return s.Control(Control, HandFirst, HandSecond);
        yield return s.Say(HandSaysWest(s) ? "West Safe" : "East Safe");

        yield return s.Tethers(8, Tether);
        stone = TetheredToStone(s, StoneSide);
        yield return s.Say(stone ? "Stone" : "Dark");

        yield return s.Hit(StoneDrop);
        yield return s.Say(stone ? "Drop Stone" : "Avoid Stone and Puddle");

        yield return s.Control(Control, HandFirst, HandSecond);
        var safe = HandSaysWest(s) ? "West" : "East";

        yield return s.Have(Confetti)
            ? s.Say($"{safe} Safe, Confetti", CallSeverity.Danger)
            : s.Say($"{safe} Safe, Confetti on {s.State.Noted(ConfettiNote)}");

        yield return s.Later("Final Soaks", 9f);
    }

    // Tele-trouncing: two arrows on you in the order they will expire, then a
    // tether, then a gaze, then the same fire and thunder lie as before.
    private static IEnumerator<Step> TeleTrounce(Run s)
    {
        yield return s.Say("Arrows");

        yield return s.WaitAll(2, e => e.Kind == EventKind.StatusGain
                                       && s.Mine(e.Target) && IsArrow(e.Id));

        var first = s.Got.FirstOrDefault(a => a.Value < ShortArrow);
        var second = s.Got.FirstOrDefault(a => a.Value >= ShortArrow);
        yield return Arrows(s, WayOfArrow(first?.Id ?? 0), WayOfArrow(second?.Id ?? 0));

        // The long arrow leaving is what puts confetti on the board.
        yield return s.Wait(e => e.Kind == EventKind.StatusLose && s.Mine(e.Target) && IsArrow(e.Id));

        yield return s.WaitAll(2, e => e.Kind == EventKind.StatusGain && e.Id == Confetti);
        var confetti = Names(s.Got);
        yield return s.Say($"Confetti on {confetti}");

        yield return s.Tethers(8, Tether);
        var sleep = TetheredToStone(s, RoomMiddle);
        yield return s.Say(sleep ? "Sleep Tether" : "Confusion Tether");

        yield return s.Wait(e => e.Kind == EventKind.StatusLose && e.Id == Confetti);
        yield return s.Say(sleep ? "Spread for Sleep" : "Spread for Confusion");

        yield return s.Control(Control, HandFirst, HandSecond);
        // The same hand read from the other side of the room, which is why this
        // one asks the opposite question to the two in Graven Image 2.
        var fakeGaze = !HandSaysWest(s);
        yield return s.Say(fakeGaze ? "Fake Gaze" : "Real Gaze");

        yield return s.MarkersOn(2, Kefka);
        var fakeFire = s.Got.Any(m => m.Id == FakeFire);
        var fakeThunder = s.Got.Any(m => m.Id == FakeThunder);

        yield return s.Markers(1, Spread, Stack);
        var reallySpread = (s.First.Id == Spread) != fakeFire;

        yield return s.Say(
            $"{(reallySpread ? "Spread" : "Stack")} "
            + $"{(fakeThunder ? "In Thunder" : "In Safe")}, "
            + $"Look {(fakeGaze ? "Towards" : "Away")}");
    }

    // Which side the thing on the other end of my tether is standing on. The
    // tether is drawn from a stone or a dark to a player, so the end that is
    // not me is the one worth looking at.
    private static bool TetheredToStone(Run s, float line)
    {
        foreach (var t in s.Got)
        {
            if (!s.Mine(t.Source) && !s.Mine(t.Target)) continue;
            var other = s.Mine(t.Target) ? t.Source : t.Target;
            var at = s.At(other.Id);
            if (!at.Known) at = other.At;
            if (at.Known) return at.X > line;
        }
        return false;
    }

    private static bool HandSaysWest(Run s)
    {
        var at = s.At(s.First.Source.Id);
        if (!at.Known) at = s.First.Source.At;
        return at.Known && at.X > RoomMiddle;
    }

    private static bool IsArrow(uint id) => id
        is ArrowNorth or ArrowSouth or ArrowEast or ArrowWest
        or ArrowNorth2 or ArrowSouth2 or ArrowEast2 or ArrowWest2;

    private static Way WayOfArrow(uint id) => id switch
    {
        ArrowNorth or ArrowNorth2 => Way.N,
        ArrowSouth or ArrowSouth2 => Way.S,
        ArrowEast or ArrowEast2 => Way.E,
        ArrowWest or ArrowWest2 => Way.W,
        _ => Way.Unknown,
    };

    // The twelve arrow pairs the fight can hand out. Anything else is the two
    // debuffs not having been read, which is worth saying rather than hiding.
    private static Speak Arrows(Run s, Way first, Way second) => (first, second) switch
    {
        (Way.N, Way.N) => s.Say("Double North"),
        (Way.S, Way.S) => s.Say("Double South"),
        (Way.E, Way.E) => s.Say("Double East"),
        (Way.W, Way.W) => s.Say("Double West"),
        (Way.N, Way.W) => Pair(s, "North West", "North -> West"),
        (Way.N, Way.E) => Pair(s, "North East", "North -> East"),
        (Way.S, Way.W) => Pair(s, "South West", "South -> West"),
        (Way.S, Way.E) => Pair(s, "South East", "South -> East"),
        (Way.E, Way.N) => Pair(s, "East North", "East -> North"),
        (Way.E, Way.S) => Pair(s, "East South", "East -> South"),
        (Way.W, Way.N) => Pair(s, "West North", "West -> North"),
        (Way.W, Way.S) => Pair(s, "West South", "West -> South"),
        _ => s.Say("Error", CallSeverity.Danger),
    };

    // Written and spoken differ here. The banner carries the arrow, which is
    // read at a glance, and the voice says the two directions plainly because
    // an arrow cannot be spoken.
    private static Speak Pair(Run s, string spoken, string banner)
        => new(new Say(banner, Severity: CallSeverity.Warn, Tts: spoken, Duration: 6f));

    private static string Names(IEnumerable<GameEvent> events)
        => string.Join(", ", events.Select(e => e.Target.Name).Where(n => n.Length > 0));
}
