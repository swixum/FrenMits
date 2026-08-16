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

    public void Reset() => _slotById.Clear();
}
