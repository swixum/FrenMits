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

    // ---- Chaotic Flood ----

    // Each set throws two lines, one along a wall and one through the middle, and
    // only the middle one is worth standing away from. The middle one is the caster
    // aiming at the middle: the wall line runs across the room's edge, so its caster
    // looks along the wall rather than in.
    private static bool AimsAtTheMiddle(Position p) =>
        Compass.Facing8(p.Heading) == Compass.Opposite8(Compass.Dir8(p));

    private static float FromTheMiddle(Position p) =>
        (p.X - Compass.Middle) * (p.X - Compass.Middle)
        + (p.Y - Compass.Middle) * (p.Y - Compass.Middle);

    // One line per set: the two casts of a set land together and the sets are a
    // second apart, so they group by when they arrived rather than by who cast them.
    private static List<Position> FloodSets(DancingMadPull pull)
    {
        var sets = new List<Position>();

        for (var i = 0; i < pull.FloodLines.Count;)
        {
            var began = pull.FloodLines[i].At;
            var together = new List<Position>();

            while (i < pull.FloodLines.Count
                   && pull.FloodLines[i].At - began <= DancingMadPull.FloodSetGap)
            {
                together.Add(pull.FloodLines[i].Where);
                i++;
            }

            // If neither is clearly looking in, the nearer one is the better guess.
            var middle = together.FirstOrDefault(AimsAtTheMiddle, together.OrderBy(FromTheMiddle).First());
            sets.Add(middle);
        }

        return sets;
    }

    // Which way the set turned: clockwise, the other way, or not readable.
    private static int Turn(int from, int to)
    {
        var gap = Compass.Wrap(to - from, 8);

        // The same spot says nothing and the opposite spot is the same line drawn
        // from its other end, so neither is a turn.
        if (gap is 0 or 4) return 0;
        return gap < 4 ? 1 : -1;
    }

    // Where to stand for Chaotic Flood and which way it goes round.
    //
    // Two sets are all it takes: the turn between them is the rotation, and where
    // they leave alone is where to start. That is about a second in and still four
    // seconds before the flood lands.
    //
    // The rotation is measured. The starting spot is one spot on from the second
    // line the way it is already going, and then the party follows it round.
    //
    // One spot rather than two. A line covers where it is drawn from and the spot
    // opposite, so two lines cover four of the eight; when those lines are a quarter
    // turn apart they cover every intercardinal, and two spots on from the second
    // one lands exactly on the first. One spot on is clear of both wherever they
    // fall, which is the whole of what the starting spot has to be.
    // How many sets of lines have been thrown so far, which is what says whether
    // there is still more to learn or the reading is as good as it will get.
    public static int FloodSetsSeen(DancingMadPull pull) => FloodSets(pull).Count;

    public static string? FloodRotation(DancingMadPull pull)
    {
        var sets = FloodSets(pull);
        if (sets.Count < 2) return null;

        var first = Compass.Dir8(sets[0]);
        var second = Compass.Dir8(sets[1]);

        var turn = Turn(first, second);
        if (turn == 0) return null;

        var start = Compass.Wrap(second + turn, 8);
        return $"start {Way(start)}, rotate {(turn > 0 ? "clockwise" : "counterclockwise")}";
    }

    // ---- Celestriad ----

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

        // "Go to Role Spots", which is what the group does about it. "Raidwide x4"
        // named the mechanic and left the answer to the reader, and theirs said the
        // same thing a moment later; theirs is taken out in
        // Data/patches/dancingmad_one_call.js so this is said once.
        //
        // It is cast more than once a fight. A recording of 17 August has BB40 at
        // 905.8s and again at 987.7s, byte for byte the same event both times.
        yield return new Trigger
        {
            Id = "ultima-repeater",
            Listed = true,
            On = EventKind.CastStart,
            MatchId = 0xBB40,
            Phase = 5,
            Make = ctx => new Call
            {
                Text = "Go to Role Spots",
                Time = Lands(ctx),
                Key = "ultima-repeater",
                Level = CallLevel.Alert,
            },
        };

        // Which of the two led into the set that is coming. They alternate, so this is
        // what tells a set that needs the call from one that has already had it.
        yield return Collect("fell-forces-led-by-orchestra", EventKind.CastStart, 0xBB50, 5,
            ctx => Pull(ctx).FellForcesAfterOrchestra = true);

        yield return Collect("fell-forces-led-by-repeater", EventKind.CastStart, 0xBB40, 5,
            ctx => Pull(ctx).FellForcesAfterOrchestra = false);

        // Fell Forces had no call at all, in ours or in theirs. Both ids land together
        // and neither is telegraphed, so this reads the one and lets the set's later
        // hits pass: swix asked for where to stand, which is said once per set.
        yield return new Trigger
        {
            Id = "fell-forces",
            Listed = true,
            On = EventKind.AbilityHit,
            MatchId = 0xC653,
            Phase = 5,
            // The fight page samples a trigger with nothing written down yet, and this
            // one says nothing until it knows which mechanic led in, so the row would
            // show its own id.
            Says = "Role Positions",
            Make = ctx =>
            {
                var pull = Pull(ctx);

                // Ultima Repeater feeds straight into its own set having just said
                // where to stand, so that one is left alone.
                if (pull.FellForcesAfterOrchestra is not true) return null;

                if (ctx.Event.Time - pull.FellForcesAt < DancingMadPull.FellForcesSet) return null;
                pull.FellForcesAt = ctx.Event.Time;

                return new Call
                {
                    Text = "Role Positions",
                    Time = ctx.Event.Time,
                    Key = "fell-forces",
                    Level = CallLevel.Alert,
                };
            },
        };

        // Chaotic Flood. Their side says "raidwide" and then "move away", which is
        // true and not much help: the mechanic is a rotating pair of lines and what
        // is wanted is where to stand and which way it turns.
        //
        // Nothing in the timeline or in their file mentions C183 at all. It is the
        // line itself, cast eight times, and it is how the rotation is read.
        // The flood's own bar, so the rotation call can count down to the thing it is
        // about rather than to the moment it was worked out.
        yield return Collect("flood-lands", EventKind.CastStart, 0xC13F, 5,
            ctx => Pull(ctx).FloodLandsAt = Lands(ctx));

        yield return Collect("flood-line", EventKind.CastStart, 0xC183, 5, ctx =>
        {
            if (!ctx.Event.Source.Known) return;

            var pull = Pull(ctx);
            if (pull.FloodLines.Count >= DancingMadPull.FloodCasts) return;
            pull.FloodLines.Add((ctx.Event.Time, ctx.Event.Source));
        });

        yield return new Trigger
        {
            Id = "flood-rotation",
            Listed = true,
            On = EventKind.CastStart,
            MatchId = 0xC183,
            Phase = 5,
            OncePerBurst = false,
            Says = "start north, rotate clockwise",
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.FloodCalled) return null;

                // Two sets is the whole reading. If the turn cannot be told even then
                // the flood is still a raidwide and saying so beats saying nothing,
                // which is what the plain call used to be for.
                if (FloodSetsSeen(pull) < 2) return null;

                var said = FloodRotation(pull) ?? "raidwide";
                pull.FloodCalled = true;

                return new Call
                {
                    Text = said,

                    // Counts down to the flood, or says nothing about time at all if
                    // its bar was never seen: a countdown to now is worse than none.
                    Time = pull.FloodLandsAt > ctx.Event.Time
                        ? pull.FloodLandsAt
                        : ctx.Event.Time,
                    Key = "p5-flood",
                    Level = CallLevel.Alert,
                };
            },
        };

        // Where to be for it rather than what it is. The four moves that follow are
        // the only other thing said about Forsaken, and theirs is patched out of all
        // of it: the raidwide, the moves and the stack.
        yield return Cast("p5-forsaken", 0xBB35, "Gather South for raidwide", 5, listed: true);

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

        // "Spread Positions" rather than "spread": swix's wording, and theirs answers
        // the same cast with a bare "Spread", which is the one taken out in
        // Data/patches/dancingmad_one_call.js so this is said once.
        yield return Cast("maddening-orchestra", 0xBB50, "Spread Positions", 5, listed: true);
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
