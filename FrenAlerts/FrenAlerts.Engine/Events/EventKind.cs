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
}
