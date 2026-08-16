namespace FrenAlerts.Engine;

public sealed class CallScheduler
{
    public const int MaxRecent = 256;

    private readonly Dictionary<string, double> _lastByKey = new(MaxRecent);

    private readonly HashSet<string> _saidOnce = [];

    private Call? _last;

    // Two calls closer together than this collide; the loser is dropped.
    public double MinGap { get; init; } = 0.7;

    // The same key inside this window is the same call happening twice.
    public double DuplicateWindow { get; init; } = 3.0;

    public int Suppressed { get; private set; }
    public int Forgotten { get; private set; }

    public Dictionary<string, int> DroppedAsRepeat { get; } = [];
    public Dictionary<string, int> DroppedForCrowding { get; } = [];

    public Dictionary<string, string> LostTo { get; } = [];

    public Call? Offer(Call call)
    {
        if (call.Once && _saidOnce.Contains(call.Key))
        {
            Suppressed++;
            Tally(DroppedAsRepeat, call.Key);
            return null;
        }

        var window = call.Hush > 0 ? call.Hush : DuplicateWindow;
        if (_lastByKey.TryGetValue(call.Key, out var seen) && call.Time - seen < window)
        {
            Suppressed++;
            Tally(DroppedAsRepeat, call.Key);
            return null;
        }

        if (_last is { } prev && call.Time - prev.Time < MinGap && !Beats(call, prev))
        {
            Suppressed++;
            Tally(DroppedForCrowding, call.Key);
            LostTo.TryAdd(call.Key, prev.Key);
            return null;
        }

        Remember(call);
        _last = call;
        return call;
    }

    private void Remember(Call call)
    {
        if (call.Once && _saidOnce.Count < MaxRecent) _saidOnce.Add(call.Key);

        if (_lastByKey.Count >= MaxRecent && !_lastByKey.ContainsKey(call.Key))
        {
            // Oldest goes, so the bound holds without forgetting what just fired.
            var oldest = _lastByKey.OrderBy(p => p.Value).First().Key;
            _lastByKey.Remove(oldest);
            Forgotten++;
        }
        _lastByKey[call.Key] = call.Time;
    }

    private static bool Beats(Call incoming, Call standing)
    {
        if (incoming.Level != standing.Level) return incoming.Level > standing.Level;
        return incoming.Personal && !standing.Personal;
    }

    // Bounded the same way the key table is, so a pathological fight cannot grow
    // the diagnosis itself without limit.
    private static void Tally(Dictionary<string, int> into, string key)
    {
        if (into.Count >= MaxRecent && !into.ContainsKey(key)) return;
        into[key] = into.GetValueOrDefault(key) + 1;
    }

    public void Reset()
    {
        _lastByKey.Clear();
        _saidOnce.Clear();
        _last = null;
        Suppressed = 0;
        Forgotten = 0;
        DroppedAsRepeat.Clear();
        DroppedForCrowding.Clear();
        LostTo.Clear();
    }
}
