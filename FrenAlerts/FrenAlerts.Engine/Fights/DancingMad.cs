namespace FrenAlerts.Engine;

// The fight is split a file per phase, because it is five fights in a trenchcoat and
// one file of it ran to a thousand lines before the direction calls went in at all.
public static partial class DancingMad
{
    public const ushort Territory = 1363;

    // Every strat setting this fight offers, named once so a typo in a key reads as
    // a build error rather than as a call that quietly never fires.
    public const string PortentStrat = "teleportent";
    public const string ForsakenStrat = "forsaken";
    public const string AgonyStrat = "boa";
    public const string AccretionStrat = "accretion";
    public const string HoleStrat = "blackHole";
    public const string HoleTetherStrat = "blackHoleTether";

    // Shorthand for the state this pull has built up. Kept on the fight state so it
    // dies with the pull rather than following it into the next one.
    private static DancingMadPull Pull(in TriggerContext ctx) =>
        ctx.State.Remember<DancingMadPull>();

    // A trigger that only writes something down. It says nothing, so it is a claim:
    // that keeps it off the fight page, where a row with no words shows its own id
    // where the call should be.
    private static Trigger Collect(
        string id, EventKind on, uint match, int phase, Action<TriggerContext> note) => new()
    {
        Id = id,
        On = on,
        MatchId = match,
        Phase = phase,
        Claims = true,
        // Collectors want every event of the burst, not just the first: eight
        // players getting one debuff is eight things to write down.
        OncePerBurst = false,
        Make = ctx =>
        {
            note(ctx);
            return null;
        },
    };

    // How this fight says a spot out loud, in one place so every phase agrees.
    private static string Way(int dir8) => Compass.Name8(dir8);

    private static string Way4(int dir4) => Compass.Name4(dir4);

    private static string Way16(int dir16) => Compass.Name16(dir16);

    private static readonly HashSet<uint> OwnedMarkers =
        [0xA1, 0x150, 0x151, 0x152, 0x153, 0x1B5, 0x1B6, 0x1B7, 0x1B8];

    private static readonly (uint Id, int Number)[] LimitCut =
    [
        (0x150, 1), (0x151, 2), (0x152, 3), (0x153, 4),
        (0x1B5, 5), (0x1B6, 6), (0x1B7, 7), (0x1B8, 8),
    ];

    private static IEnumerable<Trigger> Numbered()
    {
        foreach (var (id, number) in LimitCut)
        {
            var colour = number % 2 == 1 ? "blue" : "red";
            yield return new Trigger
            {
                Id = $"limit-cut-{number}",
                On = EventKind.HeadMarker,
                MatchId = id,
                Phase = 3,
                OnlyMe = true,
                Make = ctx => new Call
                {
                    Text = $"{colour} {number}",
                    Time = ctx.Event.Time,
                    // Its own key per number: sharing one, the first said would
                    // suppress the rest of the pull's as repeats of itself.
                    Key = $"limit-cut-{number}",
                    Level = CallLevel.Alarm,
                    Personal = true,
                },
            };
        }
    }

    // What actually moves the fight on, one cast each.
    //
    // The alternative is inferring it from a "P3" in a call's name, which gives about
    // a hundred ids any one of which can shove the fight forward early. That has
    // already gone wrong once here: a Warrior's Bloodbath shares an id with the phase
    // 3 tether and pinned the fight at phase 3 five minutes before it got there.
    public static IEnumerable<(EventKind Kind, uint Id, int Phase)> PhaseChanges()
    {
        yield return (EventKind.CastStart, 0xC24C, 2);   // Ultimate Embrace
        yield return (EventKind.CastStart, 0xC3F7, 3);   // Aero III Assault
        yield return (EventKind.CastStart, 0xC2DC, 4);   // Kefka Says
        yield return (EventKind.CastStart, 0xBB40, 5);   // Ultima Repeater
    }

    // When the cast lands, which is what the countdown on screen counts down to.
    // The voice is not held back by this; it speaks the moment the call is made.
    private static double Lands(in TriggerContext ctx) => ctx.Event.Time + ctx.Event.CastTime;

