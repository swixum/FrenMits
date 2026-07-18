using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;

namespace FrenMits;

// Timeline resync, the safe way: instead of hooking the game we watch boss cast
// bars. When a known ability begins casting, we know exactly when it will
// resolve, so we snap the pull-clock to make the timeline line up with the
// ability's scripted time. This corrects the DPS-dependent drift between phases
// without ever counting on a fixed phase length.
public class SyncEngine
{
    private readonly Plugin _plugin;
    private readonly Dictionary<uint, uint> _lastCast = new(); // actor -> last seen cast action id
    private readonly HashSet<uint> _seenBoss = new();          // boss NameIds seen this pull
    private bool _wasRunning;
    private DateTime _playbackEnemyAt = DateTime.UtcNow;       // last live enemy seen (playback watchdog)
    private bool _lastPullArmed; // LastPull cleared once per pull, on its first frame

    public string LastSync { get; private set; } = "";

    // Bumps whenever a phase anchor (or boss-appearance anchor) re-bases the clock.
    // The cue engine watches this to know a fresh phase has actually started after
    // a cutscene, rather than releasing on any minor mid-phase drift correction.
    public int PhaseSyncGeneration { get; private set; }

    // Running estimate of how far the clock drifts from the baked timeline before
    // a mechanic anchor corrects it (+ = the clock runs ahead of the fight, i.e.
    // mechanics resolve later than the sheet says, i.e. your group runs behind).
    // Shown as a "timeline fit" readout; the config button folds -drift into the
    // fight's timer offset to re-center calls on the mechanics.
    public float AvgDrift { get; private set; }
    public int DriftSamples { get; private set; }

    public sealed record Capture(uint Id, float Time, string Caster, bool IsBoss);

    // Automatic capture for CUSTOM sheets: every enemy cast of the current/last
    // pull, no toggle needed, so Sheet View's "Build from pull" can turn a wipe
    // into rows + anchors. Cleared lazily on the next pull's FIRST capture, so
    // an instant wipe (or a stray /fm sync) can't destroy the previous capture.
    public readonly List<Capture> LastPull = new();
    public uint LastPullTerritory { get; private set; }

    private void AutoCapture(uint id, float time, string caster, bool isBoss)
    {
        if (!_lastPullArmed)
        {
            LastPull.Clear();
            LastPullTerritory = Service.ClientState.TerritoryType;
            _lastPullArmed = true;
        }
        LastPull.Add(new Capture(id, time, caster, isBoss));
        if (LastPull.Count > 500) LastPull.RemoveAt(0);
    }

    public SyncEngine(Plugin plugin) => _plugin = plugin;

    public void Update()
    {
        var c = _plugin.Config;

        // Fresh pull (combat just started): re-arm boss-presence + cast detection so
        // anchors fire again. NOT keyed off Generation, which also bumps on /fm sync.
        var running = _plugin.Timer.Running;
        if (running && !_wasRunning) { Forget(); _lastPullArmed = false; _playbackEnemyAt = DateTime.UtcNow; }
        _wasRunning = running;

        if (!running)
        {
            TryPlaybackAutoStart(c);
            return;
        }

        // Custom sheets get a hands-free capture of every pull so Sheet View's
        // "Build from pull" can turn a wipe into rows + anchors. Resolved up
        // front because the playback watchdog needs to know whether the enemy
        // scan below is actually running.
        var fight = _plugin.ActiveFight();
        var autoCapture = fight != null && fight.CustomSlots.Count > 0 && !Builtin.Has(fight.TerritoryId);
        var scanning = fight != null && (c.EnableSync || autoCapture);

        // Duty-recorder playback watchdog: the spectator has no combat flag, so
        // nothing ever stops the clock between the recording's pulls. Two signals
        // end a viewing: a load screen (chapter jump / pull reset, immediate) and
        // every enemy being gone for a few seconds (wipe fade). Either stops the
        // timer; the auto-start then re-locks onto whatever plays next, and the
        // fresh generation re-arms the calls so they speak again.
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
            // Judge "no enemies" only while the scan below is feeding the
            // watchdog; otherwise a manually started clock (resync off, or no
            // profile for the duty) would be killed 4s in with enemies visible.
            else if (scanning && (DateTime.UtcNow - _playbackEnemyAt).TotalSeconds > 4)
            {
                _plugin.Timer.Reset();
                Service.Log.Information("[FrenMits] Playback: no enemies for 4s; timer stopped, waiting for the next pull.");
                return;
            }
        }
        if (fight == null || !scanning) return;

        // Work in the same clock the overlay reads (includes any door-boss phase
        // offset), so anchors line up in both phases.
        var elapsed = _plugin.ElapsedFor(fight);

