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
    private static Trigger Raidwide(string id, uint action) => new()
    {
        Id = id,
        On = EventKind.CastStart,
        MatchId = action,
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

    private static Trigger Buster(string id, uint action) => new()
    {
        Id = id,
        On = EventKind.CastStart,
        MatchId = action,
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
    private static Trigger Debuff(string id, uint status, string what) => new()
    {
        Id = id,
        On = EventKind.StatusGain,
        MatchId = status,
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

    private static Trigger Cast(string id, uint action, string text) => new()
    {
        Id = id,
        On = EventKind.CastStart,
        MatchId = action,
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
    private static Trigger Hit(string id, uint action, string text, string key = "") => new()
    {
        Id = id,
        On = EventKind.AbilityHit,
        MatchId = action,
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
    private static Trigger Element(string id, uint status, string text) => new()
    {
        Id = id,
        On = EventKind.StatusGain,
        MatchId = status,
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

    private static Trigger Hero(string id, uint status, string boss) => new()
    {
        Id = id,
        On = EventKind.StatusGain,
        MatchId = status,
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
        yield return Raidwide("forsaken", 0xBABC);
        yield return Raidwide("ultima-upsurge", 0xC24A);
        yield return Raidwide("aero-assault", 0xC3F7);
        yield return Raidwide("vacuum-wave", 0xBB13);
        yield return Raidwide("white-hole", 0xBD66);
        yield return Raidwide("umbra-smash", 0xBB00);
        yield return Raidwide("bowels-of-agony", 0xBAF2);
        yield return Raidwide("light-of-judgment", 0xC622);

        yield return Buster("revolting-ruin", 0xC403) with
        {
            Owns = ["revolting-ruin-iii"],
        };
        yield return Buster("revolting-ruin-2", 0xC4E1);

        yield return new Trigger
        {
            Id = "ultimate-embrace",
            On = EventKind.CastStart,
            MatchId = 0xC24C,
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
        yield return Raidwide("gravitas", 0xBAAC);

        // 1.0 target at 37.6k.
        yield return Buster("damning-edict", 0xBB01);

        // Exactly 4.0 targets a cast, which is what the source calls towers too, so
        // the coverage lines up and the number backs the name.
        yield return Cast("wave-cannon", 0xBAA8, "towers");

        yield return new Trigger
        {
            Id = "nothingness",
            Owns = ["nothingness"],
            On = EventKind.AbilityHit,
            MatchId = 0xBAFC,
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
        yield return Debuff("acceleration-bomb", 0x15AA, "stop when it drops");
        yield return Debuff("cursed-shriek", 0x15A7, "look away");
        // Measured at 5s, 49s and 68s in one pull, which is three different jobs.
        yield return Debuff("double-trouble-trap", 0x13D6, "trap");
        yield return Debuff("entropy", 0x640, "donut");
        yield return Debuff("dynamic-fluid", 0x641, "donut");

        yield return Raidwide("light-of-judgment-2", 0xBABD);
        yield return Raidwide("grand-cross", 0xBB14);
        yield return Raidwide("thrumming-thunder", 0xC5DE);
        yield return Raidwide("thunder-3-aoe", 0xBB12);

        yield return Buster("thunder-3-buster", 0xBB09);

        yield return Cast("blizzard-3-stack", 0xBB0D, "stack");
        yield return Cast("blizzard-3-move", 0xBB11, "keep moving");
        yield return Cast("blizzard-blowout", 0xBA95, "knockback");
        yield return Cast("knock-down-cast", 0xBB03, "stack middle");
        yield return Cast("slap-happy", 0xBAE6, "out of the middle");
        yield return Cast("slap-happy-2", 0xBAE7, "out of the middle");
        yield return Cast("despair-1", 0xBAEC, "out of the middle");
        yield return Cast("despair-2", 0xBAED, "out of the middle");
        yield return Cast("mana-release", 0xBAA5, "in the donut");
        yield return Cast("stray-flames", 0xBAF3, "bait");
        yield return Cast("stray-spray", 0xBAF6, "bait");

        // Cone on whoever it names, so it is a bait rather than a spread.
        yield return new Trigger
        {
            Id = "all-things-ending",
            On = EventKind.CastStart,
            MatchId = 0xBADC,
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
        yield return Cast("path-of-light-towers", 0xBADD, "towers");
        yield return Cast("celestriad", 0xBB42, "towers");
        yield return Cast("stray-apocalypse", 0xBB3B, "exaflares");

        yield return Hit("towers-8-a", 0xBABF, "towers", "towers-8");
        yield return Hit("towers-8-b", 0xBAC0, "towers", "towers-8");
        yield return Hit("towers-8-c", 0xBAC1, "towers", "towers-8");
        yield return Hit("towers-8-d", 0xBAC2, "towers", "towers-8");
        yield return Hit("wave-cannon-explosion", 0xBAA8, "avoid towers");
        yield return Hit("vitrophyre", 0xBAAC, "spread");
        yield return Hit("all-things-ending-bait-a", 0xBAD2, "bait", "all-things-ending-bait");
        yield return Hit("all-things-ending-bait-b", 0xBAD3, "bait", "all-things-ending-bait");

        // Their own line is "heal the target to full", and the target is the call.
        yield return new Trigger
        {
            Id = "accretion",
            On = EventKind.StatusGain,
            MatchId = 0xD2C,
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
        yield return Element("celestriad-fire", 0xB56, "fire last");
        yield return Element("celestriad-ice", 0xB57, "ice last");
        yield return Element("celestriad-lightning", 0xBB6, "lightning last");

        foreach (var t in InLine()) yield return t;

        // Which of the two bosses this player is meant to be hitting.
        yield return Hero("epic-hero", 0x1060, "Chaos");
        yield return Hero("fated-hero", 0x1062, "Exdeath");

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
