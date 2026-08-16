namespace FrenAlerts.Engine;

// Phase three: Chaos and Exdeath, and the black holes.
//
// This is the phase the direction calls are worth the most in. The black holes, the
// crystals and the boss's own teleport are all props and packets rather than casts,
// so a fight with no eye on the arena has nothing at all to say here.
public static partial class DancingMad
{
    private const uint FireCrystalId = 0x1EC03A;
    private const uint WaterCrystalId = 0x1EC03B;
    private const uint WindCrystalId = 0x1EC03C;

    private const uint HoleTether = 0x0054;
    private const uint Nothingness = 0xBAFC;

    // Which end of a tether is the black hole, whichever way round it was read.
    private static uint HoleEnd(in TriggerContext ctx)
    {
        var e = ctx.Event;
        if (!ActorId.IsPlayer(e.SourceId) && e.SourceId != 0) return e.SourceId;
        if (!ActorId.IsPlayer(e.TargetId) && e.TargetId != 0) return e.TargetId;
        return 0;
    }

    private static int HoleDir(in TriggerContext ctx)
    {
        var id = HoleEnd(ctx);
        if (id == 0) return DancingMadPull.Nowhere;
        var at = ctx.Actors.Where(id);
        return at.Known ? Compass.Dir4(at) : DancingMadPull.Nowhere;
    }

    private static bool AmDps(in TriggerContext ctx) => Audience.RoleOf(ctx.MySlot) == "dps";

    // The black holes in the order they are taken, starting from where the boss
    // teleported to. Empty when either end of that is still unknown, which is what
    // makes the call fall back to naming them rather than assigning them.
    private static IReadOnlyList<int> HoleOrder(DancingMadPull pull)
    {
        if (pull.KefkaDir == DancingMadPull.Nowhere || pull.HoleDirs.Count == 0) return [];
        return Compass.ClockwiseFrom(Compass.EightToFour(pull.KefkaDir), pull.HoleDirs, 4);
    }

    // How a tether is named: by where it actually is, or by how far round it is
    // from the boss, which is what a group counting them off prefers.
    private static string HoleName(in TriggerContext ctx, IReadOnlyList<int> order, int nth)
    {
        // Named against Kefka rather than against the arena. Checked by name, not as
        // the fallthrough, or every option added later would silently be clock spots.
        if (ctx.Running(HoleTetherStrat, "kefkaNorth")) return HoleNameFromKefka(ctx);

        if (!ctx.Running(HoleTetherStrat, "true"))
            return nth switch
            {
                0 => "first clockwise",
                1 => "second clockwise",
                _ => "third clockwise",
            };

        return nth >= 0 && nth < order.Count ? Way4(order[nth]) : Compass.Unknown;
    }

    private static string HoleList(IReadOnlyList<int> order)
    {
        if (order.Count == 0) return Compass.Unknown;
        return string.Join(' ', order.Select(Way4));
    }

