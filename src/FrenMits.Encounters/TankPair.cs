using System;
using System.Collections.Generic;

namespace FrenMits.Encounters;

// The live tank duo as one canonical key ("GNB+PLD": alphabetical, '+'-joined),
// so a press can gate on which two tank jobs are actually in the party.
public static class TankPair
{
    public static readonly string[] AllPairs =
        { "DRK+GNB", "DRK+PLD", "DRK+WAR", "GNB+PLD", "GNB+WAR", "PLD+WAR" };

    // The party's current duo, written by the host each tick; null = unresolved
    // (out of duty, solo, not exactly two tanks, mirrored jobs).
    public static string? CurrentKey { get; set; }

    // "PLD"+"gnb" in any order -> "GNB+PLD"; null when a job is missing or the
    // duo is mirrored, which no pair key can name.
    public static string? KeyFor(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return null;
        var x = a!.ToUpperInvariant();
        var y = b!.ToUpperInvariant();
        if (x == y) return null;
        return string.CompareOrdinal(x, y) < 0 ? x + "+" + y : y + "+" + x;
    }

    // An unresolved duo passes, like TankPriority's fallback: a solo session
    // or an editor away from the duty still shows every variant.
    public static bool Matches(List<string> pairs)
    {
        if (pairs.Count == 0) return true;
        var key = CurrentKey;
        if (key == null) return true;
        // A plain loop, since this runs per line per frame.
        for (var i = 0; i < pairs.Count; i++)
            if (string.Equals(pairs[i], key, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
