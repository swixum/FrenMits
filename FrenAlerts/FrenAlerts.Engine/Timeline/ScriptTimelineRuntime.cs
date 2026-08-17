namespace FrenAlerts.Engine.Scripts;

// Their timeline clock, ported as it runs.
//
// A timeline is only worth anything if it knows where it is, and that is the part
// with all the judgement in it: which entry an ability meant, when a match is close
// enough to move the clock, when to refuse to move it, and when to give up. Those
// answers are theirs, constants and all, because a clock that resyncs on a slightly
// different rule than the file was written against drifts in exactly the places the
// file was written to survive.
//
// What changed is what it reads. Theirs asks the client for the time, the combat
// flag and the object table; this one is handed a clock and told whether the source
// was an enemy, so the same logic runs offline against a recording and in a duty
// without a second copy of it.
public sealed class ScriptTimelineRuntime
{
    // A resync inside this of where the clock already is, is the clock agreeing with
    // itself. Moving it anyway makes the countdown twitch on every ability.
    private const double SyncChurnGuard = 0.3;

    // A pull nothing has hit in this long is over, whatever the combat flag says.
    private const double IdleStop = 45.0;

    // A near miss on its own is a fight running slightly late; three in a row is the
    // clock being in the wrong place.
    private const int MissResync = 3;

    private const float MissResyncMaxDistance = 15f;

    private const float CastSyncMaxDistance = 12f;

    private readonly IReadOnlyList<ScriptTimeline> _all;

    private ScriptTimeline? _synced;
    private ScriptTimeline? _zone;

    private double _timebase;
    private bool _running;
    private float _lastFired = NothingFiredYet;
    private int _syncMiss;
    private double _lastAbility = double.NaN;

    private const float NothingFiredYet = -999f;

    public ScriptTimelineRuntime(IReadOnlyList<ScriptTimeline>? all = null) => _all = all ?? [];

    // Spoken lines, handed out rather than said here.
    public Action<string>? Speak;

    // A line about each correction, for the pull diary. The diary writes casts and
    // statuses but not ability hits, and this clock syncs on both, so a pull read
    // back afterwards could not say whether the clock had corrected or coasted.
    // Nothing here decides anything: it is what the file was missing.
    public Action<string>? Note;

    // How many times the clock has moved itself, and how far out it was when it
    // did, smoothed the way FrenMits smooths the same number. Positive means the
    // clock was behind and the fight was further along than it thought.
    public int Resyncs { get; private set; }

    public double Drift { get; private set; }

    private int _driftSamples;

    public ScriptTimeline? Active => _synced ?? _zone;

    public bool Running => _running;

    // The party's combat flag, which the engine cannot read for itself.
    public bool InCombat { get; set; } = true;

    // How far into the fight the clock says we are, or zero while it is lost.
    public float Fight(double now) => _running ? (float)(now - _timebase) : 0f;

    public void SetZone(ScriptTimeline? timeline) => _zone = timeline;

    public void Stop()
    {
        _running = false;
        _synced = null;
        _lastFired = NothingFiredYet;
        _syncMiss = 0;
    }

    public void Reset()
    {
        Stop();
        _zone = null;
        _lastAbility = double.NaN;
    }

    // A pull starting: the zone's timeline is the one to run, from wherever the
    // first ability says we are.
    public void Engage()
    {
        _synced = _zone;
        _syncMiss = 0;

        // Counting starts again here, so a long night does not read as one pull
        // that corrected itself four hundred times.
        Resyncs = 0;
        Drift = 0d;
        _driftSamples = 0;
    }

    public void OnEvent(GameEvent e, bool fromEnemy)
    {
        if (!InCombat || !fromEnemy || e.Id == 0) return;

        if (e.Kind == EventKind.CastStart) OnCastStart(e);
        else if (e.Kind == EventKind.AbilityHit) OnAbility(e);
    }

