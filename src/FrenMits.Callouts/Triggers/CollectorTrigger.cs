using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Callouts;

// Gathers several events, then says one useful thing about all of them. This is
// what turns eight separate "marker on somebody" events into "you are 3rd, go
// north": the call needs the whole set before it can say anything worth hearing.
public sealed record CollectorTrigger
{
    public string Key { get; init; } = "";

    public TriggerMatch On { get; init; } = new();

    // Stop waiting once this many have landed, which is usually the whole party.
    public int Expect { get; init; } = 8;

    // Or stop waiting after this long, for mechanics that hit a subset.
    public float Window { get; init; } = 3f;

    // Say nothing unless this mechanic is on me.
    public bool MeOnly { get; init; }

    // Tokens: {count} {index} {ordinal} {others} on top of the usual ones.
    public string Text { get; init; } = "";

    public string Tts { get; init; } = "";

    public CallSeverity Severity { get; init; } = CallSeverity.Info;

    public float Duration { get; init; } = 6f;

    public bool Enabled { get; init; } = true;

    // Only while the fight is in this phase; empty means any.
    public string Phase { get; init; } = "";
}

// Runs collectors: one open window per key, closed by count or by time.
internal sealed class CollectorRunner
{
    private sealed class Window
    {
        public required CollectorTrigger Def { get; init; }
        public float OpenedAt { get; set; }
        public GameEvent? First { get; set; }
    }

    private readonly List<CollectorTrigger> _defs;
    private readonly Dictionary<string, Window> _open = new(StringComparer.Ordinal);

    public CollectorRunner(IEnumerable<CollectorTrigger> defs)
        => _defs = defs.Where(d => d.Enabled).ToList();

    public int Open => _open.Count;

    public IReadOnlyList<Call> Feed(GameEvent e, PlayerContext me, FightState state)
    {
        List<Call>? calls = null;

        // Close anything whose window ran out before this event.
        foreach (var key in _open.Keys.ToList())
        {
            var window = _open[key];
            if (e.Time - window.OpenedAt < window.Def.Window) continue;
            Close(key, window, me, state, ref calls);
        }

        foreach (var def in _defs)
        {
            if (def.Phase.Length > 0 && !string.Equals(def.Phase, state.Phase, StringComparison.Ordinal)) continue;
            if (!def.On.Matches(e, me)) continue;

            if (!_open.TryGetValue(def.Key, out var window))
            {
                _open[def.Key] = window = new Window { Def = def, OpenedAt = e.Time, First = e };
                state.Clear(def.Key);
            }

            state.Collect(def.Key, e.Target.Known ? e.Target : e.Source);

            if (state.Collected(def.Key).Count >= def.Expect)
                Close(def.Key, window, me, state, ref calls);
        }

        return (IReadOnlyList<Call>?)calls ?? [];
    }

    public void Reset() => _open.Clear();

    private void Close(string key, Window window, PlayerContext me, FightState state, ref List<Call>? calls)
    {
        _open.Remove(key);

        var def = window.Def;
        var collected = state.Collected(key);
        if (collected.Count == 0) return;

        var index = state.IndexOf(key, me.Id);
        if (def.MeOnly && index == 0) return;

        var source = window.First ?? new GameEvent();
        var others = string.Join(", ", collected.Where(a => a.Id != me.Id).Select(a => a.Name));

        (calls ??= new List<Call>()).Add(new Call
        {
            Text = Fill(def.Text, source, me, collected.Count, index, others),
            Tts = Fill(def.Tts, source, me, collected.Count, index, others),
            ClipKey = def.Key,
            Severity = def.Severity,
            At = source.Time,
            Duration = def.Duration,
            Personal = index > 0,
        });
    }

    private static string Fill(string template, GameEvent e, PlayerContext me, int count, int index, string others)
        => CallText.Fill(template, e, me)
            .Replace("{count}", count.ToString())
            .Replace("{index}", index > 0 ? index.ToString() : "")
            .Replace("{ordinal}", FightState.Ordinal(index))
            .Replace("{others}", others);
}
