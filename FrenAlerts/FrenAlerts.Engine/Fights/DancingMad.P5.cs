namespace FrenAlerts.Engine;

// Phase five: the last one.
//
// Celestriad is the mechanic here: three elemental towers, a resistance you pick up
// from each one you soak, and an order that is only readable from where the towers
// happen to have spawned and which resistances are still ticking.
public static partial class DancingMad
{
    private const uint FireTower = 0x1EC03E;
    private const uint IceTower = 0x1EC03F;
    private const uint LightningTower = 0x1EC040;

    private static string TowerElement(uint dataId) => dataId switch
    {
        FireTower => "fire",
        IceTower => "ice",
        LightningTower => "lightning",
        _ => "",
    };

    private static string ResistElement(uint status) => status switch
    {
        0xB56 => "fire",
        0xB57 => "ice",
        0xBB6 => "lightning",
        _ => "",
    };

    // Which resistances are still on this player, longest first.
    //
    // Longest first because the one with the most left is the one you took most
    // recently, which is where the order round the towers is counted from.
    private static List<string> Holding(DancingMadPull pull, double now) =>
        pull.CeleUntil
            .Where(p => p.Value > now)
            .OrderByDescending(p => p.Value)
            .Select(p => p.Key)
            .ToList();

    // The towers in clockwise order starting from a spot, with a tower standing on
    // that spot sorted last rather than first.
    private static List<(string Element, Position At)> Clockwise(
        List<(string Element, Position At)> towers, Position from)
    {
        var start = Compass.Angle(from.X, from.Y);
        return towers
            .OrderBy(t =>
            {
                var gap = Compass.Angle(t.At.X, t.At.Y) - start;
                while (gap <= 0.0001f) gap += MathF.Tau;
                return gap;
            })
            .ToList();
    }

    // Which towers to soak and in what order.
    //
    // Returns empty when the towers have not all been seen, because half an order
    // is worse than none: it names a tower that is right and then stops.
    public static string CelestriadOrder(DancingMadPull pull, double now)
    {
        var towers = pull.CeleTowers;
        if (towers.Count == 0) return "";

        var have = Holding(pull, now);
        pull.CeleNoDebuff ??= have.Count == 0;

        if (pull.CeleNoDebuff == true)
        {
            // With nothing on you, the odd tower out is the landmark: the pair share
            // an element and the single one says where the pair's second is.
            var byElement = towers.GroupBy(t => t.Element).ToList();
            var pair = byElement.FirstOrDefault(g => g.Count() > 1)?.ToList();
            var only = byElement.FirstOrDefault(g => g.Count() == 1)?.First();
            if (pair is null || only is null) return "";

            var sorted = Clockwise(pair, only.Value.At);
            var next = sorted.Count > 1 ? sorted[1] : sorted[0];
            return $"no resistance soak {next.Element}";
        }

        var point = towers.FirstOrDefault(t => t.Element == have[0]);
        if (point.Element is null or "") return "";

        // The ones you have no resistance to are the ones you can still take.
        var open = towers.Where(t => !have.Contains(t.Element)).ToList();
        var order = new List<string>();
        foreach (var t in Clockwise(open, point.At))
            if (!order.Contains(t.Element)) order.Add(t.Element);

        // Then the ones you already hold, oldest resistance first, because those
        // wear off in that order and become takeable again in it.
        for (var i = have.Count - 1; i >= 0; i--)
            if (!order.Contains(have[i])) order.Add(have[i]);

        if (order.Count == 0) return "";
        return $"{have[0]} on you soak {string.Join(" then ", order)}";
    }

