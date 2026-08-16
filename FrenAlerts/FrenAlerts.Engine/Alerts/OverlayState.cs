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

    // What the line says, and the wider form its room is measured against.
    //
    // The countdown is dropped at go, and the overlay sizes to its content around a
    // centred pivot, so losing " (1)" slid the words sideways on the one frame you
    // were reading them. Laying out against the wider form holds them still.
    // A wide number keeps its own room rather than one digit's: reserving less than
    // is being drawn would lay the words out inside a box too small for them.
    public static (string Line, string Reserve) Countdown(
        string words, bool show, bool counting, float remaining)
    {
        if (!show) return (words, words);
        if (!counting) return (words, words + " (0)");
        var number = $"{MathF.Ceiling(remaining):0}";
        return ($"{words} ({number})", $"{words} ({new string('0', number.Length)})");
    }

    // One size for a whole stack of calls: the largest that still fits the widest of
    // them.
    //
    // Fitted per call, a long line shrinks and a short one beside it does not, so
    // four calls on screen came out at four sizes. Size reads as importance, and the
    // only thing it actually tracked was how many letters the mechanic's name has.
    public static float FitFontPxFor(float wantedPx, float capPx,
        IReadOnlyList<float> needs, float minPx = 12f)
    {
        if (needs.Count == 0) return wantedPx;
        var widest = 0f;
        foreach (var need in needs) if (need > widest) widest = need;
        // The icon's room is already inside each need, so none is added again here.
        return FitFontPx(wantedPx, capPx, widest, 0f, minPx);
    }

    public static bool Locked(bool userLock, bool inCombat, bool testMode)
    {
        if (testMode) return false;
        return userLock || inCombat;
    }
}
