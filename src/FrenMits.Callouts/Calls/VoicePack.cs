using System;
using System.Collections.Generic;

namespace FrenMits.Callouts;

// Which clip to play for a call, if any. The pack is a folder the user points
// at, so nothing third party ever ships; a missing clip falls back to speech,
// which is what keeps a half finished pack usable.
public sealed class VoicePack
{
    // Clips held open at once, so a big pack cannot grow without a ceiling.
    public const int MaxCachedClips = 256;

    private readonly Dictionary<string, string> _clips = new(StringComparer.OrdinalIgnoreCase);

    public VoicePack(IEnumerable<KeyValuePair<string, string>> clips)
    {
        foreach (var (key, path) in clips)
        {
            if (_clips.Count >= MaxCachedClips) { Skipped++; continue; }
            _clips[key] = path;
        }
    }

    public static readonly VoicePack Empty = new([]);

    public int Count => _clips.Count;

    // Clips refused for being over the ceiling, so the cap is never silent.
    public int Skipped { get; }

    public bool Has(string clipKey) => clipKey.Length > 0 && _clips.ContainsKey(clipKey);

    // The clip for a call, or nothing when it should be spoken instead.
    public string? ClipFor(Call call)
        => call.ClipKey.Length > 0 && _clips.TryGetValue(call.ClipKey, out var path) ? path : null;
}

// Fills in where to go, when something knows.
public static class SpotAdvice
{
    public static Call WithSpot(this Call call, IReadOnlyList<Spotting> spots, string mechanic, string slot)
    {
        if (call.Where.Length > 0 || slot.Length == 0) return call;

        var spot = spots.Find(mechanic, slot);
        if (spot is null) return call;

        return call with
        {
            Where = spot.Value.Where,
            Text = call.Text.Length > 0 ? $"{call.Text}: {spot.Value.Where}" : spot.Value.Where,
        };
    }
}
