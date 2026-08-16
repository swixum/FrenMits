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

    private bool _muted;
    private bool _tethersMuted;
    private bool _seedCasts;
    private bool _seedStatuses;

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

    // Set from the frame when a parser is reading. Casts, statuses and tethers all
    // come down a line with more in them than the object table holds, so these reads
    // stand down rather than doubling them up.
    //
    // The zone change is not one of these: it is an event off the client, not a poll,
    // and it is what rebuilds the fight, so it keeps firing muted or not. Nor is the
    // arena, which nothing writes a line about.
    public bool Muted
    {
        get => _muted;
        set
        {
            if (_muted == value) return;
            _muted = value;

            // What was held belongs to the source that is standing down.
            _casting.Clear();
            _statuses.Clear();
            Tethers.Forget();

            // Coming back, the first pass writes down what is already there without
            // saying any of it. Against a cleared table every status the party is
            // wearing looks new, and announcing them all at once is a wall of calls
            // for mechanics that resolved while the parser was still talking.
            _seedCasts = _seedStatuses = !value;
            Tethers.Seeding = !value;
        }
    }

    // Tethers stand down on their own terms: the control packet answers them whether
    // a parser is up or not, so this goes quiet more often than the rest do.
    public bool TethersMuted
    {
        get => _tethersMuted;
        set
        {
            if (_tethersMuted == value) return;
            _tethersMuted = value;
            Tethers.Forget();
            Tethers.Seeding = !value;
        }
    }

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

        // The arena's props are read by EventSources, not here. Two of these used to
        // exist at once and every spawn, move and toggle reached the engine twice,
        // which quietly halved every call that counts occurrences: the fourth
        // maelstrom fired on the second, and four green orbs on two.
        if (!_tethersMuted) Tethers.Poll(now);

        if (_muted) return;

        PollCasts(now);

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

            // A cast that was already running before this source took over is not a
            // cast starting, and calling it one fires the mechanic's call halfway
            // through the mechanic.
            if (_seedCasts) continue;

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

        _seedCasts = false;
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
                // Already worn when this source took over, so it is state rather
                // than news. Written down, not announced.
                if (_seedStatuses) continue;

                _emit(new GameEvent
                {
                    Kind = EventKind.StatusGain,
                    Time = now,
                    Id = status.StatusId,
                    SourceId = status.SourceObject?.EntityId ?? 0,
                    TargetId = id,
                    Duration = status.RemainingTime,
                    // Stacks, and where a fight hides an answer. Neo Exdeath says
                    // which half of its debuffs are lying entirely through this.
                    Param = status.Param,
                });
            }

            foreach (var gone in had)
            {
                if (now_.Contains(gone)) continue;
                if (_seedStatuses) continue;
                _emit(new GameEvent
                {
                    Kind = EventKind.StatusLose, Time = now, Id = gone, TargetId = id,
                });
            }

            _statuses[id] = now_;
        }

        foreach (var id in _statuses.Keys.Where(k => !_seen.Contains(k)).ToList())
            _statuses.Remove(id);

        _seedStatuses = false;
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
