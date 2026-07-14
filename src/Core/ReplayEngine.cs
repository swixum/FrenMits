using System.Linq;

namespace FrenMits;

// Replays a saved pull at the desk: starts a virtual pull clock, feeds the
// recorded boss casts / appearances into the real resync path, and toggles the
// cutscene flag at the recorded windows. The overlay, cues and cutscene handling
// then run exactly as they would live, so a fix (or a timeline) can be verified
// without entering the instance.
public class ReplayEngine
{
    private readonly Plugin _plugin;
    private PullRecording? _rec;
    private FightProfile? _fight;
    private int _next;

    public bool Playing { get; private set; }
    public string Status { get; private set; } = "";

    public ReplayEngine(Plugin plugin) => _plugin = plugin;

    public void Start(PullRecording rec)
    {
        _fight = _plugin.Config.Fights.FirstOrDefault(f => f.TerritoryId == rec.TerritoryId)
                 ?? _plugin.Config.Fights.FirstOrDefault();
        if (_fight == null) { Status = "Add a fight to replay into first."; return; }

        _rec = rec;
        _next = 0;
        Plugin.ReplayFight = _fight;
        Plugin.ReplayCutsceneActive = false;
        Playing = true;
        _plugin.Timer.SyncNow();          // virtual pull starts at 0 and free-runs

        // Recorded times are sheet-clock values. A recording whose first event
        // sits far from 0 (an M12S phase-2 pull includes the door-boss phase
        // offset; the raw replay timer has none) would otherwise stall silently
        // until the raw clock caught up. Jump to just before the first event.
        if (rec.Events.Count > 0 && rec.Events[0].Time > 30f)
        {
            _plugin.Timer.SetElapsed(rec.Events[0].Time - 5f);
            _plugin.Cues.Rearm(); // the >5s clock hides this from the fresh-pull check
        }

        Status = $"Replaying \"{rec.Name}\" into {_fight.Name}";
    }

    public void Stop()
    {
        if (!Playing) return;
        Playing = false;
        Plugin.ReplayFight = null;
        Plugin.ReplayCutsceneActive = false;
        _plugin.Timer.Reset();
        Status = "Stopped.";
    }

    public void Update()
    {
        if (!Playing || _rec == null || _fight == null) return;

        var elapsed = _plugin.Timer.Elapsed;

        while (_next < _rec.Events.Count && _rec.Events[_next].Time <= elapsed)
        {
            var e = _rec.Events[_next++];
            switch (e.Type)
            {
                case PullRecording.Kind.CutsceneStart: Plugin.ReplayCutsceneActive = true; break;
                case PullRecording.Kind.CutsceneEnd: Plugin.ReplayCutsceneActive = false; break;
                case PullRecording.Kind.Cast: _plugin.Sync.SnapToCast(_fight, e.Id, 0f); break;
                case PullRecording.Kind.BossAppear: _plugin.Sync.SnapToBoss(_fight, e.Id, e.Caster); break;
            }
        }

        // A little past the last event: wrap up.
        if (_next >= _rec.Events.Count && elapsed > _rec.Duration + 8f)
            Stop();
    }
}
