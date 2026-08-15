using System;
using System.Collections.Generic;

namespace FrenMits.Callouts;

// What the directing came to.
public enum Advice
{
    // Already safe. The right answer more often than not, and saying nothing is
    // the whole point of it.
    Clear = 0,

    // Go that way.
    Move,

    // Away from the middle, any way. When the whole ring is clear the compass
    // adds nothing, and "east" would send one player where nobody else went.
    Out,

    // No floor, no room, nothing modelled, or a danger that leaves so much of
    // the room open that the geometry does not decide where to stand. Never
    // spoken as a direction.
    Unknown,
}

// Somewhere to be, named against the floor rather than against the player, so
// "north" means the north of the room to everyone who hears it.
public readonly record struct WayOut(Advice Advice, Way To, float Distance)
{
    public static readonly WayOut Clear = new(Advice.Clear, Way.Unknown, 0f);

    public static readonly WayOut Unknown = new(Advice.Unknown, Way.Unknown, 0f);

    public static readonly WayOut Out = new(Advice.Out, Way.Unknown, 0f);

    public bool Speaks => Advice == Advice.Out || (Advice == Advice.Move && To != Way.Unknown);

    public string Spoken => Advice switch
    {
        Advice.Out => "out",
        Advice.Move when To != Way.Unknown => To.Name(),
        _ => "",
    };

    public string Short => Advice switch
    {
        Advice.Out => "Out",
        Advice.Move when To != Way.Unknown => To.Short(),
        _ => "",
    };
}

// Turns a danger zone into somewhere to stand. This is the difference between
// "get out" and "get out, north", which is the whole job.
//
// Every direction here is the floor's, measured from its middle. A word that
// meant "east of where you happen to be" would send half the party into each
// other, so a floor nobody has measured or watched long enough gets Unknown
// rather than a guess.
public static class Director
{
    // Room to leave between a spot and the danger. A player is half a yalm
    // wide, a snapshot lands before the animation, and nobody runs a straight
    // line, so a spot that only just clears is not a spot.
    public const float Margin = 1.5f;

    // Room enough that a small mistake still lives. The nearest spot with this
    // much space wins over a roomier one further away.
    public const float Comfort = 4f;

    // Distances and clearances within this of each other count as equal.
    public const float SameRoom = 0.5f;

    // How far along a sector's spoke to check, as a share of the floor. A word
    // has to be true of the place it names, not just of one point somebody
    // scanned, so each part of the room is judged where a player would stand
    // in it.
    private static readonly float[] Spokes = [0.45f, 0.6f, 0.75f, 0.9f];

    // A run this short is a sidestep, and a sidestep has no direction worth
    // saying. Measured on real pulls: a three yalm melee cleave and a five yalm
    // circle both had the model naming a corner of the room, while the players
    // who lived had simply stepped aside where they stood. Naming a part of the
    // room only helps when the mechanic is the size of one.
    public const float Sidestep = 6f;

    private static readonly float[] Aside = [Sidestep / 2f, Sidestep];

    // How big a mechanic has to be, against the floor, before a part of the
    // room is the right unit to answer in. A five yalm circle on one player is
    // not a room mechanic, and calling a corner of the room for it sends people
    // running from something they could have stepped out of.
    public const float RoomShare = 0.35f;

    // Somewhere clear of the zone, named against the floor. Clear when the
    // player is already safe, Unknown when there is no floor to name against or
    // when no part of the room is worth naming.
    public static WayOut Escape(DangerZone zone, Spot from, Arena arena, Ring ring = Ring.Eight)
    {
        if (!from.Known || !zone.Shape.Known) return WayOut.Unknown;
        if (!zone.Covers(from)) return WayOut.Clear;

        var floor = arena.Floor;
        if (!floor.Known) return WayOut.Unknown;
        if (zone.Shape.Range < floor.Radius * RoomShare) return WayOut.Unknown;
        if (StepAside(zone, floor, from)) return WayOut.Unknown;

        // The part of the room the player is already in stays on the list: a
        // player caught near the middle of the east side still has to run east,
        // and the place named is checked for room either way.
        var roomy = new List<Candidate>();
        foreach (var place in Places(ring))
        {
            var found = Best(zone, floor, from, place);
            if (found is { } candidate && candidate.Room >= Comfort) roomy.Add(candidate);
        }

        if (roomy.Count == 0) return WayOut.Unknown;

        // The whole ring open and only the middle taken is a donut, and a donut
        // means out, not east.
        var ring_ = Ring_(ring);
        var middleTaken = !roomy.Exists(c => c.Named == Way.Middle);
        if (middleTaken && roomy.Count >= ring_.Count) return WayOut.Out;

        // Anything else that leaves most of the room usable is a mechanic the
        // strat decides, not the geometry. Measured on real ultimate pulls:
        // naming the nearest safe place for those sent players somewhere no
        // teammate was standing four times out of five.
        if (roomy.Count > Decisive) return WayOut.Unknown;

        Candidate? pick = null;
        foreach (var candidate in roomy)
            if (candidate.Nearer(pick)) pick = candidate;

        if (pick is not { } go) return WayOut.Unknown;
        return new WayOut(Advice.Move, go.Named, go.Travel);
    }

