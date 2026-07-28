using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;

namespace FrenMits;

// Timeline resync: watch boss cast bars and snap the pull-clock onto their scripted
// times.
public class SyncEngine
{
    private readonly Plugin _plugin;
    private readonly Dictionary<uint, uint> _lastCast = new(); // actor -> last seen cast action id
    private readonly HashSet<uint> _seenBoss = new();          // boss NameIds seen this pull
    private readonly HashSet<(uint Ability, float Time)> _fired = new(); // anchors already used this pull
    private bool _wasRunning;
    private DateTime _playbackEnemyAt = DateTime.UtcNow;       // last live enemy seen (playback watchdog)
    private bool _lastPullArmed; // LastPull cleared once per pull, on its first frame

    public string LastSync { get; private set; } = "";

    // Short human-readable form of the last snap + when it landed, for the
    // board's little trust line ("synced - P2 Ultimate Embrace").
    public string LastSyncNice { get; private set; } = "";
    public DateTime LastSyncAt { get; private set; } = DateTime.MinValue;

    // Bumps whenever a phase anchor re-bases the clock.
    public int PhaseSyncGeneration { get; private set; }

    // Running estimate of the drift before a mechanic anchor corrects it.
    public float AvgDrift { get; private set; }
    public int DriftSamples { get; private set; }

    // CasterNameId is what lets a captured pull be split back up by who cast what.
    public sealed record Capture(uint Id, float Time, string Caster, bool IsBoss, uint CasterNameId = 0);

    // Automatic capture for custom sheets: every enemy cast of the current pull.
    public readonly List<Capture> LastPull = new();
    public uint LastPullTerritory { get; private set; }

    // The capture fills from the front and stops, so a long fight can't eat the opener.
    private const int MaxCaptures = 2000;

    private void AutoCapture(uint id, float time, string caster, bool isBoss, uint casterNameId = 0)
    {
        if (!_lastPullArmed)
        {
            LastPull.Clear();
            LastPullTerritory = Service.ClientState.TerritoryType;
            _lastPullArmed = true;
        }
        if (LastPull.Count >= MaxCaptures) return;
        LastPull.Add(new Capture(id, time, caster, isBoss, casterNameId));
    }

    public SyncEngine(Plugin plugin) => _plugin = plugin;

