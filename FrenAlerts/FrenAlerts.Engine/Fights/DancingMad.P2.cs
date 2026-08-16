namespace FrenAlerts.Engine;

// Phase two: the Forsaken tower rotation.
//
// Eight sets of the same three shapes, where what you do with yours alternates every
// set and depends on which half of the party you landed in two sets ago. There is no
// single right answer to it, so the group's own is asked for and the generic
// rotation is what a group that has not said hears.
public static partial class DancingMad
{
    private const uint StackPath = 0x02CB, SpreadPath = 0x02CC, ConePath = 0x02CD;

    // The two halves of a trine, which spawn as props and are the only thing that
    // says where the safe wedges are.
    private const uint TrineWest = 0x1EBFB2, TrineEast = 0x1EBFB3;

    private static string PathShape(uint marker) => marker switch
    {
        StackPath => "stack",
        ConePath => "cone",
        SpreadPath => "spread",
        _ => "",
    };

    // Melee by the slot standard the sheets already use, rather than by job: M1 and
    // M2 are the melee, and a fight should not need its own job table to say so.
    private static bool IsMelee(string slot) => slot is "M1" or "M2";

    // Which side of the pair this player's shape belongs on.
    //
    // Straight off the plan: cones and the support stack go left, spreads and the
    // dps stack go right. The shape decides it for two of the three, and only the
    // stack needs the role, because there is one of those per role bucket.
    private static string KroxySide(in TriggerContext ctx, string shape) => shape switch
    {
        "cone" => "left",
        "spread" => "right",
        _ => Audience.RoleOf(ctx.MySlot) is "tank" or "healer" ? "left" : "right",
    };

    // Whether this player is on the half that takes the first three towers and the
    // last one, which is what the 3/4/1 in the plan's own name counts.
    //
    // The plan settles it as "stack plus light party buddy": whoever held the stack
    // on the first set and the player they are paired with. Four players, and the
    // other four take the middle towers.
    private static string StackSideOf(in TriggerContext ctx, DancingMadPull pull)
    {
        var mine = pull.PathMark.GetValueOrDefault(ctx.Player.MyId, "");
        if (mine.Length == 0) return "";
        if (mine == "stack") return "a";

        var buddy = ctx.Party.IdOf(BuddyOf(ctx.MySlot));
        if (buddy == 0) return "";
        var theirs = pull.PathMark.GetValueOrDefault(buddy, "");
        if (theirs.Length == 0) return "";
        return theirs == "stack" ? "a" : "b";
    }

    // The four sets the first half is on a tower for. Read straight off the plan's
    // own header, where the eight towers are written AAA BBBB A.
    private static bool FirstHalfTower(int set) => set is 1 or 2 or 3 or 8;

    // The bait each role takes on the sets it is not on a tower for.
    private static string PathBait(in TriggerContext ctx) => Audience.RoleOf(ctx.MySlot) switch
    {
        "healer" => "bait left cone left",
        "tank" => "bait clone far",
        _ => "bait cone right or clone far",
    };

    // Whether to be close in or well out, which the shape on your head decides.
    private static string NearOrFar(string shape) => shape == "spread" ? "be far" : "be near";

    private static string MyShape(DancingMadPull pull) =>
        pull.MyPaths.Count > 0 ? pull.MyPaths[^1] : "";

    // Who each seat is paired with for the buddy rotation. The pairing is the
    // source's own and it lands exactly on the slot standard the sheets use, so
    // nothing here is invented: a tank is with a healer and a melee with a ranged.
    private static string BuddyOf(string slot) => slot switch
    {
        "MT" => "H1", "H1" => "MT",
        "OT" => "H2", "H2" => "OT",
        "M1" => "R1", "R1" => "M1",
        "M2" => "R2", "R2" => "M2",
        _ => "",
    };

