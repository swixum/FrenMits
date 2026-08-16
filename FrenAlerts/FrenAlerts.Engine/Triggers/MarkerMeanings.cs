namespace FrenAlerts.Engine;

public static class MarkerMeanings
{
    public readonly record struct Meaning(uint Id, string Says, bool IsTether);

    public static readonly Dictionary<ushort, Meaning[]> ByFight = new()
    {
        // Dancing Mad: 25 ids, measured against 44GB of real pulls.
        [1363] =
        [
            new(0x002D, "image tether", true),
            new(0x0040, "exdeath tether", true),
            new(0x0054, "black hole tether", true),
            new(0x007F, "spread", false),
            new(0x0080, "stack", false),
            new(0x00A1, "stomp stack", false),
            // Revolting Ruin III is a cone in the game's own shape table, so the
            // people behind the tank are in it too.
            new(0x00DA, "cleave", false),
            new(0x0103, "share the buster", false),
            new(0x0150, "1", false),
            new(0x0151, "2", false),
            new(0x0152, "3", false),
            new(0x0153, "4", false),
            new(0x01B5, "5", false),
            new(0x01B6, "6", false),
            new(0x01B7, "7", false),
            new(0x01B8, "8", false),
            new(0x02A1, "fake fire", false),
            new(0x02A2, "true fire", false),
            new(0x02A3, "fake ice", false),
            new(0x02A4, "true ice", false),
            new(0x02A5, "fake thunder", false),
            new(0x02A6, "true thunder", false),
            new(0x02CB, "stack path", false),
            new(0x02CC, "spread path", false),
            new(0x02CD, "cone path", false),
        ],
        // M10S: 9 ids, unverified, no recording of this fight has one.
        [1323] =
        [
            new(0x0103, "share the buster", false),
            new(0x0174, "sick swell tether", true),
            new(0x017A, "far tether", true),
            new(0x017B, "close tether", true),
            new(0x027B, "blue tether", true),
            new(0x027C, "red tether", true),
            new(0x0293, "partner stack", false),
            new(0x0294, "spread fire puddle red", false),
            new(0x029A, "party stack fire", false),
        ],
        // M11S: 9 ids, unverified, no recording of this fight has one.
        [1325] =
        [
            new(0x001E, "atomic impact", false),
            new(0x0039, "close tether", true),
            new(0x008B, "comet spread", false),
            new(0x00A1, "partner stack", false),
            new(0x00F4, "meteor", false),
            new(0x00F9, "far tether", true),
            new(0x0131, "five hit stack", false),
            new(0x0164, "meteor tether", true),
            new(0x020D, "line stack", false),
        ],
        // M12S: 12 ids, unverified, no recording of this fight has one.
        [1327] =
        [
            new(0x00A1, "stack", false),
            new(0x013D, "slaughter stack", false),
            new(0x0158, "buster", false),
            new(0x016E, "cell chain tether", true),
            new(0x016F, "projection tether", true),
            new(0x0170, "mana burst tether", true),
            new(0x0171, "heavy slam tether", true),
            new(0x0175, "locked tether", true),
            new(0x0176, "fireball splash tether", true),
            new(0x0177, "slaughter spread", false),
            new(0x0256, "buster", false),
            new(0x0291, "cell chain", false),
        ],
        // M9S: 5 ids, unverified, no recording of this fight has one.
        [1321] =
        [
            new(0x0131, "multi hit stack", false),
            new(0x0161, "tether close", true),
            new(0x0162, "tether far", true),
            new(0x01D4, "buster", false),
            new(0x028C, "aetherletting", false),
        ],
    };

    public static bool TryFor(ushort territory, EventKind kind, uint id, out string says)
    {
        says = "";
        if (kind is not (EventKind.HeadMarker or EventKind.Tether)) return false;
        if (!ByFight.TryGetValue(territory, out var known)) return false;

        var wantTether = kind == EventKind.Tether;
        foreach (var m in known)
        {
            if (m.Id != id || m.IsTether != wantTether) continue;
            says = m.Says;
            return true;
        }
        return false;
    }
}
