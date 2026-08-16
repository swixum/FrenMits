namespace FrenAlerts.Engine;

// M12S's blob towers, which are four corners in a fixed order.
//
// Only the first blob has to be seen. Where it lands decides the whole sequence,
// because the other three always follow it the same way round.
public static class LindwurmBlobs
{
    // Rolling Mass, the cast each blob makes as it lands.
    private const uint RollingMass = 0xB4B6;

    // Which corner each of the four towers is, given where the first blob was.
    // Upstream's table, and it is not a rotation: from the north east the order
    // crosses over rather than turning, so it cannot be worked out arithmetically.
    private static readonly Dictionary<int, int[]> Order = new()
    {
        // northeast, then southwest, northwest, southeast
        [1] = [1, 5, 7, 3],
        // southeast, then northwest, southwest, northeast
        [3] = [3, 7, 5, 1],
        // southwest, then northeast, southeast, northwest
        [5] = [5, 1, 3, 7],
        // northwest, then southeast, northeast, southwest
        [7] = [7, 3, 1, 5],
    };

    // Which tower each line number takes. Ones and twos take the outer pair, which
    // are the last two in the order, and threes and fours the inner pair.
    private static readonly int[] TowerIndexForNumber = [2, 3, 0, 1];

    private static LindwurmPull Pull(in TriggerContext ctx) =>
        ctx.State.Remember<LindwurmPull>();

    // The four corners in the order they go up, or empty if the first blob landed
    // somewhere that is not a corner.
    public static IReadOnlyList<int> TowersFrom(int firstDir8) =>
        Order.TryGetValue(firstDir8, out var order) ? order : [];

    // Which of the four is this player's, as an index into that order.
    public static int IndexFor(int myNumber) =>
        myNumber >= 1 && myNumber <= TowerIndexForNumber.Length
            ? TowerIndexForNumber[myNumber - 1]
            : -1;

    // The mitotic status, which hides which tower is yours in its own count
    // rather than in the status id. Four counts, four towers, relative to where
    // you are facing rather than to the compass.
    private const uint Mitotic = 0xDE6;

    private static readonly Dictionary<ushort, string> MitoticTower = new()
    {
        [436] = "front",
        [437] = "right",
        [438] = "rear",
        [439] = "left",
    };

    // Alpha bonds, which are what put a player on one of the first two towers.
    private const uint FleshAlpha = 0x1290;

    // Under this the bonds are one of the later pair, which a different call owns.
    private const float FirstTwoUnder = 35f;

    // Past this it is the second tower rather than the first.
    private const float SecondTowerOver = 40f;

    public static IEnumerable<Trigger> Triggers()
    {
        // The first blob down settles all four. Later ones say nothing.
        yield return new Trigger
        {
            Id = "m12s-blob-order",
            On = EventKind.CastStart,
            MatchId = RollingMass,
            Claims = true,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.BlobTowers.Count > 0 || !ctx.Event.Source.Known) return null;

                pull.BlobTowers.AddRange(TowersFrom(Compass.Dir8(ctx.Event.Source)));
                return null;
            },
        };

        // Which tower to take, read off the count the status carries. The id is
        // the same for all four, so the count is the only thing that says it.
        yield return new Trigger
        {
            Id = "m12s-mitotic-tower",
            On = EventKind.StatusGain,
            MatchId = Mitotic,
            OnlyMe = true,
            Make = ctx => !MitoticTower.TryGetValue(ctx.Event.Param, out var where)
                ? null
                : new Call
                {
                    Text = $"{where} tower",
                    Time = ctx.Event.Time,
                    Key = "m12s-mitotic-tower",
                    Level = CallLevel.Alert,
                    Personal = true,
                    Hold = 10f,
                },
        };

        // The first two towers are handed out by how long the bonds last, not by
        // the line number, so this reads the duration off the status itself.
        //
        // Counted down to rather than delayed: upstream waits twenty-six or
        // thirty-one seconds and then speaks, which this says at once with the
        // seconds attached, so the screen shows it approaching instead of nothing.
        yield return new Trigger
        {
            Id = "m12s-blob-tower-early",
            On = EventKind.StatusGain,
            MatchId = FleshAlpha,
            OnlyMe = true,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                var held = ctx.Event.Duration;
                if (held < FirstTwoUnder) return null;

                var second = held > SecondTowerOver;
                var index = second ? 1 : 0;
                var at = second ? 31f : 26f;

                var where = index < pull.BlobTowers.Count
                    ? $", inner {Compass.Name8(pull.BlobTowers[index])}"
                    : "";

                return new Call
                {
                    Text = $"blob tower {index + 1}{where}",
                    Time = ctx.Event.Time + at,
                    Key = "m12s-blob-tower-early",
                    Level = CallLevel.Alert,
                    Personal = true,
                    Hold = 8f,
                };
            },
        };

        // Alpha bonds are told their tower as the blobs land, well before they take
        // it, which is why upstream says "later" in the line.
        yield return new Trigger
        {
            Id = "m12s-blob-tower",
            On = EventKind.CastStart,
            MatchId = RollingMass,
            Hush = 10f,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.MyBonds != "alpha" || pull.MyNumber == 0) return null;

                var index = IndexFor(pull.MyNumber);
                if (index < 0 || index >= pull.BlobTowers.Count) return null;

                // Threes and fours are the inner pair; ones and twos the outer.
                var ring = pull.MyNumber > 2 ? "inner" : "outer";

                return new Call
                {
                    Text = $"blob tower {index + 1}, {ring} {Compass.Name8(pull.BlobTowers[index])}",
                    Time = ctx.Event.Time,
                    Key = "m12s-blob-tower",
                    Level = CallLevel.Info,
                    Personal = true,
                    Hold = 8f,
                };
            },
        };
    }
}
