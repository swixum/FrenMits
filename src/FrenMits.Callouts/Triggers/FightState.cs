using System;
using System.Collections.Generic;

namespace FrenMits.Callouts;

// What the engine remembers within a pull. A call that says "third tower, north"
// needs to know it is the third, and which phase it is in; that is the whole
// difference between naming a mechanic and telling someone what to do.
public sealed class FightState
{
    // Bounds, so a bad trigger set cannot grow state without end.
    public const int MaxCounters = 512;
    public const int MaxCollections = 64;
    public const int MaxPerCollection = 48;

    // Named things a fight remembers in words rather than in numbers: which
    // arrow landed, which tower was blue, which way the boss turned.
    public const int MaxNotes = 128;

    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Actor>> _collected = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _notes = new(StringComparer.Ordinal);

    // The phase a phase-setting trigger last announced.
    public string Phase { get; set; } = "";

    public int Count(string key) => _counts.GetValueOrDefault(key);

    public int Bump(string key)
    {
        if (!_counts.ContainsKey(key) && _counts.Count >= MaxCounters) return 0;
        return _counts[key] = _counts.GetValueOrDefault(key) + 1;
    }

    public IReadOnlyList<Actor> Collected(string key)
        => _collected.TryGetValue(key, out var list) ? list : [];

    public void Collect(string key, Actor who)
    {
        if (!_collected.TryGetValue(key, out var list))
        {
            if (_collected.Count >= MaxCollections) return;
            _collected[key] = list = new List<Actor>();
        }
        if (list.Count >= MaxPerCollection) return;
        if (list.Exists(a => a.Id == who.Id)) return;
        list.Add(who);
    }

    public void Clear(string key)
    {
        _collected.Remove(key);
        _notes.Remove(key);
    }

    // What the fight noted under this name, or nothing if it never did.
    public string Noted(string key) => _notes.GetValueOrDefault(key, "");

    public bool HasNote(string key) => _notes.ContainsKey(key);

    public void Note(string key, string value)
    {
        if (!_notes.ContainsKey(key) && _notes.Count >= MaxNotes) return;
        _notes[key] = value;
    }

    // Called on every pull edge, so nothing leaks into the next attempt.
    public void Reset()
    {
        _counts.Clear();
        _collected.Clear();
        _notes.Clear();
        Phase = "";
    }

    // Where I come in a collected set, one based, or zero when I am not in it.
    public int IndexOf(string key, uint myId)
    {
        var list = Collected(key);
        for (var i = 0; i < list.Count; i++)
            if (list[i].Id == myId)
                return i + 1;
        return 0;
    }

    public static string Ordinal(int n) => n switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        <= 0 => "",
        _ => $"{n}th",
    };
}
