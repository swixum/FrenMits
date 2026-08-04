using System;
using System.Collections.Generic;

namespace FrenMits.Encounters;

// A reference sheet names a mit again on every mechanic its buff still reaches.
// "Temperance" on Fall of Faith 1/2, 3/4 and Burnished Glory 2 is ONE press
// lingering across three rows, not three presses - the FRU sheet even keeps a
// tab called "Lingering Mits" for exactly this. Read as three uses, the second
// and third are impossible on a 120s cooldown, which is how a correct plan ends
// up painted red.
public static class CarryOver
{
    // A press lands as a status about this much after the button, and damage
    // snapshots a beat before its hit. So the press covering a hit happens
    // shortly BEFORE it, and its buff runs out that much sooner afterwards.
    private const float ApplyDelay = 0.6f;

    // The last moment a press made for a hit at this time still covers.
    public static float Reach(float time, float duration, float coverUntil)
    {
        var end = duration > 0f ? time + duration - ApplyDelay : time;
        return coverUntil > end ? coverUntil : end;
    }

    // Charges exist to be spent inside one another's window, and a buff that a
    // following spell consumes never lingers onto the next mechanic.
    private static bool CanLinger(string name, float duration)
        => duration > 0f && !AbilityBook.HasCharges(name) && !AbilityBook.IsNoCarryOver(name);

    // Marks each use that is the previous press still running rather than a
    // press of its own. Uses must be ordered by time, and must already be
    // narrowed to one player (one slot, one job tag).
    public static bool[] Mark(IReadOnlyList<(string Name, float Time, float Duration, float CoverUntil)> uses)
    {
        var carried = new bool[uses.Count];
        if (uses.Count < 2) return carried;

        var upUntil = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < uses.Count; i++)
        {
            var (name, time, duration, coverUntil) = uses[i];
            if (CanLinger(name, duration)
                && upUntil.TryGetValue(name, out var end) && time <= end)
            {
                carried[i] = true;
                continue;
            }
            upUntil[name] = Reach(time, duration, coverUntil);
        }
        return carried;
    }
}
