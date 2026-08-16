namespace FrenAlerts.Engine;

public static class LiveCoverage
{
    public static readonly IReadOnlyDictionary<EventKind, string> Emitted =
        new Dictionary<EventKind, string>
        {
            [EventKind.CastStart] = "LiveEvents, off the object table",
            [EventKind.StatusGain] = "LiveEvents, off the party's status lists",
            [EventKind.StatusLose] = "LiveEvents, off the party's status lists",
            [EventKind.Tether] = "TetherEvents, off the character's own VFX container",
            [EventKind.ActorControl] = "ControlEvents, off the maintained packet address",
            [EventKind.AbilityHit] = "AbilityEvents, off the maintained action effect address",
            [EventKind.MapEffect] = "MapEffectEvents, off the maintained map effect packet address",
            [EventKind.HeadMarker] = "ParserBridge, off a running parser's own channel",
            [EventKind.ZoneChange] = "LiveEvents, off the client's territory change",
            [EventKind.CombatStart] = "CombatEvents, off the client's own combat flag",
            [EventKind.CombatEnd] = "CombatEvents, off the client's own combat flag",
        };

    public static readonly IReadOnlyDictionary<EventKind, string> KnownGaps =
        new Dictionary<EventKind, string>();

    public static readonly IReadOnlyDictionary<EventKind, string> NeedsAParser =
        new Dictionary<EventKind, string>
        {
            [EventKind.HeadMarker] =
                "No maintained field, no maintained address, and the character's own " +
                "VFX span dead-ends at an unmapped type. The packet route needs an " +
                "opcode that moves every patch with no length field to bound a scan, " +
                "so every route inside the client ends in an invented number. A " +
                "parser already answers it, so these come from one when it is there.",
        };

    public static bool Covered(EventKind kind) => Emitted.ContainsKey(kind);

    public static bool CoveredAlone(EventKind kind) =>
        Emitted.ContainsKey(kind) && !NeedsAParser.ContainsKey(kind);

    // What to tell somebody who asks why a call never fires.
    public static string Explain(EventKind kind) =>
        NeedsAParser.TryGetValue(kind, out var needs) ? needs
        : Emitted.TryGetValue(kind, out var source) ? source
        : KnownGaps.TryGetValue(kind, out var why) ? why
        : "Nothing emits this and it is not a written-down gap.";
}
