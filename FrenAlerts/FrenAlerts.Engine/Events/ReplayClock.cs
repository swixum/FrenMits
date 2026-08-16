namespace FrenAlerts.Engine;

// Time, when the pull being watched is a recording rather than a live one.
//
// Everything downstream reasons in seconds: half a second is one burst, seven tenths
// is a collision, four seconds is a stale line. All of that is wall-clock, which is
// the same thing as fight time only while a fight runs at one times speed and never
// goes backwards. A replay does neither.
//
// So in a replay the clock is built from real time scaled by how fast the game is
// simulating. Paused, the multiplier is zero and time stops: nothing ages, nothing
// counts down, nothing expires off the screen. At four times speed it runs four
// times as fast, so two mechanics two seconds apart stay two seconds apart.
//
// This used to read the replay manager's own position instead, which sounded better
// because it needed no integration and gave a scrub for free. In a real Dancing Mad
// replay it did not move, and a clock that does not move is the worst failure this
// code has: every countdown freezes, nothing ages off the board, and the board then
// throws away each new call as the one furthest out. Three calls on screen and a
// silent fight. The other plugin in this repo has integrated a scaled delta through
// several patches without trouble, so this does the same.
public sealed class ReplayClock
{
    // A frame longer than this is the game hitching, a zone load, or the plugin
    // being paused in a debugger. Counting it would jump the fight forward through
    // mechanics that never happened.
    public const double MaxStep = 1.0;

    // Seconds into the recording, as this clock has counted them.
    public double Now { get; private set; }

    // True on the reading where time stopped being continuous. Nothing sets it any
    // more: a scrub was visible when the position was read straight from the game,
    // and an integrated clock cannot see one. Kept so the host still compiles and
    // reads false rather than silently losing the property.
    public bool Jumped { get; private set; }

    // Fed every frame with how much real time passed and how fast the game is
    // simulating. Returns the time everything downstream should use.
    public double Tick(double realSeconds, float speed)
    {
        Jumped = false;
        if (realSeconds is > 0 and < MaxStep && speed > 0f)
            Now += realSeconds * speed;
        return Now;
    }

    // Leaving the replay, so the next one starts from nothing rather than carrying
    // this one's total.
    public void Forget()
    {
        Now = 0;
        Jumped = false;
    }
}
