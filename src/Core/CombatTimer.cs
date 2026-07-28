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

    // When the party's pull countdown reaches zero, once one has been seen.
    private DateTime? _zeroUtc;

    // How long after a countdown ends the pull is still assumed to be coming.
    public const float PullGraceSeconds = 10f;

    public bool Running => _startUtc.HasValue;

    // Where a pull starting NOW should date its clock from.
    public static DateTime PullStart(DateTime now, DateTime? countdownZero)
        => countdownZero is { } z && z <= now && CountdownLive(now, countdownZero) ? z : now;

    // Whether an armed countdown still means anything at `now`: true while it is
    // counting, and for the grace after it ends while the pull is expected.
    public static bool CountdownLive(DateTime now, DateTime? countdownZero)
        => countdownZero is { } z && (now - z).TotalSeconds < PullGraceSeconds;

    // The pull is coming but hasn't happened: the clock is live and reads negative.
    public bool PrePull => !Running && CountdownLive(DateTime.UtcNow, _zeroUtc);

    // The clock means something - in a pull, or counting into one.
    public bool Live => Running || PrePull;

    // Seconds until the pull, or 0 when none is counting.
    public float CountdownRemaining
        => !Running && _zeroUtc is { } z ? MathF.Max(0f, (float)(z - DateTime.UtcNow).TotalSeconds) : 0f;

    // Arm the clock on a countdown the game reports, or follow one that was
    // re-issued at a new length.
    public void SetCountdown(float remaining)
    {
        if (Running) return;                  // a countdown mid-fight is not a pull
        var zero = DateTime.UtcNow.AddSeconds(remaining);
        if (_zeroUtc is not { } z)
        {
            _zeroUtc = zero;
            Generation++;                     // a fresh run: re-arm every cue
        }
        else if (Math.Abs((zero - z).TotalSeconds) > 1.0)
        {
            // Re-issued at a different length - somebody restarted it.
            _zeroUtc = zero;
            Generation++;
        }
    }

    // The countdown was called off before it ran out.
    public void CancelCountdown()
    {
        if (_zeroUtc is not { } z || DateTime.UtcNow >= z) return;
        _zeroUtc = null;
        Generation++;
    }

    // A plain stopwatch of the current pull: seconds since combat actually started,
    // never moved by resync so the combat-timer overlay ticks up smoothly.
    public float CombatElapsed => _combatStartUtc is { } s ? (float)(DateTime.UtcNow - s).TotalSeconds : 0f;
    public bool CombatRunning => _combatStartUtc.HasValue;

    // Increments only on a genuine new run (pull / wipe / reset / manual sync) so
    // cue tracking can tell one run from the next.
    public int Generation { get; private set; }

    // Seconds since the pull, negative while a countdown runs down to it.
    public float Elapsed
        => _startUtc is { } s ? (float)(DateTime.UtcNow - s).TotalSeconds
         : _zeroUtc is { } z ? (float)(DateTime.UtcNow - z).TotalSeconds
         : 0f;

    public void Update()
    {
        // Freeze during a cutscene: phase transitions briefly drop combat.
        if (Plugin.CutsceneActive) return;

        var now = DateTime.UtcNow;
        var inCombat = Service.Condition[ConditionFlag.InCombat];
        if (inCombat && !_wasInCombat)
        {
            var armed = _zeroUtc.HasValue;
            var start = PullStart(now, _zeroUtc);   // the countdown's zero, or now
            _startUtc = start;                      // pull
            _combatStartUtc = start;
            _zeroUtc = null;
            // A countdown already began this run and re-armed the cues.
            if (!armed) Generation++;
        }
        else if (!inCombat && _wasInCombat)
        {
            _startUtc = null;                  // combat ended / wiped
            _combatStartUtc = null;
            _zeroUtc = null;
            Generation++;
        }
        else if (!inCombat && _zeroUtc.HasValue && !CountdownLive(now, _zeroUtc))
        {
            // The numbers ran out and nobody pulled.
            _zeroUtc = null;
            Generation++;
        }
        _wasInCombat = inCombat;
    }

    // Zero the timer to the current moment (e.g. on the first mechanic).
    public void SyncNow() { _startUtc = DateTime.UtcNow; Generation++; }

    // Force the timer to a specific elapsed value (automatic resync), same run so
    // do NOT bump Generation or it would re-arm and replay recently-spoken cues.
    public void SetElapsed(float seconds) { _startUtc = DateTime.UtcNow.AddSeconds(-seconds); }

    // Nudge the origin so Elapsed can track a replay's 2x, 0.5x or paused speed.
    public void ShiftStart(float seconds)
    {
        if (_startUtc is { } s) _startUtc = s.AddSeconds(seconds);
        if (_combatStartUtc is { } c) _combatStartUtc = c.AddSeconds(seconds);
    }

    public void Reset()
    {
        _startUtc = null;
        _combatStartUtc = null;
        _zeroUtc = null;
        // Treat the current combat flag as already-seen so a wipe that fires
        // while the flag is briefly still set cannot re-arm the timeline.
        _wasInCombat = Service.Condition[ConditionFlag.InCombat];
        Generation++;
    }
}
