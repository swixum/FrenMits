using System;
using System.Collections.Generic;

namespace FrenMits.Callouts.Fights;

// A mechanic that takes several events to resolve, written as one method.
//
// Most of a fight is one event and one sentence, which a pack row covers. The
// parts people wipe to are never that: they are "four tethers go out, find
// yours, look at which side the thing on the other end is standing on, and say
// a different word depending". A flat trigger cannot hold the middle of that,
// and a set of triggers that each remember a piece of it have to agree with
// each other about a thing none of them can see.
//
// So those are written as a method that reads top to bottom, in the order the
// mechanic happens, and pauses wherever it has to wait for the fight. The runner
// below is what does the pausing.
public abstract record Step;

// Hold here until this many events the sequence cares about have arrived.
//
// Optional means the wait is allowed to come up short: when it runs out the
// mechanic carries on with whatever did arrive, instead of stopping there. That
// is what keeps one call that depends on something the tick cannot see from
// taking every call after it down with it.
public sealed record Await(Func<GameEvent, bool> Want, int Count, float Timeout, bool Optional = false) : Step;

// Say this now. A later one in the same mechanic replaces what is on screen,
// which is how a call that gets more specific as the mechanic resolves reads.
public sealed record Speak(Say Line) : Step;

// One mechanic, written as a method, and what starts it.
public sealed record Sequence
{
    public string Key { get; init; } = "";

    // What opens it, usually the cast everyone can see.
    public Func<GameEvent, PlayerContext, bool> Starts { get; init; } = (_, _) => false;

    public Func<Run, IEnumerator<Step>> Body { get; init; } = _ => None();

    // A mechanic the fight does more than once, where each time reads
    // differently. Beyond the last one the sequence stops opening, so a third
    // cast belonging to some other mechanic does not restart it.
    public int Invocations { get; init; } = 1;

    private static IEnumerator<Step> None() { yield break; }
}

// What a running mechanic can see, and how it asks to wait.
public sealed class Run
{
    public required PlayerContext Me { get; init; }
    public required FightState State { get; init; }
    public required Arena Arena { get; init; }
    public required StatusBook Statuses { get; init; }
    public required IReadOnlyDictionary<string, string> Options { get; init; }

    // The event that opened this run, and which time round it is, counting
    // from zero.
    public GameEvent Start { get; internal set; } = new();

    public int Invocation { get; internal set; }

    // What the wait just now collected, in the order it arrived.
    public List<GameEvent> Got { get; } = new();

    public GameEvent First => Got.Count > 0 ? Got[0] : new GameEvent();

    // How long a wait sits before it gives up. A mechanic that never resolves
    // has to let go, or the next pull starts with the last one still waiting.
    public const float Patience = 60f;

    public bool Mine(Actor a) => Me.IsMe(a);

    // The one of these that landed on me, or nothing.
    public GameEvent? MineOf(IEnumerable<GameEvent> events)
    {
        foreach (var e in events)
            if (Me.IsMe(e.Target) || Me.IsMe(e.Source)) return e;
        return null;
    }

    public Step Wait(Func<GameEvent, bool> want, float timeout = Patience)
        => new Await(want, 1, timeout);

    // Wait, but carry on without it. Whatever arrived is in Got, which may be
    // nothing at all, and the mechanic decides what to say with less.
    public Step WaitSome(int count, Func<GameEvent, bool> want, float timeout)
        => new Await(want, count, timeout, Optional: true);

    public Step WaitAll(int count, Func<GameEvent, bool> want, float timeout = Patience)
        => new Await(want, count, timeout);

    public Step Cast(uint id, float timeout = Patience)
        => Wait(e => e.Kind == EventKind.CastStart && e.Id == id, timeout);

    public Step Cast(uint first, uint second, float timeout = Patience)
        => Wait(e => e.Kind == EventKind.CastStart && (e.Id == first || e.Id == second), timeout);

    public Step Hit(uint id, float timeout = Patience)
        => Wait(e => e.Kind == EventKind.Ability && e.Id == id, timeout);

