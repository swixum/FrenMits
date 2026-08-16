using System.Collections.Concurrent;
using FrenAlerts.Engine;

namespace FrenAlerts.Game;

// A bounded handover from a hook's thread to the frame.
public sealed class EventQueue
{
    private readonly ConcurrentQueue<GameEvent> _queue = new();

    private int _queued;

    public EventQueue(int max) => Max = max;

    // Past this the oldest are refused rather than queued, because a queue that
    // grows without limit ends the night as a stutter.
    public int Max { get; }

    private int _reported;
    private int _dropped;

    public int Reported => Volatile.Read(ref _reported);

    // Non-zero means the drain stopped, not that the pull was busy.
    public int Dropped => Volatile.Read(ref _dropped);

    // Called from a detour, so it never blocks and never throws. The counters are
    // interlocked like the depth is: they are written from the game's threads and
    // read from the frame, and a plain increment loses updates between the two.
    public bool Offer(GameEvent e)
    {
        if (Volatile.Read(ref _queued) >= Max)
        {
            Interlocked.Increment(ref _dropped);
            return false;
        }

        _queue.Enqueue(e);
        Interlocked.Increment(ref _queued);
        Interlocked.Increment(ref _reported);
        return true;
    }

    public IEnumerable<GameEvent> Drain()
    {
        while (_queue.TryDequeue(out var e))
        {
            Interlocked.Decrement(ref _queued);
            yield return e;
        }
    }

    public void Clear()
    {
        while (_queue.TryDequeue(out _)) Interlocked.Decrement(ref _queued);
        Interlocked.Exchange(ref _dropped, 0);
    }
}
