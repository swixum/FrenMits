namespace FrenAlerts.Engine;

// Phase four: Neo Exdeath.
//
// Every debuff in this phase comes in a real and a lying version, and which one you
// are wearing is not in the debuff. It is in a number on the boss, so the whole
// phase reads off one status nobody in the party has.
public static partial class DancingMad
{
    private const uint Telling = 0x808;

    // The four numbers that hide it. Two of them mean the debuffs do what they say
    // and two mean they do the opposite.
    private static bool? TruthIn(ushort param) => param switch
    {
        0x460 or 0x462 => true,
        0x45F or 0x461 => false,
        _ => null,
    };

    private const uint Shriek = 0x15A7, Forked = 0x15A8, Compressed = 0x15A9, Bomb = 0x15AA;
    private const uint Entropy = 0x15AB, Fluid = 0x15AC;

    // What a debuff means once the boss's number is taken into account.
    private static string SpreadOrStack(bool real) => real ? "spread" : "stack";

    private static string BombWord(bool real) => real ? "stop everything" : "keep moving";

    private static string GazeWord(bool real) => real ? "look away" : "look at";

    private static IEnumerable<Trigger> PhaseFour()
    {
        // ---- the number that decides everything ----

        yield return Collect("neo-truth", EventKind.StatusGain, Telling, 4, ctx =>
        {
            if (TruthIn(ctx.Event.Param) is not { } real) return;
            var pull = Pull(ctx);
            // In the order they land, which is the order the mechanics resolve in.
            if (pull.Debuffs1 is null) pull.Debuffs1 = real;
            else if (pull.Debuffs2 is null) pull.Debuffs2 = real;
            else if (pull.Debuffs3 is null) pull.Debuffs3 = real;
            else if (pull.Debuffs4 is null) pull.Debuffs4 = real;
        });

        yield return Collect("neo-debuff-collect", EventKind.StatusGain, 0, 4, ctx =>
        {
            var pull = Pull(ctx);
            var who = ctx.Event.TargetId;
            var seconds = ctx.Event.Duration;

            switch (ctx.Event.Id)
            {
                case Shriek:
                    DancingMadPull.Note(seconds < 61f ? pull.ShortShriek : pull.LongShriek, who);
                    return;
                case Forked:
                    // The first pair of these is what says which half of the party
                    // is on the early set, and nothing else in the phase does.
                    pull.FirstDebuffShort ??= seconds < 52f;
                    DancingMadPull.Note(seconds < 52f ? pull.ShortForked : pull.LongForked, who);
                    return;
                case Compressed:
                    DancingMadPull.Note(
                        seconds < 52f ? pull.ShortCompressed : pull.LongCompressed, who);
                    return;
                case Bomb:
                    var into = seconds switch
                    {
                        < 37f => pull.SecondShortBomb,
                        < 52f => pull.FirstShortBomb,
                        < 62f => pull.SecondLongBomb,
                        _ => pull.FirstLongBomb,
                    };
                    DancingMadPull.Note(into, who);
                    return;
            }

            if (!ctx.TargetIsMe) return;
            switch (ctx.Event.Id)
            {
                case 0x1558 or 0x566: pull.DeathOrField = "death"; break;
                case 0x1C6: pull.DeathOrField = "field"; break;
                case 0x1317 or 0x15A5: pull.Wound = "purple"; break;
                case 0x1318 or 0x15A6: pull.Wound = "blue"; break;
            }
        });

        // The same status on the two bosses, told apart by the number rather than by
        // the name: Chaos wears the low pair and Neo Exdeath the high one, so this
        // needs no actor names and works in a replay that never carried them.
        yield return Collect("boss-truth", EventKind.StatusGain, Telling, 4, ctx =>
        {
            var pull = Pull(ctx);
            switch (ctx.Event.Param)
            {
                case 0x45F: pull.ChaosReal = false; break;
                case 0x460: pull.ChaosReal = true; break;
                case 0x461: pull.NeoReal = false; break;
                case 0x462: pull.NeoReal = true; break;
            }
        });

        yield return Collect("grand-cross-count", EventKind.CastStart, 0xBB14, 4,
            ctx => Pull(ctx).GrandCrosses++);

        yield return Collect("entropy-truth", EventKind.StatusGain, Entropy, 4, ctx =>
        {
            var pull = Pull(ctx);
            pull.EntropyReal = ctx.Event.Duration > 46f ? pull.Debuffs2 : pull.Debuffs4;
        });

        yield return Collect("fluid-truth", EventKind.StatusGain, Fluid, 4, ctx =>
        {
            var pull = Pull(ctx);
            pull.FluidReal = ctx.Event.Duration > 83f ? pull.Debuffs2 : pull.Debuffs4;
        });

        yield return Collect("mana-charge", EventKind.StatusGain, 0, 4, ctx =>
        {
            var pull = Pull(ctx);
            if (ctx.Event.Id == 0x5CD) pull.ThunderCharged = pull.ThunderReal;
            else if (ctx.Event.Id == 0x5CC) pull.BlizzardCharged = pull.IceReal;
        });

        // ---- what you are holding ----

        // Your own set, said the moment it lands, because everything after it is
        // spent doing the thing rather than working out what the thing is.
        yield return MySet("neo-first-set", first: true);
        yield return MySet("neo-third-set", first: false);

        // The pair that decides which laser to stand in. Both halves are yours and
        // neither is any use without the other.
        yield return new Trigger
        {
            Id = "neo-wound-and-field",
            Says = "purple debuff + death on you",
            On = EventKind.StatusGain,
            Phase = 4,
            OnlyMe = true,
            Make = ctx =>
            {
                if (ctx.Event.Id is not (0x1317 or 0x15A5 or 0x1318 or 0x15A6
                    or 0x566 or 0x1558 or 0x1C6)) return null;
                var pull = Pull(ctx);
                if (pull.Wound.Length == 0 || pull.DeathOrField.Length == 0) return null;
                return new Call
                {
                    Text = $"{pull.Wound} debuff + {pull.DeathOrField} on you",
                    Time = ctx.Event.Time,
                    Key = "neo-wound-and-field",
                    Level = CallLevel.Alert,
                    Personal = true,
                    Once = true,
                };
            },
        };

        // ---- Flood of Naught ----

        // Which side of the room to be on, worked out from the two debuffs you are
        // wearing and which way round the lasers came out.
        yield return Flood("flood-true-blue-right", 0xC392, real: true, blueLeft: false);
        yield return Flood("flood-true-blue-left", 0xC393, real: true, blueLeft: true);
        yield return Flood("flood-fake-blue-right", 0xC3A1, real: false, blueLeft: false);
        yield return Flood("flood-fake-blue-left", 0xC3A2, real: false, blueLeft: true);

        // What is left once the laser is stood in, which is the short half of the
        // party's own spread, stack or bomb.
        yield return new Trigger
        {
            Id = "neo-short-debuffs",
            Says = "spread + stop everything",
            On = EventKind.AbilityHit,
            MatchId = 0xC394,
            Phase = 4,
            // The pack carries this on both halves of the cast and can only say the
            // mechanic's name, so it was reading "Short Debuffs" over the top of the
            // line that says what to actually do.
            Owns = ["short-debuffs"],
            Make = ctx => ShortWork(ctx) is { Length: > 0 } text
                ? new Call
                {
                    Text = text,
                    Time = ctx.Event.Time,
                    Key = "neo-short-debuffs",
                    Level = CallLevel.Alarm,
                    Personal = true,
                }
                : null,
        };

        // ---- the gazes ----

        yield return Shrieking("neo-shriek-first", first: true);
        yield return Shrieking("neo-shriek-second", first: false);

        // The bomb, called on the tick it matters rather than when it landed.
        yield return new Trigger
        {
            Id = "acceleration-bomb-drop",
            Says = "stop everything",
            On = EventKind.StatusGain,
            MatchId = Bomb,
            Phase = 4,
            OnlyMe = true,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                var seconds = ctx.Event.Duration;
                var early = seconds > 75f || seconds is > 50f and < 52f;
                if ((early ? pull.Debuffs1 : pull.Debuffs3) is not { } real) return null;
                return new Call
                {
                    Text = BombWord(real),
                    Time = ctx.Event.Time + Math.Max(0f, seconds - 3f),
                    Key = "acceleration-bomb-drop",
                    Level = CallLevel.Alarm,
                    Personal = true,
                };
            },
        };

