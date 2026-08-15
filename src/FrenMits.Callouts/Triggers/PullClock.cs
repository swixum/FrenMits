namespace FrenMits.Callouts;

// Whether a pull is on and what time it is, taken from what the frame reports
// rather than from the wall clock alone.
//
// Live, the game raises a combat flag and a second of real time is a second of
// fight. A duty recording raises no combat flag at all, and it does not run at
// one second per second either: it pauses, it doubles, and a chapter skip drops
// the watcher somewhere else. So a recording's pull is judged by the arena, a
// living enemy starts it and a quiet one ends it, and its clock runs at the
// speed the recording is being watched at. That second part is what keeps a
// countdown attached to the cast that raised it.
public sealed class PullClock
{
    // Long enough to sit through a transition without calling the arena empty,
    // and the same window the timeline watchdog already judges a replay by.
    public const float QuietSeconds = 4f;

    // A step longer than this is a load screen or a hitch, not elapsed fight.
    public const float LongestStep = 1f;

    // The clock every call and every banner is stamped with.
    public float Time { get; private set; }

    private float _enemyAt = float.NegativeInfinity;

    // One frame of it. Speed counts only in a replay, where the watcher owns it.
    public void Advance(float realSeconds, bool playback, float speed)
    {
        if (!(realSeconds > 0f) || realSeconds > LongestStep) return;
        Time += playback ? realSeconds * Sane(speed) : realSeconds;
    }

    // Something hostile is up, which is what a pull looks like with no flag to read.
    public void SawEnemy() => _enemyAt = Time;

    public bool EnemyRecently => Time - _enemyAt <= QuietSeconds;

    // The game answers this live; in a replay the arena does.
    public bool Fighting(bool inCombat, bool playback)
        => inCombat || (playback && EnemyRecently);

    // Between pulls, so the next one has to find its own enemy.
    public void Forget() => _enemyAt = float.NegativeInfinity;

    // A paused replay reports zero, and a bad read reports anything at all.
    private static float Sane(float speed) => speed >= 0f && speed <= 100f ? speed : 1f;
}
