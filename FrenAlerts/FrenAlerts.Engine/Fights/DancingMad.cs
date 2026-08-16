namespace FrenAlerts.Engine;

public static class DancingMad
{
    public const ushort Territory = 1363;

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
                Phase = 2,
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
                "healer" => "raidwide, heal",
                "tank" => "raidwide, mit",
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
                ? $"{what}, {ctx.Event.Duration:0.#}s"
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
                    Text = $"{word} in line",
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

    private static IEnumerable<Trigger> TelePortents()
    {
        foreach (var (first, second, way) in Portents)
        {
            // Two separate calls rather than one combined one: which pair you hold
            // decides where to stand, and that depends on the group's strat, so the
            // engine says what it knows and stops there.
            yield return Element($"portent-1-{way}", first, way, 1);
            yield return Element($"portent-2-{way}", second, way, 1);
        }
    }

    // The Neo Exdeath debuff you are holding, named the way the raid calls it.
    private static readonly (uint Status, string Word)[] Wounds =
    [
        (0x15A5, "purple"), (0x1317, "purple"),
        (0x15A6, "blue"), (0x1318, "blue"),
        (0x566, "death"), (0x1558, "death"),
        (0x1C6, "field"),
    ];

    private static IEnumerable<Trigger> NeoDebuffs()
    {
        foreach (var (status, word) in Wounds)
            yield return Element($"neo-debuff-{status:X}", status, word, 4);
    }

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
        yield return Raidwide("vacuum-wave", 0xBB13, 3);
        yield return Raidwide("white-hole", 0xBD66, 3);
        yield return Raidwide("umbra-smash", 0xBB00, 3);
        yield return Raidwide("bowels-of-agony", 0xBAF2, 3);
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

        // 1.0 target at 37.6k.
        yield return Buster("damning-edict", 0xBB01, 3);

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

        yield return new Trigger
        {
            Id = "knock-down",
            On = EventKind.HeadMarker,
            MatchId = 0xA1,
            Phase = 3,
            OnlyMe = true,
            Make = ctx => new Call
            {
                Text = "stack on you",
                Time = ctx.Event.Time,
                Key = $"knock-down-{ctx.Nth}",
                Level = CallLevel.Alarm,
                Personal = true,
            },
        };

        // Explodes if you are moving when it drops, so the seconds are the call.
        yield return Debuff("acceleration-bomb", 0x15AA, "stop when it drops", 4);
        yield return Debuff("cursed-shriek", 0x15A7, "look away", 4);
        // Measured at 5s, 49s and 68s in one pull, which is three different jobs.
        yield return Debuff("double-trouble-trap", 0x13D6, "trap", 1);
        yield return Debuff("entropy", 0x640, "donut", 3);
        yield return Debuff("dynamic-fluid", 0x641, "donut", 3);

        yield return Raidwide("light-of-judgment-2", 0xBABD, 2);
        yield return Raidwide("grand-cross", 0xBB14, 4);
        yield return Raidwide("thrumming-thunder", 0xC5DE, 4);
        yield return Raidwide("thunder-3-aoe", 0xBB12, 3);

        yield return Buster("thunder-3-buster", 0xBB09, 3);

        yield return Cast("blizzard-3-stack", 0xBB0D, "stack", 3);
        yield return Cast("blizzard-3-move", 0xBB11, "keep moving", 3);
        yield return Cast("blizzard-blowout", 0xBA95, "knockback", 1);
        yield return Cast("knock-down-cast", 0xBB03, "stack middle", 3);
        yield return Cast("slap-happy", 0xBAE6, "out of the middle", 3);
        yield return Cast("slap-happy-2", 0xBAE7, "out of the middle", 3);
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

        // Towers, under whichever name the fight gives them.
        yield return Cast("path-of-light-towers", 0xBADD, "towers", 2);
        yield return Cast("celestriad", 0xBB42, "towers", 5);
        yield return Cast("stray-apocalypse", 0xBB3B, "exaflares", 5);

        yield return Hit("towers-8-a", 0xBABF, "towers", 2, "towers-8");
        yield return Hit("towers-8-b", 0xBAC0, "towers", 2, "towers-8");
        yield return Hit("towers-8-c", 0xBAC1, "towers", 2, "towers-8");
        yield return Hit("towers-8-d", 0xBAC2, "towers", 2, "towers-8");
        yield return Hit("wave-cannon-explosion", 0xBAA8, "avoid towers", 1);
        yield return Hit("vitrophyre", 0xBAAC, "spread", 1);
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
                Text = ctx.TargetIsMe ? "heal to full, on you" : $"heal {ctx.NameTarget()} to full",
                Time = ctx.Event.Time,
                Key = "accretion",
                Level = CallLevel.Alarm,
                Personal = ctx.TargetIsMe,
            },
        };

        // Which element you take last, off the resistance the towers leave on you.
        yield return Element("celestriad-fire", 0xB56, "fire last", 5);
        yield return Element("celestriad-ice", 0xB57, "ice last", 5);
        yield return Element("celestriad-lightning", 0xBB6, "lightning last", 5);

        foreach (var t in InLine()) yield return t;
        foreach (var t in TelePortents()) yield return t;
        foreach (var t in NeoDebuffs()) yield return t;

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