    private static IEnumerable<Trigger> PhaseThree()
    {
        // ---- what gets written down ----

        yield return Collect("kefka-id", EventKind.CastStart, 0xBAE5, 3,
            ctx => Pull(ctx).KefkaId = ctx.Event.SourceId);

        yield return Collect("kefka-id-2", EventKind.CastStart, 0xBAE6, 3,
            ctx => Pull(ctx).KefkaId = ctx.Event.SourceId);

        yield return Collect("crystal-at", EventKind.ActorSpawn, 0, 3, ctx =>
        {
            var at = ctx.Event.Source;
            if (!at.Known) return;
            var pull = Pull(ctx);
            // The intercards, because that is where they stand and calling one of
            // them north would send the bait a wedge off.
            var dir = Compass.Dir8(at);
            switch (ctx.Event.DataId)
            {
                case FireCrystalId: pull.FireCrystal = dir; break;
                case WaterCrystalId: pull.WaterCrystal = dir; break;
                case WindCrystalId: pull.WindCrystal = dir; break;
            }
        });

        yield return Collect("element-debuff", EventKind.StatusGain, 0, 3, ctx =>
        {
            if (ctx.Event.Id is not (0x640 or 0x641)) return;
            var pull = Pull(ctx);
            var fire = ctx.Event.Id == 0x640;

            // Which element runs out first is the whole order of the phase, and it
            // is only knowable from the first pair of durations that land.
            if (pull.FireShort is null && ctx.Event.Duration > 0)
            {
                var shortOne = ctx.Event.Duration < 20f;
                pull.FireShort = shortOne == fire;
            }

            if (ctx.TargetIsMe) pull.MyElement = fire ? "fire" : "water";
            DancingMadPull.Note(fire ? pull.FirePlayers : pull.WaterPlayers, ctx.Event.TargetId);
        });

        yield return Collect("wind-debuff", EventKind.StatusGain, 0, 3, ctx =>
        {
            if (ctx.Event.Id is not (0x642 or 0x643)) return;
            if (!ctx.TargetIsMe) return;
            Pull(ctx).MyWind = ctx.Event.Id == 0x642 ? "head" : "tail";
        });

        yield return Collect("wind-debuff-gone", EventKind.StatusLose, 0, 3, ctx =>
        {
            if (ctx.Event.Id is not (0x642 or 0x643)) return;
            if (!ctx.TargetIsMe) return;
            Pull(ctx).MyWind = "";
        });

        yield return Collect("in-line-collect", EventKind.StatusGain, 0, 3, ctx =>
        {
            var n = ctx.Event.Id switch
            {
                0xBBC => 1, 0xBBD => 2, 0xBBE => 3, _ => 0,
            };
            if (n == 0) return;
            DancingMadPull.Note(Pull(ctx).InLine, ctx.Event.TargetId, n, DancingMadPull.Party);
        });

        yield return Collect("accretion-collect", EventKind.StatusGain, 0x644, 3, ctx =>
        {
            var pull = Pull(ctx);
            var who = ctx.Event.TargetId;
            if (pull.InLine.GetValueOrDefault(who) == 1) pull.FirstAccretion = who;
            else pull.SecondAccretion = who;
            DancingMadPull.Note(pull.Accretions, who);
            if (ctx.TargetIsMe) pull.HadAccretion = true;
        });

        yield return Collect("nothingness-count", EventKind.AbilityHit, Nothingness, 3, ctx =>
        {
            var pull = Pull(ctx);
            pull.Nothingness++;
            // The sets that hand out fresh tethers start their list over. Without
            // this the second set is called against the first set's spots.
            if (pull.Nothingness is 2 or 3 or 6 or 9 or 10)
            {
                pull.HolesCalled = false;
                pull.HoleDirs.Clear();
            }
        });

        yield return Collect("hole-tether-collect", EventKind.Tether, HoleTether, 3, ctx =>
        {
            var pull = Pull(ctx);
            if (pull.Nothingness is 1 or 10) return;
            var dir = HoleDir(ctx);
            if (dir == DancingMadPull.Nowhere) return;
            DancingMadPull.Note(pull.HoleDirs, dir, 4);
        });

        yield return Collect("blaster-turn", EventKind.AbilityHit, 0xBAE3, 3, ctx =>
        {
            var pull = Pull(ctx);
            var at = ctx.Event.Source;
            if (!at.Known || pull.BlasterTurn != 0) return;

            if (!pull.FirstBlaster.Known)
            {
                pull.FirstBlaster = at;
                pull.BlasterDir = Compass.Opposite8(Compass.Dir8(at));
                return;
            }

            // Which way round it sweeps, from the turn between the first two. The
            // cross product's sign is the answer and its size is not.
            var x1 = pull.FirstBlaster.X - Compass.Middle;
            var y1 = pull.FirstBlaster.Y - Compass.Middle;
            var x2 = at.X - Compass.Middle;
            var y2 = at.Y - Compass.Middle;
            var turn = MathF.Atan2(y1 * x2 - x1 * y2, y1 * y2 + x1 * x2);
            pull.BlasterTurn = turn < 0 ? -1 : turn > 0 ? 1 : 0;
        });

        yield return Collect("wind-next", EventKind.AbilityHit, 0xBAFF, 3,
            ctx => Pull(ctx).WindNext = true);

        yield return Collect("second-knock-down", EventKind.AbilityHit, 0xBB02, 3,
            ctx => Pull(ctx).SecondKnockDown = true);

        // ---- the crystals ----

        // Where all three stand and which order they go off in, said once while
        // there is still a whole mechanic to walk it in.
        yield return new Trigger
        {
            Id = "crystal-spots",
            On = EventKind.ActorSpawn,
            Phase = 3,
            OncePerBurst = false,
            Make = ctx =>
            {
                if (ctx.Event.DataId != WindCrystalId) return null;
                var pull = Pull(ctx);
                if (pull.FireCrystal == DancingMadPull.Nowhere
                    || pull.WaterCrystal == DancingMadPull.Nowhere) return null;

                var fireFirst = pull.FireShort ?? true;
                var first = fireFirst ? $"fire {Way(pull.FireCrystal)}" : $"water {Way(pull.WaterCrystal)}";
                var second = fireFirst ? $"water {Way(pull.WaterCrystal)}" : $"fire {Way(pull.FireCrystal)}";
                var wind = pull.WindCrystal == DancingMadPull.Nowhere
                    ? "wind last"
                    : $"wind {Way(pull.WindCrystal)}";

                return new Call
                {
                    Text = $"{first} then {second} then {wind} (later)",
                    Time = ctx.Event.Time + 2.0,
                    Key = "crystal-spots",
                    Level = CallLevel.Info,
                    Hold = 8f,
                    Once = true,
                };
            },
        };

        // Your element and where to take it, which is a spread for fire and a donut
        // for water, and a crystal to bait unless you are the one meleeing.
        yield return ElementCall("fire-element", 0x640, "fire", "spread");
        yield return ElementCall("water-element", 0x641, "water", "donut");

        // Which way the knockback throws you and which way to be facing when it
        // lands, both of which are read off things the cast itself does not carry.
        yield return new Trigger
        {
            Id = "vacuum-wave-knockback",
            On = EventKind.CastStart,
            MatchId = 0xBB13,
            Phase = 3,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                var to = pull.WindCrystal == DancingMadPull.Nowhere
                    ? "crystal"
                    : Way(pull.WindCrystal);
                var facing = pull.MyWind switch
                {
                    "head" => " + look away",
                    "tail" => " + face it",
                    _ => "",
                };
                return new Call
                {
                    Text = $"knockback to {to}{facing}",
                    Time = Lands(ctx),
                    Key = "vacuum-wave-knockback",
                    Level = CallLevel.Alarm,
                    Personal = true,
                };
            },
        };

