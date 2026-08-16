namespace FrenAlerts.Engine;

public enum EventKind
{
    Unknown = 0,
    CastStart,
    CastCancel,
    AbilityHit,
    StatusGain,
    StatusLose,
    HeadMarker,
    Tether,
    MapEffect,
    ActorControl,
    ActorSpawn,
    ActorDespawn,
    ZoneChange,
    CombatStart,
    CombatEnd,

    // An actor already seen turning up somewhere else. Appended rather than slotted
    // in, because a recording writes these as numbers and renumbering them would
    // silently turn every old recording's tethers into map effects.
    ActorMoved,

    // A boss saying one of its scripted lines, carried by the row id of the line
    // rather than its words, so it reads the same in every language. Nael's
    // fourteen quotes in the Unending Coil are the whole reason this exists: the
    // quote IS the mechanic there, and nothing else in the fight announces it.
    // Appended, for the same reason ActorMoved was.
    NpcYell,

    // An actor becoming targetable or not, with Arg1 holding 1 for on and 0 for
    // off. A boss that hides to jump raises no cast and no marker, so this is the
    // only thing that says the jump has started: the Weapon's Refrain reads Titan's
    // last heading at exactly this moment. Appended, like the two above.
    NameToggle,
}
