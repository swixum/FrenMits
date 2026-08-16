namespace FrenAlerts.Engine;

public readonly record struct GameEvent
{
    public required EventKind Kind { get; init; }

    public required double Time { get; init; }

    public uint SourceId { get; init; }
    public uint TargetId { get; init; }

    public uint Id { get; init; }

    // Status duration in seconds, or 0 where the kind carries none.
    public float Duration { get; init; }

    // Cast time for CastStart, so a trigger can fire relative to the resolve
    // rather than the start without needing the action sheet.
    public float CastTime { get; init; }

    public Position Source { get; init; } = Position.None;
    public Position Target { get; init; } = Position.None;

    public GameEvent() { }

    public override string ToString() =>
        $"{Time,8:F3} {Kind} id={Id:X} src={SourceId:X8} tgt={TargetId:X8}";
}
