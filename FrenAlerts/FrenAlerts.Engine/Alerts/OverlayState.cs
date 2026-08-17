namespace FrenAlerts.Engine.Alerts;

public static class OverlayState
{
    // On screen when there is something to say, or while placing it.
    //
    // Never while the game's own interface is hidden. Somebody who pressed that key
    // wants a clean screen, and this was the one thing left on it: every screenshot
    // taken during a pull had a call sitting in the middle of it, and there was no
    // setting anywhere that would take it off.
    //
    // Ahead of everything else, because it is not a preference. Test mode holds a
    // sample on screen on purpose and even that goes, or placing the call means
    // never being able to take a picture without it.
    public static bool Visible(bool alertsEnabled, bool testMode, int liveCalls,
        bool gameUiHidden = false)
    {
        if (gameUiHidden) return false;
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
    // Two digits at the least, in both branches, so the box is one width for the whole
    // life of the call.
    //
    // It reserved exactly what it was drawing, which held still from nine down but not
    // across ten. UWU's Eruption Baits counts from ten: at 10 the box was two digits
    // wide, at 9 it was one, and centred words slid half a digit sideways on the tick
    // somebody is reading them on. That is the same fault this already handled at go,
    // one boundary earlier.
    //
    // Widened rather than narrowed, and never below what is actually being drawn, so a
    // longer countdown than anything shipped today still gets its room.
    public const int CountdownDigits = 2;

    public static (string Line, string Reserve) Countdown(
        string words, bool show, bool counting, float remaining)
    {
        if (!show) return (words, words);

        var number = counting ? $"{MathF.Ceiling(remaining):0}" : "";
        var room = new string('0', Math.Max(CountdownDigits, number.Length));

        return (counting ? $"{words} ({number})" : words, $"{words} ({room})");
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
