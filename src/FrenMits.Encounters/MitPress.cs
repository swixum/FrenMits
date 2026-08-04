using System;

namespace FrenMits.Encounters;

public class MitPress
{
    public MitLine SourceLine { get; }
    public string MitName { get; }
    public float WindowStart { get; }
    public float WindowEnd { get; }
    public float TargetHitTime { get; }
    public float Duration { get; }
    public float? ComputedDelay { get; set; }

    // True when the solver timed this press off the mit's own duration, so it
    // names a span you may press anywhere inside rather than one exact moment.
    // An instant mit - or usage windows switched off - has start == end, and
    // is timed by the plain lead/hold pair instead. See Configuration.LeadFor.
    public bool HasWindow => WindowEnd - WindowStart > 0.01f;

    public MitPress(MitLine sourceLine, string mitName, float windowStart, float windowEnd, float targetHitTime, float duration)
    {
        SourceLine = sourceLine;
        MitName = mitName;
        WindowStart = windowStart;
        WindowEnd = windowEnd;
        TargetHitTime = targetHitTime;
        Duration = duration;
    }
}
