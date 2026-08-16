using FFXIVClientStructs.FFXIV.Client.Network;
using FrenAlerts.Engine;

namespace FrenAlerts.Game;

// Control packets, which carry the direction calls the object table cannot see.
public sealed class ControlEvents : HookedSource<ControlEvents.Packet>
{
    public delegate void Packet(
        uint entityId, uint category, uint arg1, uint arg2, uint arg3, uint arg4,
        uint arg5, uint arg6, uint arg7, uint arg8, ulong targetId, bool isRecorded);

    private readonly Func<double> _now;

    public ControlEvents(Func<double> now)
    {
        _now = now;
        Install(PacketDispatcher.Addresses.HandleActorControlPacket.String, OnPacket,
                "control packets unavailable, direction calls will not fire.");
    }

    private void OnPacket(
        uint entityId, uint category, uint arg1, uint arg2, uint arg3, uint arg4,
        uint arg5, uint arg6, uint arg7, uint arg8, ulong targetId, bool isRecorded)
    {
        Guard(() => Offer(new GameEvent
        {
            Kind = EventKind.ActorControl,
            Time = _now(),
            SourceId = entityId,
            TargetId = (uint)targetId,
            Id = category,
            // The first argument is what most of these carry their meaning in.
            Duration = arg1,
        }));

        // Guarded, because failing to pass the packet on would break the game's own
        // handling of it, which is far worse than losing a call.
        Hooked?.Original(entityId, category, arg1, arg2, arg3, arg4,
                         arg5, arg6, arg7, arg8, targetId, isRecorded);
    }
}
