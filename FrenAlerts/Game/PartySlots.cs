using Dalamud.Game.ClientState.Objects.SubKinds;
using FrenAlerts.Engine;

namespace FrenAlerts.Game;

public static class PartySlots
{
    public static IPlayerCharacter? Me => Service.ObjectTable?[0] as IPlayerCharacter;

    public static void Fill(PartyContext party, IReadOnlyList<(uint EntityId, uint JobId)> members) =>
        SlotResolver.Fill(party, members);

    public static List<(uint EntityId, uint JobId)> Read()
    {
        var members = new List<(uint, uint)>(8);
        foreach (var m in Service.PartyList)
        {
            if (m.GameObject is not IPlayerCharacter pc) continue;
            members.Add((pc.EntityId, pc.ClassJob.RowId));
        }

        // A replay has no party list, so the eight players standing in the object
        // table stand in for it. Without this every call in a replay says "someone".
        if (members.Count == 0)
            foreach (var pc in Watchers.StandingIn())
                members.Add((pc.EntityId, pc.ClassJob.RowId));

        // Solo, or the list has not populated yet, so at least place the player.
        if (members.Count == 0 && Me is { } me)
            members.Add((me.EntityId, me.ClassJob.RowId));

        return members;
    }
}
