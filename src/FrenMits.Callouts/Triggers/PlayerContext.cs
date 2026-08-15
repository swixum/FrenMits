namespace FrenMits.Callouts;

// Who the calls are for. The host fills this in from the live party.
public sealed record PlayerContext
{
    public uint Id { get; init; }

    public string Name { get; init; } = "";

    public string Job { get; init; } = "";

    // MT, OT, H1, H2, M1, M2, R1, R2, matching the sheet standard.
    public string Slot { get; init; } = "";

    public string Role { get; init; } = "";

    public static readonly PlayerContext Unknown = new();

    public bool IsMe(Actor a) => Id != 0 && a.Id == Id;
}
