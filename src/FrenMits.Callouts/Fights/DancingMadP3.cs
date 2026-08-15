using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Callouts.Fights;

// Dancing Mad, phase 3: Chaos and Exdeath.
//
// Two bosses at once, and most of the phase is about which of them you are
// allowed to hit and which debuff you are carrying while you do it. Bowels of
// Agony hands out a wind and sometimes a second thing on top, and the second
// thing resolves first, so the call has to name both and then come back when
// the wind is all that is left.
//
// Limit Cut reads its whole answer from two hits: where the first two clones
// stood tells you where the line starts and which way it turns, and your own
// number then places you on it.
//
// The wording is copied exactly from where these calls came from.
//
// Earthquake is the rest of this phase and lives in its own file. It is large
// enough to read on its own: it has its own role assignment, its own cleanse
// order and four sets of black holes.
public static class DancingMadP3
{
    public const uint Territory = 1363;

    private const uint AeroAssault = 50167;
    private const uint VacuumWave = 47891;
    private const uint ThunderAoe = 47890;
    private const uint ThunderProximity = 47881;

    private const uint EpicHero = 4192;
    private const uint FatedHero = 4194;

    // One cast opens two separate mechanics: the raidwide that hands out the
    // winds, and the limit cut that follows it.
    private const uint BowelsCast = 47858;

    private const uint Entropy = 1600;
    private const uint Dynamic = 1601;
    private const uint Headwind = 1602;
    private const uint Tailwind = 1603;

    // How long before a wind's partner expires the reminder goes out.
    private const float Warning = 5f;

    // The clone hits that give away where the line starts.
    private const uint LimitCutHit = 47843;

    // How long a mechanic that opens well before it resolves is given, matching
    // the reference's own allowance.
    private const float LongWait = 180f;

    // A backstop on reading the clones. Each of the eight strikes the party
    // several times over, so the whole mechanic is a few dozen events and two
    // readable clones can be five clones apart. This sits above the count a
    // full mechanic produces and below anything that would read into the rest
    // of the pull.
    private const int MaxHitsRead = 96;

    private const uint Longitudinal = 47869;
    private const uint Latitudinal = 47870;
    private const uint LatLongResolve = 47871;

    private const uint StompCast = 47887;
    private const uint Blizzard = 47885;
    private const uint StompStack = 161;
    private const uint StompHit = 47856;
    private const uint BigBang = 47889;

    // The two ways the phase can end, one of them because the party was too
    // slow earlier.
    private const uint EnrageA = 50718;
    private const uint EnrageB = 50719;
    private const uint FailedA = 49752;
    private const uint FailedB = 49753;

    // The middle of the room, and how close to it stops counting as a corner.
    // These are the reference's own numbers for this phase.
    private const float CenterX = 100f;
    private const float CenterY = 100f;
    private const float CloneBand = 10f;
    private const float Reach = 20f;

    public static FightModule Module()
    {
        var runner = new ScriptRunner(Sequences());

        var triggers = new List<Trigger>
        {
            Cast("DMU P3 Aero III Assault", AeroAssault, "Knockback"),
            Cast("DMU P3 Vacuum Wave", VacuumWave, "Knockback from {source}"),
            Cast("DMU P3 Thunder III", ThunderAoe, "Away from {source}"),
            Cast("DMU P3 Thunder III Proximity", ThunderProximity, "Proximity Buster"),
            Mine("DMU P3 Epic Hero", EpicHero, "Attack Chaos"),
            Mine("DMU P3 Fated Hero", FatedHero, "Attack Exdeath"),
            new()
            {
                Key = "DMU P3 Party",
                On = new TriggerMatch { Kind = EventKind.ActorAdd },
                Note = c =>
                {
                    if (!c.Event.Source.IsPlayer || c.Event.Flags == 0) return;
                    c.State.Note($"job:{c.Event.Source.Id:X}", Support(c.Event.Flags) ? "support" : "dps");
                },
            },
        };

        triggers.AddRange(ScriptRunner.Drivers("DMU P3", runner,
            EventKind.CastStart, EventKind.Ability, EventKind.StatusGain, EventKind.StatusLose,
            EventKind.HeadMarker));

        return new FightModule
        {
            Name = "Dancing Mad P3",
            Territory = Territory,
            Triggers = triggers,
        };
    }

    private static Trigger Cast(string key, uint id, string text) => new()
    {
        Key = key,
        On = new TriggerMatch { Kind = EventKind.CastStart, Id = id },
        Text = text,
        Severity = CallSeverity.Warn,
    };

    private static Trigger Mine(string key, uint id, string text) => new()
    {
        Key = key,
        On = new TriggerMatch { Kind = EventKind.StatusGain, Id = id, Target = ActorScope.Me },
        Text = text,
        Severity = CallSeverity.Info,
    };

