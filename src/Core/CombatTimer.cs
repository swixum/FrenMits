using System;
using Dalamud.Game.ClientState.Conditions;

namespace FrenMits;

// Seconds since the pull, synced to combat or /fm sync.
public class CombatTimer
{
    private DateTime? _startUtc;
    private DateTime? _combatStartUtc;
    private bool _wasInCombat;

    // When the party countdown hits zero.
    private DateTime? _zeroUtc;

    // How long after zero a pull is still expected.
    public const float PullGraceSeconds = 10f;

    public bool Running => _startUtc.HasValue;

    // Where a pull starting now dates its clock from.
    public static DateTime PullStart(DateTime now, DateTime? countdownZero)
        => countdownZero is { } z && z <= now && CountdownLive(now, countdownZero) ? z : now;

    // Whether an armed countdown still means anything.
    public static bool CountdownLive(DateTime now, DateTime? countdownZero)
        => countdownZero is { } z && (now - z).TotalSeconds < PullGraceSeconds;

    // The pull is coming, so the clock reads negative.
    public bool PrePull => !Running && CountdownLive(DateTime.UtcNow, _zeroUtc);

    // The clock means something right now.
    public bool Live => Running || PrePull;

    // Seconds until the pull, 0 when none is counting.
    public float CountdownRemaining
        => !Running && _zeroUtc is { } z ? MathF.Max(0f, (float)(z - DateTime.UtcNow).TotalSeconds) : 0f;

    // Arm the clock, or follow a re-issued countdown.
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
            // Re-issued at a different length.
            _zeroUtc = zero;
            Generation++;
        }
    }

    // The countdown was called off.
    public void CancelCountdown()
    {
        if (_zeroUtc is not { } z || DateTime.UtcNow >= z) return;
        _zeroUtc = null;
        Generation++;
    }

    // Plain stopwatch of the pull, never moved by resync.
    public float CombatElapsed => _combatStartUtc is { } s ? (float)(DateTime.UtcNow - s).TotalSeconds : 0f;
    public bool CombatRunning => _combatStartUtc.HasValue;

    // Bumps only on a genuine new run.
    public int Generation { get; private set; }

    // Seconds since the pull, negative before it.
    public float Elapsed
        => _startUtc is { } s ? (float)(DateTime.UtcNow - s).TotalSeconds
         : _zeroUtc is { } z ? (float)(DateTime.UtcNow - z).TotalSeconds
         : 0f;

    public void Update()
    {
        // Freeze during a cutscene.
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
            // A countdown already armed the cues this run.
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

    // Zero the timer to now.
    public void SyncNow() { _startUtc = DateTime.UtcNow; Generation++; }

    // Force an elapsed value without bumping Generation.
    public void SetElapsed(float seconds) { _startUtc = DateTime.UtcNow.AddSeconds(-seconds); }

    // Nudge the origin to track replay speed.
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
        // Treat the combat flag as seen so a wipe can't re-arm.
        _wasInCombat = Service.Condition[ConditionFlag.InCombat];
        Generation++;
    }
}
