namespace FrenAlerts.Engine;

// Phase one: the statues.
//
// Nearly everything here is a pair of halves that only mean something together. An
// element's tell says whether it is real; a head marker says what you do about it;
// neither is a call on its own. The statues are worse: five of them do five
// different things and the only thing that tells them apart is where they stand.
public static partial class DancingMad
{
    // Which statue a tether runs to, read off where it is standing. The bands come
    // from the source and are checked in this order, because they overlap: the
    // gravitas band sits inside the indulgent one.
    private static string StatueAt(float x) => x switch
    {
        > 99f and < 101f => "pulse",
        > 101f and < 103f => "gravitas",
        > 125f => "vitrophyre",
        < 100f => "indulgent",
        > 106f and < 108f => "idyllic",
        _ => "",
    };

    // Which end of the tether is not a player.
    //
    // Both ends are tried rather than one, because which end a tether is recorded
    // from is a property of how it was read, not of the fight: read off the statue
    // the player is the target, read off the player the statue is. Guessing wrong
    // loses every statue call in the phase and looks exactly like a quiet fight.
    private static Position StatueEnd(in TriggerContext ctx)
    {
        var e = ctx.Event;
        if (!ActorId.IsPlayer(e.SourceId) && ctx.Actors.Where(e.SourceId) is { Known: true } from)
            return from;
        if (!ActorId.IsPlayer(e.TargetId) && ctx.Actors.Where(e.TargetId) is { Known: true } to)
            return to;
        return Position.None;
    }

    private static bool TetherTouchesMe(in TriggerContext ctx) =>
        ctx.TargetIsMe || ctx.SourceIsMe;

    private const uint ImageTether = 0x002D;

    // The statue set's own control packet. One arrives per statue, and the first
    // one's id is what every other statue in the set is counted back from.
    private const uint StatueControl = 0x19D;
    private const uint StatueOn = 0x40, StatueLit = 0x80;

    private static bool IsStatueLighting(in TriggerContext ctx) =>
        ctx.Event.Arg1 == StatueOn && ctx.Event.Arg2 == StatueLit;

    // Which statue is which, counted back from the first id in the set. The offsets
    // are the source's own and there is nothing else in the packet that says.
    private static bool IsBlue(uint b, uint id) => id == b || id == b - 1;
    private static bool IsPurple(uint b, uint id) => id == b - 2 || id == b - 4;
    private static bool IsYellow(uint b, uint id) => id == b - 3 || id == b - 5;
    private static bool IsEye(uint b, uint id) => id == b - 7 || id == b - 9;
    private static bool IsFakeEye(uint b, uint id) => id == b - 6 || id == b - 8;

    // Both arrows in one line, in whichever language the group reads them in.
    //
    // The pairs are the source's own tables. Only twelve of the sixteen exist:
    // two opposite arrows cancel and never appear together, so those four fall
    // back to saying the arrows themselves.
    private static readonly Dictionary<string, (string First, string Second)> PortentClock = new()
    {
        ["upup"] = ("west", "south"),
        ["downdown"] = ("east", "north"),
        ["rightright"] = ("north", "west"),
        ["leftleft"] = ("south", "east"),
        ["downleft"] = ("east southeast", "south"),
        ["downright"] = ("northeast", "west"),
        ["rightup"] = ("northwest", "south"),
        ["rightdown"] = ("north northeast", "east"),
        ["leftup"] = ("south southwest", "west"),
        ["leftdown"] = ("southeast", "north"),
        ["upright"] = ("west northwest", "north"),
        ["upleft"] = ("southwest", "east"),
    };

    private static readonly Dictionary<string, (string First, string Second)> PortentStatic = new()
    {
        ["upup"] = ("southeast out", "north"),
        ["downdown"] = ("northwest out", "south"),
        ["rightright"] = ("southwest out", "east"),
        ["leftleft"] = ("northeast out", "west"),
        ["downleft"] = ("west southwest", "east"),
        ["downright"] = ("southeast in", "south"),
        ["rightup"] = ("northeast in", "east"),
        ["rightdown"] = ("south southeast", "north"),
        ["leftup"] = ("north northwest", "south"),
        ["leftdown"] = ("southwest in", "west"),
        ["upright"] = ("east northeast", "west"),
        ["upleft"] = ("northwest in", "north"),
    };

