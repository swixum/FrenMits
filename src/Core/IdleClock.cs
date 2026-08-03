namespace FrenMits;

// Parser time spent idle (cutscene, downtime) within one segment.
public sealed class IdleClock
{
    private float _markSec = -1f;
    private double _markDmg;

    public float IdleSec { get; private set; }

    // Idle only counts while nothing lands, so a personal cutscene during a live fight stays counted.
    public static bool Counts(bool idleNow, float secDelta, double dmgDelta)
        => idleNow && secDelta > 0f && dmgDelta <= 0.5;

    public void Accrue(float parserSec, double damage, bool idleNow)
    {
        if (_markSec >= 0f && Counts(idleNow, parserSec - _markSec, damage - _markDmg))
            IdleSec += parserSec - _markSec;
        _markSec = parserSec;
        _markDmg = damage;
    }

    // A fresh segment starts its idle clock over from here.
    public void Reset(float parserSec, double damage)
    {
        IdleSec = 0f;
        _markSec = parserSec;
        _markDmg = damage;
    }

    // Forget everything, for a fight ending or a replay cleanup.
    public void Clear()
    {
        IdleSec = 0f;
        _markSec = -1f;
        _markDmg = 0;
    }
}