    public void Update()
    {
        var c = _plugin.Config;

        // Fresh pull (combat just started): re-arm boss-presence + cast detection so
        // anchors fire again, NOT keyed off Generation which also bumps on /fm sync.
        var running = _plugin.Timer.Running;
        if (running && !_wasRunning) { Forget(); _lastPullArmed = false; _playbackEnemyAt = DateTime.UtcNow; }
        _wasRunning = running;

        if (!running)
        {
            TryPlaybackAutoStart(c);
            return;
        }

        // Custom sheets get a hands-free capture of every pull.
        var fight = _plugin.ActiveFight();
        // Duties with no sheet and no baked timeline get their casts recorded to learn
        // from.
        var learning = _plugin.LearningHere;
        var autoCapture = (fight != null && fight.CustomSlots.Count > 0 && !Builtin.Has(fight.TerritoryId))
                          || learning;
        var scanning = (fight != null && (c.EnableSync || autoCapture)) || learning;

        // Playback watchdog: a load screen or every enemy gone ends a viewing.
        if (Plugin.InDutyPlayback)
        {
            if (Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]
                || Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51])
            {
                _plugin.Timer.Reset();
                Service.Log.Information("[FrenMits] Playback: load screen; timer stopped, waiting for the next pull.");
                return;
            }
            // Cutscene time is a phase transition, not a wipe; keep the watchdog
            // fed so it can't fire the instant the cutscene ends.
            if (Plugin.CutsceneActive) _playbackEnemyAt = DateTime.UtcNow;
            // Judge "no enemies" only while the scan below is feeding the watchdog.
            else if (scanning && (DateTime.UtcNow - _playbackEnemyAt).TotalSeconds > 4)
            {
                _plugin.Timer.Reset();
                Service.Log.Information("[FrenMits] Playback: no enemies for 4s; timer stopped, waiting for the next pull.");
                return;
            }
        }
        if (!scanning) return;

        // Work in the same clock the overlay reads (includes any door-boss
        // phase offset), so anchors line up in both phases.
        var elapsed = fight != null ? _plugin.ElapsedFor(fight) : _plugin.Timer.Elapsed;

        foreach (var obj in Service.ObjectTable)
        {
            // A game object can go stale mid-frame, so skip it rather than abort the
            // tick.
            try
            {
                // Feed the playback watchdog: any live enemy means the recording
                // is mid-pull, so the between-pulls stop must not fire.
                if (obj is IBattleNpc alive && (byte)alive.BattleNpcKind == 5 && alive.MaxHp > 0 && alive.CurrentHp > 0)
                    _playbackEnemyAt = DateTime.UtcNow;

                // Boss-presence anchor + capture (cast-free safety net).
                if (obj is IBattleNpc npc && npc.NameId != 0 && npc.MaxHp > 0 && _seenBoss.Add(npc.NameId))
                {
                    // Subkind 5 = enemy (stable game data); pets (2), chocobos (3)
                    // and trust NPCs (9) must not pollute the capture.
                    if (autoCapture && (byte)npc.BattleNpcKind == 5)
                        AutoCapture(npc.NameId, elapsed, npc.Name.ToString(), true, npc.NameId);
                    if (c.EnableSync && fight != null)
                        SnapToBoss(fight, npc.NameId, npc.Name.ToString());
                }

                if (obj is not IBattleChara bc) continue;
                var id = bc.EntityId;
                var castId = bc.IsCasting ? bc.CastActionId : 0u;

                _lastCast.TryGetValue(id, out var prev);
                if (castId == prev) continue;
                _lastCast[id] = castId;
                if (castId == 0) continue;

                var timeToResolve = MathF.Max(0f, bc.TotalCastTime - bc.CurrentCastTime);
                var resolveTime = elapsed + timeToResolve;

                // Auto capture takes enemy casts only; player and pet casts would
                // poison anchors.
                if (autoCapture && bc.MaxHp > 0
                    && bc is IBattleNpc enemyNpc && (byte)enemyNpc.BattleNpcKind == 5)
                    AutoCapture(castId, resolveTime, bc.Name.ToString(), false, enemyNpc.NameId);

                if (c.EnableSync && fight is { SyncPoints.Count: > 0 })
                    OnCastStarted(fight, bc, castId);
            }
            catch (NullReferenceException) { /* stale actor this frame; ignore */ }
        }
    }

    // In playback the first matching enemy cast both starts and places the clock.
    private void TryPlaybackAutoStart(Configuration c)
    {
        if (!Plugin.InDutyPlayback || !c.EnableSync) return;
        if (_plugin.ActiveFight() is not { } fight || fight.SyncPoints.Count == 0) return;

        foreach (var obj in Service.ObjectTable)
        {
            try
            {
                if (obj is not IBattleChara bc || bc.MaxHp == 0 || !bc.IsCasting) continue;
                if (bc is not IBattleNpc npc || (byte)npc.BattleNpcKind != 5) continue;
                var castId = bc.CastActionId;
                if (castId == 0) continue;

                // Only start from an ability that appears exactly once in the timeline.
                SyncPoint? best = null;
                var hits = 0;
                foreach (var sp in fight.SyncPoints)
                    if (sp.Ability == castId) { best = sp; hits++; }
                if (best == null || hits != 1) continue;

                var ttr = MathF.Max(0f, bc.TotalCastTime - bc.CurrentCastTime);
                _plugin.Timer.SyncNow(); // fresh Generation, so cue tracking re-arms
                _plugin.Timer.SetElapsed(MathF.Max(0f, best.Time - ttr - _plugin.PhaseOffsetFor(fight)));
                Service.Log.Information($"[FrenMits] Playback: timer started from anchor '{best.Label}' ({best.Time:0.0}s).");
                return;
            }
            catch (NullReferenceException) { /* stale actor; next frame */ }
        }
    }

    private void OnCastStarted(FightProfile fight, IBattleChara caster, uint actionId)
    {
        // Time until this cast resolves, straight from the cast bar.
        var timeToResolve = MathF.Max(0f, caster.TotalCastTime - caster.CurrentCastTime);
        SnapToCast(fight, actionId, timeToResolve);
    }

    // Snap to the boss-appearance anchor for this NameId if the fight has one,
    // returning true if it snapped.
    private bool SnapToBoss(FightProfile fight, uint nameId, string casterName = "")
    {
        var elapsed = _plugin.ElapsedFor(fight);
        foreach (var ba in fight.BossAnchors)
            if (ba.NameId == nameId)
            {
                _plugin.Timer.SetElapsed(ba.Time - _plugin.PhaseOffsetFor(fight));
                LastSync = $"[boss] {(casterName.Length > 0 ? casterName : nameId.ToString())} -> {ba.Time:0.0}s (was {elapsed:0.0})";
                LastSyncNice = string.IsNullOrWhiteSpace(ba.Label) ? casterName : ba.Label;
                LastSyncAt = DateTime.UtcNow;
                PhaseSyncGeneration++;
                _plugin.Diag.Sync(LastSync, elapsed, true);
                return true;
            }
        return false;
    }

    // Kept as the name the rest of the code already imports; the value and the
    // reasoning live in SyncCore, next to the windows it feeds.
    public const float TimelineBlockReach = SyncCore.TimelineBlockReach;

    // Snap the clock so this cast lands on its scripted time; true if an anchor
    // matched.
    private bool SnapToCast(FightProfile fight, uint actionId, float timeToResolve)
    {
        if (fight.SyncPoints.Count == 0) return false;
        var elapsed = _plugin.ElapsedFor(fight);

        var predictedElapsed = elapsed + timeToResolve; // where the clock will be at resolve
        var windows = SyncCore.WindowsFor(_plugin.Config, fight.TimelineOnly);
        var best = SyncCore.Choose(fight.SyncPoints, actionId, predictedElapsed, windows, _fired);

        if (best == null) return false;
        _fired.Add(SyncCore.Key(best));

        // Telemetry: how far the clock was off when a mechanic anchor fired.
        if (!best.IsPhase)
        {
            AvgDrift = SyncCore.Ema(AvgDrift, DriftSamples, SyncCore.DriftAt(best, predictedElapsed));
            DriftSamples++;
        }

        var desiredElapsedNow = SyncCore.SnapElapsed(best, timeToResolve, _plugin.PhaseOffsetFor(fight));
        _plugin.Timer.SetElapsed(desiredElapsedNow);
        // Door-boss follow-up: a phase anchor sitting in the second segment lets
        // the plugin latch Phase 2 (offset-compensated, so this snap stands).
        if (best.IsPhase) _plugin.OnPhaseAnchor(fight, best);
        LastSync = $"{(best.IsPhase ? "[phase] " : "")}0x{actionId:X} -> {best.Time:0.0}s (was {elapsed:0.0}) {best.Label}";
        LastSyncNice = best.Label;
        LastSyncAt = DateTime.UtcNow;
        if (best.IsPhase) PhaseSyncGeneration++;
        _plugin.Diag.Sync(LastSync, elapsed, best.IsPhase);
        return true;
    }

    public void Forget()
    {
        _lastCast.Clear();
        _seenBoss.Clear();
        _fired.Clear();
    }
}
