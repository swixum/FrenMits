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

    // Re-arm every cue. Needed when a replay rebases the clock mid-sheet: the
    // fresh-pull check below (elapsed < 5s) can't recognize that as a new run.
    public void Rearm() => _fired.Clear();

    public void Update()
    {
        var c = _plugin.Config;

        // The clock's generation bumped (pull / wipe / sync / a brief combat flicker).
        // Only a genuine FRESH pull (the clock resets to ~0) re-arms every call, so we
        // clear the fired-set then. A mid-pull bump — a resync snap or a brief combat
        // flicker — leaves the fired-set untouched. (The old code re-armed every fired
        // line that a backward snap had pushed ahead of the clock; re-advancing onto
        // those is exactly what replayed a call we'd already spoken — the double-audio.
        // Unfired future lines aren't in the set, so they still fire normally.)
        if (_plugin.Timer.Generation != _generation)
        {
            _generation = _plugin.Timer.Generation;
            // Freshness is judged on the SHEET clock, not the cue clock: a positive
            // timer offset would otherwise read "10s in" at pull start and never
            // re-arm the fired-set.
            var fresh = _plugin.ActiveFight() is not { } genFight || _plugin.ElapsedFor(genFight) < 5f;
            if (fresh) _fired.Clear();
        }

        if (!c.AudioEnabled || !_plugin.Timer.Running || Plugin.InCutscene) return;

        // Waiting for the post-cutscene phase re-base to land — stay silent so we
        // don't announce against a drifted clock. Release only when a PHASE anchor
        // snaps the clock (a mid-phase mechanic resync isn't enough to trust the
        // new phase yet) or the timeout passes.
        if (_holding)
        {
            if (_plugin.Sync.PhaseSyncGeneration != _holdPhaseGen || DateTime.UtcNow >= _holdUntil)
                _holding = false;
            else
                return;
        }

        if (_plugin.ActiveFight() is not { } fight) return;
        if (c.OnlyInTargetTerritory && !Plugin.Replaying && fight.TerritoryId != Service.ClientState.TerritoryType) return;

        var job = _plugin.ActiveJobAbbreviation();
        // Cue clock: sheet time + the fight's timer offset, so calls shift as set.
        var elapsed = _plugin.CueClockFor(fight);

        foreach (var line in fight.Lines)
        {
            if (!line.Enabled || !line.Sound || !line.AppliesTo(job)) continue;
            if (_fired.Contains(line)) continue;

            var lead = line.LeadOverride > 0f ? line.LeadOverride : c.WarningSeconds;
            var remaining = line.Time - elapsed;
            if (remaining > lead || remaining < -0.5f) continue;

            _fired.Add(line);
            Service.Log.Information(
                $"[FrenMits] FIRE '{line.Action}' (time={line.Time} elapsed={elapsed:0.0} gen={_generation})");
            _plugin.Diag.Cue(line.Action, line.Time, elapsed, _generation, "");
            Fire(c, line, job);
        }
    }

    // After a phase-transition cutscene the wall clock has run on but hasn't been
    // snapped back onto the timeline yet, so firing now would speak the wrong call.
    // Hold cues until the resync engine actually snaps (LastSync changes) or this
    // deadline passes, whichever comes first.
    private bool _holding;
    private int _holdPhaseGen;
    private DateTime _holdUntil;

    public void HoldForResync(int phaseGen, double maxSeconds)
    {
        _holding = true;
        _holdPhaseGen = phaseGen;
        _holdUntil = DateTime.UtcNow.AddSeconds(maxSeconds);
    }

    // True while we're waiting for the post-cutscene phase re-base to land. The
    // overlay and timeline windows hide during this window so nothing visual fires
    // against the drifted clock either.
    public bool Holding => _holding;

    // When each spoken phrase was last said, to debounce identical calls.
    private readonly Dictionary<string, DateTime> _spokenAt = new();

    private void Fire(Configuration c, MitLine line, string? job)
    {
        if (!c.TtsEnabled) return;

        // Per-line override wins; otherwise speak the action (or mechanic if chosen).
        // The action is job-resolved so "Party Mit" is spoken as e.g. "Troubadour".
        var fallback = c.TtsSpeakMechanic
            ? (string.IsNullOrWhiteSpace(line.Mechanic) ? Icons.DisplayAction(line.Action, job) : line.Mechanic)
            : (string.IsNullOrWhiteSpace(line.Action) ? line.Mechanic : Icons.DisplayAction(line.Action, job));
        var text = string.IsNullOrWhiteSpace(line.Tts) ? fallback : line.Tts;
        if (string.IsNullOrWhiteSpace(text)) return;

        var now = DateTime.UtcNow;

        // Hard guard against doubled audio: never speak the exact same phrase twice
        // within a short window, whatever caused the second trigger (a resync
        // re-fire, a brief combat flicker resetting the fired-set, an in-editor time
        // change). Distinct calls are unaffected.
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