    // Head markers on one actor, which is where this fight writes down whether
    // it is lying to you this time.
    public Step MarkersOn(int count, uint nameId, float timeout = Patience)
        => WaitAll(count, e => e.Kind == EventKind.HeadMarker && e.Target.NameId == nameId, timeout);

    public Step Markers(int count, params uint[] ids)
        => WaitAll(count, e => e.Kind == EventKind.HeadMarker && Any(ids, e.Id));

    public Step Tethers(int count, uint id, float timeout = Patience)
        => WaitAll(count, e => e.Kind == EventKind.Tether && e.Id == id, timeout);

    // The game telling the client something about an actor, which is how the
    // towers and the trines say which of them lit up.
    public Step Control(uint category, uint first, uint second, float timeout = Patience)
        => Wait(e => e.Kind == EventKind.ActorControl && e.Id == category
                     && e.Extra == first && e.Flags == second, timeout);

    public Step ControlAll(int count, uint category, uint first, uint second, float timeout = Patience)
        => WaitAll(count, e => e.Kind == EventKind.ActorControl && e.Id == category
                               && e.Extra == first && e.Flags == second, timeout);

    public Speak Say(string text, CallSeverity level = CallSeverity.Warn, float hold = 6f)
        => new(new Say(text, Severity: level, Duration: hold));

    public Speak Go(string text, Way way, CallSeverity level = CallSeverity.Warn, float hold = 6f)
        => new(new Say(text, way, Severity: level, Duration: hold));

    // Said this long after the event that worked it out, for a mechanic whose
    // answer is known well before anybody can act on it.
    public Speak Later(string text, float delay, CallSeverity level = CallSeverity.Warn, float hold = 6f)
        => new(new Say(text, Severity: level, Delay: delay, Duration: hold));

    public Spot At(uint actorId) => Arena.Of(actorId);

    public bool Have(uint statusId) => Statuses.On(Me.Id, statusId).Present;

    public Held MyStatus(uint statusId) => Statuses.On(Me.Id, statusId);

    public string Option(string name, string fallback = "")
        => Options.TryGetValue(name, out var v) && v.Length > 0 ? v : fallback;

    private static bool Any(uint[] ids, uint id)
    {
        foreach (var one in ids)
            if (one == id) return true;
        return false;
    }
}

// Runs the scripted mechanics of one fight.
//
// Everything it holds is per pull and bounded: a mechanic that opens and never
// finishes is dropped when its wait runs out, and the whole set is dropped on a
// pull edge. It knows a pull ended because the engine clears its own counters
// there, and this keeps one of them.
public sealed class ScriptRunner
{
    // A fight has a handful of mechanics in flight at once, never this many.
    public const int MaxLive = 16;

    // The counter kept in the engine's state purely so a pull edge, which
    // clears that state, is visible from in here.
    private const string AliveKey = "script:alive";

    private sealed class Live
    {
        public required Sequence Def { get; init; }
        public required Run Run { get; init; }
        public required IEnumerator<Step> Body { get; init; }
        public Await? Waiting { get; set; }
        public float LastAt { get; set; }
    }

    // How many calls one event may raise. A mechanic often works out two
    // things at once, the thing to do now and the thing coming in six seconds,
    // and both have to be said: an earlier version of this kept only the last
    // one, which quietly swallowed the call that mattered.
    public const int MaxPerEvent = 4;

    private readonly List<Sequence> _defs;
    private readonly List<Live> _live = new();
    private readonly Dictionary<string, int> _opened = new(StringComparer.Ordinal);
    private readonly List<Say> _outbox = new();
    private GameEvent? _lastEvent;
    private bool _running;

    public ScriptRunner(IEnumerable<Sequence> sequences) => _defs = new List<Sequence>(sequences);

    public int Active => _live.Count;

    // One event in, up to a few calls out.
    //
    // The engine has no idea scripts exist, and asks one trigger at a time for
    // one call. So the mechanics are run once for the event, and each of the
    // triggers standing in for this runner collects the call at its own place
    // in the queue.
    public Say? Feed(TriggerContext c, int slot)
    {
        if (!ReferenceEquals(c.Event, _lastEvent))
        {
            _lastEvent = c.Event;
            _outbox.Clear();
            Deliver(c);
        }
        return slot < _outbox.Count ? _outbox[slot] : null;
    }

