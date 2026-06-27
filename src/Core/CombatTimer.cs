using System;
using Dalamud.Game.ClientState.Conditions;

namespace FrenMits;

// Tracks "seconds since the pull". Syncs to combat start by default; can be
// manually zeroed (/fm sync) or offset for sheets whose t=0 differs.
public class CombatTimer
{
    private DateTime? _startUtc;
    private DateTime? _combatStartUtc;
    private bool _wasInCombat;

    public bool Running => _startUtc.HasValue;

    // A plain stopwatch of the current pull: seconds since combat actually started.
    // Unlike Elapsed it is never moved by resync (SyncNow/SetElapsed leave it
    // alone), so the combat-timer overlay ticks up smoothly. Null between pulls.
    public float CombatElapsed => _combatStartUtc is { } s ? (float)(DateTime.UtcNow - s).TotalSeconds : 0f;
    public bool CombatRunning => _combatStartUtc.HasValue;

    // Increments only on a genuine new run (pull / wipe / reset / manual sync) so
    // cue tracking can tell one run from the next. Automatic resync does NOT bump
    // it — otherwise an already-spoken cue would replay when the clock snaps.
    public int Generation { get; private set; }

    public float Elapsed => _startUtc is { } s ? (float)(DateTime.UtcNow - s).TotalSeconds : 0f;

    public void Update()
    {
        // Freeze the state machine during a cutscene. Phase-transition cutscenes
        // (e.g. DMU) briefly drop combat; without this, the combat flicker would
        // null the clock and the next phase would be mistaken for a fresh pull,
        // replaying early-phase calls. We keep _wasInCombat untouched so resuming
        // combat afterwards isn't seen as a new pull; the clock keeps running and
        // resync re-aligns it. (Door bosses transition out of combat WITHOUT a
        // cutscene, so they still reset correctly.)
        if (Plugin.InCutscene) return;

        // During a replay the clock is driven by the ReplayEngine, not live combat,
        // so don't let the (out-of-combat) desk state null it.
        if (Plugin.Replaying) return;

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

    // Force the timer to a specific elapsed value (automatic resync). Same run, so
    // do NOT bump Generation — that would re-arm and replay recently-spoken cues.
    public void SetElapsed(float seconds) { _startUtc = DateTime.UtcNow.AddSeconds(-seconds); }

    public void Reset()
    {
        _startUtc = null;
        _combatStartUtc = null;
        // Treat the current combat flag as already-seen so a wipe that fires
        // while the flag is briefly still set cannot re-arm the timeline. The
        // next genuine combat transition starts it again.
        _wasInCombat = Service.Condition[ConditionFlag.InCombat];
        Generation++;
    }
}
