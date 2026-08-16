namespace FrenAlerts.Engine;

public static class Audience
{
    // The slot standard the sheets use, in party order.
    public static readonly string[] Slots = ["MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2"];

    public static string RoleOf(string slot) => slot.ToUpperInvariant() switch
    {
        "MT" or "OT" => "tank",
        "H1" or "H2" => "healer",
        "M1" or "M2" or "R1" or "R2" => "dps",
        _ => "",
    };

    public static bool IsSlot(string token) =>
        Slots.Contains(token.ToUpperInvariant());

    public static bool Includes(string audience, string slot)
    {
        if (string.IsNullOrWhiteSpace(audience)) return true;
        if (string.IsNullOrWhiteSpace(slot)) return false;

        var role = RoleOf(slot);
        foreach (var raw in audience.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(raw, slot, StringComparison.OrdinalIgnoreCase)) return true;
            if (role.Length > 0 && string.Equals(raw, role, StringComparison.OrdinalIgnoreCase)) return true;

            // "tanks" and "healers" read naturally in an authored line, so both the
            // singular and the plural resolve rather than silently matching nothing.
            if (role.Length > 0 && raw.EndsWith('s')
                && string.Equals(raw[..^1], role, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
