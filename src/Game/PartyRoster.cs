using System;

namespace FrenMits.Game;

// Live party reads: who this player is and who they are tanking with.
public static class PartyRoster
{
    // The local player's live job.
    public static string? LocalJob()
    {
        var rowId = Plugin.LocalPlayer?.ClassJob.RowId;
        return rowId is { } id ? Jobs.ByRowId(id)?.Abbreviation : null;
    }

    // The other tank's job in the local party, or null if it can't be read
    // (solo, out of the duty, or a mirrored comp where "the other one" is
    // ambiguous).
    public static string? CoTankJob(string? localJob)
    {
        if (string.IsNullOrEmpty(localJob)) return null;
        var selfId = Plugin.LocalPlayer?.EntityId;
        string? other = null;
        var others = 0;
        foreach (var m in Service.PartyList)
        {
            if (selfId != null && m.EntityId == selfId) continue;
            if (Jobs.ByRowId(m.ClassJob.RowId) is not { Role: JobRole.Tank } job) continue;
            other = job.Abbreviation;
            others++;
        }
        // Three tanks has no "other one", so say so rather than picking last.
        return others == 1 ? other : null;
    }

    // The pairing TankPriority resolves its columns from.
    public static (string? Local, string? CoTank) TankJobs()
    {
        var local = LocalJob();
        return (local, CoTankJob(local));
    }

    // The party's tank duo as a TankPair key, counting every tank in the party
    // (not local + other) so a healer's grid view resolves too. Null unless
    // exactly two distinct tank jobs are present.
    public static string? TankPairKey()
    {
        string? first = null, second = null;
        var tanks = 0;
        foreach (var m in Service.PartyList)
        {
            if (Jobs.ByRowId(m.ClassJob.RowId) is not { Role: JobRole.Tank } job) continue;
            if (tanks == 0) first = job.Abbreviation;
            else if (tanks == 1) second = job.Abbreviation;
            tanks++;
        }
        return tanks == 2 ? TankPair.KeyFor(first, second) : null;
    }
}
