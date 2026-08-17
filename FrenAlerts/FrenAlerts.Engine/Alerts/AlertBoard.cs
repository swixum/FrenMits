namespace FrenAlerts.Engine.Alerts;

public sealed class AlertBoard
{
    public const int Capacity = 4;

    // The same clock the fight runs on, or a call counts down at one speed while the
    // fight it belongs to runs at another. Live those are both the wall; in a replay
    // the fight's clock is the recording's position, and this follows it.
    public Func<double> Clock { get; set; } = static () => Environment.TickCount64 / 1000d;

    public double Now => Clock();

    public Func<Call, Call?> Decide { get; set; } = static c => c;

    public readonly record struct Shown(
        Call Call, double At, double FireAt, double EndsAt, CallIcon Icon = default)
    {
        // Still counting down, rather than at the moment it means go.
        public bool Counting(double now) => now < FireAt;

        public float Remaining(double now) => (float)Math.Max(0d, FireAt - now);

        public float Fraction(double now)
        {
            var lead = FireAt - At;
            if (lead <= 0.001d) return 1f;
            return (float)Math.Clamp((FireAt - now) / lead, 0d, 1d);
        }
    }

    private readonly List<Shown> _items = new(Capacity);
    private readonly object _gate = new();

    public int Dropped { get; private set; }

    public int Count { get { lock (_gate) return _items.Count; } }

    // True when the call is on the board. False means it is not, for either reason:
    // Decide threw it out (the master switch is off, this fight is muted, or the
    // player switched this one call off), or the board was full and this was the one
    // that did not fit.
    //
    // Reported rather than swallowed, because the voice is a separate path. It used
    // to speak whatever it was handed, so a fight turned off, or the whole plugin
    // turned off, went quiet on screen and kept talking out loud.
    //
    // The full case was the same fault one step further along. It returned true the
    // moment Decide let the call through, then sorted it into the list and dropped
    // whatever sat furthest out, which can be the call just added: five things landing
    // at once put the fifth on the floor and read it out anyway.
    //
    // Only ever a call the board did not already hold. Replacing frees the slot it is
    // about to fill, so a mechanic firing again cannot fail to fit however full the
    // board is.
    public bool Show(Call call, double engineNow, CallIcon icon = default)
    {
        if (Decide(call) is not { } shown) return false;
        call = shown;

        var now = Clock();
        var lead = Math.Max(0d, call.Time - engineNow);
        var fireAt = now + lead;
        var endsAt = fireAt + Math.Max(0f, call.Hold);
        var entry = new Shown(call, now, fireAt, endsAt, icon);

        lock (_gate)
        {
            // The same key twice is the same call again: it replaces rather than
            // stacks, or a re-fired mechanic reads as two of it on screen.
            _items.RemoveAll(s => s.Call.Key == call.Key);
            _items.Add(entry);
            _items.Sort(static (a, b) => a.FireAt.CompareTo(b.FireAt));
            while (_items.Count > Capacity)
            {
                _items.RemoveAt(_items.Count - 1);   // the one furthest out
                Dropped++;
            }

            // By value rather than by key: a call with no key of its own would match
            // every other keyless one, and this has to be about the entry just made.
            return _items.Contains(entry);
        }
    }

    // What is on screen now, as a list of its own.
    //
    // It used to hand back a buffer the board kept and refilled, which made every
    // caller's answer the same object. The lock ends when this returns, so the next
    // caller emptied a list somebody else was still reading: the overlay walks these
    // on the render thread while the runner walks them on the framework thread, and
    // a Clear under a running foreach is an exception thrown out of a draw.
    //
    // A copy, because there are at most Capacity of them. Four entries a frame is not
    // worth a buffer that has to be reasoned about, and it was reasoned about wrongly.
    public IReadOnlyList<Shown> Live()
    {
        var now = Clock();
        lock (_gate)
        {
            _items.RemoveAll(s => now >= s.EndsAt);
            return _items.ToArray();
        }
    }

    // The live calls that belong in the stack, which is every one that did not name a
    // place of its own.
    //
    // Asked for by the stack in both the places it needs the answer: what to draw, and
    // whether to be on screen at all. A window opened for a call it will not draw is an
    // empty box sitting there with the background switched on, for as long as the call
    // that is somewhere else lasts.
    public List<Shown> Stacked()
    {
        var stacked = new List<Shown>();
        foreach (var shown in Live())
            if (!shown.Call.Placed) stacked.Add(shown);
        return stacked;
    }

    public void Clear()
    {
        lock (_gate) _items.Clear();
    }

    // Takes one call back off early, by the key it was shown under.
    //
    // A hand-written trigger can say "and clear this when the cast lands", which is
    // the difference between a warning that goes when the mechanic resolves and one
    // that sits there for its full time while the fight has moved on.
    public bool Drop(string key)
    {
        lock (_gate) return _items.RemoveAll(s => s.Call.Key == key) > 0;
    }

    public void ResetDropped()
    {
        lock (_gate) Dropped = 0;
    }
}
