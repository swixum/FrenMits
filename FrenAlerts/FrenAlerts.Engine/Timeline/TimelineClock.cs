namespace FrenAlerts.Engine;

// A mechanic the timeline expects, and how long until it lands.
public readonly record struct Upcoming(string Mechanic, float At, double In);

// Where the fight is on its own timeline, and what is coming.
//
// The clock runs off the event stream's own times, never off the frame, so a
// recording replays to the same numbers as the pull it came from.
//
// It starts unsure rather than at zero. A fight written in phase blocks has
// nothing to say until its first anchor fires, and saying "in 3" against a
// clock that has not been anchored is worse than saying nothing.
public sealed class TimelineClock
{
    private readonly Timeline _timeline;
    private readonly TimelineSyncing.Windows _windows;
    private readonly HashSet<(uint Ability, float Time)> _fired = [];

    // Timeline second zero, expressed in event-stream time.
    private double _base;

    public TimelineClock(Timeline timeline, TimelineSyncing.Windows? windows = null)
    {
        _timeline = timeline;
        _windows = windows ?? TimelineSyncing.For(8f, 60f, 2000f, timeline.CountsFromAPhaseBase);
        Running = !timeline.CountsFromAPhaseBase;
    }

    // False until something has anchored the clock, for a fight that needs it.
    public bool Running { get; private set; }

    public int Resyncs { get; private set; }

    // The running average of how far out the clock was found to be, in seconds.
    // Positive means the clock was ahead of the fight.
    public double Drift { get; private set; }

    public double At(double now) => now - _base;

    public void Start(double now)
    {
        _base = now;
        _fired.Clear();
        Resyncs = 0;
        Drift = 0;
        Running = !_timeline.CountsFromAPhaseBase;
    }

    // Offers an event to the clock. Returns the anchor it snapped to, if any.
    //
    // A cast anchors on when it will resolve, not on when it started, because
    // that is the moment the timeline is written against.
    public TimelineSync? Feed(in GameEvent e)
    {
        if (e.Kind is EventKind.CombatStart)
        {
            Start(e.Time);
            return null;
        }
        if (e.Kind is not (EventKind.CastStart or EventKind.AbilityHit)) return null;

        var resolvesIn = e.Kind == EventKind.CastStart ? e.CastTime : 0f;
        var clock = At(e.Time) + resolvesIn;

        var best = TimelineSyncing.Choose(_timeline.Syncs, e.Id, clock, _windows, _fired);
        if (best is not { } anchor) return null;

        Drift = TimelineSyncing.Ema(Drift, Resyncs, TimelineSyncing.Drift(anchor, clock));
        _fired.Add(TimelineSyncing.Key(anchor));

        // Put the clock where the anchor says it should be, counting the cast's
        // remaining time so the snap lands on the resolve.
        _base = e.Time + resolvesIn - anchor.Time;
        Resyncs++;
        Running = true;
        return anchor;
    }

    // What the fight expects next, soonest first.
    public IEnumerable<Upcoming> Next(double now, int count = 5)
    {
        if (!Running || count <= 0) yield break;

        var clock = At(now);
        var given = 0;
        foreach (var entry in _timeline.Entries)
        {
            if (entry.Time < clock) continue;
            yield return new Upcoming(entry.Mechanic, entry.Time, entry.Time - clock);
            if (++given >= count) yield break;
        }
    }
}
