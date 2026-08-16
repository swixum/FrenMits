namespace FrenAlerts.Engine;

public sealed class PullEdge
{
    public const double SettleSeconds = 2.0;

    private bool _inCombat;
    private bool _leaving;
    private double _leftAt;

    public EventKind? Note(bool inCombat, double now)
    {
        if (inCombat)
        {
            // Back before it settled, so nothing ended: this is the flicker the
            // delay exists for.
            _leaving = false;

            if (_inCombat) return null;
            _inCombat = true;
            return EventKind.CombatStart;
        }

        if (!_inCombat) return null;

        if (!_leaving)
        {
            _leaving = true;
            _leftAt = now;
            return null;
        }

        if (now - _leftAt < SettleSeconds) return null;

        _inCombat = false;
        _leaving = false;
        return EventKind.CombatEnd;
    }

    // True once combat has been entered and not yet settled out of it.
    public bool InPull => _inCombat;

    public void Forget()
    {
        _inCombat = false;
        _leaving = false;
        _leftAt = 0;
    }
}
