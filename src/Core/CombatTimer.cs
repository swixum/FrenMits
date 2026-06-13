using System;
using Dalamud.Game.ClientState.Conditions;

namespace FrenMits;

// Tracks "seconds since the pull". Syncs to combat start by default; can be
// manually zeroed (/fm sync) or offset for sheets whose t=0 differs.
public class CombatTimer
{
    private DateTime? _startUtc;
    private bool _wasInCombat;

    public bool Running => _startUtc.HasValue;

    // Increments only on a genuine new run (pull / wipe / reset / manual sync) so
    // cue tracking can tell one run from the next. Automatic resync does NOT bump
    // it — otherwise an already-spoken cue would replay when the clock snaps.
    public int Generation { get; private set; }

    public float Elapsed => _startUtc is { } s ? (float)(DateTime.UtcNow - s).TotalSeconds : 0f;

    public void Update()
    {
        var inCombat = Service.Condition[ConditionFlag.InCombat];
        if (inCombat && !_wasInCombat)
        {
            _startUtc = DateTime.UtcNow;       // pull
            Generation++;
        }
        else if (!inCombat && _wasInCombat)
        {
            _startUtc = null;                  // combat ended / wiped
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
        // Treat the current combat flag as already-seen so a wipe that fires
        // while the flag is briefly still set cannot re-arm the timeline. The
        // next genuine combat transition starts it again.
        _wasInCombat = Service.Condition[ConditionFlag.InCombat];
        Generation++;
    }
}
