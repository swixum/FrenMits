namespace FrenAlerts.Engine;

public static class LiveCoverage
{
    public static readonly IReadOnlyDictionary<EventKind, string> Emitted =
        new Dictionary<EventKind, string>
        {
            [EventKind.CastStart] = "a parser's line when one is reading, otherwise LiveEvents off the object table",
            [EventKind.StatusGain] = "a parser's line when one is reading, otherwise LiveEvents off the party's status lists",
            [EventKind.StatusLose] = "a parser's line when one is reading, otherwise LiveEvents off the party's status lists",
            [EventKind.Tether] = "a parser's line when one is reading, otherwise ControlEvents off control category 35",
            [EventKind.AbilityHit] = "a parser's line when one is reading, otherwise AbilityEvents off the maintained action effect address",
            [EventKind.ActorSpawn] = "ArenaEvents, off the object table",
            [EventKind.ActorMoved] = "ArenaEvents, off the object table",
            [EventKind.HeadMarker] = "a parser's line when one is reading, otherwise ControlEvents off control category 34",
            [EventKind.ActorControl] = "ControlEvents, off the maintained packet address",
            [EventKind.MapEffect] = "MapEffectEvents, off the maintained map effect packet address",
            [EventKind.ZoneChange] = "LiveEvents, off the client's territory change",
            [EventKind.CombatStart] = "CombatEvents, off the client's own combat flag",
            [EventKind.CombatEnd] = "CombatEvents, off the client's own combat flag",
            [EventKind.NpcYell] = "YellEvents, matching the chat log against the client's own yell sheet",
            [EventKind.NameToggle] = "ArenaEvents, off each actor's own targetable flag on the frame",
        };

    public static readonly IReadOnlyDictionary<EventKind, string> KnownGaps =
        new Dictionary<EventKind, string>
        {
            [EventKind.ActorDespawn] =
                "Nothing on the frame reports an actor leaving: it is a hole in the " +
                "object table on some later poll, which cannot say when it went or " +
                "which of several adds it was. A parser writes the line, but its " +
                "spawns mean something else here than the arena's do, so taking one " +
                "half and not the other would be two meanings for one kind.",
        };

    // Nothing, now. Head markers were the last entry: the search for them went
    // through maintained fields, maintained addresses and the character's own VFX
    // span, all of which dead-end, and stopped there. It never went through the
    // control packet, which is where one actually arrives, on a hook this plugin has
    // had installed for the direction calls the whole time.
    //
    // Read as a claim about a bare install rather than about a parser: empty means
    // every kind the pack uses reaches the engine with nothing else running.
    public static readonly IReadOnlyDictionary<EventKind, string> NeedsAParser =
        new Dictionary<EventKind, string>();

    // Kinds whose client route is written and has not yet been watched working in a
    // duty. Not a gap and not a guess: two separate implementations of the same hook
    // agree on the category and on which argument holds what, and the tether
    // direction they use is the one a real log line writes. But agreeing decompiles
    // are still not a pull, and a route nobody has seen fire is worth saying out loud
    // rather than counting as done.
    public static readonly IReadOnlyDictionary<EventKind, string> UnprovenLive =
        new Dictionary<EventKind, string>
        {
            // Head markers and tethers used to be listed here. They came off after
            // reading the shipped plugin these calls were ported from: it hooks the
            // same function and splits the same two categories, with the marker id
            // in the first argument aimed at the actor, and the tether id in the
            // second with the far end in the third. Identical to this one, field for
            // field. That is not two decompiles agreeing on paper, it is a build
            // people raided Dancing Mad with.
            [EventKind.NpcYell] =
                "The line's row is read out of the client's own yell sheet and the " +
                "chat log is matched back to it, so no signature and no packet hook " +
                "is involved and it reads the same in any language. What is unproven " +
                "is the matching itself: whether the sheet's text and the chat log's " +
                "agree once punctuation is flattened. Watch Nael call fourteen " +
                "quotes in one Unending Coil pull, and check the status line says " +
                "fourteen lines known rather than none.",
        };

    // The kinds a reading parser answers better than the client can, so while one is
    // reading they come from it alone and the client's own reads stand down.
    //
    // One set rather than a check inside each source: two sources emitting the same
    // mechanic is not a redundancy, it is the call said twice.
    //
    // Statuses are the reason this exists. Polling reads them off the party and only
    // the party, ten times a second, so a status on the boss is invisible and a short
    // one lands between two polls. The line carries every status on every actor with
    // its real duration, which is the difference between a fight working and a fight
    // half working.
    public static readonly IReadOnlySet<EventKind> ParserOwned = new HashSet<EventKind>
    {
        EventKind.CastStart,
        EventKind.AbilityHit,
        EventKind.StatusGain,
        EventKind.StatusLose,
        EventKind.Tether,
        EventKind.HeadMarker,
    };

    // Kinds the client keeps whether a parser is there or not, and why, so the next
    // person to add one to the set above finds the reason it was left out.
    public static readonly IReadOnlyDictionary<EventKind, string> ClientKeeps =
        new Dictionary<EventKind, string>
        {
            [EventKind.ActorControl] =
                "No log line carries one, so the hook is the only source and there is " +
                "nothing to hand over.",
            [EventKind.MapEffect] =
                "Both sources have it, and the shipped calls were written against the " +
                "packet's own fields. Handing it over would be a field mapping proved " +
                "on four calls, so the hook keeps it until a real pull says the line " +
                "agrees.",
            [EventKind.ZoneChange] =
                "The client knows first and knows for certain, and this is what " +
                "rebuilds the fight, so it does not wait on a parser being up.",
            [EventKind.ActorSpawn] =
                "The arena reads these off the object table and puts the actor's " +
                "kind in Id. A line puts its max health there instead, and one kind " +
                "meaning two things is a trigger that matches whichever source " +
                "happened to be up.",
            [EventKind.ActorMoved] =
                "Read off the object table on the frame. No line reports an actor " +
                "that has simply moved.",
            [EventKind.NameToggle] =
                "Read off the object table on the frame, the same poll the arena's " +
                "spawns and moves come from. A parser writes a line for it, but the " +
                "position that rides along on this one is what the call needs and " +
                "the line does not carry it.",
            [EventKind.NpcYell] =
                "The chat log is the client's own and is there whether a parser is " +
                "reading or not, so there is nobody to hand this over to. A parser " +
                "does write a line for it, but by row id against its own table " +
                "rather than the client's, and the two would not be the same number.",
            [EventKind.CombatStart] =
                "Read off the client's own combat flag, which no line reports.",
            [EventKind.CombatEnd] =
                "Read off the client's own combat flag, which no line reports.",
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
