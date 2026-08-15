using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Callouts;

// Cuts a recording into pulls on quiet gaps. A log has no pull markers that are
// worth trusting, but combat is obvious: things cast and things get hit.
public sealed class PullSegmenter
{
    // A gap this long ends a pull. Wipes and recoveries take longer than this.
    public float QuietSeconds { get; init; } = 20f;

    // Anything shorter is a trash pack or a stray hit, not an attempt.
    public float MinSeconds { get; init; } = 15f;

    public int MinEvents { get; init; } = 20;

    // Only casts and hits count; buffs and spawns happen out of combat too.
    private static bool IsActivity(GameEvent e)
        => e.Kind is EventKind.Ability or EventKind.CastStart;

    public List<Pull> Split(IEnumerable<GameEvent> events)
    {
        var ordered = EventOrder.InTimeOrder(events);
        var health = MaxHealth(ordered);
        var territory = 0u;
        var pullTerritory = 0u;
        var pulls = new List<Pull>();
        var current = new List<GameEvent>();
        var lastActivity = float.NaN;
        var start = float.NaN;

        foreach (var e in ordered)
        {
            if (e.Kind == EventKind.Zone) territory = e.Id;

            if (IsActivity(e))
            {
                if (!float.IsNaN(lastActivity) && e.Time - lastActivity > QuietSeconds)
                {
                    Close(pulls, current, start, lastActivity, health, pullTerritory);
                    current = new List<GameEvent>();
                    start = float.NaN;
                }

                // The duty is the one in force when the pull began, not the one
                // in force by the time a later pull closes it.
                if (float.IsNaN(start)) { start = e.Time; pullTerritory = territory; }
                lastActivity = e.Time;
            }

            // Events before the first hit belong to nothing yet.
            if (!float.IsNaN(start)) current.Add(e);
        }

        Close(pulls, current, start, lastActivity, health, pullTerritory);
        return pulls;
    }

    private void Close(List<Pull> pulls, List<GameEvent> events, float start, float end, Dictionary<uint, float> health, uint territory)
    {
        if (float.IsNaN(start) || events.Count < MinEvents) return;
        var duration = end - start;
        if (duration < MinSeconds) return;

        // Trailing events after the last hit are the wipe or the loot, so drop them.
        var kept = events.Where(e => e.Time <= end).Select(e => e with { Time = e.Time - start }).ToList();
        var boss = Boss(kept, health);

        pulls.Add(new Pull
        {
            Index = pulls.Count,
            Territory = territory,
            SourceStart = start,
            Duration = duration,
            Events = kept,
            BossNameId = boss.NameId,
            BossName = boss.Name,
        });
    }

    // Spawn lines are the only place a max health shows up, and they usually
    // happen before the pull starts, so this is read across the whole recording.
    private static Dictionary<uint, float> MaxHealth(List<GameEvent> events)
    {
        var health = new Dictionary<uint, float>();
        foreach (var e in events)
        {
            if (e.Kind is not (EventKind.ActorAdd or EventKind.ActorRemove)) continue;
            if (e.Source.NameId == 0 || float.IsNaN(e.Value)) continue;
            if (e.Value > health.GetValueOrDefault(e.Source.NameId)) health[e.Source.NameId] = e.Value;
        }
        return health;
    }

    // The biggest thing that casts is the boss, which is how the plugin already
    // picks one. Hostility cannot be read off the log: allied support NPCs cast
    // on players, get healed by players, and share the same id range as enemies.
    private static (uint NameId, string Name) Boss(List<GameEvent> events, Dictionary<uint, float> health)
    {
        var casters = new Dictionary<uint, (uint NameId, string Name, int Count)>();
        foreach (var e in events)
        {
            if (e.Kind != EventKind.CastStart) continue;
            var src = e.Source;
            if (src.IsPlayer || src.NameId == 0) continue;
            var seen = casters.GetValueOrDefault(src.NameId);
            casters[src.NameId] = (src.NameId, src.Name, seen.Count + 1);
        }

        if (casters.Count == 0) return (0u, "");

        var top = casters.Values
            .OrderByDescending(c => health.GetValueOrDefault(c.NameId))
            .ThenByDescending(c => c.Count)
            .First();
        return (top.NameId, top.Name);
    }
}
