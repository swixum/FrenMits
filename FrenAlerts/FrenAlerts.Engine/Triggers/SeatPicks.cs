namespace FrenAlerts.Engine;

// Who sits in each seat, said by hand.
//
// The seats are worked out from jobs, which is right only while the group's order
// matches job order. With two melee, which one is M1 is the group's call and the game
// has no opinion, so half the calls that split a pair name the wrong person and
// nothing reports it: the call is there, it is on time, and it is about somebody else.
//
// Held by name, because a name is what somebody can pick and recognise. A name that is
// not in the party today is left alone rather than dropped, so a night with a stand-in
// does not wipe the group's own seating.
public static class SeatPicks
{
    // Applied over the worked-out seats, never instead of them: a seat nobody named
    // keeps the answer the jobs gave it.
    public static void Apply(PartyContext party,
        IReadOnlyList<(uint Id, string Name, uint Job)> roster,
        IReadOnlyDictionary<string, string> picks)
    {
        if (party.Count == 0 || roster.Count == 0 || picks.Count == 0) return;

        // In seat order rather than the dictionary's, so the same group and the same
        // picks always land the same way round.
        foreach (var slot in Audience.Slots)
        {
            if (!picks.TryGetValue(slot, out var name) || string.IsNullOrWhiteSpace(name)) continue;

            var who = Find(roster, name);
            if (who is not { } sat) continue;

            // The seat was named for somebody playing that role. Turn up as a healer
            // and the old melee seat is not yours tonight, whatever the note says:
            // seating you there would take H1 off the person actually healing.
            if (Audience.RoleOf(slot) != SlotResolver.RoleOf(sat.Job)) continue;

            // Swapping rather than assigning: whoever held the seat takes the one
            // being left, or two people answer to the same seat and the other one is
            // called for nobody.
            party.Swap(slot, sat.Id);
        }
    }

    private static (uint Id, string Name, uint Job)? Find(
        IReadOnlyList<(uint Id, string Name, uint Job)> roster, string name)
    {
        foreach (var who in roster)
            if (string.Equals(who.Name, name, StringComparison.OrdinalIgnoreCase)) return who;
        return null;
    }
}
