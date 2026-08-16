namespace FrenAlerts.Engine;

// Turns specs into triggers for one territory.
public static class TriggerPack
{
    public static IEnumerable<Trigger> Build(IEnumerable<CallSpec> specs, ushort territory) =>
        Build(specs, territory, []);

    public static IEnumerable<Trigger> Build(
        IEnumerable<CallSpec> specs, ushort territory, IEnumerable<Trigger> alreadyCovered)
    {
        var covered = alreadyCovered.ToList();
        var taken = covered
            .Where(t => t.MatchId != 0)
            .Select(t => (t.On, t.MatchId))
            .ToHashSet();
        var owned = covered.SelectMany(t => t.Owns).Distinct().ToList();

        foreach (var spec in specs)
        {
            if (spec.Territory != territory || spec.NeedsWording) continue;
            if (taken.Contains((spec.On, spec.MatchId))) continue;
            if (owned.Any(o => Names(spec.Key, o))) continue;
            yield return ToTrigger(spec, territory);
        }
    }

    private static bool Names(string key, string mechanic) =>
        $"-{key}-".Contains($"-{mechanic}-", StringComparison.Ordinal);

    public static Trigger ToTrigger(CallSpec spec) => ToTrigger(spec, spec.Territory);

    public static Trigger ToTrigger(CallSpec spec, ushort territory) => new()
    {
        Id = spec.Id,
        On = spec.On,
        MatchId = spec.MatchId,
        OnlyMe = spec.OnlyMe,
        Aim = spec.Aim,
        From = spec.From,
        Hush = spec.Hush,
        Once = spec.Once,
        // A line whose condition could not be reproduced fires at moments that look
        // arbitrary, so it loads switched off rather than chattering by default.
        Enabled = spec.DefaultOn,
        Occurrence = spec.Occurrence,
        Phase = spec.Phase,
        For = spec.For,
        Make = ctx => NeedsATarget(spec.Text) && !ctx.HasRealTarget ? null : new Call
        {
            Text = Fill(spec.Text, ctx),
            Time = ctx.Event.Time + spec.Delay,
            Key = spec.DedupeKey,
            Level = spec.Level,
            Personal = spec.Personal || spec.OnlyMe,
            Hold = spec.Hold,
            Hush = spec.Hush,
            Once = spec.Once,
            // Filled the same way the text is, or a spoken line keeps the
            // placeholder and the voice reads "on brace target" out loud.
            Speech = Fill(spec.Speech, ctx),
        },
    };

    public static bool NeedsATarget(string text) =>
        text.Contains("{target}", StringComparison.Ordinal);

    public static string Fill(string text, in TriggerContext ctx) =>
        text.Contains("{target}", StringComparison.Ordinal)
            ? text.Replace("{target}", ctx.NameTarget(), StringComparison.Ordinal)
            : text;
}
