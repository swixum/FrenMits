using System;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace FrenMits;

// The party's pull countdown, read straight off the game's own agent.
public static class Countdown
{
    // What the game says about the countdown right now.
    public readonly record struct State(bool Active, float Remaining, uint InitiatorId);

    public static readonly State None = default;

    // The game's own cap.
    public const float MaxSeconds = 30f;

    public static unsafe State Read()
    {
        try
        {
            var agent = AgentCountDownSettingDialog.Instance();
            if (agent == null) return None;

            // TimeRemaining is the signal that decides it: it is only ever
            // counting while a countdown runs, so a stale flag can't fake one.
            var remaining = agent->TimeRemaining;
            if (remaining <= 0f || remaining > MaxSeconds) return None;
            if (!agent->Active && !agent->ShowingCountdown) return None;

            return new State(true, remaining, agent->InitiatorId);
        }
        catch (Exception ex)
        {
            // Never let a game-memory read disturb the tick, but leave a trail:
            // silently reading "no countdown" looks exactly like there being none.
            Swallowed.Report("countdown read", ex);
            return None;
        }
    }

    // Who started it, when that can be resolved to a party member - for the log
    // line, so a mis-timed pull can be read back afterwards.
    public static string InitiatorName(uint entityId)
    {
        if (entityId == 0) return "";
        try
        {
            foreach (var obj in Service.ObjectTable)
                if (obj != null && obj.EntityId == entityId)
                    return obj.Name.ToString();
        }
        catch (Exception ex) { Swallowed.Report("countdown initiator", ex); }
        return "";
    }
}
