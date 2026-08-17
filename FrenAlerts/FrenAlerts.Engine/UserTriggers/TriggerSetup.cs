using System.Numerics;

namespace FrenAlerts.Engine.UserTriggers;

// The two answers a host needs before any of this can run.
//
// Both were written in the plugin first and moved here for one reason: neither can
// be checked there. A rule about which shipped sets come back is exactly the sort of
// thing that is wrong once and then wrong quietly for months, and a colour packed
// back to front makes a green call blue with nothing to say so.
public static class TriggerSetup
{
    // What somebody saved, plus any shipped set they have not been offered yet.
    //
    // Topped up rather than reconciled. A shipped set that is missing is not a set
    // to restore: they deleted it. The only reason to add one is that it is newer
    // than the revision they have already seen, which is why the number travels with
    // the config rather than being worked out from what is there.
    public static List<UserTriggerSet> TopUp(
        IEnumerable<UserTriggerSet> saved, IEnumerable<UserTriggerSet> shipped, int seen)
    {
        var sets = saved.ToList();
        if (seen >= BuiltInTriggers.Revision) return sets;

        var have = sets.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var set in shipped)
            if (have.Add(set.Id)) sets.Add(set);

        return sets;
    }

    // A colour the way the screen reads one, which is back to front from the way it
    // is written: alpha, blue, green, red.
    public static uint Packed(Vector4 colour)
    {
        static uint Byte(float v) => (uint)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);

        return Byte(colour.W) << 24 | Byte(colour.Z) << 16 | Byte(colour.Y) << 8 | Byte(colour.X);
    }
}
