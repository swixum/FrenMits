using Dalamud.Game.ClientState.Conditions;
using FrenAlerts.Engine;

namespace FrenAlerts.Game;

public sealed class CombatEvents
{
    private readonly PullEdge _edge = new();

    public int Pulls { get; private set; }

    public bool InPull => _edge.InPull;

    public GameEvent? Poll(double now)
    {
        var crossed = _edge.Note(Service.Condition[ConditionFlag.InCombat], now);
        if (crossed is not { } kind) return null;

        if (kind == EventKind.CombatStart) Pulls++;
        return new GameEvent { Kind = kind, Time = now };
    }

    public void Forget() => _edge.Forget();
}