        // ---- Entropy and Dynamic Fluid ----

        yield return new Trigger
        {
            Id = "neo-entropy",
            Says = "bait puddles / stack for donuts",
            On = EventKind.StatusGain,
            MatchId = Shriek,
            Phase = 4,
            Make = ctx =>
            {
                if (ctx.Event.Duration >= 61f) return null;
                if (Pull(ctx).EntropyReal is not { } real) return null;
                return new Call
                {
                    Text = real ? "bait puddles" : "stack for donuts",
                    Time = ctx.Event.Time + ctx.Event.Duration,
                    Key = "neo-entropy",
                    Level = CallLevel.Alarm,
                };
            },
        };

        yield return new Trigger
        {
            Id = "neo-fluid",
            Says = "stack for donuts / bait puddles",
            On = EventKind.StatusGain,
            MatchId = Shriek,
            Phase = 4,
            Make = ctx =>
            {
                if (ctx.Event.Duration <= 68f) return null;
                if (Pull(ctx).FluidReal is not { } real) return null;
                return new Call
                {
                    Text = real ? "stack for donuts" : "bait puddles",
                    Time = ctx.Event.Time + ctx.Event.Duration,
                    Key = "neo-fluid",
                    Level = CallLevel.Alarm,
                };
            },
        };

        // ---- the tells that carry over ----

