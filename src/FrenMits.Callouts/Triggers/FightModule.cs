using System.Collections.Generic;

namespace FrenMits.Callouts;

// One fight's content, in one place. A module is the unit a person authors and
// the unit the plugin enables or disables, so every fight is built the same way
// whether it came from a bake or from somebody watching their own pulls.
public sealed record FightModule
{
    public string Name { get; init; } = "";

    public uint Territory { get; init; }

    public IReadOnlyList<Trigger> Triggers { get; init; } = [];

    public IReadOnlyList<SequenceTrigger> Sequences { get; init; } = [];

    public IReadOnlyList<CollectorTrigger> Collectors { get; init; } = [];

    // Which way this group runs each mechanic. A fight reads these to pick
    // between two correct answers, the way a strat does.
    public IReadOnlyDictionary<string, string> Options { get; init; }
        = new Dictionary<string, string>();

    public int Count => Triggers.Count + Sequences.Count + Collectors.Count;

    // Two modules for the same duty, the authored one laid over a baked one.
    public FightModule Over(FightModule under)
    {
        var mine = new HashSet<string>();
        foreach (var t in Triggers) mine.Add(t.Key);
        foreach (var s in Sequences) mine.Add(s.Key);
        foreach (var c in Collectors) mine.Add(c.Key);

        var triggers = new List<Trigger>(Triggers);
        foreach (var t in under.Triggers)
            if (!mine.Contains(t.Key))
                triggers.Add(t);

        return this with
        {
            Triggers = triggers,
            Sequences = [.. Sequences, .. under.Sequences],
            Collectors = [.. Collectors, .. under.Collectors],
        };
    }
}
