namespace FrenAlerts.Engine;

// Time, when the pull being watched is a recording rather than a live one.
//
// Everything downstream reasons in seconds: half a second is one burst, seven tenths
// is a collision, four seconds is a stale line. All of that is wall-clock, which is
// the same thing as fight time only while a fight runs at one times speed and never
// goes backwards. A replay does neither.
//
// So in a replay the clock comes from the replay's own position instead. Paused, time
// stops and nothing ages. At four times speed, two mechanics two seconds apart stay
// two seconds apart rather than collapsing into one burst. And a scrub backwards is
// visible as time moving backwards, which is the one thing wall-clock can never show.
public sealed class ReplayClock
{
    // A jump this far in either direction is somebody moving the slider rather than
    // the replay running. Forward jumps count too: skipping a minute means every
    // mechanic in between never happened, and the state built from them is wrong.
    public const double JumpSeconds = 3.0;

    private double _last = double.NaN;

    // Seconds into the recording, as the replay reports it.
    public double Now { get; private set; }

    // True on the reading where the position stopped being continuous.
    public bool Jumped { get; private set; }

    // Fed the replay's own position every frame. Returns the time everything
    // downstream should use.
    public double Note(double position)
    {
        Jumped = !double.IsNaN(_last) && Math.Abs(position - _last) > JumpSeconds;
        _last = position;
        Now = position;
        return Now;
    }

    // Leaving the replay, so the next one starts without this one's position looking
    // like a jump.
    public void Forget()
    {
        _last = double.NaN;
        Jumped = false;
        Now = 0;
    }
}
