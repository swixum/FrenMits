using System;
using System.Collections.Generic;

namespace FrenMits;

// The resync decision itself, with no game attached: given a fight's anchors, an
// ability that just started casting and where the clock currently thinks it is,
// which anchor (if any) should snap the clock, and to what.
//
// SyncEngine used to hold this inline, which meant the only way to check an
// anchor set was to load a replay and watch the board. Anchors are the highest
// churn surface the plugin has and the one whose bugs are worst - a phase anchor
// on an ability the boss also casts somewhere unlisted throws the board a whole
// phase and leaves it there - so the part that decides has to be reachable from a
// test host.
//
// It lives here rather than in the test project on purpose: a test that re-states
// the rule it is checking proves nothing. SyncEngine calls exactly these
// functions in a live pull, and so does the offline replay in tests/, so a kill
// log replayed offline takes the same path a real pull does.
public static class SyncCore
{
    // A baked duty timeline packs every encounter of one instance onto a single
    // clock, each in its own 1000-second block: boss 1 sits at 1000+, boss 2 at
    // 2000+, and an alliance raid or a 3-boss dungeon runs to 4000+. Each fight
    // starts its own combat from zero, so reaching boss N's block means jumping N
    // thousand seconds forward from a standing start - and the normal 2000s
    // forward window only ever reached the FIRST one. Everything past boss 1
    // silently showed no timeline at all.
    //
    // Real sheets keep the configured window: their anchors are all on one pull
    // clock, so a window wide enough to cross blocks would be far too loose.
    public const float TimelineBlockReach = 20000f;

    // How far an anchor may sit from the clock and still take it.
    //
    // Matching the way cactbot does it: a wide FORWARD window (a phase can sit far
    // ahead of a clock still at the previous segment's loop/jump coordinate) but a
    // tight BACKWARD one (or a repeated ability later in a segment would snap the
    // clock back to the segment start).
    public readonly record struct Windows(float Mech, float PhaseBack, float PhaseForward)
    {
        // Phase / transition anchors get the wide forward window to jump onto a
        // loop/jump coordinate; mechanic anchors stay tight in both directions
        // (fine drift only) so an early stray cast can't snap the clock far
        // forward onto a later anchor.
        public float Forward(bool isPhase) => isPhase ? PhaseForward : Mech;

        // The backward window stays tight even in duty-recorder playback, since a
        // phase anchor's ability can RECAST later in the fight (DMU's Revolting
        // Ruin III comes back at ~98s) and a wide backward window would yank the
        // clock to the phase start mid-run.
        public float Backward(bool isPhase) => isPhase ? PhaseBack : Mech;
    }

    public static Windows WindowsFor(Configuration c, bool timelineOnly)
        => new(c.SyncWindowSeconds,
               MathF.Max(c.SyncPhaseWindowSeconds, c.SyncWindowSeconds),
               timelineOnly
                   ? MathF.Max(c.SyncForwardWindowSeconds, TimelineBlockReach)
                   : c.SyncForwardWindowSeconds);

    // The key an anchor is remembered by once it has fired. Time is part of it
    // because one ability can carry several anchors, and each of them fires once.
    public static (uint Ability, float Time) Key(SyncPoint sp) => (sp.Ability, sp.Time);

    // Which anchor a cast of `actionId` should snap to, given that it resolves when
    // the clock will read `predictedElapsed`, or null for none.
    //
    // `fired` holds the anchors already used this pull; an anchor in it may still
    // pull a clock that has fallen behind, but may never drag one backward onto
    // itself again. Plenty of mechanics are one ability cast many times over
    // several seconds - a channel, a per-target application, a multi-hit - and
    // every one of those casts snapped the clock back so THAT cast landed on the
    // row's time, which stops the board advancing for as long as the ability keeps
    // going. The board then reads late by exactly the length of the burst, with no
    // single row looking wrong, and the anchors that follow fall outside their
    // windows and never fire at all.
    //
    // Measured against ten kills each: this cost FRU 16s in P3 (which broke
    // fourteen anchors after it and left the whole Oracle phase uncorrected), TOP
    // 10s, DMU 6s, UWU 6s, and smaller amounts in TEA and DSR. The first cast of a
    // burst is the one the row means, so keeping it and refusing the rest is all
    // that is needed.
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
            // Take the NEAREST anchor, breaking a tie only toward a phase anchor
            // (not a strong bias, or a repeated ability whose later cast is a phase
            // anchor would drag an earlier cast forward onto it).
            var score = MathF.Abs(ahead) - (sp.IsPhase ? 0.01f : 0f);
            if (score >= bestScore) continue;
            bestScore = score;
            best = sp;
        }
        return best;
    }

    // Where the raw pull clock has to be set NOW so that, `timeToResolve` from now,
    // the fight's clock reads the anchor's time.
    //
    // SetElapsed sets the raw timer, so the phase offset comes back out here - but
    // NOT the fight's timer offset, which lives on the cue clock (CueClockFor) so a
    // user's call-shift survives every snap.
    public static float SnapElapsed(SyncPoint best, float timeToResolve, float phaseOffset)
        => best.Time - timeToResolve - phaseOffset;

    // How far the clock was off when a mechanic anchor fired (+ => the clock was
    // running ahead of the fight), and the small EMA that smooths it into a feel
    // for how well the baked timeline matches your group's pace.
    public static float DriftAt(SyncPoint best, float predictedElapsed) => predictedElapsed - best.Time;

    public static float Ema(float avg, int samples, float drift)
        => samples == 0 ? drift : avg * 0.7f + drift * 0.3f;
}
