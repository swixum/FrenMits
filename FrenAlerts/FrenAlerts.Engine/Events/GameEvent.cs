namespace FrenAlerts.Engine;

public readonly record struct GameEvent
{
    public required EventKind Kind { get; init; }

    public required double Time { get; init; }

    public uint SourceId { get; init; }
    public uint TargetId { get; init; }

    public uint Id { get; init; }

    // Which kind of thing this actor is, rather than which one it is. Two black
    // holes have two entity ids and one of these, so a call can recognise the prop
    // without being told its id first.
    public uint DataId { get; init; }

    // What a control packet carried. Kept as whole numbers beside Duration, because
    // these routinely run past what a float counts exactly and a direction read off
    // a rounded id is a direction pointing at the wrong tower.
    public uint Arg1 { get; init; }
    public uint Arg2 { get; init; }

    // What a status carries besides its id: stacks, or the number a fight hides its
    // real answer in. Neo Exdeath says which half of its debuffs are lying entirely
    // through this, so a status without it is a mechanic that cannot be called.
    public ushort Param { get; init; }

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
