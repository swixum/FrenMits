namespace FrenAlerts.Engine;

public sealed record Actor
{
    public required uint Id { get; init; }
    public string Name { get; init; } = "";

    // Only spawn lines carry a name id, so every later event has to be stamped
    // from the book rather than read directly.
    public uint NameId { get; init; }

    // Which kind of prop or monster this is, shared by every copy of it, so a
    // trigger can ask for "the crystals" without knowing this pull's ids.
    public uint DataId { get; init; }

    public uint MaxHp { get; init; }
    public bool IsPlayer { get; init; }

    public bool HasCast { get; init; }

    // Where it was last seen. Unknown until something carried a position for it,
    // never the origin, which is a real spot in the middle of every arena.
    public Position Where { get; init; } = Position.None;
}
