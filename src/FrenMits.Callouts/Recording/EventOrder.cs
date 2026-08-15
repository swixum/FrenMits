using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Callouts;

// Log lines arrive slightly out of order, by up to about half a second on real
// logs, which is enough to put a head marker before the cast that caused it.
public static class EventOrder
{
    // Stable, so events sharing a timestamp keep the order they arrived in.
    public static List<GameEvent> InTimeOrder(IEnumerable<GameEvent> events)
        => events.OrderBy(e => e.Time).ToList();

    public static bool IsOrdered(IEnumerable<GameEvent> events)
    {
        var last = float.MinValue;
        foreach (var e in events)
        {
            if (e.Time < last) return false;
            last = e.Time;
        }
        return true;
    }
}