    // A call says what to do, never what the boss is doing: the mechanic's name is
    // something you already know by the time you are hearing this.
    private static Trigger Raidwide(string id, uint action, int phase) => new()
    {
        Id = id,
        On = EventKind.CastStart,
        MatchId = action,
        Phase = phase,
        Make = ctx => new Call
        {
            // Everyone needs to know it is coming; what they do about it differs,
            // and for a raidwide that difference is not a strat choice.
            Text = Audience.RoleOf(ctx.MySlot) switch
            {
                "healer" => "raidwide heal",
                "tank" => "raidwide mit",
                _ => "raidwide",
            },
            Time = Lands(ctx),
            Key = id,
            Level = CallLevel.Alert,
        },
    };

    private static Trigger Buster(string id, uint action, int phase) => new()
    {
        Id = id,
        On = EventKind.CastStart,
        MatchId = action,
        Phase = phase,
        Make = ctx => new Call
        {
            Text = ctx.TargetIsMe ? "buster on you" : $"buster on {ctx.NameTarget()}",
            Time = Lands(ctx),
            Key = id,
            Level = CallLevel.Alarm,
            Personal = ctx.TargetIsMe,
        },
    };

    // The seconds come off the status line, never from a literal: these debuffs
    // arrive with several durations and the number is the whole call.
    private static Trigger Debuff(string id, uint status, string what, int phase) => new()
    {
        Id = id,
        On = EventKind.StatusGain,
        MatchId = status,
        Phase = phase,
        OnlyMe = true,
        Make = ctx => new Call
        {
            Text = ctx.Event.Duration > 0
                ? $"{what} {ctx.Event.Duration:0.#}s"
                : what,
            Time = ctx.Event.Time,
            Key = id,
            Level = CallLevel.Alert,
            Personal = true,
        },
    };

    private static Trigger Cast(string id, uint action, string text, int phase) => new()
    {
        Id = id,
        On = EventKind.CastStart,
        MatchId = action,
        Phase = phase,
        Make = ctx => new Call
        {
            Text = text,
            Time = Lands(ctx),
            Key = id,
            Level = CallLevel.Alert,
        },
    };

    // Fires on the landing rather than the cast, so there is nothing to count to.
    //
    // The key is separate from the id because one mechanic can arrive as several
    // ids at once. Four towers landing together under four keys are four calls
    // racing each other, and the crowding rule throws three of them away; under one
    // key they are the same call said once, which is what it is.
    private static Trigger Hit(string id, uint action, string text, int phase, string key = "") => new()
    {
        Id = id,
        On = EventKind.AbilityHit,
        MatchId = action,
        Phase = phase,
        Make = ctx => new Call
        {
            Text = text,
            Time = ctx.Event.Time,
            Key = key.Length > 0 ? key : id,
            Level = CallLevel.Alert,
        },
    };

    // Your place in the Accretion order, which is the only part of it you act on.
    private static IEnumerable<Trigger> InLine()
    {
        foreach (var (status, word) in new (uint, string)[]
                     { (0xBBC, "first"), (0xBBD, "second"), (0xBBE, "third") })
        {
            yield return new Trigger
            {
                Id = $"in-line-{word}",
                On = EventKind.StatusGain,
                MatchId = status,
                Phase = 3,
                OnlyMe = true,
                Make = ctx => new Call
                {
                    Text = word switch { "first" => "#1", "second" => "#2", _ => "#3" },
                    Time = ctx.Event.Time,
                    Key = $"in-line-{word}",
                    Level = CallLevel.Alert,
                    Personal = true,
                },
            };
        }
    }

    // A status that says which one is yours, with no timer worth reading out.
    private static Trigger Element(string id, uint status, string text, int phase) => new()
    {
        Id = id,
        On = EventKind.StatusGain,
        MatchId = status,
        Phase = phase,
        OnlyMe = true,
        Make = ctx => new Call
        {
            Text = text,
            Time = ctx.Event.Time,
            Key = id,
            Level = CallLevel.Alert,
            Personal = true,
        },
    };