    private static IEnumerable<Sequence> Sequences() =>
    [
        new()
        {
            Key = "DMU P3 Bowels of Agony",
            Starts = (e, _) => e.Kind == EventKind.CastStart && e.Id == BowelsCast,
            Body = Bowels,
        },
        new()
        {
            Key = "DMU P3 Limit Cut",
            Starts = (e, _) => e.Kind == EventKind.CastStart && e.Id == BowelsCast,
            Body = LimitCut,
        },
        new()
        {
            Key = "DMU P3 Implosion",
            Starts = (e, _) => e.Kind == EventKind.CastStart && (e.Id == Longitudinal || e.Id == Latitudinal),
            // Once for each time the pair is cast in the phase.
            Invocations = 8,
            Body = Implosion,
        },
        new()
        {
            Key = "DMU P3 Stomp-a-Mole",
            Starts = (e, _) => e.Kind == EventKind.CastStart && e.Id == StompCast,
            Body = Stomp,
        },
    ];

    // The winds, and whatever landed on top of one. Whatever is on top always
    // resolves first, so it is called first and the wind is called again once
    // it is the only thing left.
    private static IEnumerator<Step> Bowels(Run s)
    {
        yield return s.Say("Raidwide");

        yield return s.WaitAll(12, e => e.Kind == EventKind.StatusGain && IsWind(e.Id));

        var head = s.Got.FirstOrDefault(e => e.Id == Headwind && s.Mine(e.Target));
        var tail = s.Got.FirstOrDefault(e => e.Id == Tailwind && s.Mine(e.Target));
        var mineEntropy = s.Got.FirstOrDefault(e => e.Id == Entropy && s.Mine(e.Target));
        var mineDynamic = s.Got.FirstOrDefault(e => e.Id == Dynamic && s.Mine(e.Target));

        var wind = head is not null ? "Headwind" : tail is not null ? "Tailwind" : "";
        if (wind.Length > 0)
            yield return s.Say(
                mineEntropy is not null ? $"{wind} and Entropy"
                : mineDynamic is not null ? $"{wind} and Dynamic Fluid"
                : wind);

        // Everyone's entropy runs the same length, so anybody's says when.
        var anyEntropy = s.Got.FirstOrDefault(e => e.Id == Entropy);
        var lead = anyEntropy is not null ? MathF.Max(0f, anyEntropy.Value - Warning) : 0f;

        if (mineEntropy is not null)
        {
            yield return s.Later("Entropy On You Soon", lead);
            yield return s.Wait(e => e.Kind == EventKind.StatusLose && e.Id == Entropy && s.Mine(e.Target));
            if (wind.Length > 0) yield return s.Say(wind);
        }
        else if (anyEntropy is not null)
        {
            yield return s.Later("Entropies Soon", lead);
        }

        var anyDynamic = s.Got.FirstOrDefault(e => e.Id == Dynamic);
        var dynamicLead = anyDynamic is not null ? MathF.Max(0f, anyDynamic.Value - Warning) : 0f;

        if (mineDynamic is not null)
        {
            yield return s.Later("Dynamic On You Soon", dynamicLead);
            yield return s.Wait(e => e.Kind == EventKind.StatusLose && e.Id == Dynamic && s.Mine(e.Target));
            if (wind.Length > 0) yield return s.Say(wind);
        }
        else if (anyDynamic is not null)
        {
            yield return s.Later("Dynamics Soon", dynamicLead);
        }
    }

    // Where the first two clones hit tells you the whole pattern: the line
    // starts opposite the first one and runs the other way round.
    private static IEnumerator<Step> LimitCut(Run s)
    {
        // The clones strike one at a time, in order, stepping exactly one
        // eighth around the ring on each hit. That regularity is the whole
        // answer: given where any one of them stood and how far along the order
        // it was, every other position follows, including the first.
        //
        // It matters because a recording does not stamp a position on every
        // clone. Some report the exact middle of the room, which is nowhere any
        // of them stands, and on one pull half of them did. Reading the first
        // two and giving up when either is blank throws the mechanic away.
        // Picking two readable ones and treating them as the first two invents
        // a starting point. Stepping back along the order does neither.
        //
        // This also shares its opening cast with the raidwide, and the clones
        // do not start hitting for over a minute after it, so the wait outlasts
        // the ordinary one by a long way.
        var order = new List<uint>();
        var placed = new List<(int At, Way Way)>();
        var step = 0;
        var found = false;

        for (var look = 0; look < MaxHitsRead && !found; look++)
        {
            yield return s.Wait(e => e.Kind == EventKind.Ability && e.Id == LimitCutHit, LongWait);
            var hit = s.First;
            if (hit.Source.Id == 0 || order.Contains(hit.Source.Id)) continue;

            order.Add(hit.Source.Id);
            var corner = Corner(s, hit);
            if (corner == Way.Unknown) continue;

            placed.Add((order.Count - 1, corner));
            if (placed.Count < 2) continue;

            // Against the one before it, and only while the two are close
            // enough together for the turn between them to have one reading:
            // half the ring apart is the same number either way round.
            var (wasAt, wasWay) = placed[^2];
            var (nowAt, nowWay) = placed[^1];
            var gap = nowAt - wasAt;
            if (gap is < 1 or > 3) continue;

            var turn = wasWay.SixteenthsTo(nowWay);
            if (turn % gap != 0) continue;

            var each = turn / gap;
            if (each is not (2 or -2)) continue;

            step = each;
            found = true;
        }

        // Nothing readable enough to lay the line out. The number is still
        // worth saying and is said on its own, rather than pointed somewhere
        // invented.
        if (!found)
        {
            yield return s.Wait(
                e => e.Kind == EventKind.HeadMarker && s.Mine(e.Target) && Number(e.Id) > 0, LongWait);
            yield return s.Say($"{Number(s.First.Id)}");
            yield break;
        }

        // Back along the order to where the first clone stood.
        var (anchorAt, anchorWay) = placed[0];
        var from1 = anchorWay.Plus(-step * anchorAt);

        var startedClockwise = step > 0;
        var clockwise = !startedClockwise;
        var start = from1.Opposite();

        yield return s.Say($"Starting {Cap(start)} -> {(clockwise ? "Clockwise" : "CCW")}");

        yield return s.Wait(e => e.Kind == EventKind.HeadMarker && s.Mine(e.Target) && Number(e.Id) > 0);
        var number = Number(s.First.Id);
        var spot = start.PlusEighths((number - 1) * (clockwise ? 1 : -1));

        yield return s.Say($"{number} {Cap(spot)} {(clockwise ? "CW" : "CCW")}");
    }

