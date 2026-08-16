namespace FrenAlerts.Engine;

public static class Lindwurm
{
    public const ushort Territory = 1327;

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

    private static Trigger Element(string id, uint status, string element, int seconds) =>
        Debuff(id, status, $"{element} resistance down", seconds, CallLevel.Info);

    public static IEnumerable<Trigger> Triggers()
    {
        yield return Debuff("doom", 0xD24, "Doom", 8, CallLevel.Alarm);

        // 4 gains on 4 players at 17 seconds, so it picks half the party.
        yield return Debuff("bursting-grotesquerie", 0x1299, "Bursting Grotesquerie", 17);

        // The elemental family, most to least seen in the sampled pull.
        yield return Element("poison-res-down", 0xF5F, "Poison", 12);
        yield return Element("dark-res-down", 0xCFB, "Dark", 24);
        yield return Element("fire-res-down", 0xB79, "Fire", 25);
        yield return Element("light-res-down", 0x1044, "Light", 24);

        yield return new Trigger
        {
            Id = "marker-on-me",
            On = EventKind.HeadMarker,
            OnlyMe = true,
            Make = ctx => new Call
            {
                Text = "marker on you",
                Time = ctx.Event.Time,
                Key = $"marker-{ctx.Event.Id:X}",
                Level = CallLevel.Alert,
                Personal = true,
            },
        };

    }
}
