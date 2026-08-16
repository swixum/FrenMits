namespace FrenAlerts.Engine;

public static class FuturesRewritten
{
    public const ushort Territory = 1238;

    // Statuses an enemy applied to a player, with what the pull measured.
    private static Trigger Debuff(string id, uint status, string name, int seconds,
                                  CallLevel level = CallLevel.Alert) => new()
    {
        Id = id,
        On = EventKind.StatusGain,
        MatchId = status,
        OnlyMe = true,
        Make = ctx => new Call
        {
            Text = $"{name}, {seconds}s",
            Time = ctx.Event.Time,
            Key = id,
            Level = level,
            Personal = true,
        },
    };

    public static IEnumerable<Trigger> Triggers()
    {
        yield return Debuff("powder-mark", 0x1046, "Powder Mark", 16, CallLevel.Alarm);

        yield return Debuff("doom", 0x9D4, "Doom", 5, CallLevel.Alarm);

        yield return Debuff("floating-fetters", 0x900, "Fetters", 4, CallLevel.Alarm);

        // 69 gains, whole party, 12 seconds.
        yield return Debuff("fire-resistance-down", 0x111F, "Fire debuff", 12);

        // 192 gains, whole party, 15 seconds.
        yield return Debuff("bleeding", 0xB87, "Bleed", 15, CallLevel.Info);

        yield return Debuff("lightsteeped", 0x8D1, "Lightsteeped", 36, CallLevel.Info);

        yield return new Trigger
        {
            Id = "tank-marker",
            On = EventKind.HeadMarker,
            MatchId = 0xDA,
            OnlyMe = true,
            Make = ctx => new Call
            {
                Text = "marker on you, tanks",
                Time = ctx.Event.Time,
                Key = "tank-marker",
                Level = CallLevel.Alarm,
                Personal = true,
            },
        };

        yield return new Trigger
        {
            Id = "marker-on-me",
            On = EventKind.HeadMarker,
            OnlyMe = true,
            Make = ctx => OwnedMarkers.Contains(ctx.Event.Id) ? null : new Call
            {
                Text = "marker on you",
                Time = ctx.Event.Time,
                Key = $"marker-{ctx.Event.Id:X}",
                Level = CallLevel.Alert,
                Personal = true,
            },
        };

    }

    private static readonly HashSet<uint> OwnedMarkers = [0xDA];
}
