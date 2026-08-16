namespace FrenAlerts.Engine;

public sealed class PartyContext
{
    private readonly Dictionary<uint, string> _slotById = new(8);

    public int Count => _slotById.Count;

    public void Assign(uint actorId, string slot)
    {
        if (actorId == 0 || string.IsNullOrWhiteSpace(slot)) return;
        if (!Audience.IsSlot(slot)) return;
        _slotById[actorId] = slot.ToUpperInvariant();
    }

    // Empty for anyone not in the party, which includes every NPC, so a call about
    // an unknown actor says nothing rather than inventing a slot for it.
    public string SlotOf(uint actorId) =>
        _slotById.GetValueOrDefault(actorId, "");

    public string RoleOf(uint actorId) => Audience.RoleOf(SlotOf(actorId));

    // The other way round, for a call that is about the player in a named seat
    // rather than about whoever an event happened to name.
    //
    // Zero when nobody is in that seat, which is a party that has not been read yet
    // as much as it is a seat nobody filled, and both mean the same thing to a call:
    // there is nobody to say anything about.
    public uint IdOf(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return 0;
        var want = slot.ToUpperInvariant();
        foreach (var (id, seat) in _slotById)
            if (seat == want) return id;
        return 0;
    }

    public void Reset() => _slotById.Clear();
}
