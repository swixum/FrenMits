using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Callouts.Fights;

// Dancing Mad, phase 3: Earthquake.
//
// The biggest mechanic in the fight, and the one call people actually need out
// of it is the smallest: which of the three sets you are in. Everybody takes a
// crust, the crust runs for one of three lengths, and that length is the whole
// answer. Under ninety seconds is the first set, under two minutes the second,
// anything longer the third. Two people also carry accretion, which changes
// what their set has to do rather than when.
//
// Everything after that is directions, and every one of them is read off which
// way something is facing as it casts. That matters for more than tidiness:
// facings ride on casts, and a cast is one of the few things the plugin can
// see on its own tick, so these calls work in a duty and not only in a
// recording.
//
// The wording is copied exactly from where these calls came from.
public static class DancingMadEarthquake
{
    public const uint Territory = 1363;

    private const uint EarthquakeA = 50545;
    private const uint EarthquakeB = 50546;

    // What everybody takes, and how long it runs, which is the set you are in.
    private const uint Crust = 5454;
    private const uint Accretion = 1604;

    // The reference's own thresholds, confirmed against a recording where the
    // three lengths came out at 72, 106 and 139 seconds.
    private const float FirstUnder = 90f;
    private const float SecondUnder = 120f;

    // Slap Happy, in its two versions, which cleave opposite halves.
    private const uint SlapRoles = 47847;
    private const uint SlapStack = 47846;

    // How long after the cast the slap actually lands.
    private const float SlapDelay = 3.7f;

    private const uint DamningEdict = 47873;
    private const uint Despair = 47854;

    private const uint Longitudinal = 47869;
    private const uint Latitudinal = 47870;

    // The black holes, and the three casts that each bring a set of them.
    private const uint BlackHole = 19512;
    private const uint BlackHoleCast = 47867;
    private const uint WhiteHoleCast = 48486;

    // How far out a black hole has to sit before it is one of the three that
    // matter. The ones inside this are decoys, and there are eight of them.
    private const float HoleRing = 16.9f;

    // The middle of the room, and how far its ground reaches.
    private const float CenterX = 100f;
    private const float CenterY = 100f;
    private const float Reach = 20f;

    // A set spawns a few seconds after the cast that brings it, and the tethers
    // follow the spawns. Neither waits long.
    private const float SpawnWait = 20f;
    private const float TetherWait = 8f;

    // Where each set of crusts sits, remembered so a cleanse can be read
    // against your own set rather than announced for everybody.
    private static string SetNote(uint actorId) => $"eq:set:{actorId:X}";
    private const string MySetNote = "eq:myset";

    // When Earthquake began, so the standalone implosion call can tell whether
    // it is looking at its own mechanic or at one Earthquake is already
    // calling as part of a longer sentence.
    public const string RunningNote = "eq:started";

    // Read as a number of fight seconds, so a mechanic that died half way
    // through stops silencing anything rather than silencing it forever.
    public const float RunsFor = 240f;

