using Dalamud.Game.ClientState.Objects.Types;
using FrenAlerts.Engine;

namespace FrenAlerts.Game;

public sealed class LiveEvents : IDisposable
{
    private const double StatusPollSeconds = 0.1;

    private readonly Action<GameEvent> _emit;
    private readonly Dictionary<uint, uint> _casting = new(16);
    private readonly Dictionary<uint, HashSet<uint>> _statuses = new(16);
    private readonly HashSet<uint> _seen = new(64);

    private readonly Func<double> _clock;
    private double _lastStatusPoll;
    private uint _territory;

    public LiveEvents(Action<GameEvent> emit, Func<double> clock)
    {
        _emit = emit;
        _clock = clock;
        Tethers = new TetherEvents(emit);
        _territory = Service.ClientState.TerritoryType;
        Service.Framework.Update += OnUpdate;
        Service.ClientState.TerritoryChanged += OnTerritoryChanged;
    }

    // Seconds since this source started, which is what every event's Time means.
    // Handed in rather than kept here, because in a replay the clock is the
    // recording's own position and not the wall.
    public double Now => _clock();

    public bool Enabled { get; set; } = true;

    private void OnTerritoryChanged(uint territory)
    {
        _territory = territory;
        Forget();
        _emit(new GameEvent { Kind = EventKind.ZoneChange, Time = Now, Id = territory });
    }

    private void OnUpdate(Dalamud.Plugin.Services.IFramework framework)
    {
        if (!Enabled) return;
        if (PartySlots.Me is null) { Forget(); return; }

        var now = Now;
        PollCasts(now);
        Tethers.Poll(now);

        if (!Paced.Due(now, _lastStatusPoll, StatusPollSeconds)) return;
        _lastStatusPoll = now;
        PollStatuses(now);
    }

    // Read off the character rather than the wire, so it needs no signature.
    public TetherEvents Tethers { get; }

    private void PollCasts(double now)
    {
        foreach (var obj in Service.ObjectTable)
        {
            if (obj is not IBattleChara actor) continue;
            var id = actor.EntityId;

            if (!actor.IsCasting)
            {
                _casting.Remove(id);
                continue;
            }

            var action = actor.CastActionId;
            if (_casting.TryGetValue(id, out var already) && already == action) continue;
            _casting[id] = action;

            _emit(new GameEvent
            {
                Kind = EventKind.CastStart,
                Time = now,
                SourceId = id,
                TargetId = (uint)actor.CastTargetObjectId,
                Id = action,
                CastTime = actor.TotalCastTime,
                Source = At(actor),
            });
        }
    }

    private void PollStatuses(double now)
    {
        _seen.Clear();

        foreach (var obj in Service.ObjectTable)
        {
            if (obj is not IBattleChara actor) continue;
            var id = actor.EntityId;
            if (!Watchers.Watching(id)) continue;
            _seen.Add(id);

            if (!_statuses.TryGetValue(id, out var had))
            {
                had = [];
                _statuses[id] = had;
            }

            var now_ = new HashSet<uint>();
            foreach (var status in actor.StatusList)
            {
                if (status.StatusId == 0) continue;
                now_.Add(status.StatusId);
                if (had.Contains(status.StatusId)) continue;

                _emit(new GameEvent
                {
                    Kind = EventKind.StatusGain,
                    Time = now,
                    Id = status.StatusId,
                    SourceId = status.SourceObject?.EntityId ?? 0,
                    TargetId = id,
                    Duration = status.RemainingTime,
                });
            }

            foreach (var gone in had)
            {
                if (now_.Contains(gone)) continue;
                _emit(new GameEvent
                {
                    Kind = EventKind.StatusLose, Time = now, Id = gone, TargetId = id,
                });
            }

            _statuses[id] = now_;
        }

        foreach (var id in _statuses.Keys.Where(k => !_seen.Contains(k)).ToList())
            _statuses.Remove(id);
    }

    private static Position At(IBattleChara actor) =>
        new(actor.Position.X, actor.Position.Z, actor.Position.Y, actor.Rotation);

    private void Forget()
    {
        _casting.Clear();
        _statuses.Clear();
        Tethers.Forget();
    }

    public void Dispose()
    {
        Service.ClientState.TerritoryChanged -= OnTerritoryChanged;
        Service.Framework.Update -= OnUpdate;
        Forget();
    }
}
