using System;
using Dalamud.Game.ClientState.Conditions;

namespace FrenMits;

// Tracks "seconds since the pull", syncing to combat start by default and
// manually zeroable via /fm sync.
public class CombatTimer
{
    private DateTime? _startUtc;
    private DateTime? _combatStartUtc;
    private bool _wasInCombat;

    public bool Running => _startUtc.HasValue;

    // A plain stopwatch of the current pull: seconds since combat actually started,
    // never moved by resync so the combat-timer overlay ticks up smoothly.
    public float CombatElapsed => _combatStartUtc is { } s ? (float)(DateTime.UtcNow - s).TotalSeconds : 0f;
    public bool CombatRunning => _combatStartUtc.HasValue;

    // Increments only on a genuine new run (pull / wipe / reset / manual sync) so
    // cue tracking can tell one run from the next.
    public int Generation { get; private set; }

    public float Elapsed => _startUtc is { } s ? (float)(DateTime.UtcNow - s).TotalSeconds : 0f;

    public void Update()
    {
        // Freeze the state machine during a cutscene, since phase-transition
        // cutscenes (e.g. DMU) briefly drop combat and the flicker would otherwise
        // null the clock and mistake the next phase for a fresh pull.
        if (Plugin.CutsceneActive) return;

        var inCombat = Service.Condition[ConditionFlag.InCombat];
        if (inCombat && !_wasInCombat)
        {
            _startUtc = DateTime.UtcNow;       // pull
            _combatStartUtc = _startUtc;
            Generation++;
        }
        else if (!inCombat && _wasInCombat)
        {
            _startUtc = null;                  // combat ended / wiped
            _combatStartUtc = null;
            Generation++;
        }
        _wasInCombat = inCombat;
    }

    // Zero the timer to the current moment (e.g. on the first mechanic).
    public void SyncNow() { _startUtc = DateTime.UtcNow; Generation++; }

    // Force the timer to a specific elapsed value (automatic resync), same run so
    // do NOT bump Generation or it would re-arm and replay recently-spoken cues.
    public void SetElapsed(float seconds) { _startUtc = DateTime.UtcNow.AddSeconds(-seconds); }

    // Nudge the clock's origin so Elapsed advances at something other than
    // wall-clock pace, letting a Duty Recorder replay (via frameDelta * (1 -
    // gameSpeed)) freeze when paused and track 2x/0.5x correctly.
    public void ShiftStart(float seconds)
    {
        if (_startUtc is { } s) _startUtc = s.AddSeconds(seconds);
        if (_combatStartUtc is { } c) _combatStartUtc = c.AddSeconds(seconds);
    }

    public void Reset()
    {
        _startUtc = null;
        _combatStartUtc = null;
        // Treat the current combat flag as already-seen so a wipe that fires
        // while the flag is briefly still set cannot re-arm the timeline.
        _wasInCombat = Service.Condition[ConditionFlag.InCombat];
        Generation++;
    }
}
