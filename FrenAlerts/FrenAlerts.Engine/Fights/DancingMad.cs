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

    private static Trigger Raidwide(string id, uint action, string name) => new()
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
                "healer" => $"{name}, raidwide, heal",
                "tank" => $"{name}, raidwide, mit",
                _ => $"{name}, raidwide",
            },
            Time = ctx.Event.Time,
            Key = id,
            Level = CallLevel.Alert,
        },
    };

    private static Trigger Buster(string id, uint action, string name) => new()
    {
        Id = id,
        On = EventKind.CastStart,
        MatchId = action,
        Make = ctx => new Call
        {
            Text = ctx.TargetIsMe
                ? $"{name}, buster on you"
                : $"{name}, buster on {ctx.NameTarget()}",
            Time = ctx.Event.Time,
            Key = id,
            Level = CallLevel.Alarm,
            Personal = ctx.TargetIsMe,
        },
    };

    private static Trigger Debuff(string id, uint status, string name, int seconds) => new()
    {
        Id = id,
        On = EventKind.StatusGain,
        MatchId = status,
        OnlyMe = true,
        Make = ctx => new Call
        {
            Text = $"{name}, {seconds}s",
            Time = ctx.Event.Time,
            Key = id,
            Level = CallLevel.Alert,
            Personal = true,
        },
    };

    public static IEnumerable<Trigger> Triggers()
    {
        // Measured at 8.0 targets a cast, so these land on everyone.
        yield return Raidwide("forsaken", 0xBABC, "Forsaken");
        yield return Raidwide("ultima-upsurge", 0xC24A, "Ultima Upsurge");
        yield return Raidwide("aero-assault", 0xC3F7, "Aero III Assault");
        yield return Raidwide("vacuum-wave", 0xBB13, "Vacuum Wave");
        yield return Raidwide("white-hole", 0xBD66, "White Hole");
        yield return Raidwide("umbra-smash", 0xBB00, "Umbra Smash");
        yield return Raidwide("bowels-of-agony", 0xBAF2, "Bowels of Agony");
        yield return Raidwide("light-of-judgment", 0xC622, "Light of Judgment");

        yield return Buster("revolting-ruin", 0xC403, "Revolting Ruin III") with
        {
            Owns = ["revolting-ruin-iii"],
        };
        yield return Buster("revolting-ruin-2", 0xC4E1, "Revolting Ruin III");

        yield return new Trigger
        {
            Id = "ultimate-embrace",
            On = EventKind.CastStart,
            MatchId = 0xC24C,
            Make = ctx => new Call
            {
                Text = "Ultimate Embrace, hits two",
                Time = ctx.Event.Time,
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
        yield return Raidwide("gravitas", 0xBAAC, "Gravitas");

        // 1.0 target at 37.6k.
        yield return Buster("damning-edict", 0xBB01, "Damning Edict");

        // Exactly 4.0 targets a cast, which is what the source calls towers too, so
        // the coverage lines up and the number backs the name.
        yield return new Trigger
        {
            Id = "wave-cannon",
            On = EventKind.CastStart,
            MatchId = 0xBAA8,
            Make = ctx => new Call
            {
                Text = "Wave Cannon, towers",
                Time = ctx.Event.Time,
                Key = "wave-cannon",
                Level = CallLevel.Alert,
            },
        };

        yield return new Trigger
        {
            Id = "nothingness",
            Owns = ["nothingness"],
            On = EventKind.AbilityHit,
            MatchId = 0xBAFC,
            Make = ctx => new Call
            {
                Text = $"Nothingness {ctx.Nth}, pairs",
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
                Text = $"Knock Down {ctx.Nth}, on you",
                Time = ctx.Event.Time,
                Key = $"knock-down-{ctx.Nth}",
                Level = CallLevel.Alarm,
                Personal = true,
            },
        };

        yield return Debuff("acceleration-bomb", 0x15AA, "Acceleration Bomb", 56);
        yield return Debuff("cursed-shriek", 0x15A7, "Cursed Shriek", 64);
        yield return Debuff("double-trouble-trap", 0x13D6, "Double-trouble Trap", 49);

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
