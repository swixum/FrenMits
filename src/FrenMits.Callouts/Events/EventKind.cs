namespace FrenMits.Callouts;

// The kinds of thing a fight reports, from a live hook or a replayed log.
public enum EventKind
{
    Unknown = 0,
    CastStart,
    Ability,
    StatusGain,
    StatusLose,
    HeadMarker,
    Tether,
    MapEffect,

    // The line that carries a cast's true heading, which the ability line does
    // not always get right.
    AbilityExtra,

    // The game telling the client something about an actor: which tower lit up,
    // which add turned. Keyed by its category rather than by an ability.
    ActorControl,
    ActorAdd,
    ActorRemove,
    Director,

    // Who the recording belongs to, so personal calls survive a round trip.
    Self,

    // Which duty this is, which is what picks the trigger set.
    Zone,
}