    // What the portent pair comes out as, given what the group reads them as.
    //
    // The arrows themselves are the fallback rather than the exception: a group
    // that has picked nothing hears what it was given, which is always right and
    // never a spot.
    public static string PortentCall(string strat, string first, string second)
    {
        var table = strat switch
        {
            "clockwise" => PortentClock,
            "filipino" => PortentStatic,
            _ => null,
        };

        if (table is not null && table.TryGetValue(first + second, out var spots))
            return $"{spots.First} then {spots.Second}";

        return $"{first} then {second}";
    }

    private static IEnumerable<Trigger> PhaseOne()
    {
        // ---- what has to be written down before anything can be said ----

        yield return Collect("statue-count", EventKind.CastStart, 0xBCF2, 1,
            ctx => Pull(ctx).Statues++);

        yield return Collect("statue-tether-mine", EventKind.Tether, ImageTether, 1, ctx =>
        {
            if (!TetherTouchesMe(ctx)) return;
            var at = StatueEnd(ctx);
            if (!at.Known) return;
            Pull(ctx).MyTether = StatueAt(at.X);
        });

        yield return Collect("statue-set", EventKind.ActorControl, StatueControl, 1, ctx =>
        {
            if (!IsStatueLighting(ctx)) return;
            var pull = Pull(ctx);
            // The first of the set only. Later ones are counted back from it, and
            // letting a second one reset the base renumbers every statue.
            if (pull.StatueBase == 0) pull.StatueBase = ctx.Event.SourceId;
            if (IsEye(pull.StatueBase, ctx.Event.SourceId)) pull.LookAway = true;
            else if (IsFakeEye(pull.StatueBase, ctx.Event.SourceId)) pull.LookAway = false;
        });

        yield return Collect("wave-cannon-hit", EventKind.AbilityHit, 0xBAA8, 1,
            ctx => DancingMadPull.Note(Pull(ctx).WaveCannoned, ctx.Event.TargetId));

        yield return Collect("trap-on", EventKind.StatusGain, 0x13D6, 1,
            ctx => DancingMadPull.Note(Pull(ctx).Trapped, ctx.Event.TargetId));

        yield return Collect("element-real", EventKind.HeadMarker, 0, 1, ctx =>
        {
            var pull = Pull(ctx);
            switch (ctx.Event.Id)
            {
                case TrueFire: pull.FireReal = true; break;
                case FakeFire: pull.FireReal = false; break;
                case TrueIce: pull.IceReal = true; break;
                case FakeIce: pull.IceReal = false; break;
                case TrueThunder: pull.ThunderReal = true; break;
                case FakeThunder: pull.ThunderReal = false; break;
                case Dorito or StackMark:
                    if (ctx.TargetIsMe) pull.MyMark = ctx.Event.Id;
                    break;
            }
        });

        // ---- the statues ----

        // The lit statue says which side of the room is about to go, and the call is
        // the side that is left.
        yield return new Trigger
        {
            Id = "intemperate-will",
            Says = "get left (west)",
            On = EventKind.ActorControl,
            MatchId = StatueControl,
            Phase = 1,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (!IsStatueLighting(ctx) || !pull.StatuesKnown) return null;
                if (!IsYellow(pull.StatueBase, ctx.Event.SourceId)) return null;
                return new Call
                {
                    Text = "get left (west)",
                    Time = ctx.Event.Time,
                    Key = "half-room-cleave",
                    Level = CallLevel.Alarm,
                };
            },
        };

