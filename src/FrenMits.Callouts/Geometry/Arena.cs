using System.Collections.Generic;

namespace FrenMits.Callouts;

// Where everyone is standing right now, and which floor they are standing on.
// Directing is impossible without both: a call cannot say "north" until it knows
// where the player is and where the middle of the room is.
public sealed class Arena
{
    // Actors tracked at once. A duty holds a party and its enemies, not more.
    public const int MaxTracked = 256;

    private readonly Dictionary<uint, Spot> _spots = new();
    private readonly FloorEstimate _guess = new();
    private Floor _authored;
    private uint _territory;

    // Measured floors by duty, supplied by the host. A duty in the book gets
    // real numbers; one that is not falls back to what the party's own
    // positions suggest.
    public IReadOnlyDictionary<uint, Floor> Book { get; set; } = new Dictionary<uint, Floor>();

    // A duty change. Nothing about the last room carries into the next one.
    public void Enter(uint territory)
    {
        if (territory == _territory) return;

        _territory = territory;
        _authored = Book.For(territory);
        _spots.Clear();
        _guess.Reset();
    }

    public uint Territory => _territory;

    public Floor Floor => _authored.Known ? _authored : _guess.Guess(_territory);

    // True when the floor came from measurement rather than from watching the
    // party. Worth saying out loud in a diagnostic: a guessed floor directs
    // worse than a measured one.
    public bool Measured => _authored.Known;

    public Spot Center => Floor.Middle;

    public float Radius => Floor.Radius;

    public int Tracked => _spots.Count;

    public void Update(GameEvent e)
    {
        Note(e.Source);
        Note(e.Target);
    }

    public Spot Of(uint actorId) => _spots.TryGetValue(actorId, out var at) ? at : Spot.Nowhere;

    // Called on every pull edge. The room is the same next attempt, so what the
    // floor looks like survives; where people were standing does not.
    public void Reset() => _spots.Clear();

    private void Note(Actor a)
    {
        if (!a.Known || !a.At.Known) return;
        if (!_spots.ContainsKey(a.Id) && _spots.Count >= MaxTracked) return;

        _spots[a.Id] = a.At;

        // Only players walk the whole floor. A boss parked in the middle would
        // pull a guessed middle onto itself.
        if (a.IsPlayer && !_authored.Known) _guess.Note(a.At);
    }
}