        // Which of the two windings you are wearing, said as the thing you do about
        // it rather than as the debuff's own name.
        yield return new Trigger
        {
            Id = "wind-on-me",
            On = EventKind.StatusGain,
            Phase = 3,
            OnlyMe = true,
            Make = ctx =>
            {
                if (ctx.Event.Id is not (0x642 or 0x643)) return null;
                var pull = Pull(ctx);
                var wind = ctx.Event.Id == 0x642 ? "headwind on you" : "tailwind on you";
                var mine = pull.MyElement.Length > 0 ? $"{pull.MyElement} + " : "";
                return new Call
                {
                    Text = $"{mine}{wind}",
                    Time = ctx.Event.Time,
                    Key = "wind-on-me",
                    Level = CallLevel.Info,
                    Personal = true,
                };
            },
        };

        // ---- Ultima Blaster ----

        yield return new Trigger
        {
            Id = "blaster-rotation",
            On = EventKind.AbilityHit,
            MatchId = 0xBAE3,
            Phase = 3,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.BlasterTurn == 0 || pull.BlasterDir == DancingMadPull.Nowhere) return null;
                var way = pull.BlasterTurn < 0 ? "clockwise" : "counterclockwise";
                return new Call
                {
                    Text = $"{Way(pull.BlasterDir)} {way} (later)",
                    Time = ctx.Event.Time,
                    Key = "blaster-rotation",
                    Level = CallLevel.Info,
                    Hold = 8f,
                    Once = true,
                };
            },
        };

        // ---- the order ----

        yield return new Trigger
        {
            Id = "in-line-with",
            On = EventKind.StatusGain,
            Phase = 3,
            OnlyMe = true,
            Make = ctx =>
            {
                if (ctx.Event.Id is not (0xBBC or 0xBBD or 0xBBE)) return null;
                var pull = Pull(ctx);
                var mine = pull.InLine.GetValueOrDefault(ctx.Player.MyId);
                if (mine == 0) return null;

                // The healers are the ones who have to know whose bar to watch, and
                // in which order, rather than who they are moving with.
                if (Audience.RoleOf(ctx.MySlot) == "healer")
                {
                    var order = AccretionOrder(ctx, pull);
                    if (order.Count > 0)
                        return new Call
                        {
                            Text = $"heal {string.Join(" then ", order.Select(ctx.Describe))} to full",
                            Time = ctx.Event.Time,
                            Key = "in-line-with",
                            Level = CallLevel.Alert,
                            Personal = true,
                        };
                }

                var with = pull.InLine
                    .Where(p => p.Value == mine && p.Key != ctx.Player.MyId)
                    .Select(p => p.Key)
                    .ToList();

                return new Call
                {
                    Text = with.Count > 0
                        ? $"#{mine} (with {Names(ctx, with)})"
                        : $"#{mine}",
                    Time = ctx.Event.Time,
                    Key = "in-line-with",
                    Level = CallLevel.Alert,
                    Personal = true,
                };
            },
        };

        // ---- Slap Happy ----

        // Which way to run round the boss, off where it is standing, plus what the
        // stack is when you get there.
        yield return SlapHappy("slap-happy-right", 0xBAE6, right: true);
        yield return SlapHappy("slap-happy-left", 0xBAE7, right: false);

        // ---- the black holes ----

        yield return HoleSet("black-hole-1", EventKind.Tether, HoleTether, 1);
        yield return HoleSet("black-hole-2", EventKind.Tether, HoleTether, 2);
        yield return HoleSet("black-hole-3", EventKind.Tether, HoleTether, 3);
        yield return HoleSet("black-hole-4", EventKind.AbilityHit, Nothingness, 4);
        yield return HoleSet("black-hole-5", EventKind.AbilityHit, Nothingness, 5);
        yield return HoleSet("black-hole-6", EventKind.Tether, HoleTether, 6);
        yield return HoleSet("black-hole-7", EventKind.AbilityHit, Nothingness, 7);
        yield return HoleSet("black-hole-8", EventKind.AbilityHit, Nothingness, 8);
        yield return HoleSet("black-hole-9", EventKind.Tether, HoleTether, 9);
        yield return HoleSet("black-hole-10", EventKind.Tether, HoleTether, 10);

        // ---- the boss's own spot ----

        yield return new Trigger
        {
            Id = "kefka-teleport",
            On = EventKind.ActorMoved,
            Phase = 3,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.KefkaId == 0 || ctx.Event.SourceId != pull.KefkaId) return null;
                if (!ctx.Event.Source.Known) return null;

                // It teleports to the edge facing the middle, so where it stands is
                // the way it is not looking.
                pull.KefkaDir = Compass.Opposite8(Compass.Facing8(ctx.Event.Source.Heading));

                // The ninth set lands on a raidwide that has to be healed through,
                // so that one call carries both halves rather than racing itself.
                var heal = pull.Nothingness == 9 ? "heal to full + " : "";
                return new Call
                {
                    Text = $"{heal}{Way(pull.KefkaDir)} kefka",
                    Time = ctx.Event.Time,
                    Key = "kefka-teleport",
                    Level = pull.Nothingness == 9 ? CallLevel.Alarm : CallLevel.Info,
                };
            },
        };

        // ---- the rest ----

        yield return new Trigger
        {
            Id = "implosion-sides",
            On = EventKind.CastStart,
            MatchId = 0xBAFD,
            Phase = 3,
            Make = ctx => new Call
            {
                Text = "sides then front and back",
                Time = Lands(ctx),
                Key = "implosion",
                Level = CallLevel.Alert,
            },
        };

        yield return new Trigger
        {
            Id = "implosion-front-back",
            On = EventKind.CastStart,
            MatchId = 0xBAFE,
            Phase = 3,
            Make = ctx => new Call
            {
                Text = "front and back then sides",
                Time = Lands(ctx),
                Key = "implosion",
                Level = CallLevel.Alert,
            },
        };

        // After the implosion resolves there is a jump to bait, which nothing else
        // announces.
        yield return new Trigger
        {
            Id = "umbra-smash-bait",
            On = EventKind.AbilityHit,
            MatchId = 0xBAFD,
            Phase = 3,
            Make = ctx => new Call
            {
                Text = "bait jump",
                Time = ctx.Event.Time + 10.0,
                Key = "umbra-smash-bait",
                Level = CallLevel.Alert,
                Once = true,
            },
        };

        yield return new Trigger
        {
            Id = "damning-edict-behind",
            On = EventKind.CastStart,
            MatchId = 0xBB01,
            Phase = 3,
            Make = ctx => new Call
            {
                Text = ctx.TargetIsMe ? "buster on you + get behind" : "get behind",
                Time = Lands(ctx),
                Key = "damning-edict",
                Level = CallLevel.Alarm,
                Personal = ctx.TargetIsMe,
            },
        };

        // Away from whichever of the two is casting it, which is the half of the
        // call the action id alone cannot give.
        yield return new Trigger
        {
            Id = "thunder-3-away",
            On = EventKind.CastStart,
            MatchId = 0xBB12,
            Phase = 3,
            Make = ctx => new Call
            {
                Text = "away from Exdeath",
                Time = Lands(ctx),
                Key = "thunder-3-away",
                Level = CallLevel.Alert,
            },
        };

        yield return new Trigger
        {
            Id = "thunder-3-swap",
            On = EventKind.AbilityHit,
            MatchId = 0xBB09,
            Phase = 3,
            For = "tank",
            Make = ctx => new Call
            {
                Text = ctx.TargetIsMe
                    ? "away from Exdeath (swap)"
                    : "be near Exdeath (swap)",
                Time = ctx.Event.Time,
                Key = "thunder-3-swap",
                Level = CallLevel.Alarm,
                Personal = ctx.TargetIsMe,
            },
        };

        // The stomp stack, which is one thing the first time and another the second.
        yield return new Trigger
        {
            Id = "knock-down-stack",
            On = EventKind.HeadMarker,
            MatchId = 0x00A1,
            Phase = 3,
            // Every marker of the burst, not just the first. Four of these land in
            // one instant, and under the burst rule the three that are not about
            // you arrive first and swallow the one that is.
            OncePerBurst = false,
            Make = ctx =>
            {
                // The same marker comes back in phase 5 meaning nothing but a
                // stack, and that one is answered there.
                if (ctx.Phase == 5) return null;

                var pull = Pull(ctx);
                var mine = ctx.TargetIsMe;
                var theirs = ctx.Party.RoleOf(ctx.Event.TargetId) == "dps";
                var same = theirs == AmDps(ctx);

                var stack = mine
                    ? "stack on you"
                    : same ? $"stack on {ctx.NameTarget()}" : "towers";

                return new Call
                {
                    Text = pull.SecondKnockDown ? stack : $"{stack} then {(mine || same ? "towers" : "stack")}",
                    Time = ctx.Event.Time,
                    // The one about you gets its own key, so it is a new call rather
                    // than a repeat of somebody else's and reaches the screen.
                    Key = mine ? "knock-down-mine" : "knock-down-stack",
                    Level = CallLevel.Alarm,
                    Personal = mine,
                };
            },
        };

        yield return new Trigger
        {
            Id = "blizzard-3-puddles",
            On = EventKind.CastStart,
            MatchId = 0xBB0F,
            Phase = 3,
            Make = ctx => new Call
            {
                Text = "bait puddles x2",
                Time = Lands(ctx),
                Key = "blizzard-3-puddles",
                Level = CallLevel.Alert,
            },
        };
    }

    // The words rather than the digits, because this is read out loud and "1 in
    // line" comes out of the voice as a number rather than as a position.
    private static string Ordinal(int n) => n switch
    {
        1 => "first",
        2 => "second",
        3 => "third",
        _ => n.ToString(),
    };

    // Who the healers top up, and in what order.
    //
    // Two ways to read it, because groups do. By line is the order the fight itself
    // hands out. By role leans on the mechanic instead: it always lands on a healer
    // before it lands on a dps, so the healer is named first whatever order the
    // debuffs arrived in.
    //
    // Taken by value rather than by reference, because the sort below is a lambda
    // and a by-reference context cannot be captured by one.
    private static List<uint> AccretionOrder(TriggerContext ctx, DancingMadPull pull)
    {
        if (ctx.Running(AccretionStrat, "role") && pull.Accretions.Count > 0)
            // A stable sort, so two players of the same role keep the order they
            // arrived in rather than swapping about between pulls.
            return pull.Accretions
                .OrderBy(id => ctx.Party.RoleOf(id) == "healer" ? 0 : 1)
                .ToList();

        var byLine = new List<uint>(2);
        if (pull.FirstAccretion != 0) byLine.Add(pull.FirstAccretion);
        if (pull.SecondAccretion != 0) byLine.Add(pull.SecondAccretion);
        return byLine.Count > 0 ? byLine : pull.Accretions.ToList();
    }

    // Fire spreads and water drops a donut, and whoever is not meleeing also has a
    // crystal to bait on the way out.
    private static Trigger ElementCall(string id, uint status, string element, string shape) => new()
    {
        Id = id,
        On = EventKind.StatusGain,
        MatchId = status,
        Phase = 3,
        Make = ctx =>
        {
            var pull = Pull(ctx);
            var mine = pull.MyElement == element;
            var who = element == "fire" ? pull.FirePlayers : pull.WaterPlayers;
            if (who.Count == 0) return null;

            var text = $"{shape} on {Names(ctx, who)}";

            // The tanks and the melee are already standing on the boss, so they
            // have no crystal to walk to.
            var stuck = Audience.RoleOf(ctx.MySlot) == "tank" || IsMelee(ctx.MySlot);
            if (!stuck)
            {
                var dir = element == "fire" ? pull.FireCrystal : pull.WaterCrystal;
                text = dir == DancingMadPull.Nowhere
                    ? $"{text} then bait the {element}"
                    : $"{text} then bait {element} {Way(dir)}";
            }

            return new Call
            {
                Text = text,
                Time = ctx.Event.Time + Math.Max(0f, ctx.Event.Duration - 5f),
                Key = $"element-{element}",
                Level = mine ? CallLevel.Alarm : CallLevel.Info,
                Personal = mine,
            };
        },
    };

    // Which way round the boss to run, and what waits at the end of it.
    private static Trigger SlapHappy(string id, uint action, bool right) => new()
    {
        Id = id,
        On = EventKind.CastStart,
        MatchId = action,
        Phase = 3,
        Make = ctx =>
        {
            var at = ctx.Event.Source;
            if (!at.Known)
                return new Call
                {
                    Text = $"out of middle + {(right ? "party stack" : "role stacks")}",
                    Time = Lands(ctx),
                    Key = "slap-happy",
                    Level = CallLevel.Alarm,
                };

            var boss = Compass.Dir8(at);
            // Two eighths round from the boss, the way the slap is not coming from.
            var safe = Compass.Wrap(boss + (right ? 2 : 6), 8);
            return new Call
            {
                Text = $"{Way(safe)} + {(right ? "party stack" : "role stacks")} then out",
                Time = Lands(ctx),
                Key = "slap-happy",
                Level = CallLevel.Alarm,
            };
        },
    };

    // One of the ten black hole moments.
    //
    // Which tether you take, and whether you keep or pass the one you have, is the
    // group's own answer. Every branch below is the source's; nothing here invents
    // an order for a group that has not picked one, which is what "off" means.
    private static Trigger HoleSet(string id, EventKind on, uint match, int nth) => new()
    {
        Id = id,
        On = on,
        MatchId = match,
        Phase = 3,
        OncePerBurst = false,
        Make = ctx =>
        {
            var pull = Pull(ctx);
            if (pull.Nothingness != nth) return null;

            // The sets that hand out tethers wait until every one of them has been
            // seen, or the order is read off half a set.
            var wanted = nth switch
            {
                1 or 10 => 1,
                2 or 9 => 2,
                3 or 6 => 3,
                _ => 0,
            };
            if (wanted > 0)
            {
                if (nth is 1 or 10)
                {
                    var one = HoleDir(ctx);
                    if (one == DancingMadPull.Nowhere) return null;
                    DancingMadPull.Note(pull.HoleDirs, one, 4);
                }
                if (pull.HoleDirs.Count != wanted || pull.HolesCalled) return null;
                pull.HolesCalled = true;
            }

            var order = HoleOrder(pull);
            var said = HoleWork(ctx, pull, order, nth);
            var mine = said.Length > 0;

            // Nothing to do and nowhere known to say it about is nothing to say. The
            // alternative was a call reading "4: 4", which is the set's number twice
            // and no information at all.
            if (!mine)
            {
                if (order.Count == 0) return null;
                said = HoleList(order);
            }

            return new Call
            {
                Text = $"{nth}: {said}",
                Time = ctx.Event.Time,
                Key = "black-hole",
                // Naming where they are is worth knowing; being sent to one is worth
                // acting on, and only one of those should shout.
                Level = mine ? CallLevel.Alarm : CallLevel.Info,
                Personal = mine,
            };
        },
    };

    // What this player does at one black hole moment, or empty to fall back to
    // simply naming where they are.
    // Taken by value rather than by reference, because the branches below are local
    // functions and a by-reference context cannot be captured by one.
    private static string HoleWork(
        TriggerContext ctx, DancingMadPull pull, IReadOnlyList<int> order, int nth)
    {
        var plan = ctx.Strat(HoleStrat);
        if (plan is "" or "none") return "";

        // Two of the three orders move the non-dps first, and the third moves both
        // at once. Everything below reads off that one difference.
        var dpsLeads = plan is "dsa" or "modified";
        var doubled = plan == "modified";
        var line = pull.InLine.GetValueOrDefault(ctx.Player.MyId);
        var dps = AmDps(ctx);
        var had = pull.HadAccretion;

        string Take(int at) => $"take the {HoleName(ctx, order, at)} tether";
        string Later(int at) => $"middle then {HoleName(ctx, order, at)} tether";

        return nth switch
        {
            1 => First(),
            2 => Second(),
            3 => Third(),
            4 => Fourth(),
            5 => Fifth(),
            6 => Sixth(),
            7 => Seventh(),
            8 => Eighth(),
            9 => Ninth(),
            10 => Tenth(),
            _ => "",
        };

        string First()
        {
            if (line != 1 || had) return "";
            var mine = plan is "sda" or "modified" ? !dps : dps;
            if (mine) return Take(0);
            if (doubled) return "middle then both tethers";
            return Later(dps ? 0 : 1);
        }

        string Second()
        {
            if (line != 1 || had) return "";
            if (doubled) return dps ? "take both tethers" : "";
            return Take(dps ? 0 : 1);
        }

        string Third()
        {
            if (line == 1)
                return had ? Take(2) : Take(dps ? 0 : 1);
            if (line == 2 && !had && (dpsLeads ? dps : !dps))
                return Later(dpsLeads ? 0 : 1);
            return "";
        }

        string Fourth()
        {
            if (line == 1)
                return had || (dpsLeads ? !dps : dps) ? "keep the tether" : "pass the tether";
            if (line != 2 || had) return "";
            return (dpsLeads ? dps : !dps) ? Take(dpsLeads ? 0 : 1) : Later(dpsLeads ? 1 : 0);
        }

        string Fifth()
        {
            var swaps = dpsLeads ? !dps : dps;
            if (line == 1)
                return had ? "keep the tether" : swaps ? "pass the tether" : "";
            if (line != 2 || had) return "";
            return swaps ? Take(dpsLeads ? 1 : 0) : "keep the tether";
        }

        string Sixth()
        {
            if (line == 2)
                return had ? Take(2) : Take(dps ? 0 : 1);
            if (line == 3 && (dpsLeads ? dps : !dps))
                return Later(dpsLeads ? 0 : 1);
            return "";
        }

        string Seventh()
        {
            if (line == 2)
                return had || (dpsLeads ? !dps : dps) ? "keep the tether" : "pass the tether";
            if (line != 3) return "";
            return (dpsLeads ? dps : !dps) ? Take(dpsLeads ? 0 : 1) : Later(dpsLeads ? 1 : 0);
        }

        string Eighth()
        {
            if (line == 2)
                return had ? "keep the tether" : (dpsLeads ? !dps : dps) ? "pass the tether" : "";
            if (line != 3) return "";
            return (dpsLeads ? dps : !dps) ? "keep the tether" : Take(dpsLeads ? 1 : 0);
        }

        string Ninth()
        {
            if (line != 3) return "";
            if (doubled) return dps ? "" : "take both tethers";
            return Take(dps ? 0 : 1);
        }

        string Tenth()
        {
            if (line != 3) return "";
            var mine = plan is "sda" or "modified" ? dps : !dps;
            return mine ? Take(0) : "";
        }
    }
}