    private void OnAbility(GameEvent e)
    {
        _lastAbility = e.Time;

        var timeline = _synced ?? _zone ?? Find(e.Id);
        if (timeline is null) return;

        // Lost, so any entry this ability belongs to is better than none, and the
        // ability is allowed to name the fight as well as the place in it.
        if (!_running)
        {
            var opener = FirstMatch(timeline, e.Id);
            if (opener is null)
            {
                timeline = Find(e.Id);
                opener = timeline is null ? null : FirstMatch(timeline, e.Id);
            }
            if (timeline is not null && opener is not null) Adopt(timeline, opener, e.Time);
            return;
        }

        var fightNow = Fight(e.Time);
        var due = ActiveSync(timeline, e.Id, fightNow);

        if (due is null)
        {
            OnNoEntryDue(timeline, e, fightNow);
            return;
        }

        _synced = timeline;
        _syncMiss = 0;

        if (!due.HasJump)
        {
            SyncTo(due.Time, e.Time, on: due);
            return;
        }

        // Their loops jump backwards on purpose, so this one moves the clock even
        // when it already agrees with itself. A jump to zero is the file saying the
        // timeline is finished.
        if (due.Jump <= 0f) Stop();
        else SyncTo(due.Jump, e.Time, force: true, on: due);
    }

    // Nothing was due: either the clock is a little off, or this is a different
    // fight entirely.
    private void OnNoEntryDue(ScriptTimeline timeline, GameEvent e, float fightNow)
    {
        var near = ClosestMatch(timeline, e.Id, fightNow, MissResyncMaxDistance);
        if (near is null)
        {
            var other = Find(e.Id);
            if (other is null || other == timeline) return;
            var opener = FirstMatch(other, e.Id);
            if (opener is not null) Adopt(other, opener, e.Time);
            return;
        }

        if (++_syncMiss < MissResync) return;

        _syncMiss = 0;
        SyncTo(near.HasJump ? near.Jump : near.Time, e.Time, on: near);
    }

    // A cast places the clock at where its resolve is written, minus the cast time,
    // which puts the countdown right rather than a cast bar late.
    private void OnCastStart(GameEvent e)
    {
        var cast = e.CastTime;
        if (cast <= 0f) return;

        var timeline = _synced ?? _zone ?? Find(e.Id);
        if (timeline is null) return;

        var resolvesAt = _running ? Fight(e.Time) + cast : 0f;
        var entry = _running
            ? ActiveSync(timeline, e.Id, resolvesAt) ?? ClosestMatch(timeline, e.Id, resolvesAt, CastSyncMaxDistance)
            : FirstMatch(timeline, e.Id);
        if (entry is null) return;

        _synced = timeline;
        _syncMiss = 0;
        SyncTo(entry.Time - cast, e.Time, on: entry);
        _lastAbility = e.Time;
    }

    private void Adopt(ScriptTimeline timeline, ScriptTimelineEntry entry, double now)
    {
        _synced = timeline;
        _syncMiss = 0;
        SyncTo(entry.HasJump ? entry.Jump : entry.Time, now, on: entry);
    }

    private void SyncTo(float fightNow, double now, bool force = false, ScriptTimelineEntry? on = null)
    {
        var timebase = now - fightNow;
        if (!force && _running && Math.Abs(timebase - _timebase) <= SyncChurnGuard) return;

        // Read before the move, since that is the number worth writing down.
        var was = _running ? Fight(now) : float.NaN;

        _timebase = timebase;
        _running = true;

        if (!float.IsNaN(was))
        {
            Resyncs++;

            // Signed the way TimelineSyncing.Drift signs it, clock minus anchor, because
            // the config window reads both clocks through one line and says "ahead of the
            // fight" for a positive number. The note prints the move instead, which is the
            // same number the other way round and the one worth reading in a diary.
            var drift = was - fightNow;
            Drift = _driftSamples == 0 ? drift : Drift * 0.7d + drift * 0.3d;
            _driftSamples++;
            Note?.Invoke($"{was:F1}s -> {fightNow:F1}s ({-drift:+0.0;-0.0}s) on "
                + $"{(string.IsNullOrEmpty(on?.Name) ? "?" : on!.Name)}"
                + (on?.IsWide == true ? " [gate]" : ""));
        }

        // A clock that moved back has calls to make again, so the guard against
        // saying the same one twice moves back with it.
        if (fightNow <= _lastFired) _lastFired = fightNow - 0.01f;
    }

