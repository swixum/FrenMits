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
    private readonly List<Shown> _live = new(Capacity);
    private readonly object _gate = new();

    public int Dropped { get; private set; }

    public int Count { get { lock (_gate) return _items.Count; } }

    public void Show(Call call, double engineNow, CallIcon icon = default)
    {
        if (Decide(call) is not { } shown) return;
        call = shown;

        var now = Clock();
        var lead = Math.Max(0d, call.Time - engineNow);
        var fireAt = now + lead;
        var endsAt = fireAt + Math.Max(0f, call.Hold);

        lock (_gate)
        {
            // The same key twice is the same call again: it replaces rather than
            // stacks, or a re-fired mechanic reads as two of it on screen.
            _items.RemoveAll(s => s.Call.Key == call.Key);
            _items.Add(new Shown(call, now, fireAt, endsAt, icon));
            _items.Sort(static (a, b) => a.FireAt.CompareTo(b.FireAt));
            while (_items.Count > Capacity)
            {
                _items.RemoveAt(_items.Count - 1);   // the one furthest out
                Dropped++;
            }
        }
    }

    public IReadOnlyList<Shown> Live()
    {
        var now = Clock();
        lock (_gate)
        {
            _items.RemoveAll(s => now >= s.EndsAt);
            _live.Clear();
            _live.AddRange(_items);
            return _live;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _items.Clear();
            _live.Clear();
        }
    }

    public void ResetDropped()
    {
        lock (_gate) Dropped = 0;
    }
}