    private static IEnumerable<Trigger> PhaseFive()
    {
        yield return Collect("cele-reset", EventKind.CastStart, 0xBB42, 5, ctx =>
        {
            var pull = Pull(ctx);
            pull.CeleCalled = false;
            pull.CeleNoDebuff = null;
            pull.CeleTowers.Clear();
        });

        yield return Collect("cele-tower-at", EventKind.ActorSpawn, 0, 5, ctx =>
        {
            var element = TowerElement(ctx.Event.DataId);
            if (element.Length == 0 || !ctx.Event.Source.Known) return;
            var pull = Pull(ctx);
            if (pull.CeleTowers.Count >= DancingMadPull.Towers) return;
            pull.CeleTowers.Add((element, ctx.Event.Source));
        });

        yield return Collect("cele-resist", EventKind.StatusGain, 0, 5, ctx =>
        {
            if (!ctx.TargetIsMe) return;
            var element = ResistElement(ctx.Event.Id);
            if (element.Length == 0) return;
            var pull = Pull(ctx);
            DancingMadPull.Note(pull.CeleUntil, element, ctx.Event.Time + ctx.Event.Duration, 3);
        });

        // The order, asked for from both ends: the resistance landing and the towers
        // starting to cast. Whichever comes first says it and the other stays quiet.
        yield return CeleOrder("cele-order-debuff", EventKind.StatusGain, 0, 1.0);
        yield return CeleOrder("cele-order-towers-a", EventKind.CastStart, 0xBB43, 0.3);
        yield return CeleOrder("cele-order-towers-b", EventKind.CastStart, 0xBB44, 0.3);
        yield return CeleOrder("cele-order-towers-c", EventKind.CastStart, 0xBB45, 0.3);

        // ---- the rest of the phase ----

        yield return new Trigger
        {
            Id = "ultima-repeater",
            On = EventKind.CastStart,
            MatchId = 0xBB40,
            Phase = 5,
            Make = ctx => new Call
            {
                Text = "raidwide x4",
                Time = Lands(ctx),
                Key = "ultima-repeater",
                Level = CallLevel.Alert,
            },
        };

        yield return Raidwide("p5-flood", 0xC13F, 5);
        yield return Raidwide("p5-forsaken", 0xBB35, 5);

        yield return new Trigger
        {
            Id = "p5-flood-move",
            On = EventKind.AbilityHit,
            MatchId = 0xC269,
            Phase = 5,
            Make = ctx => new Call
            {
                Text = "move",
                Time = ctx.Event.Time,
                Key = "p5-move",
                Level = CallLevel.Alert,
            },
        };

        yield return new Trigger
        {
            Id = "p5-forsaken-move",
            On = EventKind.CastStart,
            MatchId = 0xBB38,
            Phase = 5,
            Make = ctx => new Call
            {
                Text = "move",
                Time = Lands(ctx),
                Key = "p5-move",
                Level = CallLevel.Alert,
            },
        };

        yield return Cast("maddening-orchestra", 0xBB50, "spread", 5);
        yield return Cast("stray-entropy", 0xBB3E, "spread", 5);

        // Two people get the opposite of what everyone else is doing, and neither of
        // them is told by anything on screen.
        yield return new Trigger
        {
            Id = "orchestra-flare",
            On = EventKind.StatusGain,
            MatchId = 0x14E6,
            Phase = 5,
            OnlyMe = true,
            Make = ctx => new Call
            {
                Text = "surprise flare (get out)",
                Time = ctx.Event.Time,
                Key = "orchestra-flare",
                Level = CallLevel.Alarm,
                Personal = true,
            },
        };

        yield return new Trigger
        {
            Id = "orchestra-holy",
            On = EventKind.StatusGain,
            MatchId = 0x14E7,
            Phase = 5,
            OnlyMe = true,
            Make = ctx => new Call
            {
                Text = "surprise holy (get in)",
                Time = ctx.Event.Time,
                Key = "orchestra-holy",
                Level = CallLevel.Alarm,
                Personal = true,
            },
        };

        yield return Cast("catastrophic-out", 0xC24E, "out", 5);
        yield return Cast("catastrophic-in", 0xC24F, "in", 5);

        yield return new Trigger
        {
            Id = "p5-stack",
            Says = "stack on you / stack on Bob",
            On = EventKind.HeadMarker,
            MatchId = 0x00A1,
            Phase = 5,
            // Same as phase 3: the markers land together, and the one about you is
            // not the first of them.
            OncePerBurst = false,
            Make = ctx => ctx.Phase != 5 ? null : new Call
            {
                Text = ctx.TargetIsMe ? "stack on you" : $"stack on {ctx.NameTarget()}",
                Time = ctx.Event.Time,
                Key = ctx.TargetIsMe ? "p5-stack-mine" : "p5-stack",
                Level = CallLevel.Alarm,
                Personal = ctx.TargetIsMe,
            },
        };

        yield return new Trigger
        {
            Id = "p5-enrage",
            On = EventKind.CastStart,
            MatchId = 0xBB3A,
            Phase = 5,
            Make = ctx => new Call
            {
                Text = "enrage",
                Time = Lands(ctx),
                Key = "p5-enrage",
                Level = CallLevel.Alarm,
            },
        };
    }

    private static Trigger CeleOrder(string id, EventKind on, uint match, double after) => new()
    {
        Id = id,
        Says = "fire on you soak ice then lightning",
        On = on,
        MatchId = match,
        Phase = 5,
        OncePerBurst = false,
        Make = ctx =>
        {
            if (on == EventKind.StatusGain
                && (!ctx.TargetIsMe || ResistElement(ctx.Event.Id).Length == 0)) return null;

            var pull = Pull(ctx);
            if (pull.CeleCalled) return null;

            var said = CelestriadOrder(pull, ctx.Event.Time);
            if (said.Length == 0) return null;
            pull.CeleCalled = true;

            return new Call
            {
                Text = said,
                Time = ctx.Event.Time + after,
                Key = "celestriad-order",
                Level = CallLevel.Alarm,
                Hold = 10f,
                Personal = true,
            };
        },
    };
}
