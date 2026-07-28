using System;
using System.Collections.Generic;

namespace FrenMits;

// Fires the audio cue for a line exactly once when it enters its warning window,
// running every framework tick so cues sound even if the overlay is hidden.
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

        // Only a fresh pull re-arms every call; a mid-pull bump leaves the fired-set
        // alone.
        if (_plugin.Timer.Generation != _generation)
        {
            _generation = _plugin.Timer.Generation;
            // Freshness is judged on the raw timer, not a sheet clock with a phase
            // offset.
            var fresh = _plugin.Timer.Elapsed < 5f;
            if (fresh) _fired.Clear();
        }

        // Stay silent until a phase anchor re-bases the clock after a cutscene.
        if (_holding && (_plugin.Sync.PhaseSyncGeneration != _holdPhaseGen || DateTime.UtcNow >= _holdUntil))
            _holding = false;

        // Live, not Running: the countdown is part of the pull, and a sheet's
        // pre-pull presses are only worth anything if they're called before it.
        if (!c.AudioEnabled || !_plugin.Timer.Live || Plugin.CutsceneActive) return;
        if (_holding) return;

        if (_plugin.ActiveFight() is not { } fight) return;
        if (fight.TimelineOnly) return; // universal timelines are silent
        if (c.OnlyInTargetTerritory && fight.TerritoryId != Service.ClientState.TerritoryType) return;

        var job = _plugin.ActiveJobAbbreviation();
        // Cue clock: sheet time + the fight's timer offset, so calls shift as set.
        var elapsed = _plugin.CueClockFor(fight);

        foreach (var line in fight.Lines)
        {
            if (!line.Enabled || !line.Sound || !line.AppliesTo(job)) continue;
            if (_fired.Contains(line)) continue;

            var lead = line.LeadOverride > 0f ? line.LeadOverride : c.WarningSeconds;
            var remaining = line.CueTime - elapsed; // honors the per-line offset
            if (remaining > lead || remaining < -0.5f) continue;

            _fired.Add(line);
            Service.Log.Information(
                $"[FrenMits] FIRE '{line.Action}' (time={line.Time} elapsed={elapsed:0.0} gen={_generation})");
            _plugin.Diag.Cue(line.Action, line.Time, elapsed, _generation, "");
            Fire(c, line, job);
        }
    }

    // How long to hold cues after a cutscene while waiting for the snap.
    private bool _holding;
    private int _holdPhaseGen;
    private DateTime _holdUntil;

    // Re-arm every cue: a practice phase-jump parks the clock mid-sheet.
    public void Rearm() => _fired.Clear();

    public void HoldForResync(int phaseGen, double maxSeconds)
    {
        _holding = true;
        _holdPhaseGen = phaseGen;
        _holdUntil = DateTime.UtcNow.AddSeconds(maxSeconds);
    }

    // True while waiting for the post-cutscene re-base; the overlays hide too.
    public bool Holding => _holding;

    // When each spoken phrase was last said, to debounce identical calls.
    private readonly Dictionary<string, DateTime> _spokenAt = new();

    private void Fire(Configuration c, MitLine line, string? job)
    {
        if (!c.TtsEnabled) return;

        // Per-line override wins, otherwise speak the action or mechanic.
        var fallback = c.TtsSpeakMechanic
            ? (string.IsNullOrWhiteSpace(line.Mechanic) ? Icons.DisplayAction(line.ActionFor(job), job) : line.Mechanic)
            : (string.IsNullOrWhiteSpace(line.Action) ? line.Mechanic : Icons.DisplayAction(line.ActionFor(job), job));
        var text = string.IsNullOrWhiteSpace(line.Tts) ? fallback : line.Tts;
        if (string.IsNullOrWhiteSpace(text)) return;

        var now = DateTime.UtcNow;

        // Never speak the same phrase twice within a short window.
        if (_spokenAt.TryGetValue(text, out var lastSame) && (now - lastSame).TotalSeconds < 2.0)
        {
            Service.Log.Information($"[FrenMits] (debounced duplicate '{text}', {(now - lastSame).TotalSeconds:0.00}s after last)");
            _plugin.Diag.Cue(text, 0, 0, 0, $"debounced ({(now - lastSame).TotalSeconds:0.0}s after last)");
            return;
        }

        // Optional minimum gap between ANY cues.
        if (c.TtsMinGapSeconds > 0f && (now - _lastSpoke).TotalSeconds < c.TtsMinGapSeconds)
            return;

        _spokenAt[text] = now;
        if (_spokenAt.Count > 256) _spokenAt.Clear();
        _lastSpoke = now;

        var voice = c.TtsUseEdge
            ? (string.IsNullOrWhiteSpace(c.TtsCustomVoice) ? c.TtsEdgeVoice : c.TtsCustomVoice)
            : c.TtsVoice;
        _audio.Speak(text, c.TtsRate, c.TtsVolume, c.TtsUseEdge, voice);
    }
}
