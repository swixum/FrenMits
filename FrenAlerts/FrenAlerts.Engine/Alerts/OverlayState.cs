namespace FrenAlerts.Engine.Alerts;

public static class OverlayState
{
    // On screen when there is something to say, or while placing it.
    public static bool Visible(bool alertsEnabled, bool testMode, int liveCalls)
    {
        if (!alertsEnabled) return false;
        return testMode || liveCalls > 0;
    }

    public static float FitFontPx(float wantedPx, float capPx, float widthPerPx,
        float iconFactor, float minPx = 12f)
    {
        var need = widthPerPx + iconFactor;
        if (need <= 0.0001f || capPx <= 0f) return wantedPx;
        var fits = capPx / need;
        return fits >= wantedPx ? wantedPx : Math.Clamp(fits, Math.Min(minPx, wantedPx), wantedPx);
    }

    public static bool Locked(bool userLock, bool inCombat, bool testMode)
    {
        if (testMode) return false;
        return userLock || inCombat;
    }
}