    public static FightModule Module()
    {
        var runner = new ScriptRunner(Sequences());

        var triggers = new List<Trigger>
        {
            // Who is in the room and what they play, which decides whether a
            // set is called for a support or for a dps.
            new()
            {
                Key = "DMU EQ Party",
                On = new TriggerMatch { Kind = EventKind.ActorAdd },
                Note = c =>
                {
                    if (!c.Event.Source.IsPlayer || c.Event.Flags == 0) return;
                    c.State.Note($"job:{c.Event.Source.Id:X}", Support(c.Event.Flags) ? "support" : "dps");
                },
            },
            // Your own crust coming off is the one cleanse you never have to be
            // told about by anybody else.
            new()
            {
                Key = "DMU EQ Self Cleansed",
                About = "Your own crust coming off.",
                On = new TriggerMatch { Kind = EventKind.StatusLose, Id = Crust, Target = ActorScope.Me },
                Text = "Cleansed",
                Severity = CallSeverity.Info,
            },
            // Somebody else's crust coming off only matters when they are the
            // set immediately before yours, because that is the one that says
            // you are next. Every other cleanse in the room is noise.
            new()
            {
                Key = "DMU EQ Prior Set Cleansed",
                About = "The set before yours being cleansed, which is your cue.",
                On = new TriggerMatch
                {
                    Kind = EventKind.StatusLose,
                    Id = Crust,
                    Target = ActorScope.OtherPlayer,
                },
                Says = c =>
                {
                    if (!int.TryParse(c.State.Noted(MySetNote), out var mine) || mine <= 1) return null;
                    if (!int.TryParse(c.State.Noted(SetNote(c.Event.Target.Id)), out var theirs)) return null;
                    return theirs == mine - 1
                        ? new Say($"{c.Event.Target.Name} Cleansed", Severity: CallSeverity.Info)
                        : null;
                },
            },
        };

        triggers.AddRange(ScriptRunner.Drivers("DMU EQ", runner,
            EventKind.CastStart, EventKind.StatusGain, EventKind.StatusLose,
            EventKind.ActorAdd, EventKind.Tether));

        return new FightModule
        {
            Name = "Dancing Mad Earthquake",
            Territory = Territory,
            Triggers = triggers,
        };
    }

    private static IEnumerable<Sequence> Sequences() =>
    [
        new()
        {
            Key = "DMU EQ",
            Starts = (e, _) => e.Kind == EventKind.CastStart && (e.Id == EarthquakeA || e.Id == EarthquakeB),
            Body = Earthquake,
        },
        new()
        {
            Key = "DMU EQ Holes",
            Starts = (e, _) => e.Kind == EventKind.CastStart && e.Id == BlackHoleCast,
            Body = Holes,
        },
    ];

    // The four sets of black holes. Eleven spawn each time and eight of them
    // are decoys sitting near the middle; the three that matter are the ones
    // out on the ring, and naming those three is the call.
    //
    // Two of the sets arrive staggered, some tethering before the rest, and
    // those are worth calling in the order they land. The tethers are the one
    // thing here the plugin cannot see on its own tick, so that wait is allowed
    // to come up short: without it the set is still named, just not split.
    private static IEnumerator<Step> Holes(Run s)
    {
        // How many tether ahead of the others, per set, in the order the fight
        // brings them.
        int[] staggered = [1, 0, 0, 2];

        for (var set = 0; set < staggered.Length; set++)
        {
            if (set > 0) yield return s.Cast(set == 3 ? WhiteHoleCast : DamningEdict);

            yield return s.WaitAll(3,
                e => e.Kind == EventKind.ActorAdd && e.Id == BlackHole && Ring(s, e) != Way.Unknown,
                SpawnWait);

            var holes = new Dictionary<uint, Way>();
            foreach (var spawn in s.Got) holes[spawn.Source.Id] = Ring(s, spawn);
            var all = Sorted(holes.Values);

            if (staggered[set] == 0)
            {
                yield return s.Say(Names(all));
                continue;
            }

            yield return s.WaitSome(staggered[set],
                e => e.Kind == EventKind.Tether
                     && (holes.ContainsKey(e.Source.Id) || holes.ContainsKey(e.Target.Id)),
                TetherWait);

            var first = new List<Way>();
            foreach (var t in s.Got)
            {
                var id = holes.ContainsKey(t.Source.Id) ? t.Source.Id : t.Target.Id;
                if (holes.TryGetValue(id, out var way) && !first.Contains(way)) first.Add(way);
            }

            // No tethers reached us, so there is no order to give. The three
            // spots are still the three spots.
            if (first.Count == 0) { yield return s.Say(Names(all)); continue; }

            var rest = Sorted(all.Where(w => !first.Contains(w)));
            yield return s.Say($"{Names(Sorted(first))} then {Names(rest)}");
        }
    }

    private static List<Way> Sorted(IEnumerable<Way> ways)
        => ways.Distinct().OrderBy(w => (int)w).ToList();

