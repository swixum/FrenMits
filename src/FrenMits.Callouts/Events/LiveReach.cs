using System.Collections.Generic;

namespace FrenMits.Callouts;

// Which events a duty can actually raise, and which only a log has.
//
// The plugin reads the object table on its own tick, so it sees what is there,
// what it is casting, and what it is carrying. A map effect and a landed hit
// are neither: they are packets that touch no actor, so nothing on that tick
// can hand one over. A recording of a night has all of them, which is the trap.
// A trigger written on one passes every offline check and then never fires once
// in a duty, and a sequence that waits on one waits forever, which takes every
// call after it in that mechanic down with it quietly.
//
// Head markers and tethers used to be on that list. They arrive at the recap's
// actor-control detour as categories 34, 35 and 47, where they were being
// dropped, so the runner now takes them there rather than on the tick.
//
// So this is the contract: raise a new kind from the runner, add it here, and
// the check that reads it starts counting that kind as real.
public static class LiveReach
{
    // Exactly what CalloutRunner feeds the engine in a duty: the first five off
    // its tick, the last two off the actor-control detour.
    public static readonly IReadOnlySet<EventKind> Live = new HashSet<EventKind>
    {
        EventKind.CastStart,
        EventKind.StatusGain,
        EventKind.StatusLose,
        EventKind.ActorAdd,
        EventKind.Zone,
        EventKind.HeadMarker,
        EventKind.Tether,
    };

    public static bool Reachable(EventKind kind) => Live.Contains(kind);

    // Why one is out of reach, in the words of what would have to change.
    public static string Why(EventKind kind) => kind switch
    {
        EventKind.MapEffect => "map effects touch no actor, so the tick cannot see one",
        EventKind.ActorControl => "actor control is a packet, not a thing on an actor",
        EventKind.Ability => "a landed hit is a packet; the tick sees the cast, not the hit",
        EventKind.AbilityExtra => "the heading line is a packet, not a thing on an actor",
        EventKind.ActorRemove => "the tick notices what is gone, it does not raise it",
        EventKind.Director => "duty director lines are packets",
        EventKind.Self => "only a recording carries who it belongs to",
        _ => "not raised from a live tick",
    };
}
