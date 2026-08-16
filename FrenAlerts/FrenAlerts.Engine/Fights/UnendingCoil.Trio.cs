namespace FrenAlerts.Engine;

// What one pull of the Unending Coil has told us so far.
//
// Lives on the fight state so it dies with the pull, the same as DancingMadPull.
public sealed class UnendingCoilPull
{
    // Which of the six trio sections is running. Every call below is gated on it,
    // because the three bosses are the same actors doing different things each time.
    public string Trio = "";

    // Which actor each of the three is, learned when they dive in Quickmarch and
    // reused for the rest of the phase.
    public uint NaelId;
    public uint BahamutId;
    public uint TwinId;

    // Where each of the three was standing when Heavensfall lined them up. Keyed by
    // actor so they can arrive in any order, and bounded at three because that is
    // how many bosses there are.
    public readonly Dictionary<uint, float> TrioAngle = new(3);

    // Which of Twintania's pushes has been reached. Counts from one, and the
    // fourth is the last, so nothing is said past it.
    public int TwinPhase = 1;

    // Said once per section, so re-entering a section is what clears it.
    public string SaidNaelDir = "";

    // The eight Heavensfall towers, as they are cast. Bounded at the eight the
    // mechanic has: a ninth is a tower from the next set, not this one.
    public const int Towers = 8;
    public readonly List<(float Angle, int Dir16)> TowerSpots = new(Towers);
    public bool SaidTower;

    public void NewTrio(string trio)
    {
        Trio = trio;
        TrioAngle.Clear();
        TowerSpots.Clear();
        SaidNaelDir = "";
        SaidTower = false;
    }
}

public static partial class UnendingCoilTrio
{
    // Bahamut opens each section with its own cast.
    private static readonly (uint Cast, string Trio)[] Sections =
    [
        (0x26E2, "quickmarch"),
        (0x26E3, "blackfire"),
        (0x26E4, "fellruin"),
        (0x26E5, "heavensfall"),
        (0x26E6, "tenstrike"),
        (0x26E7, "octet"),
    ];

    // The dive each of the three casts in Quickmarch, which is where they are told
    // apart. Nothing else in the phase names them.
    private const uint NaelDive = 0x26C3;
    private const uint BahamutDive = 0x26E1;
    private const uint TwinDive = 0x26B2;

    // The neurolink prop, by its base id.
    private const uint Neurolink = 0x1E88FF;

    // Twintania has three pushes; the fourth phase is the one she does not reach.
    private const int LastTwinPhase = 4;

    // This arena is measured from the origin, not from the hundred the rest of the
    // game uses. Reading it as 100/100 puts every boss in the same quadrant.
    public const float Center = 0f;

    private static UnendingCoilPull Pull(in TriggerContext ctx) =>
        ctx.State.Remember<UnendingCoilPull>();

    // Clockwise degrees from north, which is what the three bosses are compared in.
    public static float AngleOf(Position at) =>
        Wrap360(Compass.Angle(at.X, at.Y, Center, Center) * 180f / MathF.PI);

    private static float Wrap360(float deg) => ((deg % 360f) + 360f) % 360f;

    // Whether going clockwise from one angle reaches the other first.
    //
    // Equal is not clockwise, which is upstream's answer and matters: two bosses
    // read as standing in the same place must not both be called left.
    public static bool IsClockwise(float start, float compare) =>
        compare > start ? compare - start <= 180f
        : compare < start && start - compare >= 180f;

