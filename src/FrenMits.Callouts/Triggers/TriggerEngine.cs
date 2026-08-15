using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Callouts;

// Events in, calls out. Holds no clock and draws nothing, so a replay and a
// live pull run it exactly the same way.
public sealed class TriggerEngine
{
    // Keys remembered for once-per-pull, capped so a bad trigger set cannot grow.
    public const int MaxFiredKeys = 4096;

    private readonly List<Trigger> _triggers;
    private readonly SequenceRunner _sequences;
    private readonly CollectorRunner _collectors;
    private readonly HashSet<string> _fired = new();
    private readonly Dictionary<string, float> _quietUntil = new(StringComparer.Ordinal);

    public TriggerEngine(
        IEnumerable<Trigger> triggers,
        PlayerContext me,
        IEnumerable<SequenceTrigger>? sequences = null,
        IEnumerable<CollectorTrigger>? collectors = null)
    {
        _triggers = triggers.Where(t => t.Enabled).ToList();
        Me = me;
        _sequences = new SequenceRunner(sequences ?? []);
        _collectors = new CollectorRunner(collectors ?? []);
    }

    // Everything a fight brings, wired in one step.
    public static TriggerEngine For(FightModule module, PlayerContext me)
        => new(module.Triggers, me, module.Sequences, module.Collectors) { Options = module.Options };

    // What the engine knows about the pull so far.
    public FightState State { get; } = new();

    // Where everyone is and what the room looks like, so a call can name a
    // place on the floor rather than a way to lean.
    public Arena Arena { get; } = new();

    // Measured floors by duty. Without one for this duty the engine watches the
    // party instead, and until that settles it directs nobody.
    public IReadOnlyDictionary<uint, Floor> Floors
    {
        get => Arena.Book;
        set => Arena.Book = value;
    }

    // Action shapes, when the host has them. Without these the engine still
    // calls mechanics, it just cannot direct anyone.
    public IReadOnlyDictionary<uint, ActionShape> Shapes { get; set; }
        = new Dictionary<uint, ActionShape>();

    // Where this player's slot stands for each mechanic, from a plan or from
    // mined kills. This is what directs choreography that geometry cannot see.
    public IReadOnlyList<Spotting> Spots { get; set; } = [];

    // Which way the group runs each mechanic. A fight's own code reads these to
    // pick between two correct answers, the way a strat does.
    public IReadOnlyDictionary<string, string> Options { get; set; }
        = new Dictionary<string, string>();

    public PlayerContext Me { get; set; }

    public int TriggerCount => _triggers.Count;

    // Calls raised by this one event, usually none or one.
    public IReadOnlyList<Call> Feed(GameEvent e)
    {
        List<Call>? calls = null;
        if (e.Kind == EventKind.Zone) Arena.Enter(e.Id);
        Arena.Update(e);

        foreach (var t in _triggers)
        {
            if (t.Phase.Length > 0 && !string.Equals(t.Phase, State.Phase, StringComparison.Ordinal)) continue;
            if (!Wants(t.Roles, Me.Role) || !Wants(t.Jobs, Me.Job)) continue;
            if (!t.On.Matches(e, Me)) continue;
            if (t.OncePerPull && _fired.Contains(t.Key)) continue;

            var ctx = new TriggerContext(e, Me, State, Arena, Options);
            if (t.When is not null && !t.When(ctx)) continue;

            // Remembering happens even while the call itself is held back, so a
            // suppressed trigger still keeps the fight's books straight.
            t.Note?.Invoke(ctx);

            if (t.Suppress > 0f && _quietUntil.TryGetValue(t.Key, out var until) && e.Time < until) continue;
            if (t.Silent) continue;

            var said = t.Says?.Invoke(ctx);
            if (t.Says is not null && said is null) continue;

            if (t.OncePerPull && _fired.Count < MaxFiredKeys) _fired.Add(t.Key);
            if (t.SetsPhase.Length > 0) State.Phase = t.SetsPhase;
            if (t.Suppress > 0f && _quietUntil.Count < MaxFiredKeys) _quietUntil[t.Key] = e.Time + t.Suppress;

            var n = State.Bump(t.Key);
            var call = Build(t, e, n, said);

            // One mechanic can be several rows, one per audience, and a player
            // whose role or job is not known yet matches more than one of them.
            // Hearing the same sentence twice off one event is never right.
            calls ??= new List<Call>();
            var already = calls.FindIndex(c => c.Text == call.Text && c.At == call.At);
            if (already < 0)
            {
                calls.Add(call);
                continue;
            }

            // The louder of the two wins, because the row that shouts is the
            // one written for the player who has to act.
            if (calls[already].Severity < call.Severity)
                calls[already] = calls[already] with { Severity = call.Severity };
        }

        var fromSequences = _sequences.Feed(e, Me);
        if (fromSequences.Count > 0) (calls ??= new List<Call>()).AddRange(fromSequences);

        var fromCollectors = _collectors.Feed(e, Me, State);
        if (fromCollectors.Count > 0) (calls ??= new List<Call>()).AddRange(fromCollectors);

        return (IReadOnlyList<Call>?)calls ?? [];
    }

