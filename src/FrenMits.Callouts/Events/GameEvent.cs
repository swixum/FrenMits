namespace FrenMits.Callouts;

// One thing that happened, normalized so a hook and a replayed log look alike.
public sealed record GameEvent
{
    public EventKind Kind { get; init; }

    // Seconds from an origin the producer picks; a pull start once segmented.
    public float Time { get; init; }

    // Ability, status, head marker or tether id, by kind.
    public uint Id { get; init; }

    public string Name { get; init; } = "";

    public Actor Source { get; init; } = Actor.Nobody;
    public Actor Target { get; init; } = Actor.Nobody;

    // Cast seconds, status duration or damage, by kind.
    public float Value { get; init; }

    // Status stacks, raw damage word or a director command, by kind.
    public uint Extra { get; init; }

    // Ability flags, map effect flags or director data, by kind.
    public uint Flags { get; init; }

    public override string ToString()
        => $"{Time:0.00} {Kind} {Id:X} {Name} {Source.Name} -> {Target.Name}";
}
