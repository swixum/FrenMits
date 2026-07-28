using System;
using System.Collections.Generic;

namespace FrenMits;

// The resync decision itself, with no game attached.
public static class SyncCore
{
    // A baked duty timeline gives each encounter its own 1000-second block.
    public const float TimelineBlockReach = 20000f;

    // How far an anchor may sit from the clock and still take it.
    public readonly record struct Windows(float Mech, float PhaseBack, float PhaseForward)
    {
        // Phase anchors get the wide forward window; mechanic anchors stay tight both
        // ways.
        public float Forward(bool isPhase) => isPhase ? PhaseForward : Mech;

        // The backward window stays tight even in playback: a phase ability can recast
        // later.
        public float Backward(bool isPhase) => isPhase ? PhaseBack : Mech;
    }

    public static Windows WindowsFor(Configuration c, bool timelineOnly)
        => new(c.SyncWindowSeconds,
               MathF.Max(c.SyncPhaseWindowSeconds, c.SyncWindowSeconds),
               timelineOnly
                   ? MathF.Max(c.SyncForwardWindowSeconds, TimelineBlockReach)
                   : c.SyncForwardWindowSeconds);

    // The key an anchor is remembered by once it has fired.
    public static (uint Ability, float Time) Key(SyncPoint sp) => (sp.Ability, sp.Time);

    // Which anchor a cast of `actionId` should snap to, given that it resolves
    // when the clock will read `predictedElapsed`, or null for none.
    public static SyncPoint? Choose(IReadOnlyList<SyncPoint> points, uint actionId, float predictedElapsed,
        in Windows w, IReadOnlySet<(uint Ability, float Time)> fired)
    {
        SyncPoint? best = null;
        var bestScore = float.MaxValue;
        for (var i = 0; i < points.Count; i++)
        {
            var sp = points[i];
            if (sp.Ability != actionId) continue;
            var ahead = sp.Time - predictedElapsed; // + => the anchor is ahead of the clock
            if (ahead > w.Forward(sp.IsPhase) || ahead < -w.Backward(sp.IsPhase)) continue;
            if (ahead < 0 && fired.Contains(Key(sp))) continue;
            // Take the nearest anchor, breaking a tie only toward a phase anchor.
            var score = MathF.Abs(ahead) - (sp.IsPhase ? 0.01f : 0f);
            if (score >= bestScore) continue;
            bestScore = score;
            best = sp;
        }
        return best;
    }

    // Where the raw pull clock has to be set NOW so that, `timeToResolve` from
    // now, the fight's clock reads the anchor's time.
    public static float SnapElapsed(SyncPoint best, float timeToResolve, float phaseOffset)
        => best.Time - timeToResolve - phaseOffset;

    // How far the clock was off when a mechanic anchor fired, and the EMA that smooths
    // it.
    public static float DriftAt(SyncPoint best, float predictedElapsed) => predictedElapsed - best.Time;

    public static float Ema(float avg, int samples, float drift)
        => samples == 0 ? drift : avg * 0.7f + drift * 0.3f;
}
