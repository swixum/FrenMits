using System;
using System.Collections.Generic;

namespace FrenMits;

// Fires a line's audio cue once, even with the overlay hidden.
public class CueEngine
{
    private readonly Plugin _plugin;
    private readonly Audio _audio;
    private readonly HashSet<MitLine> _fired = new();
    private int _generation = -1;
    private DateTime _lastSpoke = DateTime.MinValue;

    // When each line's earliest press window opens, rebuilt only when the solver does.
    private readonly Dictionary<MitLine, float> _windowStarts = new();
    private IReadOnlyList<MitPress>? _windowSrc;

    public CueEngine(Plugin plugin, Audio audio)
    {
        _plugin = plugin;
        _audio = audio;
    }

    public void Update()
    {
        var c = _plugin.Config;

        // Only a fresh pull re-arms every call.
        if (_plugin.Timer.Generation != _generation)
        {
            _generation = _plugin.Timer.Generation;
            // Freshness reads the raw timer, not a shifted clock.
            var fresh = _plugin.Timer.Elapsed < 5f;
            if (fresh) _fired.Clear();
        }

        // Stay silent until a phase anchor re-bases the clock.
        if (_holding && (_plugin.Sync.PhaseSyncGeneration != _holdPhaseGen || DateTime.UtcNow >= _holdUntil))
            _holding = false;

        // Live, not Running, so pre-pull presses still get called.
        if (!c.AudioEnabled || !_plugin.Timer.Live || Plugin.CutsceneActive) return;
        if (_holding) return;

        if (_plugin.ActiveFight() is not { } fight) return;
        if (fight.TimelineOnly) return; // universal timelines are silent
        if (c.OnlyInTargetTerritory && fight.TerritoryId != Service.ClientState.TerritoryType) return;

        var job = _plugin.ActiveJobAbbreviation();
        // Cue clock: sheet time plus the fight's offset.
        var elapsed = _plugin.CueClockFor(fight);

        // Speak off the press window, so voice and overlay open together.
        // A solver fault falls back to plain cue times rather than losing the voice.
        try
        {
            var presses = _plugin.ActivePresses();
            if (!ReferenceEquals(_windowSrc, presses))
            {
                _windowSrc = presses;
                _windowStarts.Clear();
                // The earliest of a combined cell's presses, since that is what shows first.
                foreach (var p in presses)
                    if (!_windowStarts.TryGetValue(p.SourceLine, out var ws) || p.WindowStart < ws)
                        _windowStarts[p.SourceLine] = p.WindowStart;
            }
        }
        catch (Exception ex) { Swallowed.Report("cue press windows", ex); }

        foreach (var line in fight.Lines)
        {
            if (!line.Enabled || !line.Sound || !line.AppliesTo(job)) continue;
            if (_fired.Contains(line)) continue;

            var lead = line.LeadOverride > 0f ? line.LeadOverride : c.WarningSeconds;
            // A line with no tracked mit keeps its plain cue time.
            var cueAt = _windowStarts.TryGetValue(line, out var open) ? open : line.CueTime;
            var remaining = cueAt - elapsed; // honors the per-line offset
            if (remaining > lead || remaining < -0.5f) continue;

            _fired.Add(line);
            Service.Log.Information(
                $"[FrenMits] FIRE '{line.Action}' (time={line.Time} cue={cueAt:0.0} elapsed={elapsed:0.0} gen={_generation})");
            _plugin.Diag.Cue(line.Action, line.Time, elapsed, _generation, "");
            Fire(c, line, job);
        }
    }

    // How long to hold cues after a cutscene.
    private bool _holding;
    private int _holdPhaseGen;
    private DateTime _holdUntil;

    // Re-arm every cue after a phase jump.
    public void Rearm() => _fired.Clear();

    public void HoldForResync(int phaseGen, double maxSeconds)
    {
        _holding = true;
        _holdPhaseGen = phaseGen;
        _holdUntil = DateTime.UtcNow.AddSeconds(maxSeconds);
    }

    // True while waiting for the post-cutscene re-base.
    public bool Holding => _holding;

    // When each phrase was last said, to debounce repeats.
    private readonly Dictionary<string, DateTime> _spokenAt = new();

    private void Fire(Configuration c, MitLine line, string? job)
    {
        if (!c.TtsEnabled) return;

        // Per-line override wins over the action or mechanic.
        var fallback = c.TtsSpeakMechanic
            ? (string.IsNullOrWhiteSpace(line.Mechanic) ? Icons.DisplayAction(line.ActionFor(job), job) : line.Mechanic)
            : (string.IsNullOrWhiteSpace(line.Action) ? line.Mechanic : Icons.DisplayAction(line.ActionFor(job), job));
        var text = string.IsNullOrWhiteSpace(line.Tts) ? fallback : line.Tts;
        if (string.IsNullOrWhiteSpace(text)) return;

        var now = DateTime.UtcNow;

        // Never say the same phrase twice in a short window.
        if (_spokenAt.TryGetValue(text, out var lastSame) && (now - lastSame).TotalSeconds < 2.0)
        {
            Service.Log.Information($"[FrenMits] (debounced duplicate '{text}', {(now - lastSame).TotalSeconds:0.00}s after last)");
            _plugin.Diag.Cue(text, 0, 0, 0, $"debounced ({(now - lastSame).TotalSeconds:0.0}s after last)");
            return;
        }

        // Optional minimum gap between any two cues.
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