    // Which way your portent points. The direction is fixed per status id, so this
    // needs no geometry at all: 130C-130F are the first one, 13D7-13DA the second.
    private static readonly (uint First, uint Second, string Way)[] Portents =
    [
        (0x130C, 0x13D7, "up"),
        (0x130D, 0x13D8, "down"),
        (0x130E, 0x13D9, "right"),
        (0x130F, 0x13DA, "left"),
    ];

    // Both portents in one line, because the pair is what you act on and hearing
    // "up" then "right" four seconds apart makes you do the arithmetic yourself.
    //
    // Armed on your own first portent only. Without that it arms on whoever in the
    // party got theirs first and pairs it with yours, which is eight chances to say
    // the wrong pair.
    public static IEnumerable<SequenceTrigger> Sequences()
    {
        foreach (var (first, _, firstWay) in Portents)
        {
            foreach (var (_, second, secondWay) in Portents)
            {
                yield return new SequenceTrigger
                {
                    Id = $"portent-{firstWay}-{secondWay}",
                    StartOn = EventKind.StatusGain,
                    StartId = first,
                    StartOnMe = true,
                    ThenOn = EventKind.StatusGain,
                    ThenId = second,
                    ThenOnMe = true,
                    Phase = 1,
                    Within = 20.0,
                    // The arrows themselves unless the group reads them as spots,
                    // in which case the pair is a place to stand and then a place
                    // to end up. Both tables are the source's own.
                    Make = ctx => new Call
                    {
                        Text = PortentCall(ctx.Strat(PortentStrat), firstWay, secondWay),
                        Time = ctx.Event.Time,
                        Key = "portents",
                        Level = CallLevel.Alarm,
                        Personal = true,
                    },
                };
            }
        }
    }

    // Everything that needs two events in order, in one list.
    public static IEnumerable<SequenceTrigger> AllSequences() =>
        Sequences().Concat(MysteryMagic());

    // The statuses the sequence is built from, claimed so the pack's own bare
    // "Tele-Portents" rows do not fire alongside it.
    private static IEnumerable<Trigger> TelePortents()
    {
        foreach (var (first, second, _) in Portents)
        {
            yield return Silent($"portent-1-{first:X}", first, 1);
            yield return Silent($"portent-2-{second:X}", second, 1);
        }
    }

    private static Trigger Claim(string id, EventKind on, uint match, int phase) => new()
    {
        Id = id,
        On = on,
        MatchId = match,
        Phase = phase,
        Claims = true,
        Make = _ => null,
    };

    // Holds an event so the pack does not answer it, and says nothing itself.
    private static Trigger Silent(string id, uint status, int phase) => new()
    {
        Id = id,
        On = EventKind.StatusGain,
        MatchId = status,
        Phase = phase,
        Claims = true,
        Make = _ => null,
    };

    // The Neo Exdeath debuff you are holding, and whether it is the real one.
    //
    // The game gives the fake a separate status id from the real one, so this needs
    // no head marker and no guessing: 1317 is White Wound (Fake) against 15A5 the
    // real one, and the same pairing runs through the other two. Lumping the pair
    // under one word throws away the only thing you act on.
    private static readonly (uint Status, string Word)[] Wounds =
    [
        (0x15A5, "purple"), (0x1317, "fake purple"),
        (0x15A6, "blue"), (0x1318, "fake blue"),
        (0x566, "death"), (0x1558, "fake death"),
        (0x1C6, "field"),
    ];

    private static IEnumerable<Trigger> NeoDebuffs()
    {
        foreach (var (status, word) in Wounds)
            yield return Element($"neo-debuff-{status:X}", status, word, 4);
    }

    // Mystery Magic. The element markers land on the field and say which tell is
    // real; the dorito or stack marker lands on you. Both are head markers, so this
    // is the part that needs a parser running.
    //
    // 007F is "spread if the element is real, stack if it is fake" and 0080 is the
    // other way round, which comes out as an exclusive or.
    private const uint FakeFire = 0x02A1, TrueFire = 0x02A2;
    private const uint FakeIce = 0x02A3, TrueIce = 0x02A4;
    private const uint FakeThunder = 0x02A5, TrueThunder = 0x02A6;
    private const uint Dorito = 0x007F, StackMark = 0x0080;

