namespace FrenAlerts.Engine;

public static class EventOrder
{
    // Stable on purpose: events sharing a timestamp keep the order the source gave
    // them, which is the only information available about what really came first.
    public static GameEvent[] Sorted(IEnumerable<GameEvent> events) =>
        events.OrderBy(e => e.Time).ToArray();

    public static bool IsOrdered(IReadOnlyList<GameEvent> events)
    {
        for (var i = 1; i < events.Count; i++)
            if (events[i].Time < events[i - 1].Time) return false;
        return true;
    }

    public static double WorstBackstep(IReadOnlyList<GameEvent> events)
    {
        var worst = 0.0;
        for (var i = 1; i < events.Count; i++)
        {
            var step = events[i - 1].Time - events[i].Time;
            if (step > worst) worst = step;
        }
        return worst;
    }
}
