namespace FrenAlerts.Engine;

// Which anchor a cast should snap the clock to, with no game attached.
//
// Carried over from the mit planner, where these windows were measured against
// real pulls rather than picked: 8 seconds of backward drift on a mechanic, 60
// backward on a phase because a phase ability can recast, and a forward window
// wide enough that a phase base can pull the clock a whole block forward.
public static class TimelineSyncing
{
    // A fight written in phase blocks gives each one a 1000 second block, so the
    // clock has to be able to reach the far end of the last of them.
    public const float BlockReach = 20_000f;

    public readonly record struct Windows(float Mech, float PhaseBack, float PhaseForward)
    {
        public float Forward(bool isPhase) => isPhase ? PhaseForward : Mech;

        public float Backward(bool isPhase) => isPhase ? PhaseBack : Mech;
    }

    public static Windows Default { get; } = For(8f, 60f, 2000f, true);

    public static Windows For(float mech, float phase, float forward, bool blockTimes) =>
        new(mech,
            MathF.Max(phase, mech),
            blockTimes ? MathF.Max(forward, BlockReach) : forward);

    // The nearest anchor for this ability that the clock is allowed to take.
    //
    // An anchor already behind the clock and already fired is refused: without
    // that, a boss recasting the same ability drags the clock back to the first
    // time it ever used it.
    public static TimelineSync? Choose(
        IReadOnlyList<TimelineSync> anchors, uint ability, double clock,
        in Windows w, IReadOnlySet<(uint Ability, float Time)> fired)
    {
        TimelineSync? best = null;
        var bestScore = float.MaxValue;

        foreach (var a in anchors)
        {
            if (a.Ability != ability) continue;

            // Positive means the anchor sits ahead of where the clock thinks it is.
            var ahead = a.Time - (float)clock;
            if (ahead > w.Forward(a.IsPhase) || ahead < -w.Backward(a.IsPhase)) continue;
            if (ahead < 0 && fired.Contains(Key(a))) continue;

            // Nearest wins, a tie going to the phase anchor because re-basing is
            // the more useful of two equally close answers.
            var score = MathF.Abs(ahead) - (a.IsPhase ? 0.01f : 0f);
            if (score >= bestScore) continue;
            bestScore = score;
            best = a;
        }
        return best;
    }

    public static (uint Ability, float Time) Key(TimelineSync a) => (a.Ability, a.Time);

    // How far the clock was out when the anchor landed, smoothed so one late
    // packet does not read as the fight running slow.
    public static double Drift(TimelineSync anchor, double clock) => clock - anchor.Time;

    public static double Ema(double average, int samples, double drift) =>
        samples == 0 ? drift : average * 0.7 + drift * 0.3;
}