        yield return new Trigger
        {
            Id = "thrumming-thunder-tell",
            Says = "true lightning (lines) / fake lightning (lines)",
            On = EventKind.CastStart,
            MatchId = 0xC5DE,
            Phase = 4,
            Make = ctx => Pull(ctx).ThunderReal is { } real
                ? new Call
                {
                    Text = real ? "true lightning (lines)" : "fake lightning (lines)",
                    Time = Lands(ctx),
                    Key = "thrumming-thunder-tell",
                    Level = CallLevel.Alert,
                }
                : null,
        };

        yield return new Trigger
        {
            Id = "blizzard-blowout-tell",
            Says = "avoid cone / in cone",
            On = EventKind.CastStart,
            MatchId = 0xBA95,
            Phase = 4,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.GrandCrosses != 3 || pull.IceReal is not { } ice) return null;
                return new Call
                {
                    Text = ice ? "avoid cone" : "in cone",
                    Time = Lands(ctx),
                    Key = "blizzard-blowout-tell",
                    Level = CallLevel.Alert,
                };
            },
        };

        // Mana Release turns both charged tells inside out, so what was real in
        // phase one is the opposite of what is real here.
        yield return new Trigger
        {
            Id = "mana-release-tells",
            Says = "true ice (cones) / true lightning (lines) + in donut",
            On = EventKind.CastStart,
            MatchId = 0xBAA5,
            Phase = 4,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.IceReal is not { } ice || pull.ThunderReal is not { } thunder) return null;

                var lineReal = pull.ThunderCharged == thunder;
                var coneReal = pull.BlizzardCharged == ice;
                var tells = (coneReal, lineReal) switch
                {
                    (true, true) => "true ice (cones) / true lightning (lines)",
                    (false, true) => "fake ice (cones) / true lightning (lines)",
                    (true, false) => "true ice (cones) / fake lightning (lines)",
                    _ => "fake ice (cones) / fake lightning (lines)",
                };
                var donut = pull.FluidReal == true ? " + in donut" : "";
                return new Call
                {
                    Text = $"{tells}{donut}",
                    Time = Lands(ctx) + 0.3,
                    Key = "mana-release-tells",
                    Level = CallLevel.Alarm,
                };
            },
        };

        // ---- the shotcalls ----
        //
        // The whole raid's answer rather than your own, for whoever is calling it out
        // loud. They go on your own screen and nowhere else: nothing here sends a
        // message to anybody.

        yield return new Trigger
        {
            Id = "shotcall-gaze",
            Says = "first / second / out",
            On = EventKind.StatusGain,
            MatchId = Shriek,
            Phase = 4,
            Make = ctx =>
            {
                if (Pull(ctx).NeoReal is not { } real) return null;
                var which = ctx.Event.Duration < 65f ? "first" : "second";
                return new Call
                {
                    Text = $"gaze{(which == "first" ? 1 : 2)}: look {(real ? "out" : "inside")}",
                    Time = ctx.Event.Time + 1.0,
                    Key = $"shotcall-gaze-{which}",
                    Level = CallLevel.Info,
                    Hold = 8f,
                    Once = true,
                };
            },
        };

        yield return Shotcall("shotcall-fire", Entropy,
            real: "fire is aoe (dodge)", fake: "fire is dynamo (stay)");
        yield return Shotcall("shotcall-water", Fluid,
            real: "water is dynamo (stay)", fake: "water is aoe (dodge)");

        // Both tells at once for the caller, which is the same answer the personal
        // line gives but said as one thing the raid does.
        yield return new Trigger
        {
            Id = "shotcall-tells",
            Says = "ice real lightning fake",
            On = EventKind.CastStart,
            MatchId = 0xBA94,
            Phase = 4,
            Make = ctx =>
            {
                if (ctx.Phase != 4) return null;
                var pull = Pull(ctx);
                var known = (pull.IceReal is null ? 0 : 1)
                            + (pull.FireReal is null ? 0 : 1)
                            + (pull.ThunderReal is null ? 0 : 1);
                if (known < 2) return null;

                var said = Tell(pull.IceReal, "ice") + Tell(pull.ThunderReal, "lightning")
                           + Tell(pull.FireReal, "fire");
                return said.Length == 0 ? null : new Call
                {
                    Text = said.TrimEnd(),
                    Time = Lands(ctx),
                    Key = "shotcall-tells",
                    Level = CallLevel.Info,
                    Hold = 8f,
                };

                static string Tell(bool? real, string element) =>
                    real is null ? "" : $"{element} {(real.Value ? "real" : "fake")} ";
            },
        };

        yield return new Trigger
        {
            Id = "neo-enrage",
            On = EventKind.CastStart,
            MatchId = 0xBABB,
            Phase = 4,
            Make = ctx => new Call
            {
                Text = "enrage",
                Time = Lands(ctx),
                Key = "neo-enrage",
                Level = CallLevel.Alarm,
            },
        };
    }

    // What Chaos is actually doing this time, for whoever calls it out. Which of the
    // two elements lies is on the boss rather than on the debuff, so a group with
    // nobody watching the boss has no way to know.
    private static Trigger Shotcall(string id, uint status, string real, string fake) => new()
    {
        Id = id,
        Says = "fire is aoe (dodge)",
        On = EventKind.StatusGain,
        MatchId = status,
        Phase = 4,
        Make = ctx => Pull(ctx).ChaosReal is { } truth
            ? new Call
            {
                Text = truth ? real : fake,
                Time = ctx.Event.Time + 1.0,
                Key = id,
                Level = CallLevel.Info,
                Hold = 8f,
                Once = true,
            }
            : null,
    };

    // Your own half of a debuff set: what shape you are, when it goes, and whether
    // the bomb wants you still or moving.
    private static Trigger MySet(string id, bool first) => new()
    {
        Id = id,
        Says = "spread first / stack second",
        On = EventKind.StatusGain,
        Phase = 4,
        OnlyMe = true,
        Make = ctx =>
        {
            if (ctx.Event.Id is not (Shriek or Forked or Compressed or Bomb)) return null;
            var pull = Pull(ctx);
            if (!first && pull.GrandCrosses != 2) return null;

            var real = first ? pull.Debuffs1 : pull.Debuffs3;
            if (real is not { } truth || pull.FirstDebuffShort is not { } shortFirst) return null;

            var me = ctx.Player.MyId;
            var early = first == shortFirst;

            var gaze = (first ? pull.ShortShriek : pull.LongShriek).Contains(me);
            if (gaze)
                return Say($"{GazeWord(truth)} + {BombWord(truth)} on you {(first ? "first" : "second")}");

            var forked = (first == shortFirst ? pull.ShortForked : pull.LongForked).Contains(me);
            var squashed = (first == shortFirst ? pull.ShortCompressed : pull.LongCompressed)
                .Contains(me);
            var when = early ? "first" : "second";

            if ((forked && truth) || (squashed && !truth)) return Say($"spread {when}");
            if ((forked && !truth) || (squashed && truth)) return Say($"stack {when}");

            if ((first ? pull.FirstShortBomb : pull.SecondShortBomb).Contains(me))
                return Say($"{BombWord(truth)} on you first");
            if ((first ? pull.FirstLongBomb : pull.SecondLongBomb).Contains(me))
                return Say($"{BombWord(truth)} on you second");

            return Say($"no debuff, stack {when}");

            Call Say(string text) => new()
            {
                Text = text,
                Time = ctx.Event.Time,
                Key = id,
                Level = CallLevel.Alert,
                Personal = true,
                Once = true,
            };
        },
    };

    // Which colour to stand in and which side it is on.
    //
    // Death keeps your own colour when the lasers are telling the truth and swaps it
    // when they are not; the field is the other way round. Getting this backwards is
    // standing in the one thing that kills you.
    private static Trigger Flood(string id, uint action, bool real, bool blueLeft) => new()
    {
        Id = id,
        Says = "stand in purple (left)",
        On = EventKind.CastStart,
        MatchId = action,
        Phase = 4,
        Make = ctx =>
        {
            var pull = Pull(ctx);
            if (pull.Wound.Length == 0 || pull.DeathOrField.Length == 0) return null;

            var keep = (pull.DeathOrField == "death") == real;
            var colour = keep ? pull.Wound : pull.Wound == "purple" ? "blue" : "purple";
            // Blue on the left puts purple on the right, and whether you want blue
            // or purple has already been worked out above.
            var side = (colour == "blue") == blueLeft ? "left" : "right";

            var rest = ShortWork(ctx);
            var tail = rest.Length > 0 ? $" then {rest}" : "";

            return new Call
            {
                Text = $"stand in {colour} ({side}){tail}",
                Time = Lands(ctx),
                Key = "flood-of-naught",
                Level = CallLevel.Alarm,
                Personal = true,
            };
        },
    };

    // The short half of the party's own job once the laser is dealt with.
    private static string ShortWork(in TriggerContext ctx)
    {
        var pull = Pull(ctx);
        if (pull.Debuffs1 is not { } one || pull.Debuffs3 is not { } three) return "";
        if (pull.FirstDebuffShort is not { } shortFirst) return "";

        var truth = shortFirst ? one : three;
        var me = ctx.Player.MyId;

        var forked = pull.ShortForked.Contains(me);
        var squashed = pull.ShortCompressed.Contains(me);
        var firstBomb = pull.FirstShortBomb.Contains(me);
        var secondBomb = pull.SecondShortBomb.Contains(me);

        var spread = (forked && truth) || (squashed && !truth);
        var stack = (forked && !truth) || (squashed && truth);
        var bombTruth = firstBomb ? one : three;

        if (spread && (firstBomb || secondBomb)) return $"spread + {BombWord(bombTruth)}";
        if (stack && (firstBomb || secondBomb)) return $"stack + {BombWord(bombTruth)}";
        if (spread) return "spread";
        if (stack) return "stack";
        if (firstBomb || secondBomb) return $"{BombWord(bombTruth)} and stack";
        return "stack";
    }

    // Who to be looking away from, and whether looking away is even the answer.
    private static Trigger Shrieking(string id, bool first) => new()
    {
        Id = id,
        Says = "look away from Bob",
        On = EventKind.StatusGain,
        MatchId = Shriek,
        Phase = 4,
        Make = ctx =>
        {
            var seconds = ctx.Event.Duration;
            if (first ? seconds >= 61f : seconds <= 68f) return null;

            var pull = Pull(ctx);
            if ((first ? pull.Debuffs1 : pull.Debuffs3) is not { } real) return null;

            var who = first ? pull.ShortShriek : pull.LongShriek;
            if (who.Count == 0) return null;

            return new Call
            {
                Text = real ? $"look away from {Names(ctx, who)}" : $"face {Names(ctx, who)}",
                Time = ctx.Event.Time + Math.Max(0f, seconds - 3f),
                Key = "neo-shriek",
                Level = CallLevel.Alarm,
            };
        },
    };
}
