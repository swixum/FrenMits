using System;
using System.Collections.Generic;

namespace FrenMits;

// Fires the audio cue for a line exactly once when it enters its warning window.
// Runs every framework tick so cues sound even if the overlay is hidden.
public class CueEngine
{
    private readonly Plugin _plugin;
    private readonly Audio _audio;
    private readonly HashSet<MitLine> _fired = new();
    private int _generation = -1;
    private DateTime _lastSpoke = DateTime.MinValue;

    public CueEngine(Plugin plugin, Audio audio)
    {
        _plugin = plugin;
        _audio = audio;
    }

    public void Update()
    {
        var c = _plugin.Config;

        // New pull / sync / reset -> forget what we have already announced.
        if (_plugin.Timer.Generation != _generation)
        {
            _generation = _plugin.Timer.Generation;
            _fired.Clear();
        }

        if (!c.AudioEnabled || !_plugin.Timer.Running || Plugin.InCutscene) return;

        // Waiting for the post-cutscene resync to land — stay silent so we don't
        // announce against a drifted clock. Release on the snap or the timeout.
        if (_holdMarker != null)
        {
            if (_plugin.Sync.LastSync != _holdMarker || DateTime.UtcNow >= _holdUntil)
                _holdMarker = null;
            else
                return;
        }

        if (_plugin.ActiveFight() is not { } fight) return;
        if (c.OnlyInTargetTerritory && !Plugin.Replaying && fight.TerritoryId != Service.ClientState.TerritoryType) return;

        var job = _plugin.ActiveJobAbbreviation();
        var elapsed = _plugin.ElapsedFor(fight);

        foreach (var line in fight.Lines)
        {
            if (!line.Enabled || !line.Sound || !line.AppliesTo(job)) continue;
            if (_fired.Contains(line)) continue;

            var lead = line.LeadOverride > 0f ? line.LeadOverride : c.WarningSeconds;
            var remaining = line.Time - elapsed;
            if (remaining > lead || remaining < -0.5f) continue;

            _fired.Add(line);
            Fire(c, line);
        }
    }

    // After a phase-transition cutscene the wall clock has run on but hasn't been
    // snapped back onto the timeline yet, so firing now would speak the wrong call.
    // Hold cues until the resync engine actually snaps (LastSync changes) or this
    // deadline passes, whichever comes first.
    private string? _holdMarker;
    private DateTime _holdUntil;

    public void HoldForResync(string marker, double maxSeconds)
    {
        _holdMarker = marker ?? "";
        _holdUntil = DateTime.UtcNow.AddSeconds(maxSeconds);
    }

    // True while we're waiting for the post-cutscene resync to land. The overlay
    // and timeline windows hide during this brief window so nothing visual fires
    // against the drifted clock either.
    public bool Holding => _holdMarker != null;

    private void Fire(Configuration c, MitLine line)
    {
        if (!c.TtsEnabled) return;

        // Respect a minimum gap between spoken cues, if set.
        if (c.TtsMinGapSeconds > 0f && (DateTime.UtcNow - _lastSpoke).TotalSeconds < c.TtsMinGapSeconds)
            return;
        _lastSpoke = DateTime.UtcNow;

        // Per-line override wins; otherwise speak the action (or mechanic if chosen).
        var fallback = c.TtsSpeakMechanic
            ? (string.IsNullOrWhiteSpace(line.Mechanic) ? line.Action : line.Mechanic)
            : (string.IsNullOrWhiteSpace(line.Action) ? line.Mechanic : line.Action);
        var text = string.IsNullOrWhiteSpace(line.Tts) ? fallback : line.Tts;

        var voice = c.TtsUseEdge
            ? (string.IsNullOrWhiteSpace(c.TtsCustomVoice) ? c.TtsEdgeVoice : c.TtsCustomVoice)
            : c.TtsVoice;
        _audio.Speak(text, c.TtsRate, c.TtsVolume, c.TtsUseEdge, voice);
    }
}