    // Which job the pair took. Matching your buddy's shape means you are the one
    // helping; a different shape means the tower is yours.
    private static string BuddyGroup(in TriggerContext ctx, DancingMadPull pull)
    {
        var buddy = ctx.Party.IdOf(BuddyOf(ctx.MySlot));
        if (buddy == 0 || buddy == ctx.Player.MyId) return "";

        var mine = pull.PathMark.GetValueOrDefault(ctx.Player.MyId, "");
        var theirs = pull.PathMark.GetValueOrDefault(buddy, "");
        if (mine.Length == 0 || theirs.Length == 0) return "";

        return mine == theirs ? "helper" : "tower";
    }

    // The sets the tower half of a pair is on a tower for. The other four are the
    // helper's, and the two swap over after sets three and seven.
    private static bool BuddyTowerSet(int set) => set is 1 or 2 or 3 or 8;

    private static string BuddyShape(string shape) => shape switch
    {
        "stack" => "stack",
        "cone" => "cone",
        // The source calls the spread shape a circle here, and the group calling it
        // out loud says circle, so the call says circle.
        "spread" => "circle",
        _ => "",
    };

    // One set of the 3/4/1 rotation, as the plan writes it.
    //
    // The half that is not on a tower has a fixed home rather than a bait: the plan
    // says supports always take theirs on the left and dps always stand in the stack
    // on the right, and it says so on every one of the eight slides.
    private static string KroxySet(in TriggerContext ctx, DancingMadPull pull, int set)
    {
        var shape = MyShape(pull);
        if (shape.Length == 0) return "";

        var side = pull.StackSide;
        // Settled by whichever of the pair's two markers lands second, so an
        // unresolved one means the other half has not arrived yet.
        if (side.Length == 0) return "";

        if ((side == "a") == FirstHalfTower(set))
            return $"{KroxySide(ctx, shape)} tower + {PathMarker(shape)}";

        return Audience.RoleOf(ctx.MySlot) is "tank" or "healer"
            ? $"left {(shape == "cone" ? "cone" : "stack")}"
            : "right stack";
    }

    private static string BuddySet(in TriggerContext ctx, DancingMadPull pull, int set)
    {
        var group = pull.Buddy;
        var shape = BuddyShape(pull.PathMark.GetValueOrDefault(ctx.Player.MyId, MyShape(pull)));

        // Somebody has to swap after these two, and hearing it a set late is hearing
        // it once the towers are already up.
        var swap = set is 3 or 7 ? " then swap" : "";

        // The pairing is settled by whichever of the two markers lands second, so an
        // unresolved one here means the other half has not arrived yet. Saying
        // nothing lets that event make the call instead; guessing which half of the
        // pair you are in sends one of you to a tower nobody is helping with.
        if (group.Length == 0) return "";

        var mine = group == "tower" ? BuddyTowerSet(set) : !BuddyTowerSet(set);
        if (mine)
            return shape.Length > 0 ? $"tower + {shape}{swap}" : $"tower{swap}";

        return $"help your buddy{swap}";
    }

