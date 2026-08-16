namespace FrenAlerts.Engine;

public sealed record Actor
{
    public required uint Id { get; init; }
    public string Name { get; init; } = "";

    // Only spawn lines carry a name id, so every later event has to be stamped
    // from the book rather than read directly.
    public uint NameId { get; init; }

    public uint MaxHp { get; init; }
    public bool IsPlayer { get; init; }

    public bool HasCast { get; init; }
}
