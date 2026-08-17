namespace FrenAlerts.Engine;

// The Unending Coil of Bahamut.
//
// Only the part the call pack could not carry. Everything else in this fight
// comes from the pack, and a trigger written here silences the pack's row on the
// same event, so nothing is repeated here that the pack already says.
//
// Nael's quotes are the fight. She says one of fourteen lines and the line is the
// only thing that tells you what the next two or three mechanics are; no cast, no
// marker and no status announces them. The pack could not carry them because they
// arrive as a spoken line rather than an action.
//
// Matched on the line's row id, never on its words: the id is the same in every
// language, and matching text would break for anybody not playing in English.
//
// The wording is upstream's, as swix ruled on 2026-08-16. The only change is that
// the arrows are written as "then", because a voice cannot read "=>" and the pack
// bans it for that reason.
public static class UnendingCoil
{
    public const ushort Territory = 733;

    // How long a quote's call stays up. Upstream holds these for six seconds
    // rather than the usual four, because the line covers two or three mechanics
    // and you are still acting on it well after it is said.
    private const float Held = 6f;

    private static Trigger Quote(int number, uint yellId, string says) => new()
    {
        Id = $"ucob-nael-quote-{number}",
        On = EventKind.NpcYell,
        MatchId = yellId,
        Phase = 2,
        Make = ctx => new Call
        {
            Text = says,
            Time = ctx.Event.Time,
            Key = $"ucob-nael-quote-{number}",
            Level = CallLevel.Alert,
            Hold = Held,
        },
    };

    // Exaflares walk across the arena in a straight line. The cast's own heading is
    // the way they travel, so the far side is where they start and you cross behind
    // them. Upstream's own spoken form, which already has no arrow in it.
    private static Trigger Exaflares() => new()
    {
        Id = "ucob-exaflares",
        Says = "exaflares someone towards someone",
        On = EventKind.CastStart,
        MatchId = 0x26F0,
        Phase = 3,
        // One cast per flare and eight of them, so upstream stays quiet for twenty
        // seconds after the first. Without it this is the same line eight times.
        Hush = 20f,
        Make = ctx =>
        {
            if (!ctx.Event.Source.Known) return null;

            var towards = Compass.Facing8(ctx.Event.Source.Heading);
            var from = Compass.Opposite8(towards);

            return new Call
            {
                Text = $"exaflares {Compass.Name8(from)} towards {Compass.Name8(towards)}",
                Time = ctx.Event.Time,
                Key = "ucob-exaflares",
                Level = CallLevel.Info,
            };
        },
    };

    // The three tank calls this fight was missing.
    //
    // The pack was baked from a trigger set that carried these on the timeline rather
    // than on the cast, and only the cast ones were imported, so all three were
    // silent. Words, levels and the split between them are upstream's: a buster
    // names whoever it landed on, a cleave only tells the tanks to turn it away.
    private static Trigger Buster(string id, uint cast) => new()
    {
        Id = id,
        Says = "Tank Buster on YOU / Tank Buster on someone",
        On = EventKind.CastStart,
        MatchId = cast,
        Phase = 1,
        Make = ctx => new Call
        {
            Text = ctx.TargetIsMe ? "Tank Buster on YOU" : $"Tank Buster on {ctx.NameTarget()}",
            Time = ctx.Event.Time,
            Key = id,
            Level = ctx.TargetIsMe ? CallLevel.Alert : CallLevel.Info,
        },
    };

    private static Trigger Cleave(string id, uint cast, float hush = 0f) => new()
    {
        Id = id,
        Says = "Tank Cleave on YOU / Tank Cleave",
        On = EventKind.CastStart,
        MatchId = cast,
        Phase = 1,
        Hush = hush,
        Make = ctx => new Call
        {
            Text = ctx.TargetIsMe ? "Tank Cleave on YOU" : "Tank Cleave",
            Time = ctx.Event.Time,
            Key = id,
            Level = CallLevel.Info,
        },
    };

    public static IEnumerable<Trigger> Triggers()
    {
        foreach (var t in UnendingCoilTrio.Triggers()) yield return t;

        yield return Exaflares();

        yield return Buster("ucob-bahamuts-claw", 0x26B5);
        yield return Cleave("ucob-plummet", 0x26A8);
        yield return Cleave("ucob-flare-breath", 0x26D4);

        yield return Quote(1, 0x1961, "Spread then In");
        yield return Quote(2, 0x1960, "Spread then Out");
        yield return Quote(3, 0x195F, "Stack then In");
        yield return Quote(4, 0x195E, "Stack then Out");
        yield return Quote(5, 0x195D, "In then Stack");
        yield return Quote(6, 0x195C, "In then Out");
        yield return Quote(7, 0x1965, "Away from Tank then Stack");
        yield return Quote(8, 0x1964, "Spread then Away from Tank");
        yield return Quote(9, 0x1966, "Spread then In");
        yield return Quote(10, 0x1967, "In then Spread");
        yield return Quote(11, 0x196B, "In then Out then Spread");
        yield return Quote(12, 0x196A, "In then Spread then Stack");
        yield return Quote(13, 0x1968, "Out then Stack then Spread");
        yield return Quote(14, 0x1969, "Out then Spread then Stack");
    }

    // The quote ids, for anything that needs to know which yells this fight reads.
    // Off the yell triggers alone, so adding a call on any other kind cannot widen
    // what the chat listener watches for.
    public static IReadOnlySet<uint> QuoteIds { get; } =
        Triggers().Where(t => t.On == EventKind.NpcYell).Select(t => t.MatchId).ToHashSet();
}