    public static IEnumerable<Trigger> Triggers()
    {
        // A neurolink appearing is Twintania being pushed to the next phase. There
        // is no cast for it, so the prop turning up is the only announcement.
        yield return new Trigger
        {
            Id = "ucob-twin-push",
            On = EventKind.ActorSpawn,
            MatchId = Neurolink,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.TwinPhase >= LastTwinPhase) return null;

                var said = pull.TwinPhase++;
                return new Call
                {
                    Text = $"phase {said} push",
                    Time = ctx.Event.Time,
                    Key = "ucob-twin-push",
                    Level = CallLevel.Info,
                };
            },
        };

        // Which section is running. Says nothing itself.
        yield return new Trigger
        {
            Id = "ucob-trio",
            On = EventKind.CastStart,
            Claims = true,
            OncePerBurst = false,
            Make = ctx =>
            {
                foreach (var (cast, trio) in Sections)
                    if (ctx.Event.Id == cast)
                    {
                        Pull(ctx).NewTrio(trio);
                        break;
                    }
                return null;
            },
        };

        // Who is who, learned from the dives in the first section.
        yield return new Trigger
        {
            Id = "ucob-trio-who",
            On = EventKind.CastStart,
            Claims = true,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.Trio != "quickmarch") return null;
                switch (ctx.Event.Id)
                {
                    case NaelDive: pull.NaelId = ctx.Event.SourceId; break;
                    case BahamutDive: pull.BahamutId = ctx.Event.SourceId; break;
                    case TwinDive: pull.TwinId = ctx.Event.SourceId; break;
                }
                return null;
            },
        };

        // Blackfire: Nael teleports and where she lands is the call. Once only,
        // because she stays put for the rest of the section.
        yield return new Trigger
        {
            Id = "ucob-blackfire-nael",
            On = EventKind.ActorMoved,
            OncePerBurst = false,
            Phase = 3,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.Trio != "blackfire" || pull.NaelId == 0) return null;
                if (ctx.Event.SourceId != pull.NaelId) return null;
                if (pull.SaidNaelDir.Length > 0 || !ctx.Event.Source.Known) return null;

                var dir = Compass.Name8(Compass.Dir8(ctx.Event.Source, Center, Center));
                pull.SaidNaelDir = dir;

                return new Call
                {
                    Text = $"Nael is {dir}",
                    Time = ctx.Event.Time,
                    Key = "ucob-blackfire-nael",
                    Level = CallLevel.Alert,
                };
            },
        };

        // Heavensfall: the three line up side by side and which one Nael is decides
        // where you go. Angles rather than compass points, because they stand
        // adjacent and two of them routinely land in the same eighth.
        yield return new Trigger
        {
            Id = "ucob-heavensfall-nael",
            On = EventKind.ActorMoved,
            OncePerBurst = false,
            Phase = 3,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.Trio != "heavensfall" || !ctx.Event.Source.Known) return null;

                var who = ctx.Event.SourceId;
                if (who != pull.NaelId && who != pull.BahamutId && who != pull.TwinId) return null;

                // First position each of them takes, so a later shuffle cannot
                // rewrite a call already made.
                if (!pull.TrioAngle.TryAdd(who, AngleOf(ctx.Event.Source))) return null;
                if (pull.TrioAngle.Count < 3 || pull.SaidNaelDir.Length > 0) return null;

                var spot = NaelSpot(
                    pull.TrioAngle[pull.NaelId],
                    pull.TrioAngle[pull.BahamutId],
                    pull.TrioAngle[pull.TwinId]);
                pull.SaidNaelDir = spot;

                return new Call
                {
                    Text = $"{spot} Nael",
                    Time = ctx.Event.Time,
                    Key = "ucob-heavensfall-nael",
                    Level = CallLevel.Alert,
                };
            },
        };

        // Heavensfall: eight towers go up and yours is counted round from Nael.
        // Silent until the group has picked a seat, because every seat is a real
        // tower and naming the wrong one puts two people in it.
        yield return new Trigger
        {
            Id = "ucob-heavensfall-tower",
            On = EventKind.CastStart,
            MatchId = 0x26DF,
            OncePerBurst = false,
            Phase = 3,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.Trio != "heavensfall" || !ctx.Event.Source.Known) return null;
                if (pull.TowerSpots.Count >= UnendingCoilPull.Towers) return null;

                pull.TowerSpots.Add((
                    AngleOf(ctx.Event.Source),
                    Compass.Dir16(ctx.Event.Source, Center, Center)));

                if (pull.SaidTower) return null;
                if (!pull.TrioAngle.TryGetValue(pull.NaelId, out var naelAngle)) return null;

                var seat = ctx.Strat("heavensfallTower");
                if (!int.TryParse(seat, out var wanted)) return null;

                if (TowerFor(pull.TowerSpots, naelAngle, wanted) is not { } dir) return null;
                pull.SaidTower = true;

                return new Call
                {
                    Text = $"tower {Compass.Name16(dir)}",
                    Time = ctx.Event.Time,
                    Key = "ucob-heavensfall-tower",
                    Level = CallLevel.Info,
                    Hold = 8f,
                };
            },
        };

        // Grand Octet: where the party starts and which way it walks.
        yield return new Trigger
        {
            Id = "ucob-grand-octet",
            On = EventKind.ActorMoved,
            OncePerBurst = false,
            Phase = 3,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.Trio != "octet" || !ctx.Event.Source.Known) return null;

                var who = ctx.Event.SourceId;
                if (who != pull.NaelId && who != pull.BahamutId && who != pull.TwinId) return null;
                if (!pull.TrioAngle.TryAdd(who, AngleOf(ctx.Event.Source))) return null;
                if (pull.TrioAngle.Count < 3 || pull.SaidNaelDir.Length > 0) return null;

                var (start, path) = Octet(
                    Dir8At(pull.TrioAngle[pull.NaelId]),
                    Dir8At(pull.TrioAngle[pull.BahamutId]));
                pull.SaidNaelDir = path;

                return new Call
                {
                    Text = $"start {Compass.Name8(start)}, go {path}",
                    Time = ctx.Event.Time,
                    Key = "ucob-grand-octet",
                    Level = CallLevel.Alert,
                    Hold = 8f,
                };
            },
        };
    }

    // The stored angles are degrees clockwise from north, which is the same ring
    // the eight are counted in.
    public static int Dir8At(float degrees) =>
        Compass.Wrap((int)MathF.Round(degrees / 45f), 8);

    // Which tower is yours, counted round from the one Nael is standing at.
    //
    // The towers are sorted by angle, the one at or after Nael's angle is the
    // count's origin, and your seat is that many places further round. Null when
    // the group has not said which seat it takes, because every seat is a real
    // tower and naming the wrong one puts two people in it.
    public static int? TowerFor(
        IReadOnlyList<(float Angle, int Dir16)> towers, float naelAngle, int seat)
    {
        if (towers.Count < UnendingCoilPull.Towers) return null;

        var sorted = towers.OrderBy(t => t.Angle).ToList();

        // No tower past Nael means she is past the last of them, so the count wraps
        // to the first. Upstream lands on the same place by adding eight to minus one.
        var naelIdx = sorted.FindIndex(t => t.Angle >= naelAngle);
        if (naelIdx < 0) naelIdx = UnendingCoilPull.Towers - 1;

        return sorted[(seat + naelIdx) % UnendingCoilPull.Towers].Dir16;
    }

    // Where the party starts the Grand Octet rotation and which way it goes.
    //
    // Bahamut on a cardinal turns the party counterclockwise, on an intercardinal
    // clockwise. They start opposite him, unless Nael is already standing there, in
    // which case they shift one seat the way they are about to rotate.
    public static (int Start, string Path) Octet(int naelDir8, int bahamutDir8)
    {
        var cardinal = bahamutDir8 % 2 == 0;
        var step = cardinal ? -1 : 1;
        var path = cardinal ? "counterclockwise" : "clockwise";

        var start = Compass.Opposite8(bahamutDir8);
        if (naelDir8 == start) start = Compass.Wrap(start + step, 8);

        return (start, path);
    }

    // Which of the three Nael is, read left to right across the line they form.
    public static string NaelSpot(float nael, float bahamut, float twin) =>
        IsClockwise(nael, bahamut)
            ? IsClockwise(nael, twin) ? "left" : "middle"
            : IsClockwise(nael, twin) ? "middle" : "right";
}
