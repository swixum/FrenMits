using Dalamud.Hooking;
using FrenAlerts.Engine;

namespace FrenAlerts.Game;

// A source that reads events out of the client by hooking a maintained address.
//
// Three of these existed as near-copies, each repeating the same bound, the same
// swallow-everything detour and the same drain, so a mistake in that shape had to be
// found three times.
public abstract class HookedSource<TDetour> : IDisposable where TDetour : Delegate
{
    private readonly EventQueue _queue;

    protected HookedSource(int max = 4096) => _queue = new EventQueue(max);

    protected Hook<TDetour>? Hooked { get; private set; }

    public bool Available => Hooked is not null;

    public int Reported => _queue.Reported;

    public int Dropped => _queue.Dropped;

    // Called from the derived constructor once its own fields are set, because the
    // detour is an instance method and the hook can fire the moment it is enabled.
    protected void Install(string address, TDetour detour, string whatBreaks)
    {
        try
        {
            Hooked = Service.GameInterop.HookFromSignature(address, detour);
            Hooked.Enable();
        }
        catch (Exception ex)
        {
            // A patch that moves the address leaves the hook absent and everything
            // else working, which is why this is a warning rather than a throw.
            Service.Log.Warning(ex, $"Fren Alerts: {whatBreaks}");
            Hooked = null;
        }
    }

    // Drained on the frame, never in the detour, so the engine never runs inside the
    // game's own handling.
    public IEnumerable<GameEvent> Drain() => _queue.Drain();

    protected bool Offer(GameEvent e) => _queue.Offer(e);

    // Wraps the part of a detour that reads game memory.
    //
    // A throw there would propagate into the game's own handler, so losing an event
    // is the only acceptable failure; the caller passes the original on regardless.
    protected void Guard(Action read)
    {
        try
        {
            read();
        }
        catch
        {
            // Losing one call is survivable; taking the client down with it is not.
        }
    }

    public virtual void Dispose()
    {
        Hooked?.Disable();
        Hooked?.Dispose();
        Hooked = null;
        _queue.Clear();
        GC.SuppressFinalize(this);
    }
}
