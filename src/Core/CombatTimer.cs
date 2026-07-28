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

    // When the party's pull countdown reaches zero, once one has been seen. This is
    // the pull, ahead of time: the clock runs against it from the moment the
    // countdown starts, so the board, the calls and the cues are all live and
    // correct through it instead of appearing a beat after the fight begins.
    //
    // It is deliberately NOT the same thing as Running. A countdown starts the
    // clock, not the pull - resync, the mit recap and the learner all still wait
    // for real combat, because there is nothing yet for them to read.
    private DateTime? _zeroUtc;

    // How long after a countdown ends the pull is still assumed to be coming. Long
    // enough for a fumbled start, short enough that a countdown nobody pulled on
    // doesn't leave a clock ticking against a fight that never began.
    public const float PullGraceSeconds = 10f;

    public bool Running => _startUtc.HasValue;

    // Where a pull starting NOW should date its clock from.
    //
    // The countdown's own zero when there is one and it has passed, since that is
    // the exact instant the fight begins and the combat flag is a frame or two
    // behind it, by a different amount every pull. A party that starts early makes
    // the countdown meaningless - the fight began when they hit it, not when the
    // numbers would have run out - so then it is simply now.
    //
    // Pure and unit-tested; the caller passes the clock.
    //
    // Built ON CountdownLive rather than repeating its arithmetic, so the two can
    // never disagree about the edge of the grace - which they did, by one
    // comparison, until the test below said so. A pull dated from a countdown the
    // board had already given up on is the worst of both.
    public static DateTime PullStart(DateTime now, DateTime? countdownZero)
        => countdownZero is { } z && z <= now && CountdownLive(now, countdownZero) ? z : now;

    // Whether an armed countdown still means anything at `now`: true while it is
    // counting, and for the grace after it ends while the pull is expected.
    public static bool CountdownLive(DateTime now, DateTime? countdownZero)
        => countdownZero is { } z && (now - z).TotalSeconds < PullGraceSeconds;

    // The pull is coming but hasn't happened: the clock is live and reads negative.
    public bool PrePull => !Running && CountdownLive(DateTime.UtcNow, _zeroUtc);

    // The clock means something - in a pull, or counting into one. What every
    // display and the cue engine gate on, where Running is "the fight has started".
    public bool Live => Running || PrePull;

    // Seconds until the pull, or 0 when none is counting.
    public float CountdownRemaining
        => !Running && _zeroUtc is { } z ? MathF.Max(0f, (float)(z - DateTime.UtcNow).TotalSeconds) : 0f;

    // Arm the clock on a countdown the game reports, or follow one that was
    // re-issued at a new length. Called every tick while a countdown runs, so it
    // holds the instant it first worked out rather than recomputing from a float
    // that ticks - otherwise the clock would jitter by a frame each time.
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
            // Re-issued at a different length - somebody restarted it. That is a
            // new run, so re-arm the cues with it: a call already spoken against
            // the old zero has to be able to come round again on the new one.
            _zeroUtc = zero;
            Generation++;
        }
    }

    // The countdown was called off before it ran out. Not the same as it finishing:
    // that leaves the arm in place for the pull it is about to become.
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

    // Seconds since the pull - negative while a countdown is still running down to
    // it, which is what puts the board and the calls on screen before the fight
    // rather than a beat into it.
    public float Elapsed
        => _startUtc is { } s ? (float)(DateTime.UtcNow - s).TotalSeconds
         : _zeroUtc is { } z ? (float)(DateTime.UtcNow - z).TotalSeconds
         : 0f;

    public void Update()
    {
        // Freeze the state machine during a cutscene, since phase-transition
        // cutscenes (e.g. DMU) briefly drop combat and the flicker would otherwise
        // null the clock and mistake the next phase for a fresh pull.
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
            // A countdown already began this run and re-armed the cues. Bumping
            // again here would clear the fired-set a second time and let a call
            // just spoken be spoken over again - and on an early pull, where the
            // clock jumps forward onto the real start, that is exactly when the
            // fired-set matters most.
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
            // The numbers ran out and nobody pulled. End the armed run rather than
            // leave a clock counting up against a fight that never started.
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
        _zeroUtc = null;
        // Treat the current combat flag as already-seen so a wipe that fires
        // while the flag is briefly still set cannot re-arm the timeline.
        _wasInCombat = Service.Condition[ConditionFlag.InCombat];
        Generation++;
    }
}
