namespace FrenAlerts.Engine.Alerts;

public enum CallIconKind
{
    None = 0,

    // A status that landed on you: the debuff's own game icon.
    Status,

    Marker,

    // A game icon by its own number, which is what somebody picks in a hand-written
    // trigger: their editor asks for the icon rather than for the thing that has it.
    Sheet,
}

public readonly record struct CallIcon(CallIconKind Kind, uint Id)
{
    public static readonly CallIcon None = new(CallIconKind.None, 0);

    public static CallIcon Status(uint statusId) => new(CallIconKind.Status, statusId);

    public static CallIcon Marker(uint markerId) => new(CallIconKind.Marker, markerId);

    // Zero is no icon rather than icon zero. A trigger that picked none would
    // otherwise still reserve the space one takes, leaving every call it makes
    // sitting off centre with a gap beside it.
    public static CallIcon Sheet(uint iconId) =>
        iconId == 0 ? None : new(CallIconKind.Sheet, iconId);

    public bool Any => Kind != CallIconKind.None;

    public static CallIcon For(in GameEvent e, uint me) => e.Kind switch
    {
        EventKind.StatusGain when e.TargetId == me && me != 0 => Status(e.Id),
        EventKind.HeadMarker when e.TargetId == me && me != 0 => Marker(e.Id),
        _ => None,
    };
}
