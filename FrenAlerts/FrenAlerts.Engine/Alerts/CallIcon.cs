namespace FrenAlerts.Engine.Alerts;

// Where a call's picture comes from.
//
// Every one of these resolves through a sheet the game itself owns, so the art beside a
// call is always the game's own art for the thing being called. Nothing here holds an
// icon number picked by hand: a guessed number is a real row in the sheet and draws a
// real picture, of something else entirely, and there is no way to tell from the code
// that it is wrong.
//
// That is why there is no kind for a head marker. The head markers are VFX rather than
// sheet rows, so the game has no icon to hand over for one, and what used to stand in
// for it was a crosshair from the window's own glyph font.
public enum CallIconKind
{
    None = 0,

    // A status that landed on you: the debuff's own game icon.
    Status,

    // A game icon by its own number, which is what somebody picks in a hand-written
    // trigger: their editor asks for the icon rather than for the thing that has it.
    Sheet,
}

public readonly record struct CallIcon(CallIconKind Kind, uint Id)
{
    public static readonly CallIcon None = new(CallIconKind.None, 0);

    public static CallIcon Status(uint statusId) =>
        statusId == 0 ? None : new(CallIconKind.Status, statusId);

    public bool Any => Kind != CallIconKind.None;

    // Zero is no icon rather than icon zero. A trigger that picked none would
    // otherwise still reserve the space one takes, leaving every call it makes
    // sitting off centre with a gap beside it.
    public static CallIcon Sheet(uint iconId) =>
        iconId == 0 ? None : new(CallIconKind.Sheet, iconId);

    // The picture for the event a call came out of.
    //
    // A debuff and nothing else, which is what swix asked for: an icon is there to say
    // "this one is on you", and a picture beside every raidwide and every cone says
    // nothing at all while making the whole stack wider and busier.
    //
    // Casts used to carry the ability's own art here. It read as decoration rather than
    // information, because it fired on the ordinary calls too, so the icon stopped
    // meaning the one thing it was for.
    public static CallIcon For(in GameEvent e, uint me) =>
        e.Kind == EventKind.StatusGain && e.TargetId == me && me != 0 ? Status(e.Id) : None;

    // The picture for a call sitting on a page, where nothing has fired and there is
    // nobody it landed on.
    //
    // The debuff shows whoever it would land on, which is the one way this differs
    // from the live answer above: a list is describing the mechanic rather than
    // reporting a hit, and a row that hides its icon until the night it happens to be
    // yours is a row that looks like a different call every pull.
    public static CallIcon Listed(EventKind kind, uint matchId) =>
        kind == EventKind.StatusGain ? Status(matchId) : None;
}
