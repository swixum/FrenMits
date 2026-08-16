namespace FrenAlerts.Engine;

// M12S, Lindwurm.
//
// Only what the pack could not carry. Most of what is left in this fight hangs on
// the line-up the group hands out before the pull (who is chain one, who is beta),
// which no event carries, so those calls need the raid plan rather than a trigger.
public static class Lindwurm
{
    public const ushort Territory = 1327;

    // The boss coming back and turning targetable again, which is the only thing
    // that announces the two scourges: both are instant, so there is no cast to
    // fire on and no marker to read.
    private const uint Targetable = 0x8000000D;

    // Two values, one per platform set it returns from. Both mean the same thing
    // here, and only one of them arrives in any given pull.
    private static readonly uint[] ComingBack = [0x1E01, 0x1E001];

    public static IEnumerable<Trigger> Triggers()
    {
        foreach (var t in LindwurmChains.Triggers()) yield return t;
        foreach (var t in LindwurmBlobs.Triggers()) yield return t;
        foreach (var t in LindwurmReplication.Triggers()) yield return t;
        foreach (var t in LindwurmCoil.Triggers()) yield return t;
        foreach (var t in LindwurmSlam.Triggers()) yield return t;

        // Split Scourge lands first and every head takes the nearest player with a
        // line, so the tanks step out and everyone else stays off them.
        yield return new Trigger
        {
            Id = "m12s-scourges",
            Says = "get out, line on you / away from the tanks",
            On = EventKind.ActorControl,
            MatchId = Targetable,
            // Said once a pull: it fires on the boss returning and it returns more
            // than once.
            Once = true,
            OncePerBurst = false,
            Make = ctx => !ComingBack.Contains(ctx.Event.Arg1) ? null : new Call
            {
                Text = ctx.ForMe("tank") ? "get out, line on you" : "away from the tanks",
                Time = ctx.Event.Time,
                Key = "m12s-scourges",
                Level = CallLevel.Alert,
                Hold = 9f,
            },
        };
    }
}
