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
    // Wall-clock time each line was last spoken, so a resync that re-arms a line
    // can't make it speak again moments later. Cleared on a genuine fresh pull.
    private readonly Dictionary<MitLine, DateTime> _firedAt = new();
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

        // The clock's generation bumped (pull / wipe / sync / a brief combat
        // flicker). Don't blanket-forget everything — that re-announces calls we've
        // already passed (a flicker or a mid-fight /fm sync would replay them). Only
        // re-arm calls now in the FUTURE: a genuine pull resets the clock to ~0 so
        // everything re-arms, while a mid-pull bump keeps the past calls silent.
        if (_plugin.Timer.Generation != _generation)
        {
            _generation = _plugin.Timer.Generation;
            if (_plugin.ActiveFight() is { } genFight)
            {
                var el = _plugin.ElapsedFor(genFight);
                _fired.RemoveWhere(l => l.Time > el + 0.5f);
                if (el < 5f) _firedAt.Clear(); // a genuine fresh pull — allow every call again
            }
            else
            {
                _fired.Clear();
                _firedAt.Clear();
            }
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
        var elapsed = _plugin.ElapsedFor(fight);

        foreach (var line in fight.Lines)
        {
            if (!line.Enabled || !line.Sound || !line.AppliesTo(job)) continue;
            if (_fired.Contains(line)) continue;

            var lead = line.LeadOverride > 0f ? line.LeadOverride : c.WarningSeconds;
            var remaining = line.Time - elapsed;
            if (remaining > lead || remaining < -0.5f) continue;

            _fired.Add(line);
            // A backward resync / phase re-base can re-arm a line we already spoke
            // (the clock steps back across it, then advances onto it again). Don't
            // re-speak the same line within 90s; a real re-pull clears this above.
            // This is what stops the resync double-calls in the legacy ultimates.
            if (_firedAt.TryGetValue(line, out var prevFire) && (DateTime.UtcNow - prevFire).TotalSeconds < 90.0)
                continue;
            _firedAt[line] = DateTime.UtcNow;
            Fire(c, line);
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

    private void Fire(Configuration c, MitLine line)
    {
        if (!c.TtsEnabled) return;

        // Per-line override wins; otherwise speak the action (or mechanic if chosen).
        var fallback = c.TtsSpeakMechanic
            ? (string.IsNullOrWhiteSpace(line.Mechanic) ? line.Action : line.Mechanic)
            : (string.IsNullOrWhiteSpace(line.Action) ? line.Mechanic : line.Action);
        var text = string.IsNullOrWhiteSpace(line.Tts) ? fallback : line.Tts;
        if (string.IsNullOrWhiteSpace(text)) return;

        var now = DateTime.UtcNow;

        // Hard guard against doubled audio: never speak the exact same phrase twice
        // within a short window, whatever caused the second trigger (a resync
        // re-fire, a brief combat flicker resetting the fired-set, an in-editor time
        // change). Distinct calls are unaffected.
        if (_spokenAt.TryGetValue(text, out var lastSame) && (now - lastSame).TotalSeconds < 2.0)
            return;

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
