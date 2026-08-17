namespace FrenAlerts.Engine;

public static class SlotResolver
{
    public static readonly uint[] TankJobs = [19, 21, 32, 37];              // PLD WAR DRK GNB
    public static readonly uint[] HealerJobs = [24, 28, 33, 40];            // WHM SCH AST SGE
    public static readonly uint[] MeleeJobs = [20, 22, 30, 34, 39, 41];     // MNK DRG NIN SAM RPR VPR

    // The role a job can be called in, in the same words Audience uses for a seat, so
    // a seat somebody named can be held against the job they turned up on.
    public static string RoleOf(uint job) =>
        TankJobs.Contains(job) ? "tank"
        : HealerJobs.Contains(job) ? "healer"
        : "dps";

    public static void Fill(PartyContext party, IReadOnlyList<(uint EntityId, uint JobId)> members)
    {
        party.Reset();
        if (members.Count == 0) return;

        List<uint> tanks = [], healers = [], melee = [], ranged = [];
        foreach (var (id, job) in members)
        {
            // A job that has not resolved is not a role.
            //
            // Everything unrecognised falls to ranged, and a player whose ClassJob row
            // has not loaded reads as 0, so somebody the client had not finished
            // describing was seated R1 and every call that splits the ranged pair named
            // the wrong person for as long as it took to load.
            //
            // Dropped rather than guessed, which is the rule the roster beside this
            // already follows: it drops a member whose name has not loaded rather than
            // passing on an empty one. An unseated player is a call that says nothing;
            // a wrongly seated one is a call that says something false.
            if (job == 0) continue;

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