    private void Deliver(TriggerContext c)
    {
        var e = c.Event;

        if (_running && c.State.Count(AliveKey) == 0) Clear();

        for (var i = _live.Count - 1; i >= 0; i--)
        {
            var inst = _live[i];
            if (inst.Waiting is not { } waiting || e.Time - inst.LastAt <= waiting.Timeout) continue;

            // A wait that was allowed to come up short wakes the mechanic with
            // whatever it managed to collect. Any other one ends it.
            if (!waiting.Optional) { _live.RemoveAt(i); continue; }

            inst.LastAt = e.Time;
            if (!Advance(inst)) _live.RemoveAt(i);
        }

        // Existing mechanics first, so a cast that both finishes one and opens
        // the next is read in that order.
        for (var i = _live.Count - 1; i >= 0; i--)
        {
            var inst = _live[i];
            if (inst.Waiting is not { } want || !want.Want(e)) continue;

            inst.Run.Got.Add(e);
            inst.LastAt = e.Time;
            if (inst.Run.Got.Count < want.Count) continue;

            if (!Advance(inst)) _live.RemoveAt(i);
        }

        foreach (var def in _defs)
        {
            if (!def.Starts(e, c.Me)) continue;

            var opened = _opened.GetValueOrDefault(def.Key);
            if (opened >= def.Invocations) continue;
            if (_live.Count >= MaxLive) continue;

            // The same mechanic opening again while the last one still waits
            // means the last one is never going to resolve.
            _live.RemoveAll(l => l.Def.Key == def.Key);

            _opened[def.Key] = opened + 1;
            if (!_running) { c.State.Bump(AliveKey); _running = true; }

            var run = new Run
            {
                Me = c.Me,
                State = c.State,
                Arena = c.Arena,
                Statuses = c.Statuses,
                Options = c.Options,
                Start = e,
                Invocation = opened,
            };

            var inst = new Live
            {
                Def = def,
                Run = run,
                Body = def.Body(run),
                LastAt = e.Time,
            };

            if (Advance(inst)) _live.Add(inst);
        }
    }

    // Runs the method until it asks to wait again, or until it ends. False
    // means it ended and there is nothing left to feed.
    private bool Advance(Live inst)
    {
        while (inst.Body.MoveNext())
        {
            switch (inst.Body.Current)
            {
                case Speak s:
                    if (_outbox.Count < MaxPerEvent) _outbox.Add(s.Line);
                    continue;

                case Await a:
                    inst.Waiting = a;
                    // Cleared only now, because the method reads what the last
                    // wait collected the moment it wakes up.
                    inst.Run.Got.Clear();
                    return true;
            }
        }
        return false;
    }

    private void Clear()
    {
        _live.Clear();
        _opened.Clear();
        _running = false;
    }

    // The triggers that hand this runner its events. The engine has no idea
    // scripts exist: it sees an ordinary trigger per kind of event, whose words
    // happen to be worked out by a method that has been running for a minute.
    public static List<Trigger> Drivers(string key, ScriptRunner runner, params EventKind[] kinds)
    {
        var built = new List<Trigger>(kinds.Length * MaxPerEvent);
        foreach (var kind in kinds)
            for (var slot = 0; slot < MaxPerEvent; slot++)
            {
                var mine = slot;
                built.Add(new Trigger
                {
                    Key = $"{key} {kind} {mine}",
                    // Deliberately blank. These are plumbing, one per kind of
                    // event and one per place in the queue, and a settings page
                    // that lists what a fight says would otherwise fill with
                    // rows reading "CastStart 0" that nobody can act on. The
                    // mechanics they carry are named where they are written.
                    About = "",
                    On = new TriggerMatch { Kind = kind },
                    Says = c => runner.Feed(c, mine),
                    Duration = 6f,
                });
            }
        return built;
    }
}
