using System;

namespace FrenMits;

public class MitPress
{
    public MitLine SourceLine { get; }
    public string MitName { get; }
    public float WindowStart { get; }
    public float WindowEnd { get; }
    public float TargetHitTime { get; }
    public float Duration { get; }
    public float? ComputedDelay { get; set; }

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