    private static IEnumerable<SequenceTrigger> MysteryMagic()
    {
        foreach (var (element, real) in new (uint, bool)[] { (TrueFire, true), (FakeFire, false) })
        {
            foreach (var (mark, dorito) in new (uint, bool)[] { (Dorito, true), (StackMark, false) })
            {
                var spread = real == dorito;
                yield return new SequenceTrigger
                {
                    Id = $"mystery-magic-{element:X}-{mark:X}",
                    StartOn = EventKind.HeadMarker,
                    StartId = element,
                    ThenOn = EventKind.HeadMarker,
                    ThenId = mark,
                    ThenOnMe = true,
                    Within = 5.0,
                    Phase = 1,
                    Make = ctx => new Call
                    {
                        Text = spread ? "aoe on you" : "stack",
                        Time = ctx.Event.Time,
                        Key = "mystery-magic",
                        Level = CallLevel.Alarm,
                        Personal = true,
                    },
                };
            }
        }
    }

    // The tell that goes with it, which lands on the field rather than on a player:
    // a real one is a thing to dodge, a fake one is the safe place to stand.
    private static IEnumerable<Trigger> Tells()
    {
        yield return Tell("tell-ice-real", TrueIce, "avoid tell");
        yield return Tell("tell-ice-fake", FakeIce, "in cone");
        yield return Tell("tell-thunder-real", TrueThunder, "avoid tell");
        yield return Tell("tell-thunder-fake", FakeThunder, "in line");
    }

    private static Trigger Tell(string id, uint marker, string text) => new()
    {
        Id = id,
        On = EventKind.HeadMarker,
        MatchId = marker,
        Phase = 1,
        Make = ctx => new Call
        {
            Text = text,
            Time = ctx.Event.Time,
            Key = id,
            Level = CallLevel.Alert,
        },
    };

    private static Trigger Hero(string id, uint status, string boss, int phase) => new()
    {
        Id = id,
        On = EventKind.StatusGain,
        MatchId = status,
        Phase = phase,
        OnlyMe = true,
        Make = ctx => new Call
        {
            Text = $"attack {boss}",
            Time = ctx.Event.Time,
            Key = id,
            Level = CallLevel.Alert,
            Personal = true,
        },
    };

