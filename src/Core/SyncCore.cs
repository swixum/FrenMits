using System;
using System.Collections.Generic;

namespace FrenMits;

// The resync decision itself, with no game attached.
public static class SyncCore
{
    // A baked duty gives each encounter a 1000-second block.
    public const float TimelineBlockReach = 20000f;

    // How far an anchor may sit from the clock and still take it.
    public readonly record struct Windows(float Mech, float PhaseBack, float PhaseForward)
    {
        // Phase anchors get the wide forward window.
        public float Forward(bool isPhase) => isPhase ? PhaseForward : Mech;

        // Backward stays tight, since a phase ability can recast.
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

    // Which anchor this cast should snap to, or null for none.
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
            // Nearest anchor wins, ties going to a phase anchor.
            var score = MathF.Abs(ahead) - (sp.IsPhase ? 0.01f : 0f);
            if (score >= bestScore) continue;
            bestScore = score;
            best = sp;
        }
        return best;
    }

    // Where the raw clock goes so the anchor lands on its time.
    public static float SnapElapsed(SyncPoint best, float timeToResolve, float phaseOffset)
        => best.Time - timeToResolve - phaseOffset;

    // How far off the clock was, and the EMA that smooths it.
    public static float DriftAt(SyncPoint best, float predictedElapsed) => predictedElapsed - best.Time;

    public static float Ema(float avg, int samples, float drift)
        => samples == 0 ? drift : avg * 0.7f + drift * 0.3f;
}
