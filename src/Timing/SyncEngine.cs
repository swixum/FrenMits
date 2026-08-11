using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;

namespace FrenMits.Timing;

// Snaps the pull clock onto boss cast bars.
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

    // Short form of the last snap, for the board's trust line.
    public string LastSyncNice { get; private set; } = "";
    public DateTime LastSyncAt { get; private set; } = DateTime.MinValue;

    // Bumps whenever a phase anchor re-bases the clock.
    public int PhaseSyncGeneration { get; private set; }

    // Running estimate of the drift before a snap corrects it.
    public float AvgDrift { get; private set; }
    public int DriftSamples { get; private set; }

    // CasterNameId lets a capture be split by who cast what.
    public sealed record Capture(uint Id, float Time, string Caster, bool IsBoss, uint CasterNameId = 0);

    // Automatic capture for custom sheets, every enemy cast.
    public readonly List<Capture> LastPull = new();
    public uint LastPullTerritory { get; private set; }

    // Fills from the front, so a long fight can't eat the opener.
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

        // Fresh pull: re-arm detection, not keyed off Generation.
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
        // Duties with no timeline record their casts to learn from.
        var learning = _plugin.LearningHere;
        var autoCapture = (fight != null && fight.CustomSlots.Count > 0 && !Builtin.Has(fight.TerritoryId))
                          || learning;
        var scanning = (fight != null && (c.EnableSync || autoCapture)) || learning;

        // Playback watchdog: a load screen or no enemies ends it.
        if (Plugin.InDutyPlayback)
        {
            if (Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]
                || Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51])
            {
                _plugin.Timer.Reset();
                Service.Log.Information("[FrenMits] Playback: load screen; timer stopped, waiting for the next pull.");
                return;
            }
            // A cutscene is a transition, so keep the watchdog fed.
            if (Plugin.CutsceneActive) _playbackEnemyAt = DateTime.UtcNow;
            // Judge no enemies only while the scan is feeding it.
            else if (scanning && (DateTime.UtcNow - _playbackEnemyAt).TotalSeconds > 4)
            {
                _plugin.Timer.Reset();
                Service.Log.Information("[FrenMits] Playback: no enemies for 4s; timer stopped, waiting for the next pull.");
                return;
            }
        }
        if (!scanning) return;

        // Same clock the overlay reads, so anchors line up.
        var elapsed = fight != null ? _plugin.ElapsedFor(fight) : _plugin.Timer.Elapsed;

        foreach (var obj in Service.ObjectTable)
        {
            // An object can go stale mid-frame, so skip it.
            try
            {
                // Any live enemy means the recording is mid-pull.
                if (obj is IBattleNpc alive && (byte)alive.BattleNpcKind == 5 && alive.MaxHp > 0 && alive.CurrentHp > 0)
                    _playbackEnemyAt = DateTime.UtcNow;

                // Boss-presence anchor, the cast-free safety net.
                if (obj is IBattleNpc npc && npc.NameId != 0 && npc.MaxHp > 0 && _seenBoss.Add(npc.NameId))
                {
                    // Enemies only, so pets and trust NPCs stay out.
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

                // Enemy casts only, since player casts poison anchors.
                if (autoCapture && bc.MaxHp > 0
                    && bc is IBattleNpc enemyNpc && (byte)enemyNpc.BattleNpcKind == 5)
                    AutoCapture(castId, resolveTime, bc.Name.ToString(), false, enemyNpc.NameId);

                if (c.EnableSync && fight is { SyncPoints.Count: > 0 })
                    OnCastStarted(fight, bc, castId);
            }
            catch (NullReferenceException) { /* stale actor this frame; ignore */ }
        }
    }

    // In playback the first matching cast starts the clock.
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

                // Only start from an ability that appears once.
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
        // Time until this cast resolves, from the cast bar.
        var timeToResolve = MathF.Max(0f, caster.TotalCastTime - caster.CurrentCastTime);
        SnapToCast(fight, actionId, timeToResolve);
    }

    // Snap to this NameId's appearance anchor, if there is one.
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

    // Kept under the name the rest of the code imports.
    public const float TimelineBlockReach = SyncCore.TimelineBlockReach;

    // Snap the clock so this cast lands on its scripted time.
    private bool SnapToCast(FightProfile fight, uint actionId, float timeToResolve)
    {
        if (fight.SyncPoints.Count == 0) return false;
        var elapsed = _plugin.ElapsedFor(fight);

        var predictedElapsed = elapsed + timeToResolve; // where the clock will be at resolve
        var windows = SyncCore.WindowsFor(_plugin.Config.SyncWindowSeconds,
            _plugin.Config.SyncPhaseWindowSeconds, _plugin.Config.SyncForwardWindowSeconds,
            fight.TimelineOnly || Builtin.FieldOp(fight.TerritoryId));
        var best = SyncCore.Choose(fight.SyncPoints, actionId, predictedElapsed, windows, _fired);

        if (best == null) return false;
        _fired.Add(SyncCore.Key(best));

        // Telemetry: how far off the clock was at the snap.
        if (!best.IsPhase)
        {
            AvgDrift = SyncCore.Ema(AvgDrift, DriftSamples, SyncCore.DriftAt(best, predictedElapsed));
            DriftSamples++;
        }

        var desiredElapsedNow = SyncCore.SnapElapsed(best, timeToResolve, _plugin.PhaseOffsetFor(fight));
        _plugin.Timer.SetElapsed(desiredElapsedNow);
        // A phase anchor in the second segment latches Phase 2.
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