    public static IEnumerable<Trigger> Triggers()
    {
        // Measured at 8.0 targets a cast, so these land on everyone.
        yield return Raidwide("forsaken", 0xBABC, 2);
        yield return Raidwide("ultima-upsurge", 0xC24A, 4);
        yield return Raidwide("aero-assault", 0xC3F7, 2);

        yield return Raidwide("white-hole", 0xBD66, 3);
        yield return Raidwide("umbra-smash", 0xBB00, 3);
        yield return Raidwide("bowels-of-agony", 0xBAF2, 3);

        // Tanks press it here, not in phase 2, and only if that is what the group
        // answers Bowels of Agony with. A group running SG3K gets the raidwide line
        // like everyone else rather than being told to press a limit break they are
        // saving.
        yield return new Trigger
        {
            Id = "vacuum-wave-tank-lb",
            On = EventKind.CastStart,
            MatchId = 0xBB13,
            Phase = 3,
            For = "tank",
            Make = ctx => ctx.Running("boa", "lb3")
                ? new Call
                {
                    Text = "tank limit break",
                    Time = Lands(ctx),
                    Key = "vacuum-wave",
                    Level = CallLevel.Alarm,
                }
                : new Call
                {
                    Text = "raidwide mit",
                    Time = Lands(ctx),
                    Key = "vacuum-wave",
                    Level = CallLevel.Alert,
                },
        };
        yield return new Trigger
        {
            Id = "vacuum-wave",
            On = EventKind.CastStart,
            MatchId = 0xBB13,
            Phase = 3,
            For = "healer,dps",
            Make = ctx => new Call
            {
                Text = Audience.RoleOf(ctx.MySlot) == "healer" ? "raidwide heal" : "raidwide",
                Time = Lands(ctx),
                Key = "vacuum-wave",
                Level = CallLevel.Alert,
            },
        };
        yield return Raidwide("light-of-judgment", 0xC622, 1);

        yield return Buster("revolting-ruin", 0xC403, 1) with
        {
            Owns = ["revolting-ruin-iii"],
        };
        yield return Buster("revolting-ruin-2", 0xC4E1, 1);

        yield return new Trigger
        {
            Id = "ultimate-embrace",
            On = EventKind.CastStart,
            MatchId = 0xC24C,
            Phase = 2,
            Make = ctx => new Call
            {
                Text = "share",
                Time = Lands(ctx),
                Key = "ultimate-embrace",
                Level = CallLevel.Alarm,
            },
        };

        foreach (var trigger in Numbered()) yield return trigger;

        yield return new Trigger
        {
            Id = "marker-on-me",
            On = EventKind.HeadMarker,
            OnlyMe = true,
            Make = ctx => OwnedMarkers.Contains(ctx.Event.Id)
                          || MarkerMeanings.TryFor(Territory, ctx.Event.Kind, ctx.Event.Id, out _)
                ? null
                : new Call
            {
                Text = "marker on you",
                Time = ctx.Event.Time,
                Key = $"marker-{ctx.Event.Id:X}",
                Level = CallLevel.Alarm,
                Personal = true,
            },
        };

        // 8.0 targets a cast, 40.5k each, 16 casts in one session.
        yield return Raidwide("gravitas", 0xBAAC, 1);

        // Exactly 4.0 targets a cast, which is what the source calls towers too, so
        // the coverage lines up and the number backs the name.
        yield return Cast("wave-cannon", 0xBAA8, "towers", 1);

        yield return new Trigger
        {
            Id = "nothingness",
            Owns = ["nothingness"],
            On = EventKind.AbilityHit,
            MatchId = 0xBAFC,
            Phase = 3,
            Make = ctx => new Call
            {
                Text = "pairs",
                Time = ctx.Event.Time,
                Key = $"nothingness-{ctx.Nth}",
                Level = CallLevel.Alert,
            },
        };

        // The stomp stack is answered in phase 3 and again in phase 5, and what it
        // means differs both times, so it lives with the phases rather than here.

        // Explodes if you are moving when it drops, so the seconds are the call.
        yield return Debuff("acceleration-bomb", 0x15AA, "stop everything", 4);
        yield return Debuff("cursed-shriek", 0x15A7, "look away", 4);
        // Measured at 5s, 49s and 68s in one pull, which is three different jobs.
        yield return Debuff("double-trouble-trap", 0x13D6, "trap", 1);
        yield return Debuff("entropy", 0x640, "donut", 3);
        yield return Debuff("dynamic-fluid", 0x641, "donut", 3);

        yield return Raidwide("light-of-judgment-2", 0xBABD, 2);
        yield return Raidwide("grand-cross", 0xBB14, 4);
        yield return Raidwide("thrumming-thunder", 0xC5DE, 4);
        yield return Buster("thunder-3-buster", 0xBB09, 3);

        yield return Cast("blizzard-3-stack", 0xBB0D, "stack", 3);
        yield return Cast("blizzard-3-move", 0xBB11, "keep moving", 3);
        yield return Cast("blizzard-blowout", 0xBA95, "knockback", 1);
        yield return Cast("knock-down-cast", 0xBB03, "stack middle", 3);
        yield return Cast("despair-1", 0xBAEC, "out of the middle", 3);
        yield return Cast("despair-2", 0xBAED, "out of the middle", 3);
        yield return Cast("mana-release", 0xBAA5, "in the donut", 4);
        // The highlighted wing is the one that cleaves, so the call is the far side:
        // BACD lights the left wing and BACE the right.
        yield return Cast("single-wing-left", 0xBACD, "right", 2);
        yield return Cast("single-wing-right", 0xBACE, "left", 2);

        yield return Cast("stray-flames", 0xBAF3, "bait", 3);
        yield return Cast("stray-spray", 0xBAF6, "bait", 3);

        // Cone on whoever it names, so it is a bait rather than a spread.
        yield return new Trigger
        {
            Id = "all-things-ending",
            On = EventKind.CastStart,
            MatchId = 0xBADC,
            Phase = 2,
            Make = ctx => new Call
            {
                Text = ctx.TargetIsMe ? "cone on you" : "bait cones",
                Time = ctx.Event.Time,
                Key = "all-things-ending",
                Level = CallLevel.Alert,
                Personal = ctx.TargetIsMe,
            },
        };

        // Towers, under whichever name the fight gives them. The phase 2 rotation
        // says which tower rather than that there are towers, so it lives in there.
        yield return Cast("celestriad", 0xBB42, "element towers", 5);
        yield return Cast("stray-apocalypse", 0xBB3B, "exaflares", 5);

        // Phase 5 is left for the port rather than named from here. The target
        // counts below are measured and worth keeping, but a count cannot say what
        // to do about a mechanic, and naming them from it got them wrong:
        //   BB52 Flare 2.0 targets   BB53 Chaotic Flare 1.9   BB54 Holy 3.1
        //   BB55 Flare Diffusion 2.6 BB56 Chaotic Holy 1.1    BB4A Quake 1.1
        //   BB35 Forsaken 6.8        BB36 Forsaken 2 6.0      BB50 Orchestra 1.0
        // Measured over 21 to 42 casts each in a pull that reaches phase 5.

        // The eighth set of towers is the last of the rotation, so it is called by
        // the rotation, which knows which tower is yours rather than that there are
        // some. The four ids still share one key, for the same reason they did here.
        yield return Hit("all-things-ending-bait-a", 0xBAD2, "bait", 2, "all-things-ending-bait");
        yield return Hit("all-things-ending-bait-b", 0xBAD3, "bait", 2, "all-things-ending-bait");

        // Their own line is "heal the target to full", and the target is the call.
        yield return new Trigger
        {
            Id = "accretion",
            On = EventKind.StatusGain,
            MatchId = 0xD2C,
            Phase = 3,
            Make = ctx => new Call
            {
                Text = ctx.TargetIsMe ? "heal to full on you" : $"heal {ctx.NameTarget()} to full",
                Time = ctx.Event.Time,
                Key = "accretion",
                Level = CallLevel.Alarm,
                Personal = ctx.TargetIsMe,
            },
        };

        // The resistance the towers leave on you used to be called as "fire last".
        // Phase 5 now works the whole soak order out from where the towers stand and
        // what is still ticking, which says the same thing and the rest of it.

        // The bare "second in line" is answered in phase 3 now, where it also knows
        // who is moving with you and, for a healer, whose bar to watch. Both firing
        // on the same status put two calls in one instant and the crowding rule
        // threw the one with the names in it away.
        foreach (var t in TelePortents()) yield return t;
        foreach (var t in NeoDebuffs()) yield return t;
        foreach (var t in Tells()) yield return t;

        // The phases go in last, and each one puts what it writes down ahead of what
        // reads it. The engine walks this list in order for every event, so a
        // collector listed after its own call would answer with the last event's
        // answer for the whole fight.
        foreach (var t in PhaseOne()) yield return t;
        foreach (var t in PhaseTwo()) yield return t;
        foreach (var t in PhaseThree()) yield return t;
        foreach (var t in PhaseFour()) yield return t;
        foreach (var t in PhaseFive()) yield return t;

        // Claimed so the pack's own bare rows stay quiet while the sequence answers.
        yield return Claim("mystery-fire-real", EventKind.HeadMarker, TrueFire, 1);
        yield return Claim("mystery-fire-fake", EventKind.HeadMarker, FakeFire, 1);

        // Which of the two bosses this player is meant to be hitting.
        yield return Hero("epic-hero", 0x1060, "Chaos", 3);
        yield return Hero("fated-hero", 0x1062, "Exdeath", 3);

        yield return new Trigger
        {
            Id = "tether-on-me",
            On = EventKind.Tether,
            OnlyMe = true,
            // Same as the marker catch-all: quiet wherever a named call exists.
            Make = ctx => MarkerMeanings.TryFor(Territory, ctx.Event.Kind, ctx.Event.Id, out _)
                ? null
                : new Call
            {
                Text = "tether on you",
                Time = ctx.Event.Time,
                Key = $"tether-{ctx.Event.Id:X}",
                Level = CallLevel.Alert,
                Personal = true,
            },
        };
    }
}
