namespace FrenAlerts.Engine.Alerts;

public enum CallIconKind
{
    None = 0,

    // A status that landed on you: the debuff's own game icon.
    Status,

    Marker,
}

public readonly record struct CallIcon(CallIconKind Kind, uint Id)
{
    public static readonly CallIcon None = new(CallIconKind.None, 0);

    public static CallIcon Status(uint statusId) => new(CallIconKind.Status, statusId);

    public static CallIcon Marker(uint markerId) => new(CallIconKind.Marker, markerId);

    public bool Any => Kind != CallIconKind.None;

    public static CallIcon For(in GameEvent e, uint me) => e.Kind switch
    {
        EventKind.StatusGain when e.TargetId == me && me != 0 => Status(e.Id),
        EventKind.HeadMarker when e.TargetId == me && me != 0 => Marker(e.Id),
        _ => None,
    };
}
