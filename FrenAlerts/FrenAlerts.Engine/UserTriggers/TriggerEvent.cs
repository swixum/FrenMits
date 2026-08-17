namespace FrenAlerts.Engine.UserTriggers;

// What a user trigger is matched against.
//
// Theirs read a parsed log line, which carried names beside ids and a kind of its
// own. Ours reads the engine's event and has the names looked up on the way in, so
// the same trigger written in their editor asks the same questions here.
public sealed record TriggerEvent
{
    public required TriggerEventKind Kind { get; init; }

    public required double Time { get; init; }

    // The words a trigger's pattern is matched against: the cast, the status, or the
    // chat line, whichever this event carries.
    public string Name { get; init; } = "";

    public uint DataId { get; init; }

    public uint SourceId { get; init; }
    public string SourceName { get; init; } = "";
    public ActorSide SourceSide { get; init; }

    public uint TargetId { get; init; }
    public string TargetName { get; init; } = "";
    public ActorSide TargetSide { get; init; }

    public uint IconId { get; init; }

    // A cast time or a status duration, which is what a countdown counts.
    public float Value { get; init; }

    public uint Count { get; init; }

    public uint Category { get; init; }
    public uint Param1 { get; init; }
    public uint Param2 { get; init; }
    public uint Param3 { get; init; }
    public uint Param4 { get; init; }

    public bool IsStatus => Kind is TriggerEventKind.StatusGain or TriggerEventKind.StatusLose;

    public bool IsCast => Kind is TriggerEventKind.CastStart or TriggerEventKind.CastFinish
        or TriggerEventKind.Ability;

    // The engine's own event, in the shape a user trigger reads. The names and the
    // sides cannot be read from the event itself, so they are handed in by whoever
    // knows the object table.
    public static TriggerEvent? From(
        in GameEvent e, Func<uint, string>? nameOf = null, Func<uint, ActorSide>? sideOf = null,
        string what = "")
    {
        var kind = KindOf(e.Kind);
        if (kind is null) return null;

        var name = nameOf ?? (_ => "");
        var side = sideOf ?? (_ => ActorSide.Other);

        return new TriggerEvent
        {
            Kind = kind.Value,
            Time = e.Time,
            Name = what,
            DataId = e.Id,
            SourceId = e.SourceId,
            SourceName = name(e.SourceId),
            SourceSide = side(e.SourceId),
            TargetId = e.TargetId,
            TargetName = name(e.TargetId),
            TargetSide = side(e.TargetId),
            Value = e.Kind == EventKind.CastStart ? e.CastTime : e.Duration,
            Count = e.Param,
            Category = e.Kind == EventKind.ActorControl ? e.Id : 0,
            Param1 = e.Arg1,
            Param2 = e.Arg2,
        };
    }

    private static TriggerEventKind? KindOf(EventKind kind) => kind switch
    {
        EventKind.CastStart => TriggerEventKind.CastStart,
        EventKind.CastCancel => TriggerEventKind.CastFinish,
        EventKind.AbilityHit => TriggerEventKind.Ability,
        EventKind.StatusGain => TriggerEventKind.StatusGain,
        EventKind.StatusLose => TriggerEventKind.StatusLose,
        EventKind.HeadMarker => TriggerEventKind.Headmarker,
        EventKind.Tether => TriggerEventKind.Tether,
        EventKind.ActorSpawn => TriggerEventKind.Added,
        EventKind.ActorControl => TriggerEventKind.ActorControl,
        EventKind.MapEffect => TriggerEventKind.MapEffect,
        EventKind.ActorMoved => TriggerEventKind.ActorMove,
        EventKind.NpcYell => TriggerEventKind.NpcYell,
        _ => null,
    };
}

public enum TriggerEventKind : byte
{
    CastStart,
    CastFinish,
    StatusGain,
    StatusLose,
    Death,
    Ability,
    Headmarker,
    Tether,
    Added,
    ActorControl,
    MapEffect,
    Chat,
    ActorMove,
    NpcYell,
}

public enum ActorSide : byte
{
    Other,
    You,
    Party,
    Enemy,
}

// What the engine cannot know for itself: who you are, where you are, and who
// everybody else is. Handed in so the whole matcher stays testable offline.
public interface ITriggerWorld
{
    ushort Territory { get; }

    uint You { get; }

    RoleFilter YourRole { get; }

    RoleFilter RoleOf(uint actorId);

    // Health as a percentage, or below zero where nothing knows: their own number
    // conditions on health are skipped rather than guessed when it is unknown.
    float HealthPercent(uint actorId);

    string JobOf(uint actorId);

    // Whether somebody is already carrying a status, which is a different question
    // from whether one just arrived: their follow-ups check both, so a step armed
    // after the debuff landed still resolves.
    bool HasStatus(uint actorId, uint statusId, string namePart);
}

// Nothing known, which is what an offline run and a fresh pull both start as.
public sealed class NoWorld : ITriggerWorld
{
    public ushort Territory { get; set; }
    public uint You { get; set; }
    public RoleFilter YourRole { get; set; } = RoleFilter.Any;
    public Func<uint, RoleFilter>? Roles { get; set; }
    public Func<uint, float>? Health { get; set; }
    public Func<uint, string>? Jobs { get; set; }
    public Func<uint, uint, string, bool>? Statuses { get; set; }

    public bool HasStatus(uint actorId, uint statusId, string namePart) =>
        Statuses?.Invoke(actorId, statusId, namePart) ?? false;

    public RoleFilter RoleOf(uint actorId) => Roles?.Invoke(actorId) ?? RoleFilter.Any;

    public float HealthPercent(uint actorId) => Health?.Invoke(actorId) ?? -1f;

    public string JobOf(uint actorId) => Jobs?.Invoke(actorId) ?? "";
}
