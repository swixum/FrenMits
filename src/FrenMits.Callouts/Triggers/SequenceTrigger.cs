using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Callouts;

// One leg of a mechanic that takes several events to resolve.
public sealed record SequenceStep
{
    public TriggerMatch On { get; init; } = new();

    // Give up if the next event does not arrive within this long.
    public float Timeout { get; init; } = 30f;

    public string Text { get; init; } = "";

    public string Tts { get; init; } = "";

    public CallSeverity Severity { get; init; } = CallSeverity.Info;

    public float Delay { get; init; }

    public float Duration { get; init; } = 4f;

    public string Where { get; init; } = "";

    public bool Speaks => Text.Length > 0 || Tts.Length > 0;
}

// A mechanic spanning several events: the cast, then the marker, then the hit.
// One definition rather than three triggers that have to agree with each other.
public sealed record SequenceTrigger
{
    public string Key { get; init; } = "";

    public SequenceStep Start { get; init; } = new();

    public IReadOnlyList<SequenceStep> Then { get; init; } = [];

    public bool OncePerPull { get; init; }

    public bool Enabled { get; init; } = true;
}

// Tracks sequences in flight. Bounded, and everything clears on a pull edge.
internal sealed class SequenceRunner
{
    public const int MaxActive = 64;

    private sealed class Instance
    {
        public required SequenceTrigger Def { get; init; }
        public int Step { get; set; }
        public float LastAt { get; set; }
    }

    private readonly List<SequenceTrigger> _defs;
    private readonly List<Instance> _active = new();
    private readonly HashSet<string> _done = new();

    public SequenceRunner(IEnumerable<SequenceTrigger> defs)
        => _defs = defs.Where(d => d.Enabled && d.Then.Count > 0).ToList();

    public int Active => _active.Count;

    public IReadOnlyList<Call> Feed(GameEvent e, PlayerContext me)
    {
        List<Call>? calls = null;

        _active.RemoveAll(i => e.Time - i.LastAt > i.Def.Then[i.Step].Timeout);

        foreach (var inst in _active.ToList())
        {
            var step = inst.Def.Then[inst.Step];
            if (!step.On.Matches(e, me)) continue;

            if (step.Speaks) (calls ??= new List<Call>()).Add(StepCall(step, e, me));

            inst.Step++;
            inst.LastAt = e.Time;
            if (inst.Step >= inst.Def.Then.Count) _active.Remove(inst);
        }

        foreach (var def in _defs)
        {
            if (!def.Start.On.Matches(e, me)) continue;
            if (def.OncePerPull && _done.Contains(def.Key)) continue;
            if (_active.Count >= MaxActive) continue;

            if (def.OncePerPull) _done.Add(def.Key);
            if (def.Start.Speaks) (calls ??= new List<Call>()).Add(StepCall(def.Start, e, me));
            _active.Add(new Instance { Def = def, Step = 0, LastAt = e.Time });
        }

        return (IReadOnlyList<Call>?)calls ?? [];
    }

    public void Reset()
    {
        _active.Clear();
        _done.Clear();
    }

    private static Call StepCall(SequenceStep s, GameEvent e, PlayerContext me) => new()
    {
        Text = CallText.Fill(s.Text, e, me),
        Tts = CallText.Fill(s.Tts, e, me),
        Severity = s.Severity,
        At = e.Time + s.Delay,
        Duration = s.Duration,
        Personal = me.IsMe(e.Target),
        Where = s.Where,
    };
}
