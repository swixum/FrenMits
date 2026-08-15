using System.Collections.Generic;

namespace FrenMits.Callouts;

// One pull's events, capped so a long fight or a stuck pull cannot grow forever.
public sealed class EventBuffer
{
    public const int MaxEvents = 20000;

    private readonly List<GameEvent> _events = new();

    public IReadOnlyList<GameEvent> Events => _events;

    public int Count => _events.Count;

    // How many were refused since the last reset, so the cap is never silent.
    public int Dropped { get; private set; }

    public bool Full => _events.Count >= MaxEvents;

    public bool Add(GameEvent e)
    {
        if (Full) { Dropped++; return false; }
        _events.Add(e);
        return true;
    }

    // Called on every pull edge and on zone change, so nothing carries over.
    public void Reset()
    {
        _events.Clear();
        Dropped = 0;
    }
}
