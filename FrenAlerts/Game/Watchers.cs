using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace FrenAlerts.Game;

// Who is worth reading statuses, tethers and markers off.
//
// The party, normally: a status on a trash mob is not a call, and walking every actor
// in the zone ten times a second is work for nothing.
//
// A replay has no party. The client is playing back packets, so the eight players are
// in the object table but the party list is empty, and every filter written as "is
// this a party member" answers no to all of them. That silently cost the statuses,
// the tethers and every slot name, which is most of what the calls are made of.
//
// So the party list is used when it has anybody in it, and inside a duty with an
// empty one the players in the object table stand in for it. The duty check is what
// keeps this from watching a whole city.
public static class Watchers
{
    // A full alliance, which is the most any duty puts in one instance. Past it the
    // read is refused rather than allowed to grow.
    public const int Max = 24;

    public static bool Watching(uint entityId)
    {
        if (entityId == 0) return false;
        if (PartySlots.Me?.EntityId == entityId) return true;

        foreach (var m in Service.PartyList)
            if (m.GameObject?.EntityId == entityId) return true;

        // Only once the party list has proved empty, and only inside content.
        return StandIn() && IsPlayerHere(entityId);
    }

    // True when the party list cannot answer and something else has to.
    public static bool StandIn() =>
        Service.PartyList.Length == 0
        && (Replay.InPlayback || Service.Condition[ConditionFlag.BoundByDuty]);

    // Every player the object table can see, for the places that need the whole set
    // rather than a yes or no. Bounded, and empty when the party list is fine.
    public static IEnumerable<IPlayerCharacter> StandingIn()
    {
        if (!StandIn()) yield break;

        var n = 0;
        foreach (var obj in Service.ObjectTable)
        {
            if (obj is not IPlayerCharacter pc) continue;
            if (++n > Max) yield break;
            yield return pc;
        }
    }

    private static bool IsPlayerHere(uint entityId)
    {
        var n = 0;
        foreach (var obj in Service.ObjectTable)
        {
            if (obj is not IPlayerCharacter pc) continue;
            if (++n > Max) return false;
            if (pc.EntityId == entityId) return true;
        }
        return false;
    }
}
