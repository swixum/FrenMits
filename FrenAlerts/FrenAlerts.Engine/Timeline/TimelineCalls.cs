namespace FrenAlerts.Engine;

// A call that fires off the timeline rather than off an event.
//
// Most calls answer something that just happened. A few answer something that has
// not happened yet and gives no warning of its own: the cast bar is shorter than
// the warning is worth, so waiting for it says "move" long after moving was the
// answer. Upstream carries those on its timeline, counted back from the moment the
// mechanic lands, and this is the same thing.
//
// Measured before it was written, on three real kills: Feather Rain's cast bar is
// 0.70s against a warning worth 3s, and Eruption's is 2.70s against 10s. There is
// no event early enough, which is the whole reason this exists.
public sealed record TimelineCall
{
    public required ushort Territory { get; init; }

    // The mechanic's name on the timeline, matched whole and case-insensitively.
    public required string Mechanic { get; init; }

    // How long before it lands the call goes out.
    public required float Lead { get; init; }

    public required string Text { get; init; }

    public CallLevel Level { get; init; } = CallLevel.Info;

    public required string Key { get; init; }

    // Empty means everyone.
    public string For { get; init; } = "";
}

// Watches what the timeline says is coming and speaks each one once.
public sealed class TimelineCaller
{
    // Far enough ahead to see the longest lead any call uses, and no further: the
    // clock's own list is walked on every event.
    private const int LookAhead = 8;

    // One pull of a long fight has a few hundred timeline entries, and each can only
    // be spoken once, so this is bounded by the timeline itself. Cleared whenever the
    // clock is rebuilt or restarted, which is where a pull begins.
    private readonly HashSet<(string Mechanic, float At)> _said = [];

    private readonly List<TimelineCall> _calls = [];

    public TimelineCaller(ushort territory) =>
        _calls.AddRange(Shipped.Where(c => c.Territory == territory));

    public int Count => _calls.Count;

    public int Said => _said.Count;

    // Called when a pull starts, so the second pull of the night is not silent for
    // every mechanic the first one already said.
    public void Forget() => _said.Clear();

    // What is due right now. Never throws and never speaks twice for the same entry.
    public IEnumerable<Call> Due(TimelineClock? clock, double now)
    {
        if (clock is not { Running: true } || _calls.Count == 0) yield break;

        foreach (var next in clock.Next(now, LookAhead))
        {
            foreach (var call in _calls)
            {
                if (next.In > call.Lead) continue;
                if (!string.Equals(next.Mechanic, call.Mechanic, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!_said.Add((call.Mechanic, next.At))) continue;

                yield return new Call
                {
                    Text = call.Text,
                    Time = (float)now,
                    Key = call.Key,
                    Level = call.Level,
                };
            }
        }
    }

    // Carried over as upstream has them, counted back from the mechanic landing.
    public static readonly IReadOnlyList<TimelineCall> Shipped =
    [
        new()
        {
            Territory = 777, Mechanic = "Feather Rain", Lead = 3f,
            Text = "Move!", Key = "uwu-feather-rain",
        },
        new()
        {
            Territory = 777, Mechanic = "Eruption 1", Lead = 10f,
            Text = "Eruption Baits", Key = "uwu-eruption", Level = CallLevel.Alert,
        },
        new()
        {
            Territory = 777, Mechanic = "Diffractive Laser", Lead = 5f,
            Text = "Tank Cleave", Key = "uwu-diffractive-laser",
        },
    ];
}
