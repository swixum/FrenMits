namespace FrenAlerts.Engine;

public static class MarkerCalls
{
    public static IEnumerable<Trigger> Triggers(ushort territory) =>
        Triggers(territory, []);

    public static IEnumerable<Trigger> Triggers(ushort territory, IEnumerable<Trigger> alreadyCovered)
    {
        if (!MarkerMeanings.ByFight.TryGetValue(territory, out var known)) yield break;

        var taken = alreadyCovered
            .Where(t => t.MatchId != 0)
            .Select(t => (t.On, t.MatchId))
            .ToHashSet();

        foreach (var m in known)
        {
            var on = m.IsTether ? EventKind.Tether : EventKind.HeadMarker;
            if (taken.Contains((on, m.Id))) continue;

            var says = m.Says;
            var tether = m.IsTether;
            yield return new Trigger
            {
                Id = $"{(tether ? "tether" : "marker")}-{m.Id:X}",
                On = on,
                MatchId = m.Id,
                OnlyMe = true,
                Make = ctx => new Call
                {
                    Text = Line(says),
                    Time = ctx.Event.Time,
                    Key = $"{(tether ? "tether" : "marker")}-{m.Id:X}",
                    // A marker is the game telling you personally to move, so it
                    // outranks anything happening to the party at the same instant.
                    Level = CallLevel.Alarm,
                    Personal = true,
                },
            };
        }
    }

    private static string Line(string says) =>
        says.Length <= 2 && says.All(char.IsDigit) ? says : $"{says} on you";
}
