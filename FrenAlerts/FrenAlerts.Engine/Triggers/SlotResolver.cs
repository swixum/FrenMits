namespace FrenAlerts.Engine;

public static class SlotResolver
{
    public static readonly uint[] TankJobs = [19, 21, 32, 37];              // PLD WAR DRK GNB
    public static readonly uint[] HealerJobs = [24, 28, 33, 40];            // WHM SCH AST SGE
    public static readonly uint[] MeleeJobs = [20, 22, 30, 34, 39, 41];     // MNK DRG NIN SAM RPR VPR

    public static void Fill(PartyContext party, IReadOnlyList<(uint EntityId, uint JobId)> members)
    {
        party.Reset();
        if (members.Count == 0) return;

        List<uint> tanks = [], healers = [], melee = [], ranged = [];
        foreach (var (id, job) in members)
        {
            if (TankJobs.Contains(job)) tanks.Add(id);
            else if (HealerJobs.Contains(job)) healers.Add(id);
            else if (MeleeJobs.Contains(job)) melee.Add(id);
            else ranged.Add(id);
        }

        Pair(party, tanks, "MT", "OT");
        Pair(party, healers, "H1", "H2");
        Pair(party, melee, "M1", "M2");
        Pair(party, ranged, "R1", "R2");
    }

    private static void Pair(PartyContext party, List<uint> ids, string first, string second)
    {
        if (ids.Count > 0) party.Assign(ids[0], first);
        if (ids.Count > 1) party.Assign(ids[1], second);
    }
}
