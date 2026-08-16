using Dalamud.Game.ClientState.Objects.Enums;
using FrenAlerts.Engine;

namespace FrenAlerts.Game;

// Where the arena's own furniture is standing.
//
// Half the direction calls in an ultimate are read off props rather than off the
// boss: black holes, crystals, trines, elemental towers. None of them cast, none of
// them apply a status, and none of them appear in any of the other five sources, so
// without this they are invisible and every call about them can only say "unknown".
//
// Two events come out of here and no more. A thing turning up is a spawn; a thing
// that had a spot turning up somewhere else is a move, which is how a boss teleport
// is seen without reading a packet for it.
public sealed class ArenaEvents
{
    private const double PollSeconds = 0.1;

    // Under a step of drift is the same spot: props do not move at all and a boss
    // walking is not the event this is looking for. A teleport crosses the arena.
    private const float MovedBy = 5f;

    // A duty's worth of furniture with room to spare. Past it nothing new is
    // tracked, rather than the table growing for as long as the zone lasts.
    public const int Max = 512;

    private readonly Action<GameEvent> _emit;
    private readonly Dictionary<uint, Position> _where = new(Max);

    // Whether each tracked actor could be targeted last poll. Separate from the
    // position table because this is checked on every poll, including the ones
    // where the actor has not moved and the position check bails early.
    private readonly Dictionary<uint, bool> _targetable = new(Max);

    private double _lastPoll = -99;

    public ArenaEvents(Action<GameEvent> emit) => _emit = emit;

    public int Tracking => _where.Count;

    public int Reported { get; private set; }

    public int Dropped { get; private set; }

    public void Poll(double now)
    {
        if (!Paced.Due(now, _lastPoll, PollSeconds)) return;
        _lastPoll = now;

        foreach (var obj in Service.ObjectTable)
        {
            var id = obj.EntityId;
            if (id == 0) continue;

            // Players are covered by every other source and are the one thing here
            // that legitimately moves all the time. Told apart by the id's own top
            // nibble, the way the rest of the engine does it, rather than by an
            // object kind: this runs per object per tenth of a second.
            if (ActorId.IsPlayer(id)) continue;

            var at = new Position(obj.Position.X, obj.Position.Z, obj.Position.Y, obj.Rotation);

            // Read before the move check bails out, because a boss hiding to jump
            // has not moved: that is the whole point of the event.
            //
            // The position rides along on it rather than being looked up from the
            // last move. A boss turning on the spot raises no move at all, so the
            // cached one would carry the heading it had before it turned, which is
            // the heading that decides where it is about to jump.
            Toggled(obj, id, at, now);

            if (!_where.TryGetValue(id, out var had))
            {
                if (_where.Count >= Max) { Dropped++; continue; }
                _where[id] = at;
                // BaseId, which is the same number the fight data calls an npc base
                // id: two black holes share it and their entity ids differ.
                Emit(EventKind.ActorSpawn, now, id, obj.BaseId, at, NameIdOf(obj));
                continue;
            }

            if (Near(had, at)) continue;
            _where[id] = at;
            Emit(EventKind.ActorMoved, now, id, obj.BaseId, at);
        }
    }

    // A change in whether this actor can be targeted, and nothing when it holds.
    //
    // First sight is recorded rather than announced: everything in the duty is seen
    // for the first time at some point, and calling all of it a toggle would fire
    // every prop in the arena as an event on the first poll of the zone.
    private void Toggled(Dalamud.Game.ClientState.Objects.Types.IGameObject obj,
                         uint id, Position at, double now)
    {
        if (obj is not Dalamud.Game.ClientState.Objects.Types.IBattleChara actor) return;

        var now_ = actor.IsTargetable;
        if (!_targetable.TryGetValue(id, out var was))
        {
            if (_targetable.Count < Max) _targetable[id] = now_;
            return;
        }
        if (was == now_) return;

        _targetable[id] = now_;
        Reported++;
        _emit(new GameEvent
        {
            Kind = EventKind.NameToggle,
            Time = now,
            SourceId = id,
            DataId = obj.BaseId,
            Id = obj.BaseId,
            Arg1 = now_ ? 1u : 0u,
            Source = at,
        });
    }

    // An actor's name id, which is a different number from its base id and the only
    // one some adds can be recognised by. Upstream matches those by their name
    // string; this is that same row, as a number, so no text is compared.
    private static uint NameIdOf(Dalamud.Game.ClientState.Objects.Types.IGameObject obj) =>
        obj is Dalamud.Game.ClientState.Objects.Types.IBattleNpc npc ? npc.NameId : 0;

    private void Emit(EventKind kind, double now, uint id, uint dataId, Position at,
                      uint nameId = 0)
    {
        Reported++;
        _emit(new GameEvent
        {
            Kind = kind,
            Time = now,
            SourceId = id,
            DataId = dataId,
            Id = dataId,
            // Beside the base id rather than instead of it: some adds are told apart
            // by one and some by the other.
            Arg2 = nameId,
            Source = at,
        });
    }

    private static bool Near(Position a, Position b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy < MovedBy * MovedBy;
    }

    public void Forget()
    {
        _where.Clear();
        // Cleared with the rest of it. Left behind, the next duty starts holding
        // last duty's answer for an actor id the game has since reused, so the
        // first toggle of the pull is either missed or invented.
        _targetable.Clear();
        Dropped = 0;
        _lastPoll = -99;
    }
}
