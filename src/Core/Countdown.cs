using System;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace FrenMits;

// The party's pull countdown, read straight off the game's own agent.
//
// No hook and no signature: AgentCountDownSettingDialog is the thing that runs the
// countdown, and it already holds everything wanted here - how long is left, and
// who started it. A signature would rot on the next patch for no gain.
//
// This is the only moment before a pull when the game says exactly when the fight
// begins, which is what lets the clock be right from the first second instead of
// starting a frame or two late on the combat flag.
public static class Countdown
{
    // What the game says about the countdown right now.
    public readonly record struct State(bool Active, float Remaining, uint InitiatorId);

    public static readonly State None = default;

    // The game's own cap. A value past it is a stale read, not a countdown.
    public const float MaxSeconds = 30f;

    public static unsafe State Read()
    {
        try
        {
            var agent = AgentCountDownSettingDialog.Instance();
            if (agent == null) return None;

            // TimeRemaining is the signal that decides it: it is only ever counting
            // while a countdown runs, so a stale flag can't fake one. The two flags
            // are read together on purpose - `Active` is the countdown itself and
            // `ShowingCountdown` the display it puts up, and taking either means
            // this keeps working whichever of them the game moves next.
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
