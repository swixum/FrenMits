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

    public string LastSync { get; private set; } = "";

    // Bumps whenever a phase anchor (or boss-appearance anchor) re-bases the clock.
    // The cue engine watches this to know a fresh phase has actually started after
    // a cutscene, rather than releasing on any minor mid-phase drift correction.
    public int PhaseSyncGeneration { get; private set; }

    // Running estimate of how far the clock drifts from the baked timeline before a
    // mechanic anchor corrects it (+ = your group runs ahead of the sheet). Shown
    // as a "timeline fit" readout; nudge the fight's timer offset to match.
    public float AvgDrift { get; private set; }
    public int DriftSamples { get; private set; }

    // Capture mode: record every boss cast / first boss appearance (with the
    // time it lands on the current clock) so anchors for phases with no public
    // timeline can be built from a real pull.
    public bool Recording { get; set; }
    public readonly List<Capture> Captured = new();
    public readonly List<PullRecording.RecEvent> CutsceneMarks = new(); // cutscene enter/exit while recording
    public sealed record Capture(uint Id, float Time, string Caster, bool IsBoss);

    public SyncEngine(Plugin plugin) => _plugin = plugin;

    public void Update()
    {
        var c = _plugin.Config;

        // Fresh pull (combat just started): re-arm boss-presence + cast detection so
        // anchors fire again. NOT keyed off Generation, which also bumps on /fm sync.
        var running = _plugin.Timer.Running;
        if (running && !_wasRunning) Forget();
        _wasRunning = running;

        if ((!c.EnableSync && !Recording) || !running) return;
        if (_plugin.ActiveFight() is not { } fight) return;

        // Work in the same clock the overlay reads (includes any door-boss phase
        // offset), so anchors line up in both phases.
        var elapsed = _plugin.ElapsedFor(fight);
        var phaseOffset = _plugin.PhaseOffsetFor(fight);

        foreach (var obj in Service.ObjectTable)
        {
            // A game object can go stale mid-frame (an actor despawning during a
            // phase transition leaves BattleChara.IsCasting dereferencing a null
            // pointer). Skip just that object so the rest of the table — and the
            // cue engine after us — still run this tick, instead of letting the
            // NRE abort the whole framework update.
            try
            {
                // Boss-presence anchor + capture (cast-free safety net).
                if (obj is IBattleNpc npc && npc.NameId != 0 && npc.MaxHp > 0 && _seenBoss.Add(npc.NameId))
                {
                    if (Recording)
                    {
                        Captured.Add(new Capture(npc.NameId, elapsed, npc.Name.ToString(), true));
                        if (Captured.Count > 160) Captured.RemoveAt(0);
                    }
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

                if (Recording && bc.MaxHp > 0)
                {
                    Captured.Add(new Capture(castId, resolveTime, bc.Name.ToString(), false));
                    if (Captured.Count > 160) Captured.RemoveAt(0);
                }

                if (c.EnableSync && fight.SyncPoints.Count > 0)
                    OnCastStarted(fight, bc, castId);
            }
            catch (NullReferenceException) { /* stale actor this frame; ignore */ }
        }
    }

    private void OnCastStarted(FightProfile fight, IBattleChara caster, uint actionId)
    {
        // Time until this cast resolves, straight from the cast bar.
        var timeToResolve = MathF.Max(0f, caster.TotalCastTime - caster.CurrentCastTime);
        SnapToCast(fight, actionId, timeToResolve);
    }

    // Snap to the boss-appearance anchor for this NameId, if the fight has one.
    // Public so a replay can drive it without a live game object. Returns true if
    // it snapped.
    public bool SnapToBoss(FightProfile fight, uint nameId, string casterName = "")
    {
        var elapsed = _plugin.ElapsedFor(fight);
        foreach (var ba in fight.BossAnchors)
            if (ba.NameId == nameId)
            {
                _plugin.Timer.SetElapsed(ba.Time - fight.TimerOffset - _plugin.PhaseOffsetFor(fight));
                LastSync = $"[boss] {(casterName.Length > 0 ? casterName : nameId.ToString())} -> {ba.Time:0.0}s (was {elapsed:0.0})";
                PhaseSyncGeneration++;
                return true;
            }
        return false;
    }

    // Snap the clock so a cast of `actionId` resolving `timeToResolve` from now
    // lands on its scripted time. Public so a replay can feed recorded casts
    // (with timeToResolve 0, i.e. at their recorded resolve moment). Returns true
    // if a matching anchor snapped the clock.
    public bool SnapToCast(FightProfile fight, uint actionId, float timeToResolve)
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
        // sets the raw timer, so subtract the per-fight + phase offsets back out.
        var desiredElapsedNow = best.Time - timeToResolve - fight.TimerOffset - _plugin.PhaseOffsetFor(fight);
        _plugin.Timer.SetElapsed(desiredElapsedNow);
        LastSync = $"{(best.IsPhase ? "[phase] " : "")}0x{actionId:X} -> {best.Time:0.0}s (was {elapsed:0.0}) {best.Label}";
        if (best.IsPhase) PhaseSyncGeneration++;
        return true;
    }

    public void Forget()
    {
        _lastCast.Clear();
        _seenBoss.Clear();
    }
}
