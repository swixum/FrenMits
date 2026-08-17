namespace FrenAlerts.Engine;

// What actually happened this pull, in order, for reading back afterwards.
//
// It exists because a replay produced calls that were wrong and calls that never
// came, and nothing on the screen could tell the two apart. A count of head markers
// cannot say that a marker arrived and no trigger wanted it. A call on the board
// cannot say that three triggers fired and two were dropped as repeats. A seat
// table read off the object table looks exactly like one read off a party list. All
// three are one line each in here.
//
// Off unless switched on, and bounded: a fight that misbehaves for twenty minutes
// stops the recording rather than the game.
public sealed class Diary
{
    // A long pull with every status on eight players runs to a few thousand lines.
    // Past this, something is wrong with the recording rather than with the fight,
    // and the file stops rather than growing until the night ends.
    public const int MaxLines = 20000;

    // The kinds worth a line whether a trigger wanted them or not, because a call
    // that never came is usually missing one of these. Hits and arena polls are
    // deliberately absent: eight players attacking for twenty minutes would bury
    // everything else, so those get a line only when a trigger matches one.
    public static readonly IReadOnlySet<EventKind> Loud = new HashSet<EventKind>
    {
        EventKind.HeadMarker,
        EventKind.Tether,
        EventKind.StatusGain,
        EventKind.StatusLose,
        EventKind.CastStart,
        EventKind.MapEffect,
        EventKind.NpcYell,
        EventKind.CombatStart,
        EventKind.CombatEnd,
        EventKind.ZoneChange,
    };

    private readonly List<string> _lines = [];

    public bool On { get; set; }

    // True once the bound was hit, so a short file and a truncated one cannot be
    // mistaken for each other.
    public bool Full { get; private set; }

    public int Lines => _lines.Count;

    // Events only, never header notes. A section holding nothing but a header
    // describes a pull that did not happen, and writing one out per zone change
    // fills the file with blocks nobody can learn anything from.
    public int Events { get; private set; }

    // ---- what goes in ----

    // Free text about the run rather than about one event: which fight, whether this
    // is a recording, where the seats came from.
    public void Note(string what, string detail) => Write($"        \t{what}\t{detail}");

    // An event that reached the engine. The reason is why it earned a line, so a
    // file full of casts is obviously the loud set rather than a trigger storm.
    public void Saw(in GameEvent e, string why)
    {
        if (!On) return;
        Events++;
        Write($"{e.Time,8:F2}\t{e.Kind}\t{Detail(e)}\t{why}");
    }

    // A trigger matched and its call reached the board.
    public void Fired(double at, string trigger, Call call) =>
        Write($"{at,8:F2}\tcall\t{trigger}\t{call.Key}\t{call.Level}\t{call.Text}");

    // A trigger matched, made a call, and the scheduler threw it away. This is the
    // line that says "it did fire, you just did not hear it".
    public void Dropped(double at, string trigger, Call call, string why) =>
        Write($"{at,8:F2}\tdropped\t{trigger}\t{call.Key}\t{why}\t{call.Text}");

    // How many names a silent event lists before it stops. A mechanic that ten
    // triggers all declined is one fact, and printing all ten of them is a wall.
    public const int MaxNamed = 8;

    // The triggers that matched and had nothing to say, written only when nothing
    // else spoke for that event.
    //
    // One line rather than one each, and only on a silent event, because most of
    // these are collectors: triggers whose whole job is to watch a debuff land and
    // stay quiet until the set is complete. On an event that did produce a call
    // they are working exactly as written, and listing them buried the lines that
    // mattered six deep. On an event that produced nothing they are the diagnosis.
    public void Quiet(double at, IReadOnlyList<string> triggers)
    {
        if (triggers.Count == 0) return;

        var named = string.Join(" ", triggers.Take(MaxNamed));
        var rest = triggers.Count - MaxNamed;
        Write($"{at,8:F2}\tno-call\t{named}{(rest > 0 ? $" and {rest} more" : "")}");
    }

    // Whether this is the first time an id of this kind has been seen this pull.
    //
    // A status is worth a line once. The party wears and loses thousands of them in
    // a pull, drawn from a couple of dozen ids: measured on one recording of Dancing
    // Mad, 8,662 status lines out of 20,003, which with their declines was the whole
    // file. It stopped at its bound seventeen minutes in and never reached the end
    // of the fight, which is the one thing a recording must not do.
    //
    // Once each is enough to answer the question they are kept for, which is "did
    // this id arrive at all". Every one a trigger actually wants is written anyway.
    public bool FirstOfItsKind(in GameEvent e)
    {
        if (_ids.Count >= MaxIds) return false;
        return _ids.Add((e.Kind, e.Id));
    }

    // A pull's worth of distinct ids across every kind. Far above what a fight uses,
    // and a hard stop rather than a set that grows with a misbehaving feed.
    public const int MaxIds = 4096;

    private readonly HashSet<(EventKind Kind, uint Id)> _ids = [];

    private static string Detail(in GameEvent e) =>
        $"id={e.Id:X}\tsrc={e.SourceId:X}\ttgt={e.TargetId:X}"
        + (e.Param != 0 ? $"\tparam={e.Param}" : "")
        + (e.Duration != 0 ? $"\tfor={e.Duration:F1}" : "")
        + (e.CastTime != 0 ? $"\tcast={e.CastTime:F1}" : "");

    private void Write(string line)
    {
        if (!On) return;
        if (_lines.Count >= MaxLines)
        {
            Full = true;
            return;
        }
        _lines.Add(line);
    }

    public string Render() =>
        _lines.Count == 0
            ? ""
            : string.Join(Environment.NewLine, _lines)
              + (Full ? $"{Environment.NewLine}# stopped at {MaxLines} lines." : "");

    // One pull per file, so nothing carries into the next one.
    public void Forget()
    {
        _lines.Clear();
        _ids.Clear();
        Full = false;
        Events = 0;
    }
}