    // How few places have to be left before the danger, rather than the group's
    // plan, is what decides where to stand.
    public const int Decisive = 3;

    private static IReadOnlyList<Way> Ring_(Ring ring) => ring switch
    {
        Ring.Sixteen => Compass.Sixteenths,
        Ring.Cardinal => Compass.Cardinals,
        Ring.Intercard => Compass.Intercards,
        _ => Compass.Eighths,
    };

    // Every place a call is allowed to name on this ring, the middle included.
    private static IEnumerable<Way> Places(Ring ring)
    {
        yield return Way.Middle;
        foreach (var way in Ring_(ring)) yield return way;
    }

    // The best a player could do by going to one named part of the room, or
    // nothing when standing there would not help.
    //
    // A sector is a wedge, so anywhere along it will do and the roomiest spot
    // stands for the whole thing. The middle is a place you go to rather than a
    // direction you run in, so it only counts when all of it is usable: "get
    // in" is wrong if the middle of the middle is where the cast is coming
    // from.
    private static Candidate? Best(DangerZone zone, Floor floor, Spot from, Way place)
    {
        if (place == Way.Middle) return Whole(zone, floor, from);

        Candidate? best = null;
        foreach (var at in Standing(floor, place))
        {
            if (!floor.Inside(at)) continue;

            var room = zone.Clearance(at);
            if (room < Margin) continue;

            var candidate = new Candidate(place, room, from.DistanceTo(at));
            if (candidate.Roomier(best)) best = candidate;
        }
        return best;
    }

    private static Candidate? Whole(DangerZone zone, Floor floor, Spot from)
    {
        var least = float.MaxValue;
        foreach (var at in Standing(floor, Way.Middle))
        {
            if (!floor.Inside(at)) continue;

            var room = zone.Clearance(at);
            if (room < Margin) return null;
            least = MathF.Min(least, room);
        }

        if (least is float.MaxValue) return null;
        return new Candidate(Way.Middle, least, from.DistanceTo(floor.Middle));
    }

    private static IEnumerable<Spot> Standing(Floor floor, Way place)
    {
        if (place == Way.Middle)
        {
            // The middle has room in it like anywhere else, so it is judged on
            // more than its one exact point.
            yield return floor.Middle;
            foreach (var way in Compass.Eighths)
            {
                var (dx, dy) = way.Step();
                yield return new Spot(floor.CenterX + dx * floor.BandX, floor.CenterY + dy * floor.BandY, 0f);
            }
            yield break;
        }

        foreach (var spoke in Spokes)
            yield return floor.At(place, floor.Radius * spoke);
    }

    // Could the player just move their feet? If somewhere within a sidestep is
    // roomy enough, the mechanic is smaller than the room's own parts and no
    // compass word describes the answer.
    private static bool StepAside(DangerZone zone, Floor floor, Spot from)
    {
        foreach (var step in Aside)
            foreach (var way in Compass.Sixteenths)
            {
                var at = way.From(from, step);
                if (floor.Inside(at) && zone.Clearance(at) >= Comfort) return true;
            }
        return false;
    }

    // Which way the middle is, for a call that wants you off the wall. Middle
    // when the player is already there.
    public static Way ToMiddle(Spot from, Arena arena)
    {
        var floor = arena.Floor;
        if (!floor.Known || !from.Known) return Way.Unknown;
        if (floor.IsMiddle(from)) return Way.Middle;
        return Compass.Of(floor.Middle, from, Ring.Eight);
    }

    // Where a spot is, by bearing from the middle. What a fight means when its
    // safe spots sit on a circle.
    public static Way Where(Spot at, Arena arena, Ring ring = Ring.Eight)
        => arena.Floor.Where(at, ring);

    // Where a spot is, by which ninth of the room it falls in. What a fight
    // means when it splits the floor into quadrants.
    public static Way Sector(Spot at, Arena arena) => arena.Floor.Sector(at);

    // Which side of an actor something is on, from that actor's own view.
    public static Side SideOf(Actor a, Spot at) => Compass.SideOf(a.At, a.Heading, at);

    // One place to stand, with everything the ranking needs.
    private readonly record struct Candidate(Way Named, float Room, float Travel)
    {
        // The shorter run wins, since both places are roomy enough already.
        public bool Nearer(Candidate? against)
        {
            if (against is not { } other) return true;
            if (Same(Travel, other.Travel)) return Roomier(other);
            return Travel < other.Travel;
        }

        // Failing that, whichever leaves the most room.
        public bool Roomier(Candidate? against)
        {
            if (against is not { } other) return true;
            if (Same(Room, other.Room)) return (int)Named < (int)other.Named;
            return Room > other.Room;
        }

        // Two numbers this close are the same number. Without this the answer
        // flips between two equally good places on floating point noise.
        private static bool Same(float a, float b) => MathF.Abs(a - b) < SameRoom;
    }
}
