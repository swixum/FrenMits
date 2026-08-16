namespace FrenAlerts.Engine;

// One mechanic on a fight's timeline, at the second it lands.
public readonly record struct TimelineEntry(float Time, string Mechanic);

// A cast the clock is allowed to correct itself against.
//
// IsPhase marks the ones that may move the clock a long way rather than nudge
// it: a phase change is the only honest reason for the clock to jump.
public readonly record struct TimelineSync(float Time, uint Ability, bool IsPhase);

// One fight's timeline, sorted, with the resyncs that keep it honest.
public sealed class Timeline
{
    public required ushort Territory { get; init; }
    public required IReadOnlyList<TimelineEntry> Entries { get; init; }
    public required IReadOnlyList<TimelineSync> Syncs { get; init; }

    // A duty whose timeline counts from a phase base rather than from the pull.
    // Dancing Mad is written this way: phase one starts at 1000, not at 0, so
    // until its first anchor fires there is nothing sensible to count down to.
    public bool CountsFromAPhaseBase => Entries.Count > 0 && Entries[0].Time >= BlockBase;

    // The step between one written block and the next.
    public const float BlockBase = 1000f;
}
