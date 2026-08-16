namespace FrenAlerts.Engine;

public sealed class EventBuffer
{
    public const int DefaultCapacity = 20000;

    private readonly GameEvent[] _items;
    private int _start;

    public EventBuffer(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _items = new GameEvent[capacity];
    }

    public int Capacity => _items.Length;
    public int Count { get; private set; }

    public int Dropped { get; private set; }

    public GameEvent this[int i]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(i);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(i, Count);
            return _items[(_start + i) % _items.Length];
        }
    }

    public void Add(in GameEvent e)
    {
        if (Count < _items.Length)
        {
            _items[(_start + Count) % _items.Length] = e;
            Count++;
            return;
        }
        _items[_start] = e;
        _start = (_start + 1) % _items.Length;
        Dropped++;
    }

    public GameEvent[] ToArray()
    {
        var copy = new GameEvent[Count];
        for (var i = 0; i < Count; i++) copy[i] = this[i];
        return copy;
    }

    public void Reset()
    {
        Array.Clear(_items);
        _start = 0;
        Count = 0;
        Dropped = 0;
    }
}