    // Called on every pull edge, so nothing carries into the next attempt.
    public void Reset()
    {
        _fired.Clear();
        _quietUntil.Clear();
        _sequences.Reset();
        _collectors.Reset();
        State.Reset();
        Arena.Reset();
    }

    // A call meant for other roles, or for jobs this one is not, is not this
    // player's problem. Not knowing which we are means hearing it.
    private static bool Wants(string wanted, string mine)
    {
        if (wanted.Length == 0 || mine.Length == 0) return true;

        foreach (var one in wanted.Split(','))
            if (one.Trim().Equals(mine, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    // The trigger key doubles as the clip name, so a voice pack needs no mapping.
    private Call Build(Trigger t, GameEvent e, int n, Say? said)
    {
        // What the fight's own code decided beats anything worked out for it.
        var (where, direction) = said is { } s
            ? (s.Where, s.Direction)
            : Advice(t, e);

        var text = said is { } a && a.Text.Length > 0 ? a.Text : t.Text;
        var tts = said is { } b && b.Tts.Length > 0 ? b.Tts : t.Tts;

        return new Call
        {
            Text = Fill(text, e, n),
            Tts = Fill(tts, e, n),
            ClipKey = t.Key,
            Severity = said?.Severity ?? t.Severity,
            At = When(t, e) + (said?.Delay ?? 0f),
            Duration = said is { Duration: > 0f } d ? d.Duration : t.Duration,
            Personal = Me.IsMe(e.Target),
            Where = where,
            Direction = direction,
        };
    }

    // Three ways to know where to go, most specific first: the trigger said so,
    // the group's plan says so for this slot, or the shape of the thing coming
    // says where there is room.
    private (string Where, Way Direction) Advice(Trigger t, GameEvent e)
    {
        // An authored direction goes through the same compass as a worked out
        // one, so "northeast" is spoken and drawn like every other. Anything
        // that is not a direction stays the words the author wrote.
        if (t.Where.Length > 0) return Said(t.Where);

        if (Spots.Count > 0 && Me.Slot.Length > 0)
        {
            var spot = Spots.Find(t.Key, Me.Slot)
                ?? Spots.Find(e.Name, Me.Slot)
                ?? Spots.Find(KeyFor(e), Me.Slot);
            if (spot is not null)
            {
                // The spot is stored relative to the middle and scaled to the
                // floor, so the direction is worked out against the room the
                // party is actually in rather than the one it was learned in.
                var way = Arena.Floor.Where(PlacedAt(spot.Value), Ring.Eight);
                return way != Way.Unknown ? ("", way) : Said(spot.Value.Where);
            }
        }

        var escape = Escape(e);
        return ("", escape.Speaks ? escape.To : Way.Unknown);
    }

    private static (string Where, Way Direction) Said(string text)
    {
        var way = Compass.Parse(text);
        return way != Way.Unknown ? ("", way) : (text, Way.Unknown);
    }

    private Spot PlacedAt(Spotting spot)
    {
        var floor = Arena.Floor;
        if (!floor.Known) return Spot.Nowhere;
        return new Spot(
            floor.CenterX + spot.X * floor.Radius,
            floor.CenterY + spot.Y * floor.Radius,
            0f);
    }

    // Markers and tethers have no name, so they are known by their id.
    private static string KeyFor(GameEvent e) => e.Kind switch
    {
        EventKind.HeadMarker => $"marker:{e.Id:X}",
        EventKind.Tether => $"tether:{e.Id:X}",
        _ => "",
    };

    // If the thing being cast covers where this player is standing, the call
    // names somewhere with room. Silence means the spot is already safe, or
    // that nothing here knows the room well enough to name a part of it.
    private WayOut Escape(GameEvent e)
    {
        if (Shapes.Count == 0 || Me.Id == 0) return WayOut.Unknown;
        if (!Shapes.TryGetValue(e.Id, out var shape) || !shape.Known) return WayOut.Unknown;

        // The caster turns while it casts, so which way is out is not settled
        // yet. Nothing can predict that turn, so the call goes out without a
        // direction rather than with a stale one.
        if (shape.Reaims) return WayOut.Unknown;

        var origin = DangerZone.FromCaster(shape) ? e.Source.At : Arena.Of(e.Target.Id);
        if (!origin.Known) return WayOut.Unknown;

        var zone = new DangerZone(origin, e.Source.Heading, shape);
        return Director.Escape(zone, Arena.Of(Me.Id), Arena);
    }

    // A debuff call lands before the debuff does, using the length on the event.
    private static float When(Trigger t, GameEvent e)
        => t.BeforeExpiry > 0f && e.Value > 0f
            ? e.Time + MathF.Max(0f, e.Value - t.BeforeExpiry)
            : e.Time + t.Delay;

    private string Fill(string template, GameEvent e, int n)
        => CallText.Fill(template, e, Me)
            .Replace("{n}", n.ToString())
            .Replace("{nth}", FightState.Ordinal(n));
}
