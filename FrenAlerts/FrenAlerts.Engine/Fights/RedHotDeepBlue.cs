namespace FrenAlerts.Engine;

public sealed class RedHotDeepBluePull
{
    // Which way the wave knocks, learned from where the tethered player stands.
    // Minus one until a tether has landed, because north is a real answer.
    public int WaveDir4 = -1;

    // The pair of snaking mechanics currently up, water first. Bounded at the two
    // that make a call: a third would be the next set, not this one.
    public readonly List<(string Elem, string Mech)> Snakings = new(2);

    // How many fire snakings have gone by, which is what turns the call from a
    // pair of mechanics into a role swap.
    public int SnakingCount;
}

// M10S, Red Hot & Deep Blue.
public static class RedHotDeepBlue
{
    public const ushort Territory = 1323;

    private const uint SickSwellTether = 0x0174;
    private const uint SickSwell = 0xB5CE;
    private const uint BlueTether = 0x027B;
    private const uint RedTether = 0x027C;

    // The arena's own edges. Inside these the cast is middle rather than a side.
    private const float Near = 95f;
    private const float Far = 105f;

    // The nine platforms, by the index the map effect reports.
    private static readonly uint[] SnakingSlots =
        [0x16, 0x0F, 0x10, 0x15, 0x0E, 0x11, 0x14, 0x13, 0x12];

    // What each flag pattern means. Upstream's table, unchanged.
    private static readonly Dictionary<uint, (string Elem, string Mech)> SnakingFlags = new()
    {
        [0x00020001] = ("water", "protean"),
        [0x00200010] = ("water", "stack"),
        [0x00800040] = ("water", "buster"),
        [0x02000100] = ("fire", "protean"),
        [0x08000400] = ("fire", "stack"),
        [0x20001000] = ("fire", "buster"),
    };

    // Past this many fires the call stops naming both mechanics and names the role
    // that swaps instead.
    private const int SwapsAfter = 5;

    private static RedHotDeepBluePull Pull(in TriggerContext ctx) =>
        ctx.State.Remember<RedHotDeepBluePull>();

    public static IEnumerable<Trigger> Triggers()
    {
        // Where the tethered player is standing is which way the wave will push.
        yield return new Trigger
        {
            Id = "m10s-wave-dir",
            On = EventKind.Tether,
            MatchId = SickSwellTether,
            Claims = true,
            OncePerBurst = false,
            Make = ctx =>
            {
                var at = ctx.Actors.Where(ctx.Event.TargetId);
                if (at.Known) Pull(ctx).WaveDir4 = Compass.Dir4(at);
                return null;
            },
        };

        // The knockback, plus which end of the arena the cast is at, because that
        // is the half you have to be standing in when it lands.
        yield return new Trigger
        {
            Id = "m10s-sick-swell",
            Says = "knock north, middle",
            On = EventKind.CastStart,
            MatchId = SickSwell,
            OncePerBurst = false,
            Make = ctx =>
            {
                var pull = Pull(ctx);
                if (pull.WaveDir4 < 0 || !ctx.Event.Source.Known) return null;

                return new Call
                {
                    Text = $"knock {Compass.Name4(pull.WaveDir4)}, "
                         + $"{Half(pull.WaveDir4, ctx.Event.Source)}",
                    // Counts down to the cast landing rather than to now.
                    Time = ctx.Event.Time + ctx.Event.CastTime,
                    Key = "m10s-sick-swell",
                    Level = CallLevel.Alert,
                    Hold = 6f,
                };
            },
        };

        // The snaking platforms. Water always resolves first, so it goes to the
        // front of the pair however the two arrive.
        yield return new Trigger
        {
            Id = "m10s-snaking",
            Says = "north / west / south",
            On = EventKind.MapEffect,
            OncePerBurst = false,
            Make = ctx =>
            {
                if (!SnakingSlots.Contains(ctx.Event.TargetId)) return null;
                if (!SnakingFlags.TryGetValue(ctx.Event.Id, out var snaking)) return null;

                var pull = Pull(ctx);
                if (pull.Snakings.Count >= 2) pull.Snakings.Clear();

                if (snaking.Elem == "water") pull.Snakings.Insert(0, snaking);
                else pull.Snakings.Add(snaking);

                // A fire buster past the fourth is the same set repeating rather
                // than a new one, so it does not move the count on.
                if (snaking.Elem == "fire"
                    && (snaking.Mech != "buster" || pull.SnakingCount < 4))
                    pull.SnakingCount++;

                if (pull.Snakings.Count < 2) return null;

                return new Call
                {
                    Text = Snaking(pull.Snakings[0], pull.Snakings[1], pull.SnakingCount),
                    Time = ctx.Event.Time,
                    Key = "m10s-snaking",
                    Level = CallLevel.Info,
                    Hold = 6f,
                };
            },
        };

        yield return Tether("m10s-blue-tether", BlueTether, "blue tether on you");
        yield return Tether("m10s-red-tether", RedTether, "red tether on you");
    }

    // Which end to stand at, read across the axis the wave does not push along.
    public static string Half(int waveDir4, Position cast)
    {
        var sideways = waveDir4 is 1 or 3;
        var along = sideways ? cast.Y : cast.X;

        if (along < Near) return sideways ? "north" : "west";
        if (along > Far) return sideways ? "south" : "east";
        return "middle";
    }

    // What the two platforms are asking for, or once the fires stack up, which
    // role is swapping out.
    public static string Snaking(
        (string Elem, string Mech) water, (string Elem, string Mech) fire, int count)
    {
        if (count < SwapsAfter) return $"water {water.Mech}, fire {fire.Mech}";

        var swap = water.Mech == "buster" ? "tanks"
            : count == 5 ? "healers"
            : count == 6 ? "melee"
            : "ranged";

        return $"{water.Mech}, {swap} swap";
    }

    private static Trigger Tether(string id, uint marker, string says) => new()
    {
        Id = id,
        On = EventKind.HeadMarker,
        MatchId = marker,
        OnlyMe = true,
        Make = ctx => new Call
        {
            Text = says,
            Time = ctx.Event.Time,
            Key = id,
            Level = CallLevel.Alert,
            Personal = true,
        },
    };
}