    private static IEnumerator<Step> Earthquake(Run s)
    {
        s.State.Note(RunningNote, s.Start.Time.ToString("0.###",
            System.Globalization.CultureInfo.InvariantCulture));

        yield return s.Say("Earthquake - 1 HP", CallSeverity.Danger);

        // All eight crusts land together, and the set cannot be read off any
        // one of them without the rest, because the lengths only mean anything
        // next to each other.
        yield return s.WaitAll(8, e => e.Kind == EventKind.StatusGain && e.Id == Crust);
        var mine = s.Got.FirstOrDefault(e => s.Mine(e.Target));

        // Everybody's set, so a crust coming off somebody else can be read
        // against yours without asking the room again.
        foreach (var crust in s.Got)
        {
            if (!crust.Target.IsPlayer) continue;
            s.State.Note(SetNote(crust.Target.Id), SetOf(crust.Value).ToString());
        }

        if (mine is not null)
        {
            s.State.Note(MySetNote, SetOf(mine.Value).ToString());

            var set = mine.Value < FirstUnder ? "First"
                : mine.Value < SecondUnder ? "Second"
                : "Third";

            // Accretion only ever rides on the first two sets, and it is worth
            // saying because it changes what that set does rather than when.
            var carrying = s.Have(Accretion);
            yield return s.Say(carrying ? $"{set} + Accretion" : set, CallSeverity.Danger, 8f);
        }

        // Slap Happy, then the edict, then Slap Happy again.
        yield return s.Cast(SlapRoles, SlapStack);
        yield return Slap(s);

        yield return s.Cast(DamningEdict);
        var chaos = Compass.Facing(s.First.Source.Heading);
        yield return s.Say($"{Name(chaos.Opposite())} Behind Chaos");

        yield return s.Cast(SlapRoles, SlapStack);
        yield return Slap(s);

        // The body slam and the edict together, which leaves a gap that depends
        // on how the two of them line up.
        //
        // Both are taken in one wait because they land within a couple of
        // seconds of each other and not always in the same order: on the pull
        // this was built against the edict came first. Waiting for one and then
        // the other reads whichever arrived second and then waits forever for a
        // third cast that never comes.
        yield return s.WaitAll(2, e => e.Kind == EventKind.CastStart
                                       && (e.Id == Despair || e.Id == DamningEdict));

        var slam = s.Got.FirstOrDefault(e => e.Id == Despair);
        var edictCast = s.Got.FirstOrDefault(e => e.Id == DamningEdict);
        var despair = slam is not null ? Compass.Facing(slam.Source.Heading) : Way.Unknown;
        var edict = edictCast is not null ? Compass.Facing(edictCast.Source.Heading) : Way.Unknown;

        yield return s.Say($"{Names(SafeFrom(edict, despair))} safe");

        // Slap Happy again, this time on top of the implosion, so there is a
        // spot to start in, a spot to move to, and a spot to finish in. These
        // two overlap as well, and the implosion is the one that came first.
        yield return s.WaitAll(2, e => e.Kind == EventKind.CastStart
                                       && (e.Id == SlapRoles || e.Id == SlapStack
                                           || e.Id == Longitudinal || e.Id == Latitudinal));

        var slapCast = s.Got.FirstOrDefault(e => e.Id is SlapRoles or SlapStack);
        var implosionCast = s.Got.FirstOrDefault(e => e.Id is Longitudinal or Latitudinal);
        if (slapCast is null || implosionCast is null) yield break;

        var roles = slapCast.Id == SlapRoles;
        var boss = Compass.Facing(slapCast.Source.Heading);
        var longways = implosionCast.Id == Longitudinal;
        var implosion = Compass.Facing(implosionCast.Source.Heading);

        var cleaving = roles ? boss.PlusQuads(-1) : boss.PlusQuads(1);
        var cleaved = new[] { cleaving.PlusEighths(-1), cleaving, cleaving.PlusEighths(1) };

        var sides = new[] { implosion.PlusQuads(-1), implosion.PlusQuads(1) }
            .Where(w => !cleaved.Contains(w)).ToList();
        var frontBack = new[] { implosion, implosion.Opposite() }
            .Where(w => !cleaved.Contains(w)).ToList();

        var first = longways ? sides : frontBack;
        var second = longways ? frontBack : sides;
        var final = cleaving.Opposite();
        var what = roles ? "Roles" : "Stack";

        yield return s.Say($"{Names(first)} to {Names(second)}, {what} {Name(final)}");

        // The slap lands a few seconds after the implosion resolves, and there
        // is nothing left to read by then, so it is simply said on time.
        yield return s.Later($"{what} {Name(final)}", SlapDelay);

        // One last body slam on its own.
        yield return s.Cast(Despair);
        var last = Compass.Facing(s.First.Source.Heading);
        yield return s.Say($"{Names([last.PlusQuads(1), last.PlusQuads(-1)])} Safe");
    }

