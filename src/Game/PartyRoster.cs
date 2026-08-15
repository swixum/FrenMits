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
}
