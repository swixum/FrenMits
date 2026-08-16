using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FrenAlerts.Engine;

namespace FrenAlerts.Game;

public sealed unsafe class TetherEvents
{
    private const double PollSeconds = 0.1;

    private const int PerActor = 2;

    private readonly Action<GameEvent> _emit;

    // What each actor was tethered by last time, so only a new tether is an event.
    private readonly Dictionary<uint, (ushort Id, ulong To)> _held = new(16);
    private readonly HashSet<uint> _seen = new(16);

    private double _lastPoll = -99;

    public TetherEvents(Action<GameEvent> emit) => _emit = emit;

    public int Reported { get; private set; }

    public void Poll(double now)
    {
        if (!Paced.Due(now, _lastPoll, PollSeconds)) return;
        _lastPoll = now;

        _seen.Clear();

        foreach (var obj in Service.ObjectTable)
        {
            if (obj is not IBattleChara actor) continue;
            var id = actor.EntityId;
            if (!Watchers.Watching(id)) continue;
            _seen.Add(id);

            Read(actor, id, now);
        }

        foreach (var gone in _held.Keys.Where(k => !_seen.Contains(k)).ToList())
            _held.Remove(gone);
    }

    private void Read(IBattleChara actor, uint id, double now)
    {
        if (actor.Address == nint.Zero) return;

        var character = (Character*)actor.Address;
        var tethers = character->Vfx.Tethers;

        // The struct says two; the span is the authority on how many there are.
        var count = Math.Min(tethers.Length, PerActor);
        (ushort Id, ulong To) now_ = (0, 0);

        for (var i = 0; i < count; i++)
        {
            if (tethers[i].Id == 0) continue;
            now_ = (tethers[i].Id, tethers[i].TargetId.Id);
            break;
        }

        var had = _held.GetValueOrDefault(id);
        if (now_ == had) return;
        _held[id] = now_;

        if (now_.Id == 0) return;

        Reported++;
        _emit(new GameEvent
        {
            Kind = EventKind.Tether,
            Time = now,
            // The one wearing it is the source, whoever it runs to is the target,
            // which is the direction a recording writes them in too.
            SourceId = id,
            TargetId = (uint)now_.To,
            Id = now_.Id,
        });
    }

    public void Forget()
    {
        _held.Clear();
        _seen.Clear();
        _lastPoll = -99;
    }
}