    // Slap Happy cleaves the half it is not facing, so the safe quarter is a
    // turn either side depending on which version is up.
    private static Speak Slap(Run s)
    {
        var roles = s.First.Id == SlapRoles;
        var facing = Compass.Facing(s.First.Source.Heading);
        var safe = roles ? facing.PlusQuads(1) : facing.PlusQuads(-1);
        return new Speak(new Say(
            $"{(roles ? "Roles" : "Stack")} {Name(safe)}",
            Severity: CallSeverity.Warn,
            Delay: SlapDelay,
            Duration: 6f));
    }

    // Where the body slam and the edict leave room, which is three different
    // shapes depending on how far apart the two of them are pointing.
    private static List<Way> SafeFrom(Way edict, Way despair)
    {
        if (edict == Way.Unknown || despair == Way.Unknown) return [];

        // Pointing the same way or straight opposite: the gap is either side of
        // the slam, three eighths round.
        if (edict == despair || edict.Opposite() == despair)
            return [despair.PlusEighths(3), despair.PlusEighths(-3)];

        // A quarter apart: only the spot behind the edict survives.
        if (edict.PlusQuads(1) == despair || edict.PlusQuads(-1) == despair)
            return [edict.Opposite()];

        // Anything else: either side of the slam, less whatever the edict is
        // already covering.
        var blocked = new[] { edict, edict.PlusEighths(1), edict.PlusEighths(-1) };
        return new[] { despair.PlusQuads(1), despair.PlusQuads(-1) }
            .Where(w => !blocked.Contains(w)).ToList();
    }

    // Capitalised, so a worked out direction reads like the ones written by
    // hand next to it: "Roles North", not "Roles north".
    private static string Name(Way w)
    {
        var word = w == Way.Middle ? "Middle" : w == Way.Unknown ? "Unknown" : w.Name();
        return word.Length > 0 ? char.ToUpperInvariant(word[0]) + word[1..] : word;
    }

    private static string Names(IEnumerable<Way> ways)
    {
        var named = ways.Where(w => w != Way.Unknown).Select(Name).ToList();
        return named.Count > 0 ? string.Join(", ", named) : "Unknown";
    }

    private static int SetOf(float crustSeconds)
        => crustSeconds < FirstUnder ? 1 : crustSeconds < SecondUnder ? 2 : 3;

    // Which way out a black hole is sitting, or nothing when it is one of the
    // decoys parked near the middle.
    private static Way Ring(Run s, GameEvent spawn)
    {
        var at = s.At(spawn.Source.Id);
        if (!at.Known) at = spawn.Source.At;
        if (!at.Known) return Way.Unknown;

        var floor = new Floor(Territory, "", CenterX, CenterY, Reach, HoleRing, HoleRing,
            Square: false, Authored: true);
        var way = floor.Sector(at);
        return way.IsCardinal() ? way : Way.Unknown;
    }

    private static bool Support(uint job) => job is 19 or 21 or 32 or 37 or 24 or 28 or 33 or 40;
}
