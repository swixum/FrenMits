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

        if (!c.AudioEnabled || !_plugin.Timer.Running) return;
        if (_plugin.ActiveFight() is not { } fight) return;
        if (c.OnlyInTargetTerritory && fight.TerritoryId != Service.ClientState.TerritoryType) return;

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

    private void Fire(Configuration c, MitLine line)
    {
        if (c.TtsEnabled)
        {
            var text = string.IsNullOrWhiteSpace(line.Tts)
                ? (string.IsNullOrWhiteSpace(line.Action) ? line.Mechanic : line.Action)
                : line.Tts;
            _audio.Speak(text, c.TtsRate, c.TtsVolume, c.TtsUseEdge,
                c.TtsUseEdge ? c.TtsEdgeVoice : c.TtsVoice);
        }
    }
}
