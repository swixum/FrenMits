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

    public Call? Offer(Call call) => Offer(call, out _);

    // The same decision, saying why when it says no. The counters answer "how much
    // was dropped"; this answers "why was that one call missing", which is the
    // question somebody asks after a pull rather than during it.
    public Call? Offer(Call call, out string dropped)
    {
        dropped = "";

        if (call.Once && _saidOnce.Contains(call.Key))
        {
            dropped = "said once already this pull";
            Suppressed++;
            Tally(DroppedAsRepeat, call.Key);
            return null;
        }

        var window = call.Hush > 0 ? call.Hush : DuplicateWindow;
        if (_lastByKey.TryGetValue(call.Key, out var seen) && call.Time - seen < window)
        {
            dropped = $"same call {call.Time - seen:F1}s ago, window {window:F1}s";
            Suppressed++;
            Tally(DroppedAsRepeat, call.Key);
            return null;
        }

        // How far apart the two will actually land, either way round.
        //
        // This used to be prev-then-now subtraction, which reads as "behind" and is
        // only true when calls arrive in the order they fire. A few do not: a call
        // that counts itself back from a mechanic is offered now and lands a minute
        // from now, and it then sat here as the one to measure against. Everything
        // real for the next minute was a large negative number, which is less than
        // the gap, so it was dropped as crowding.
        //
        // Measured in one recording of Dancing Mad: a raidwide and a direction call
        // among twelve, all thrown away for landing before something that had not
        // happened yet.
        var apart = _last is { } prev ? Math.Abs(call.Time - prev.Time) : double.MaxValue;

        if (_last is { } standing && apart < MinGap && !Beats(call, standing))
        {
            dropped = $"{apart:F1}s from {standing.Key}, gap {MinGap:F1}s";
            Suppressed++;
            Tally(DroppedForCrowding, call.Key);
            LostTo.TryAdd(call.Key, standing.Key);
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
