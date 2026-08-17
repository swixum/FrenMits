using Dalamud.Game.ClientState.Conditions;
using FrenAlerts.Engine;

namespace FrenAlerts.Game;

public sealed class CombatEvents
{
    private readonly PullEdge _edge = new();

    public int Pulls { get; private set; }

    public bool InPull => _edge.InPull;

    // A recording counts as a pull whatever the combat flag says.
    //
    // The flag is read off the client and playback never sets it: measured across
    // seventeen recorded minutes of Dancing Mad, it fired zero times. Handed in
    // rather than emitted around this, so the edge is tracked here like any other
    // and everything that counts pulls keeps counting them. Emitting the event
    // directly instead left the engine reset correctly while this still said no pull
    // was running, and the window hides its pull, phase and timeline readouts on
    // exactly that answer.
    public GameEvent? Poll(double now, bool watchingARecording = false)
    {
        var crossed = _edge.Note(
            watchingARecording || Service.Condition[ConditionFlag.InCombat], now);
        if (crossed is not { } kind) return null;

        if (kind == EventKind.CombatStart) Pulls++;
        return new GameEvent { Kind = kind, Time = now };
    }

    public void Forget() => _edge.Forget();
}