    // Sides then front and back, or the other way round.
    private static IEnumerator<Step> Implosion(Run s)
    {
        // Earthquake casts these too, and calls them as part of a longer
        // sentence that says where to start, where to move and where to finish.
        // Saying "Sides" over the top of that is two calls for one mechanic and
        // the shorter one is the wrong one.
        if (DuringEarthquake(s)) yield break;

        var longways = s.Start.Id == Longitudinal;
        yield return s.Say(longways ? "Sides Then Front/Back" : "Front/Back then Sides");

        yield return s.Hit(LatLongResolve);
        yield return s.Say(longways ? "Front/Back" : "Sides");
    }

    private static IEnumerator<Step> Stomp(Run s)
    {
        yield return s.Say("Bait Blizzards then Stacks");

        yield return s.Cast(Blizzard);
        yield return s.Say("Move");

        yield return s.Wait(e => e.Kind == EventKind.HeadMarker && e.Id == StompStack);
        var on = s.State.Noted($"job:{s.First.Target.Id:X}");
        yield return s.Say($"Stack on {(on == "support" ? "Support" : "DPS")}");

        yield return s.Cast(Blizzard);
        yield return s.Say("Move");

        // Both ways round the reference's own branch end on the same word, so
        // this waits for the hit and says it once.
        yield return s.Hit(StompHit);
        yield return s.Say("Swap");

        yield return s.Cast(BigBang);
        yield return s.Say("Away from Stacks, Keep Moving");

        yield return s.WaitAll(2, e => e.Kind == EventKind.CastStart && IsEnding(e.Id));
        yield return s.Got.Any(e => e.Id is FailedA or FailedB)
            ? s.Say("Failed", CallSeverity.Danger)
            : s.Say("Enrage", CallSeverity.Danger);
    }

    private static bool DuringEarthquake(Run s)
    {
        var noted = s.State.Noted(DancingMadEarthquake.RunningNote);
        if (noted.Length == 0) return false;
        if (!float.TryParse(noted, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var began)) return false;
        return s.Start.Time - began <= DancingMadEarthquake.RunsFor;
    }

    // Which corner of the room something stood in when it hit.
    private static Way Corner(Run s, GameEvent? e)
    {
        if (e is null) return Way.Unknown;
        var at = s.At(e.Source.Id);
        if (!at.Known) at = e.Source.At;
        if (!at.Known) return Way.Unknown;

        var floor = new Floor(Territory, "", CenterX, CenterY, Reach, CloneBand, CloneBand,
            Square: false, Authored: true);
        var way = floor.Sector(at);
        return way == Way.Middle ? Way.Unknown : way;
    }

    // Capitalised, so a worked out direction reads like the ones written by
    // hand next to it.
    private static string Cap(Way w)
    {
        var word = w.Name();
        return word.Length > 0 ? char.ToUpperInvariant(word[0]) + word[1..] : word;
    }

    private static bool IsWind(uint id) => id is Entropy or Dynamic or Headwind or Tailwind;

    private static bool IsEnding(uint id) => id is EnrageA or EnrageB or FailedA or FailedB;

    // The eight limit cut numbers, in the two runs of marker ids the game uses.
    private static int Number(uint marker) => marker switch
    {
        336 => 1, 337 => 2, 338 => 3, 339 => 4,
        437 => 5, 438 => 6, 439 => 7, 440 => 8,
        _ => 0,
    };

    private static bool Support(uint job) => job is 19 or 21 or 32 or 37 or 24 or 28 or 33 or 40;
}