        foreach (var obj in Service.ObjectTable)
        {
            // A game object can go stale mid-frame (an actor despawning during a
            // phase transition leaves BattleChara.IsCasting dereferencing a null
            // pointer). Skip just that object so the rest of the table — and the
            // cue engine after us — still run this tick, instead of letting the
            // NRE abort the whole framework update.
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
                        AutoCapture(npc.NameId, elapsed, npc.Name.ToString(), true);
                    if (c.EnableSync)
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

                // Auto capture takes ENEMY casts only (subkind 5): player,
                // trust-NPC and pet casts are noise for a mechanic timeline
                // (and would poison anchors).
                if (autoCapture && bc.MaxHp > 0
                    && bc is IBattleNpc enemyNpc && (byte)enemyNpc.BattleNpcKind == 5)
                    AutoCapture(castId, resolveTime, bc.Name.ToString(), false);

                if (c.EnableSync && fight.SyncPoints.Count > 0)
                    OnCastStarted(fight, bc, castId);
            }
            catch (NullReferenceException) { /* stale actor this frame; ignore */ }
        }
    }

    // Duty-recorder playback (A Realm Recorded and friends): the spectator has
    // no combat flag, so the timer would never start by itself. Instead, the
    // first enemy cast matching a resync anchor both starts AND places the
    // clock; from there the normal anchor pipeline keeps it honest, including
    // through chapter skips (phase anchors re-base).
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

                // Only start from an ability that appears EXACTLY once in the
                // timeline. A repeated ability (DMU's Ultimate Embrace anchors
                // 221/371/378) is ambiguous after a chapter jump; guessing the
                // earliest instance could start the clock minutes off. Unique
                // anchors are dense enough that the wait is a few seconds.
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

    // Snap to the boss-appearance anchor for this NameId, if the fight has one.
    // Returns true if it snapped.
    private bool SnapToBoss(FightProfile fight, uint nameId, string casterName = "")
    {
        var elapsed = _plugin.ElapsedFor(fight);
        foreach (var ba in fight.BossAnchors)
            if (ba.NameId == nameId)
            {
                _plugin.Timer.SetElapsed(ba.Time - _plugin.PhaseOffsetFor(fight));
                LastSync = $"[boss] {(casterName.Length > 0 ? casterName : nameId.ToString())} -> {ba.Time:0.0}s (was {elapsed:0.0})";
                PhaseSyncGeneration++;
                _plugin.Diag.Sync(LastSync, elapsed, true);
                return true;
            }
        return false;
    }

    // Snap the clock so a cast of `actionId` resolving `timeToResolve` from now
    // lands on its scripted time. Returns true if a matching anchor snapped the
    // clock.
    private bool SnapToCast(FightProfile fight, uint actionId, float timeToResolve)
    {
        if (fight.SyncPoints.Count == 0) return false;
        var elapsed = _plugin.ElapsedFor(fight);

        var predictedElapsed = elapsed + timeToResolve; // where the clock will be at resolve

        // Match the way cactbot does: a wide FORWARD window and a tight BACKWARD
        // one. Some accurate timelines (the legacy ultimates) carry loop/jump
        // coordinates, so a phase or sub-phase can sit a long way ahead of a clock
        // that's still at the previous segment's coordinate — we need to jump
        // forward onto it. But we must not jump backward far, or a repeated
        // ability later in a segment would snap the clock back to the segment
        // start. (For continuous timelines like DMU the forward gap is ~0, so this
        // is a no-op there.) Among candidates we take the nearest one ahead.
        SyncPoint? best = null;
        var bestDelta = float.MaxValue;
        foreach (var sp in fight.SyncPoints)
        {
            if (sp.Ability != actionId) continue;
            // Phase / transition anchors get the wide forward window to jump onto a
            // loop/jump coordinate; mechanic anchors stay tight in both directions
            // (fine drift only) so an early stray cast can't snap the clock far
            // forward onto a later anchor.
            var fwd = sp.IsPhase ? _plugin.Config.SyncForwardWindowSeconds : _plugin.Config.SyncWindowSeconds;
            // The backward window stays tight even in duty-recorder playback: a
            // phase anchor's ability can RECAST later in the fight (DMU's
            // Revolting Ruin III comes back at ~98s), and a wide backward window
            // would yank the clock to the phase start mid-run. Playback resets
            // and chapter jumps are handled by the playback watchdog instead: the
            // load screen / no-enemies gap stops the timer and the auto-start
            // re-locks onto the new position from scratch.
            var bwd = sp.IsPhase
                ? MathF.Max(_plugin.Config.SyncPhaseWindowSeconds, _plugin.Config.SyncWindowSeconds)
                : _plugin.Config.SyncWindowSeconds;
            var ahead = sp.Time - predictedElapsed; // + => anchor is ahead of the clock
            if (ahead > fwd || ahead < -bwd) continue;
            // Take the NEAREST anchor; only break a tie toward a phase anchor. (Not
            // a strong phase bias — otherwise a repeated ability whose later cast is
            // a phase anchor would drag an earlier cast forward onto it.)
            var score = MathF.Abs(ahead) - (sp.IsPhase ? 0.01f : 0f);
            if (score < bestDelta)
            {
                bestDelta = score;
                best = sp;
            }
        }

        if (best == null) return false;

        // Self-tuning telemetry: how far the clock was off when this mechanic
        // anchor (not a big phase re-base) fired — a running feel for how well the
        // baked timeline matches your group's pace. A small EMA, ignoring the large
        // phase jumps which aren't drift.
        if (!best.IsPhase)
        {
            var drift = predictedElapsed - best.Time; // + => clock was running ahead
            AvgDrift = DriftSamples == 0 ? drift : AvgDrift * 0.7f + drift * 0.3f;
            DriftSamples++;
        }

        // Snap so that, timeToResolve from now, ElapsedFor == best.Time. SetElapsed
        // sets the raw timer, so subtract the phase offset back out. The fight's
        // timer offset is deliberately NOT subtracted: it lives on the cue clock
        // (CueClockFor), so a user's call-shift survives every snap.
        var desiredElapsedNow = best.Time - timeToResolve - _plugin.PhaseOffsetFor(fight);
        _plugin.Timer.SetElapsed(desiredElapsedNow);
        LastSync = $"{(best.IsPhase ? "[phase] " : "")}0x{actionId:X} -> {best.Time:0.0}s (was {elapsed:0.0}) {best.Label}";
        if (best.IsPhase) PhaseSyncGeneration++;
        _plugin.Diag.Sync(LastSync, elapsed, best.IsPhase);
        return true;
    }

    public void Forget()
    {
        _lastCast.Clear();
        _seenBoss.Clear();
    }
}
