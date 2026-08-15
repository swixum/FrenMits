using System.Collections.Generic;

namespace FrenMits.Callouts;

// One attempt at a fight, with times rebased so the first hostile act is zero.
public sealed record Pull
{
    public int Index { get; init; }

    // Where this pull started in the source recording, kept so a fixture can
    // be traced back to the log it came from.
    public float SourceStart { get; init; }

    public float Duration { get; init; }

    public IReadOnlyList<GameEvent> Events { get; init; } = new List<GameEvent>();

    // The duty this happened in, which is what picks a trigger set.
    public uint Territory { get; init; }

    // The name id of the biggest thing in the pull, once something sets it.
    public uint BossNameId { get; init; }

    public string BossName { get; init; } = "";

    public override string ToString()
        => $"pull {Index}: {Duration:0}s, {Events.Count} events, {BossName} (territory {Territory})";
}
