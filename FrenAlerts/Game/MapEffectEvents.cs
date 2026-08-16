using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.Network;
using FrenAlerts.Engine;

namespace FrenAlerts.Game;

// Map effects: the arena itself changing, which four shipped calls fire on.
public sealed unsafe class MapEffectEvents : HookedSource<MapEffectEvents.Handle>
{
    public delegate void Handle(MapEffectPacket* packet);

    private readonly Func<double> _now;

    public MapEffectEvents(Func<double> now) : base(max: 1024)
    {
        _now = now;
        Install(PacketDispatcher.Addresses.HandleMapEffectPacket.String, OnPacket,
                "map effects unavailable, calls about the arena will not fire.");
    }

    private void OnPacket(MapEffectPacket* packet)
    {
        var p = packet;

        Guard(() =>
        {
            if (p is null) return;

            Offer(new GameEvent
            {
                Kind = EventKind.MapEffect,
                Time = _now(),
                // Which of the two a fight keys on differs, so both travel.
                Id = p->State,
                SourceId = p->EventId,
                TargetId = p->Index,
                Duration = p->TimelineIndex,
            });
        });

        Hooked?.Original(packet);
    }
}