        yield return new Trigger
        {
            Id = "gravitational-wave",
            Says = "get right (east)",
            On = EventKind.ActorControl,
            MatchId = StatueControl,
            Phase = 1,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (!IsStatueLighting(ctx) || !pull.StatuesKnown) return null;
                if (!IsPurple(pull.StatueBase, ctx.Event.SourceId)) return null;
                return new Call
                {
                    Text = "get right (east)",
                    Time = ctx.Event.Time,
                    Key = "half-room-cleave",
                    Level = CallLevel.Alarm,
                };
            },
        };

        // The blue statues are the line spread, which is the same call every time
        // and worth saying once rather than twice a statue.
        yield return new Trigger
        {
            Id = "wave-cannon-spread",
            Says = "east west spread",
            On = EventKind.ActorControl,
            MatchId = StatueControl,
            Phase = 1,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (!IsStatueLighting(ctx) || !pull.StatuesKnown) return null;
                if (!IsBlue(pull.StatueBase, ctx.Event.SourceId)) return null;
                return new Call
                {
                    Text = "east west spread",
                    Time = ctx.Event.Time,
                    Key = "wave-cannon-spread",
                    Level = CallLevel.Alert,
                    Once = true,
                };
            },
        };

        // Which way to be looking, said while there is still time to turn round.
        yield return new Trigger
        {
            Id = "statue-gaze-early",
            Says = "look away from statue later / look at statue later",
            On = EventKind.ActorControl,
            MatchId = StatueControl,
            Phase = 1,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (!IsStatueLighting(ctx) || !pull.StatuesKnown) return null;
                var id = ctx.Event.SourceId;
                if (!IsEye(pull.StatueBase, id) && !IsFakeEye(pull.StatueBase, id)) return null;
                return new Call
                {
                    Text = IsEye(pull.StatueBase, id)
                        ? "look away from statue later"
                        : "look at statue later",
                    Time = ctx.Event.Time,
                    Key = "statue-gaze-early",
                    Level = CallLevel.Info,
                };
            },
        };

        // ---- the tethers ----

        yield return new Trigger
        {
            Id = "pulse-wave-tether",
            Says = "knockback on you / tether on you",
            On = EventKind.Tether,
            MatchId = ImageTether,
            Phase = 1,
            Make = ctx =>
            {
                if (!TetherTouchesMe(ctx)) return null;
                if (Pull(ctx).Statues != 1) return null;
                var at = StatueEnd(ctx);
                return new Call
                {
                    Text = at.Known && StatueAt(at.X) == "pulse" ? "knockback on you" : "tether on you",
                    Time = ctx.Event.Time,
                    Key = "statue-tether",
                    Level = CallLevel.Alert,
                    Personal = true,
                };
            },
        };

        // The third set is the one that puts a status on you rather than a puddle,
        // and confuse and sleep are answered differently.
        yield return new Trigger
        {
            Id = "will-tether",
            Says = "confuse tether on you / sleep tether on you / tether on you",
            On = EventKind.Tether,
            MatchId = ImageTether,
            Phase = 1,
            Make = ctx =>
            {
                if (!TetherTouchesMe(ctx)) return null;
                if (Pull(ctx).Statues != 3) return null;
                var at = StatueEnd(ctx);
                var which = at.Known ? StatueAt(at.X) : "";
                var text = which switch
                {
                    "indulgent" => "confuse tether on you",
                    "idyllic" => "sleep tether on you",
                    _ => "tether on you",
                };
                return new Call
                {
                    Text = text,
                    Time = ctx.Event.Time,
                    Key = "statue-tether",
                    Level = CallLevel.Alarm,
                    Personal = true,
                };
            },
        };

        // ---- Wave Cannon ----

        // The people the line already went through are the ones who cannot take a
        // tower, so the call is the opposite thing for each half of the party.
        yield return new Trigger
        {
            Id = "wave-cannon-towers",
            On = EventKind.AbilityHit,
            MatchId = 0xBAA8,
            Phase = 1,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                var missed = !pull.WaveCannoned.Contains(ctx.Player.MyId);
                var text = missed
                    ? "get towers"
                    : pull.WaveCannoned.Count > 4 ? "extra tower" : "avoid towers";
                return new Call
                {
                    Text = text,
                    Time = ctx.Event.Time,
                    Key = "wave-cannon-towers",
                    Level = missed ? CallLevel.Alert : CallLevel.Alert,
                    Personal = true,
                };
            },
        };

        // ---- Double-trouble Trap ----

        // Who you are about to be shoved by. The seconds on the debuff say which of
        // the three sets this is, and the third one lands with a status on top.
        yield return new Trigger
        {
            Id = "trap-knockback",
            Says = "knockback from Bob then confuse",
            On = EventKind.StatusGain,
            MatchId = 0x13D6,
            Phase = 1,
            OnlyMe = true,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.Trapped.Count == 0) return null;
                var who = Names(ctx, pull.Trapped);
                var seconds = ctx.Event.Duration;

                var tail = seconds switch
                {
                    < 6f => "",
                    > 67f => "",
                    _ => pull.MyTether switch
                    {
                        "idyllic" => " then sleep",
                        "indulgent" => " then confuse",
                        _ => " then debuffs",
                    },
                };

                return new Call
                {
                    Text = $"knockback from {who}{tail}",
                    Time = ctx.Event.Time + Math.Max(0f, seconds - 3.9f),
                    Key = "trap-knockback",
                    Level = CallLevel.Alarm,
                    Personal = true,
                };
            },
        };

        // ---- Mystery Magic ----

        // Two tells at once, which is the whole call: one of them is a thing to
        // dodge and the other is the only safe place to be standing.
        yield return new Trigger
        {
            Id = "mystery-ice-and-thunder",
            Says = "avoid both / cone only / line only",
            On = EventKind.CastStart,
            MatchId = 0xBA94,
            Phase = 1,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.IceReal is not { } ice || pull.ThunderReal is not { } thunder) return null;
                var text = (ice, thunder) switch
                {
                    (true, true) => "avoid both",
                    (false, true) => "cone only",
                    (true, false) => "line only",
                    _ => "cone + line",
                };
                return new Call
                {
                    Text = text,
                    Time = Lands(ctx),
                    Key = "mystery-magic-pair",
                    Level = CallLevel.Alarm,
                };
            },
        };

        yield return new Trigger
        {
            Id = "mystery-ice-and-fire",
            Says = "avoid tell / in cone",
            On = EventKind.CastStart,
            MatchId = 0xBA94,
            Phase = 1,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (ctx.Phase is 4 or 5) return null;
                if (pull.IceReal is not { } ice || pull.FireReal is not { } fire) return null;
                if (MyMystery(pull, fire) is not { } mine) return null;
                return new Call
                {
                    Text = $"{mine} + {(ice ? "avoid tell" : "in cone")}",
                    Time = Lands(ctx),
                    Key = "mystery-magic-pair",
                    Level = CallLevel.Alarm,
                    Personal = true,
                };
            },
        };

        yield return new Trigger
        {
            Id = "mystery-fire-and-thunder",
            Says = "stack + in line + look away from statue",
            On = EventKind.CastStart,
            MatchId = 0xBA94,
            Phase = 1,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (ctx.Phase is 4 or 5) return null;
                if (pull.ThunderReal is not { } thunder || pull.FireReal is not { } fire) return null;
                if (MyMystery(pull, fire) is not { } mine) return null;
                var look = pull.LookAway switch
                {
                    true => " + look away from statue",
                    false => " + look at statue",
                    _ => "",
                };
                return new Call
                {
                    Text = $"{mine} + {(thunder ? "avoid tell" : "in line")}{look}",
                    Time = Lands(ctx),
                    Key = "mystery-magic-pair",
                    Level = CallLevel.Alarm,
                    Personal = true,
                };
            },
        };

        // The ice tell with a puddle to drop, where the tether you are holding
        // decides whether the puddle goes in the middle or out with everyone else.
        yield return new Trigger
        {
            Id = "mystery-ice-and-puddle",
            Says = "aoe on you / get middle / avoid tell",
            On = EventKind.CastStart,
            MatchId = 0xBA95,
            Phase = 1,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.IceReal is not { } ice) return null;
                if (pull.FireReal is not null || pull.ThunderReal is not null) return null;
                var after = pull.MyTether == "vitrophyre" ? "aoe on you" : "get middle";
                return new Call
                {
                    Text = $"{(ice ? "avoid tell" : "in cone")} + bait puddle then {after}",
                    Time = Lands(ctx),
                    Key = "mystery-ice-puddle",
                    Level = CallLevel.Alert,
                    Personal = true,
                };
            },
        };

        // Vitrophyre drops where you stand, so the tethered players spread and
        // everybody else stays away from them.
        yield return new Trigger
        {
            Id = "vitrophyre-spread",
            On = EventKind.AbilityHit,
            MatchId = 0xBAAC,
            Phase = 1,
            Make = ctx => new Call
            {
                Text = Pull(ctx).MyTether == "vitrophyre"
                    ? "spread (avoid puddles)"
                    : "avoid tethered players",
                Time = ctx.Event.Time,
                Key = "vitrophyre",
                Level = CallLevel.Alert,
            },
        };
    }

    // Spread or stack, worked out from the marker you got and whether fire is real.
    //
    // The dorito means spread when fire is real and stack when it is fake, and the
    // stack marker is the other way round, which comes out as an exclusive or.
    private static string? MyMystery(DancingMadPull pull, bool fireReal) => pull.MyMark switch
    {
        Dorito => fireReal ? "aoe on you" : "stack",
        StackMark => fireReal ? "stack" : "aoe on you",
        _ => null,
    };

    // A list of players as a call reads it: you first as "you", everyone else by
    // whichever of name and slot the player asked for.
    private static string Names(in TriggerContext ctx, List<uint> who)
    {
        if (who.Count == 0) return "nobody";
        var parts = new List<string>(who.Count);
        foreach (var id in who)
            parts.Add(id == ctx.Player.MyId ? "you" : ctx.Describe(id));
        // No commas anywhere in a call: two names read as "and", more than two and
        // the names stop being the point.
        return parts.Count switch
        {
            1 => parts[0],
            2 => $"{parts[0]} and {parts[1]}",
            _ => $"{parts.Count} players",
        };
    }
}