    // Speaks anything whose countdown crossed since the last tick.
    public void Tick(double now)
    {
        if (!_running) return;

        if (!double.IsNaN(_lastAbility) && now - _lastAbility > IdleStop)
        {
            Stop();
            return;
        }

        var fightNow = Fight(now);
        var timeline = _synced ?? _zone;
        if (timeline is not null && fightNow > _lastFired)
        {
            foreach (var entry in timeline.Entries)
            {
                foreach (var callout in entry.Callouts)
                {
                    var at = entry.Time - callout.Before;
                    if (at <= _lastFired || at > fightNow) continue;

                    var line = string.IsNullOrWhiteSpace(callout.Label) ? entry.Name : callout.Label;
                    if (!string.IsNullOrWhiteSpace(line)) Speak?.Invoke(line);
                }
            }
        }

        _lastFired = fightNow;
    }

    // What is due in the next few seconds, nearest first. This is the seam the
    // script host's own timeline triggers read: they match a name and a lead time,
    // and this is what says which names are coming.
    public IEnumerable<ScriptTimelineEntry> Upcoming(double now, float seconds)
    {
        var timeline = Active;
        if (!_running || timeline is null) yield break;

        var fightNow = Fight(now);
        foreach (var entry in timeline.Entries)
            if (entry.Time >= fightNow && entry.Time <= fightNow + seconds)
                yield return entry;
    }

    public ScriptTimelineEntry? Next(double now)
    {
        foreach (var entry in Upcoming(now, float.MaxValue)) return entry;
        return null;
    }

    // The first entry an ability belongs to, preferring the last phase gate: a
    // fight's opener repeats every phase, and the latest gate is the one that says
    // which time round it is.
    private static ScriptTimelineEntry? FirstMatch(ScriptTimeline timeline, uint id)
    {
        ScriptTimelineEntry? first = null;
        ScriptTimelineEntry? gate = null;
        var latest = float.MinValue;

        foreach (var entry in timeline.Entries)
        {
            if (!entry.Matches(id)) continue;

            if (entry.IsWide)
            {
                if (entry.Time <= latest) continue;
                latest = entry.Time;
                gate = entry;
            }
            else first ??= entry;
        }

        return gate ?? first;
    }

    private static ScriptTimelineEntry? ActiveSync(ScriptTimeline timeline, uint id, float fightNow) =>
        PickSync(timeline, id, fightNow, requireWindow: true, float.MaxValue);

    private static ScriptTimelineEntry? ClosestMatch(
        ScriptTimeline timeline, uint id, float fightNow, float maxDistance) =>
        PickSync(timeline, id, fightNow, requireWindow: false, maxDistance);

    private static ScriptTimelineEntry? PickSync(
        ScriptTimeline timeline, uint id, float fightNow, bool requireWindow, float maxDistance)
    {
        ScriptTimelineEntry? nearest = null;
        ScriptTimelineEntry? gate = null;
        var closest = float.MaxValue;
        var latest = float.MinValue;

        foreach (var entry in timeline.Entries)
        {
            if (!entry.Matches(id)) continue;
            if (requireWindow && !entry.InWindow(fightNow)) continue;

            var distance = MathF.Abs(entry.Time - fightNow);
            if (distance > maxDistance) continue;

            if (entry.IsWide && (!requireWindow || entry.InWindow(fightNow)))
            {
                if (entry.Time <= latest) continue;
                latest = entry.Time;
                gate = entry;
            }
            else if (distance < closest)
            {
                closest = distance;
                nearest = entry;
            }
        }

        return gate ?? nearest;
    }

    // Which timeline an ability belongs to, the zone's first.
    private ScriptTimeline? Find(uint id)
    {
        if (_zone is not null && FirstMatch(_zone, id) is not null) return _zone;

        foreach (var timeline in _all)
            if (timeline != _zone && FirstMatch(timeline, id) is not null) return timeline;

        return null;
    }
}
