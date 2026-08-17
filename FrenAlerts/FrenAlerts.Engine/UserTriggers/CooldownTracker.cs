namespace FrenAlerts.Engine.UserTriggers;

// What the cooldown tracker watches, ported from theirs.
//
// One entry per thing somebody wants to see the timer on: an action of their own, or
// a status somebody else applied. Kept as data with a clock beside it, so the whole
// thing is testable without a screen: what is ready, what is not, and how long is
// left are questions with right answers.
public sealed class CooldownEntry
{
    public uint Id { get; set; }

    public string Name { get; set; } = "";

    public uint IconId { get; set; }

    // An action is tracked by its recast; a status by how long it has left to run.
    public bool IsStatus { get; set; }

    public bool Enabled { get; set; } = true;

    public float Scale { get; set; } = 1f;

    public CooldownStyle Style { get; set; }

    public bool HideWhenReady { get; set; }

    public bool ShowName { get; set; } = true;

    public bool UseColor { get; set; }

    public float ColorR { get; set; } = 0.2f;
    public float ColorG { get; set; } = 0.56f;
    public float ColorB { get; set; } = 0.96f;

    // Empty means every job. A tracker set up for one job showing on all of them is
    // the complaint their own list of jobs exists to answer.
    public List<string> Jobs { get; set; } = [];

    public bool ShowsOn(string job) =>
        Jobs.Count == 0 || Jobs.Exists(j => string.Equals(j, job, StringComparison.OrdinalIgnoreCase));

    public CooldownEntry Clone()
    {
        var copy = (CooldownEntry)MemberwiseClone();
        copy.Jobs = [.. Jobs];
        return copy;
    }
}

public enum CooldownStyle : byte
{
    Icon,
    Bar,
}

public enum CooldownVisibility : byte
{
    Always,
    InDuty,
    InCombat,
}

// What each tracked thing is doing right now.
//
// Fed from outside: an action's recast comes off the client, a status off the events
// the engine already reads. Holding it here means the overlay draws a list rather
// than working anything out.
public sealed class CooldownBoard
{
    private readonly Dictionary<uint, (double Until, float Total)> _running = [];

    public List<CooldownEntry> Entries { get; } = [];

    public CooldownVisibility Visibility { get; set; } = CooldownVisibility.InDuty;

    public void Note(uint id, double until, float total)
    {
        if (id == 0 || total <= 0f) return;
        _running[id] = (until, total);
    }

    public void Forget(uint id) => _running.Remove(id);

    public void Reset() => _running.Clear();

    public float Left(uint id, double now) =>
        _running.TryGetValue(id, out var run) ? (float)Math.Max(0, run.Until - now) : 0f;

    // Nothing left to wait for reads as ready, which is what a tracker set to hide
    // when ready hides on.
    public bool Ready(uint id, double now) => Left(id, now) <= 0f;

    // How far through, for the sweep on an icon or the fill on a bar.
    public float Progress(uint id, double now)
    {
        if (!_running.TryGetValue(id, out var run) || run.Total <= 0f) return 1f;
        return Math.Clamp(1f - (float)Math.Max(0, run.Until - now) / run.Total, 0f, 1f);
    }

    public IEnumerable<CooldownEntry> Showing(string job, double now)
    {
        foreach (var entry in Entries)
        {
            if (!entry.Enabled || !entry.ShowsOn(job)) continue;
            if (entry.HideWhenReady && Ready(entry.Id, now)) continue;
            yield return entry;
        }
    }
}
