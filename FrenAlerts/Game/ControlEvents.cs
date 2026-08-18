using FFXIVClientStructs.FFXIV.Client.Network;
using FrenAlerts.Engine;

namespace FrenAlerts.Game;

// Control packets, which carry the direction calls the object table cannot see.
//
// Two of the categories are whole event kinds rather than a direction. A head marker
// is category 34 with the marker id in the first argument and the packet aimed at
// whoever got it; a tether is category 35 with its id in the second, the packet on
// the far end of it and whoever wears it in the third, which is the direction a real
// 35 line writes. That is the only route to a head marker inside the client.
public sealed class ControlEvents : HookedSource<ControlEvents.Packet>
{
    // Marker and tether, which arrive as control packets rather than as anything the
    // object table or a status list can be asked about.
    private const uint HeadMarker = 34;
    private const uint Tether = 35;

    public delegate void Packet(
        uint entityId, uint category, uint arg1, uint arg2, uint arg3, uint arg4,
        uint arg5, uint arg6, uint arg7, uint arg8, ulong targetId, bool isRecorded);

    private readonly Func<double> _now;

    // Counted separately from the packets, because these two are the whole reason a
    // bare install can call a mechanic at all, and "the hook is installed" is not the
    // same claim as "markers are arriving off it".
    public int Markers { get; private set; }

    public int Tethers { get; private set; }

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
        Guard(() =>
        {
            var now = _now();

            Offer(new GameEvent
            {
                Kind = EventKind.ActorControl,
                Time = now,
                SourceId = entityId,
                TargetId = (uint)targetId,
                Id = category,
                // The first argument is what most of these carry their meaning in.
                Duration = arg1,
                // The same two as whole numbers, because these run past what a float
                // counts exactly and one of them is an actor id that gets arithmetic
                // done to it.
                Arg1 = arg1,
                Arg2 = arg2,
            });

            // Offered as their own kind on top of the raw packet, never instead of
            // it: the raw one is what the direction calls and the probe read, and a
            // category is not an event kind to anything watching ActorControl.
            switch (category)
            {
                // The packet is aimed at whoever got the marker, so the actor it is
                // about is the target and not the source. Same shape a log line
                // writes: 27|ts|<target>|<name>|..|..|<markerId>|<target>.
                case HeadMarker:
                    Markers++;
                    Offer(new GameEvent
                    {
                        Kind = EventKind.HeadMarker,
                        Time = now,
                        Id = arg1,
                        TargetId = entityId,
                    });
                    break;

                // The far end is the source and the one wearing it is the target,
                // which is the direction a line writes and the direction every named
                // tether call reads.
                case Tether:
                    Tethers++;
                    Offer(new GameEvent
                    {
                        Kind = EventKind.Tether,
                        Time = now,
                        Id = arg2,
                        SourceId = entityId,
                        TargetId = arg3,
                    });
                    break;
            }
        });

        // Guarded, because failing to pass the packet on would break the game's own
        // handling of it, which is far worse than losing a call.
        Hooked?.Original(entityId, category, arg1, arg2, arg3, arg4,
                         arg5, arg6, arg7, arg8, targetId, isRecorded);
    }
}
