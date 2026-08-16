namespace FrenAlerts.Engine;

// M12S's Top-Tier Slam, where one clone is the fire one and everything else is
// read off where it stands.
//
// Fire is baited by pairs and dark alone, and the two take opposite spots: if
// fire is baited inside, dark is baited outside on the same line.
public static class LindwurmSlam
{
    // The elemental resistance downs the replication hands out.
    private const uint DarkDebuff = 0xCFB;
    private const uint FireDebuff = 0xB79;

    // Winged Scourge, cast by every clone. The fire one is told apart by having
    // moved: the rest sit on whole numbers and face a perfect heading.
    private const uint WingedScourge = 0xB4D9;

    // Inside this band across the arena the clone went in rather than out.
    private const float InsideFrom = 94f;
    private const float InsideTo = 106f;

    private static LindwurmPull Pull(in TriggerContext ctx) =>
        ctx.State.Remember<LindwurmPull>();

    // Whether this clone is the one that moved. The others never do, and they show
    // it by sitting on exact coordinates with an exact heading.
    public static bool Moved(Position at) =>
        at.Known && !(IsWhole(at.X) && IsWhole(at.Y) && at.Heading == 0f);

    private static bool IsWhole(float v) => MathF.Abs(v - MathF.Round(v)) < 0.0001f;

    // Where to bait each element, given where the fire clone ended up.
    //
    // The clone's own spot and the far side of it are the two lines, and which of
    // them is the inside one depends on whether the clone went in or out.
    public static (string In, string Out) BaitAt(Position fireClone, bool mine)
    {
        var here = Compass.Dir8(fireClone);
        var there = Compass.Opposite8(here);

        var wentIn = fireClone.X > InsideFrom && fireClone.X < InsideTo;
        var fireIn = wentIn ? here : there;
        var fireOut = wentIn ? there : here;

        // Dark is the opposite pattern of fire, always.
        return mine
            ? (Compass.Name8(fireIn), Compass.Name8(fireOut))
            : (Compass.Name8(fireOut), Compass.Name8(fireIn));
    }

    public static IEnumerable<Trigger> Triggers()
    {
        yield return Debuff("m12s-replication-dark", DarkDebuff, "dark");
        yield return Debuff("m12s-replication-fire", FireDebuff, "fire");

        // The fire clone, and the call in one. Only the clone that moved is it, so
        // the others cast this and say nothing.
        yield return new Trigger
        {
            Id = "m12s-top-tier-slam",
            On = EventKind.AbilityHit,
            MatchId = WingedScourge,
            Hush = 5f,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.MyElement.Length == 0) return null;
                if (!Moved(ctx.Event.Source)) return null;

                // Fire players bait dark and dark players bait fire: whichever you
                // are carrying is the one you are not standing in.
                var baiting = pull.MyElement == "fire" ? "dark" : "fire";
                var (inside, outside) = BaitAt(ctx.Event.Source, baiting == "fire");
                var how = baiting == "fire" ? "partners" : "solo";

                return new Call
                {
                    Text = $"bait {baiting} in {inside}, out {outside} ({how})",
                    Time = ctx.Event.Time,
                    Key = "m12s-top-tier-slam",
                    Level = CallLevel.Alert,
                    Personal = true,
                    Hold = 8f,
                };
            },
        };
    }

    private static Trigger Debuff(string id, uint status, string element) => new()
    {
        Id = id,
        On = EventKind.StatusGain,
        MatchId = status,
        OnlyMe = true,
        Claims = true,
        Make = ctx =>
        {
            Pull(ctx).MyElement = element;
            return null;
        },
    };
}
