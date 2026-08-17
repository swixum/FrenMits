namespace FrenAlerts.Engine.Scripts;

// Where a call goes to be typed out in chat.
//
// Two of their features land here: a call that has been given macro text, and the
// limit cut list a fight works out and posts for the group. Both arm a body of one
// or more lines, wait a random moment inside a window, then send a line at a time.
//
// The randomness and the wait are not decoration. Eight people running the same boss
// mod would otherwise all type the same line into party chat on the same frame, which
// is how a group ends up muting the person who set it up.
public sealed class ScriptMacros
{
    // Their gap between lines of a multi-line post.
    private const double BetweenLines = 0.33;

    public enum Mode : byte
    {
        Off,
        Echo,
        Party,
    }

    private readonly List<(double Due, string Line)> _waiting = [];
    private string[]? _list;
    private int _next;
    private double _listDue;
    private string _listPrefix = "/e ";

    // Where lines go. Left to the host, because sending chat is the game's business
    // and everything above this line is testable without it.
    public Action<string>? Send;

    public Mode Posting { get; set; } = Mode.Off;

    public double DelayMin { get; set; }

    public double DelayMax { get; set; } = 1.0;

    // Told rather than read: whether there is a party to post to decides between
    // party chat and an echo only you can see.
    public bool InParty { get; set; }

    public int Waiting => _waiting.Count + (_list is null ? 0 : Math.Max(0, _list.Length - _next));

    // The random moment inside the window, handed in so a run is repeatable. The
    // game side passes a real one.
    public Func<double>? Roll;

    public void Reset()
    {
        _waiting.Clear();
        _list = null;
        _next = 0;
    }

    // One line, from a call that has macro text.
    public void Arm(string line, double now)
    {
        if (Posting == Mode.Off || string.IsNullOrWhiteSpace(line)) return;
        _waiting.Add((now + Wait(), Prefix() + line));
    }

    // A whole list, from a fight that worked one out. Replaces anything still
    // queued: a second limit cut list means the first one was wrong.
    public void ArmList(string body, double now)
    {
        if (Posting == Mode.Off || string.IsNullOrWhiteSpace(body)) return;

        _listPrefix = Prefix();
        _list = body.Replace("\r", "").Split('\n');
        _next = 0;
        _listDue = now + Wait();
    }

    public void Tick(double now)
    {
        for (var i = _waiting.Count - 1; i >= 0; i--)
        {
            if (_waiting[i].Due > now) continue;
            var line = _waiting[i].Line;
            _waiting.RemoveAt(i);
            Send?.Invoke(line);
        }

        if (_list is null || now < _listDue) return;

        while (_next < _list.Length && string.IsNullOrWhiteSpace(_list[_next])) _next++;

        if (_next >= _list.Length) { _list = null; return; }

        Send?.Invoke(_listPrefix + _list[_next]);
        _next++;

        if (_next >= _list.Length) _list = null;
        else _listDue = now + BetweenLines;
    }

    private string Prefix() => Posting == Mode.Party && InParty ? "/p " : "/e ";

    private double Wait()
    {
        var low = Math.Max(0, DelayMin);
        var high = Math.Max(low, DelayMax);
        return low + (Roll?.Invoke() ?? 0) * (high - low);
    }
}
