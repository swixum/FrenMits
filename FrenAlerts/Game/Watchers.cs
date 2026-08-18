using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FrenAlerts.Engine;

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

    // The boss and its adds, for the status poll only.
    //
    // A debuff on the boss is a mechanic and was invisible: this walked the party and
    // only the party, and a parser is what covered the difference. A replay has no
    // parser and never can have one, so in a recording every call that reads what the
    // boss is wearing fired never.
    //
    // Bounded and counted separately from the players, so an arena full of adds
    // cannot turn a ten-times-a-second poll into a walk of the whole zone.
    public static bool WatchingEnemy(uint entityId)
    {
        if (entityId == 0) return false;

        var n = 0;
        foreach (var obj in Service.ObjectTable)
        {
            if (obj is not IBattleChara bc) continue;
            if (obj is IPlayerCharacter) continue;

            // Counted only where a mechanic could be worn. An ultimate arena is mostly
            // furniture: 326 combatants stood in one recorded Dancing Mad pull and 48
            // of them arrived in its first second, so counting all of them spent the
            // budget long before the boss and every status it wore was dropped.
            if (!bc.IsTargetable) continue;

            if (++n > StatusWatch.MaxEnemies) return false;
            if (bc.EntityId == entityId) return true;
        }
        return false;
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
