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

    public string LastSync { get; private set; } = "";

    // Capture mode: record every boss cast / first boss appearance (with the
    // time it lands on the current clock) so anchors for phases with no public
    // timeline can be built from a real pull.
    public bool Recording { get; set; }
    public readonly List<Capture> Captured = new();
    public sealed record Capture(uint Id, float Time, string Caster, bool IsBoss);

    public SyncEngine(Plugin plugin) => _plugin = plugin;

    public void Update()
    {
        var c = _plugin.Config;
        if ((!c.EnableSync && !Recording) || !_plugin.Timer.Running) return;
        if (_plugin.ActiveFight() is not { } fight) return;

        // Work in the same clock the overlay reads (includes any door-boss phase
        // offset), so anchors line up in both phases.
        var elapsed = _plugin.ElapsedFor(fight);
        var phaseOffset = _plugin.PhaseOffsetFor(fight);

        foreach (var obj in Service.ObjectTable)
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
                    foreach (var ba in fight.BossAnchors)
                        if (ba.NameId == npc.NameId)
                        {
                            _plugin.Timer.SetElapsed(ba.Time - fight.TimerOffset - phaseOffset);
                            LastSync = $"[boss] {npc.Name} -> {ba.Time:0.0}s (was {elapsed:0.0})";
                            break;
                        }
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
    }

    private void OnCastStarted(FightProfile fight, IBattleChara caster, uint actionId)
    {
        // Time until this cast resolves, straight from the cast bar.
        var timeToResolve = MathF.Max(0f, caster.TotalCastTime - caster.CurrentCastTime);
        var elapsed = _plugin.ElapsedFor(fight);

        var predictedElapsed = elapsed + timeToResolve; // where the clock will be at resolve

        // Phase anchors get a wide window (a phase can start far from the sheet's
        // nominal time); mechanic anchors use the tight window for fine drift.
        SyncPoint? best = null;
        var bestDelta = float.MaxValue;
        foreach (var sp in fight.SyncPoints)
        {
            if (sp.Ability != actionId) continue;
            var window = sp.IsPhase
                ? MathF.Max(_plugin.Config.SyncPhaseWindowSeconds, _plugin.Config.SyncWindowSeconds)
                : _plugin.Config.SyncWindowSeconds;
            var delta = MathF.Abs(sp.Time - predictedElapsed);
            if (delta > window) continue;
            // Prefer phase anchors, then the closest in time.
            var score = delta - (sp.IsPhase ? 1000f : 0f);
            if (score < bestDelta)
            {
                bestDelta = score;
                best = sp;
            }
        }

        if (best == null) return;

        // Snap so that, timeToResolve from now, ElapsedFor == best.Time. SetElapsed
        // sets the raw timer, so subtract the per-fight + phase offsets back out.
        var desiredElapsedNow = best.Time - timeToResolve - fight.TimerOffset - _plugin.PhaseOffsetFor(fight);
        _plugin.Timer.SetElapsed(desiredElapsedNow);
        LastSync = $"{(best.IsPhase ? "[phase] " : "")}0x{actionId:X} -> {best.Time:0.0}s (was {elapsed:0.0}) {best.Label}";
    }

    public void Forget()
    {
        _lastCast.Clear();
        _seenBoss.Clear();
    }
}
