using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FrenAlerts.Engine;

namespace FrenAlerts.Game;

// Abilities landing, which a fifth of the call pack fires on.
//
// Both the address and the signature come from the maintained list, which makes this
// a safer read than the control packet next door, where only the address is.
public sealed unsafe class AbilityEvents : HookedSource<AbilityEvents.Receive>
{
    // One action reports its targets in a block the header counts. Eight is the most
    // the effects array holds and the count is a byte, so it is clamped rather than
    // trusted: reading past the end here is reading somebody else's memory.
    private const int MaxTargets = 8;

    public delegate void Receive(
        uint casterEntityId, nint casterPtr, nint targetPos,
        ActionEffectHandler.Header* header, nint effects, GameObjectId* targetEntityIds);

    private readonly Func<double> _now;

    public AbilityEvents(Func<double> now)
    {
        _now = now;
        Install(ActionEffectHandler.Addresses.Receive.String, OnReceive,
                "abilities unavailable, calls that fire on a hit will not.");
    }

    private void OnReceive(
        uint casterEntityId, nint casterPtr, nint targetPos,
        ActionEffectHandler.Header* header, nint effects, GameObjectId* targetEntityIds)
    {
        // Captured before the guard because a lambda cannot close over a pointer.
        var head = header;
        var targets = targetEntityIds;

        Guard(() =>
        {
            if (head is null || targets is null) return;

            var count = Math.Min((int)head->NumTargets, MaxTargets);
            var action = head->ActionId;
            var now = _now();

            // One event per target, which is how a recording writes them too: a
            // raidwide on eight players is eight lines, and the burst window
            // upstream collapses them back into one mechanic.
            for (var i = 0; i < count; i++)
            {
                if (!Offer(new GameEvent
                {
                    Kind = EventKind.AbilityHit,
                    Time = now,
                    SourceId = casterEntityId,
                    TargetId = (uint)targets[i].Id,
                    Id = action,
                })) break;
            }
        });

        // Guarded, because failing to pass it on would break the game's handling of
        // every ability in the zone, which is far worse than a missing call.
        Hooked?.Original(casterEntityId, casterPtr, targetPos, header, effects, targetEntityIds);
    }
}
