namespace FrenAlerts.Engine;

// One answer a frame, for a question that gets asked more than once in it.
//
// An overlay is asked twice each frame: whether to be on screen, and then what to put
// there. Both were reading the board directly, and a read of the board takes the clock
// and drops whatever has run out since. A call ending between the two answered yes to
// the first question and was gone by the second, so the window opened, found nothing to
// draw, and showed its background with nothing in it.
//
// The frame number is passed in rather than read here, because this has to be testable
// without ImGui loaded and the caller is the one holding it anyway.
public sealed class FrameSnapshot<T>
{
    private int _at = -1;
    private T _value;

    public FrameSnapshot(T empty) => _value = empty;

    // How many times the answer was actually fetched, which is the thing worth
    // knowing about a cache and the only thing a test can see from outside.
    public int Taken { get; private set; }

    public T Of(int frame, Func<T> take)
    {
        if (frame == _at) return _value;

        _at = frame;
        _value = take();
        Taken++;
        return _value;
    }
}