    private static IEnumerable<Trigger> PhaseTwo()
    {
        yield return Collect("path-mark", EventKind.HeadMarker, 0, 2, ctx =>
        {
            var shape = PathShape(ctx.Event.Id);
            if (shape.Length == 0) return;

            var pull = Pull(ctx);
            var who = ctx.Event.TargetId;

            if (ctx.TargetIsMe && pull.MyPaths.Count < 16) pull.MyPaths.Add(shape);
            DancingMadPull.Note(pull.PathMark, who, shape, DancingMadPull.Party);

            // A player only holds one shape at a time, so landing a new one takes
            // them out of whichever list they were in.
            pull.PathStacks.Remove(who);
            pull.PathCones.Remove(who);
            pull.PathSpreads.Remove(who);

            // Which half of the party you are in is settled by the second set and
            // never revisited: matching again later is chance, not a swap.
            if (pull.PathSet == 2 && ctx.TargetIsMe) pull.GroupA = true;

            // The buddy pairing and the 3/4/1 half are both settled by the first set
            // for the same reason. A later set matching by chance would swap your
            // job halfway through the mechanic.
            if (pull.PathSet == 1 && pull.Buddy.Length == 0)
                pull.Buddy = BuddyGroup(ctx, pull);
            if (pull.PathSet == 1 && pull.StackSide.Length == 0)
                pull.StackSide = StackSideOf(ctx, pull);

            var into = shape switch
            {
                "stack" => pull.PathStacks,
                "cone" => pull.PathCones,
                _ => pull.PathSpreads,
            };
            DancingMadPull.Note(into, who);
        });

        yield return Collect("path-set", EventKind.AbilityHit, 0xBABE, 2,
            ctx => Pull(ctx).PathSet++);

        yield return Collect("trine-at", EventKind.ActorSpawn, 0, 2, ctx =>
        {
            if (ctx.Event.DataId is not (TrineWest or TrineEast)) return;
            var pull = Pull(ctx);
            var at = ctx.Event.Source;
            if (!at.Known) return;

            // The fourth one is the middle trine, and which way it sweeps is the
            // only thing the tanks need out of it.
            if (pull.TrineDirs.Count == 3)
            {
                if (at.X is > 99f and < 101f)
                    pull.MiddleTrine = ctx.Event.DataId == TrineWest ? "west" : "east";
                return;
            }
            DancingMadPull.Note(pull.TrineDirs, Compass.Dir16(at), 3);
        });

        // ---- the rotation ----

        // The odd sets arrive on the head marker, the even ones on the cast that
        // resolves them, which is what the source keys each of the eight on.
        yield return PathSet("path-towers-1", EventKind.HeadMarker, 0, 1);
        yield return PathSet("path-towers-2", EventKind.HeadMarker, 0, 2);
        yield return PathSet("path-towers-3a", EventKind.CastStart, 0xBADC, 3);
        yield return PathSet("path-towers-3b", EventKind.CastStart, 0xBADD, 3);
        yield return PathSet("path-towers-4", EventKind.HeadMarker, 0, 4);
        yield return PathSet("path-towers-5a", EventKind.CastStart, 0xBADC, 5);
        yield return PathSet("path-towers-5b", EventKind.CastStart, 0xBADD, 5);
        yield return PathSet("path-towers-6", EventKind.HeadMarker, 0, 6);
        yield return PathSet("path-towers-7a", EventKind.CastStart, 0xBADC, 7);
        yield return PathSet("path-towers-7b", EventKind.CastStart, 0xBADD, 7);
        yield return PathSet("path-towers-8a", EventKind.AbilityHit, 0xBABF, 8);
        yield return PathSet("path-towers-8b", EventKind.AbilityHit, 0xBAC0, 8);
        yield return PathSet("path-towers-8c", EventKind.AbilityHit, 0xBAC1, 8);
        yield return PathSet("path-towers-8d", EventKind.AbilityHit, 0xBAC2, 8);

        // ---- the endings ----

        // Which way the ending sweeps, said on the cast so there is time to walk it
        // rather than on the hit, when the bait is already placed.
        yield return new Trigger
        {
            Id = "ending-early",
            On = EventKind.CastStart,
            MatchId = 0xBAD2,
            Phase = 2,
            Make = ctx => new Call
            {
                Text = EndingText(ctx, future: true, early: true),
                Time = Lands(ctx),
                Key = "ending-bait",
                Level = CallLevel.Alert,
            },
        };

        yield return new Trigger
        {
            Id = "ending-early-past",
            On = EventKind.CastStart,
            MatchId = 0xBAD3,
            Phase = 2,
            Make = ctx => new Call
            {
                Text = EndingText(ctx, future: false, early: true),
                Time = Lands(ctx),
                Key = "ending-bait",
                Level = CallLevel.Alert,
            },
        };

        // The last one of the phase has a second half: the future ending wants you
        // behind it afterwards and the past one wants you to stand still.
        yield return EndingBait("ending-bait-future", 0xBAD2, future: true, "get behind");
        yield return EndingBait("ending-bait-past", 0xBAD3, future: false, "stay");

        // ---- the trines ----

        yield return new Trigger
        {
            Id = "trine-spots",
            On = EventKind.ActorSpawn,
            Phase = 2,
            OncePerBurst = false,
            Make = ctx =>
            {
                if (ctx.Event.DataId is not (TrineWest or TrineEast)) return null;
                var pull = Pull(ctx);
                if (pull.TrineDirs.Count != 3) return null;

                var spots = pull.TrineDirs.Order().ToList();
                // The party takes the first wedge round and the tanks the last, so
                // each half hears its own spot before it hears all three.
                var mine = Audience.RoleOf(ctx.MySlot) == "tank" ? spots[2] : spots[0];
                return new Call
                {
                    Text = $"{Way16(mine)} later ({Way16(spots[0])}/{Way16(spots[1])}/{Way16(spots[2])})",
                    Time = ctx.Event.Time,
                    Key = "trine-spots",
                    Level = CallLevel.Info,
                    Once = true,
                };
            },
        };

        yield return new Trigger
        {
            Id = "wings-of-destruction",
            On = EventKind.CastStart,
            MatchId = 0xC487,
            Phase = 2,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                var wings = Audience.RoleOf(ctx.MySlot) != "tank"
                    ? "outer 2 rings"
                    : pull.MiddleTrine.Length > 0
                        ? $"be near/far + {pull.MiddleTrine}ward trine"
                        : "be near/far";

                if (pull.TrineDirs.Count != 3)
                    return new Call
                    {
                        Text = wings,
                        Time = Lands(ctx),
                        Key = "wings-of-destruction",
                        Level = CallLevel.Alarm,
                    };

                var spots = pull.TrineDirs.Order().ToList();
                return new Call
                {
                    Text = $"{Way16(spots[0])}/{Way16(spots[1])}/{Way16(spots[2])} + {wings}",
                    Time = Lands(ctx),
                    Key = "wings-of-destruction",
                    Level = CallLevel.Alarm,
                };
            },
        };
    }

    // Where the ending goes, and under the buddy rotation how far out: their past
    // bait is taken at max melee rather than anywhere between the towers.
    // The source says it two ways: which ending is coming while there is still time
    // to walk, then where the bait goes once it lands.
    private static string EndingText(in TriggerContext ctx, bool future, bool early = false)
    {
        if (early)
            return future
                ? "future, bait away from towers"
                : ctx.Running(ForsakenStrat, "buddy")
                    ? "past, bait between towers (max melee)"
                    : "past, bait between towers";

        if (future) return "bait ending opposite towers";
        return ctx.Running(ForsakenStrat, "buddy")
            ? "bait ending between towers (max melee)"
            : "bait ending between towers";
    }

    // The last ending of the phase.
    //
    // The 3/4/1 plan changed this one deliberately: both the past and the future
    // ending are baited between the last two towers, and the future one is moved
    // out of afterwards. Everyone else keeps the two different baits.
    private static string LastEnding(in TriggerContext ctx, bool future, string after)
    {
        if (!ctx.Running(ForsakenStrat, "kroxy-rinon")) return $"bait ending then {after}";
        return future
            ? "bait between the last towers then move out"
            : "bait between the last towers";
    }

    private static Trigger EndingBait(string id, uint action, bool future, string after) => new()
    {
        Id = id,
        On = EventKind.AbilityHit,
        MatchId = action,
        Phase = 2,
        Make = ctx => new Call
        {
            // The ninth is the one that runs into the phase change, and only that
            // one has anything to do afterwards.
            Text = Pull(ctx).PathSet == 9 ? LastEnding(ctx, future, after) : EndingText(ctx, future),
            Time = ctx.Event.Time + 1.2,
            Key = "ending-bait-late",
            Level = CallLevel.Alarm,
        },
    };

    // One set of the rotation. Which set it is comes off the counter rather than off
    // the event, because the same three ids carry all eight of them.
    private static Trigger PathSet(string id, EventKind on, uint match, int set) => new()
    {
        Id = id,
        On = on,
        MatchId = match,
        Phase = 2,
        // Every marker of the set, not just the first.
        //
        // Eight of them land at once and three ids carry them, so the first of a
        // burst is whoever happened to be read first. Under the burst rule the call
        // was only made if this player's own marker was the first of its shape,
        // which is a one in three chance of being told anything at all. The shared
        // key is what keeps that from becoming eight calls.
        OncePerBurst = false,
        Make = ctx =>
        {
            var pull = Pull(ctx);
            if (pull.PathSet != set) return null;

            if (on == EventKind.HeadMarker)
            {
                // Any of the three shapes and nothing else, or every marker in the
                // fight would land here.
                if (PathShape(ctx.Event.Id).Length == 0) return null;

                // And not until this player's own marker for this set has arrived.
                // One arrives per player per set, so the count is the set once mine
                // has landed; answering before that reads the previous set's shape
                // off the board and calls it as this set's.
                if (pull.MyPaths.Count < set) return null;
            }

            var text = PathFor(ctx, pull, set);
            if (text.Length == 0) return null;

            return new Call
            {
                Text = $"{set}: {text}",
                Time = on == EventKind.CastStart ? Lands(ctx) : ctx.Event.Time,
                Key = "path-of-light",
                Level = CallLevel.Alarm,
                Personal = true,
            };
        },
    };

    // What this player does on one set of the rotation.
    //
    // The shape of it is the source's: group A takes towers on the odd sets and
    // baits on the even ones, group B the other way round, and which group you are
    // in was decided by whether you were marked on set two.
    private static string PathFor(in TriggerContext ctx, DancingMadPull pull, int set)
    {
        // Both named rotations are a different shape from the generic one, so they
        // answer before it rather than as a modifier on top of it.
        if (ctx.Running(ForsakenStrat, "buddy")) return BuddySet(ctx, pull, set);
        if (ctx.Running(ForsakenStrat, "kroxy-rinon")) return KroxySet(ctx, pull, set);

        var shape = MyShape(pull);
        var onTowers = pull.GroupA == set is 1 or 3 or 5 or 7;

        // Sets one and two hand out the shapes; from three on it alternates.
        return set switch
        {
            1 => FirstSet(ctx, pull, shape),
            2 or 4 or 6 or 8 => onTowers ? Tower(shape) : PathBait(ctx),
            _ => onTowers ? StackOrTower(ctx, pull, shape) : Stacking(ctx),
        };
    }

    // How the source names the marker on your head, as opposed to the shape name
    // the rotation reasons with.
    private static string PathMarker(string shape) => shape switch
    {
        "stack" => "stack",
        "cone" => "cone on you",
        "spread" => "aoe on you",
        _ => "",
    };

    private static string FirstSet(in TriggerContext ctx, DancingMadPull pull, string shape)
    {
        if (shape == "stack") return "stack on you + tower";
        if (shape.Length == 0) return "";

        return $"{PathMarker(shape)} + stack on {Names(ctx, pull.PathStacks)}";
    }

    // A set this player is on a tower for, plus how close to stand.
    private static string Tower(string shape) =>
        shape.Length == 0 ? "" : $"tower + {NearOrFar(shape)}";

    private static string StackOrTower(
        in TriggerContext ctx, DancingMadPull pull, string shape)
    {
        if (shape == "stack")
            return $"stack on {Names(ctx, pull.PathStacks)} + tower";
        return shape.Length == 0 ? "" : $"{PathMarker(shape)} + tower";
    }

    // The half that is not on towers takes the stacks and stays out of them.
    private static string Stacking(in TriggerContext ctx) =>
        Audience.RoleOf(ctx.MySlot) switch
        {
            "tank" => "left stack + avoid towers",
            "healer" => "bait left cone out",
            _ => "right stack + avoid towers",
        };
}
