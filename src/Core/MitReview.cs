using System;
using System.Collections.Generic;

namespace FrenMits;

// Logs the mitigations you actually used during a pull (when each defensive buff
// first appeared on you), so after a wipe you can eyeball your usage against the
// plan. Accurate by construction — it just records what happened — and fully
// guarded. Read-only game state.
public class MitReview
{
    private readonly Plugin _plugin;
    private readonly HashSet<string> _prevActive = new(StringComparer.OrdinalIgnoreCase);
    private bool _wasRunning;

    public sealed record Use(float Time, string Name, MitTypes.Kind Kind);
    public List<Use> Current { get; } = new();   // this pull, live
    public List<Use> Last { get; private set; } = new(); // the previous completed pull

    public MitReview(Plugin plugin) => _plugin = plugin;

    public void Update()
    {
        try
        {
            // A phase cutscene is a FREEZE, not a pull boundary: the timer keeps
            // running through it, so pausing here (no scan, no state flip) keeps
            // one pull = one log. Treating it as a boundary used to wipe the log
            // at every DMU transition.
            if (Plugin.CutsceneActive) return;

            var running = _plugin.Timer.Running;
            if (running && !_wasRunning) { Current.Clear(); _prevActive.Clear(); }   // pull start
            else if (!running && _wasRunning && Current.Count > 0) Last = new List<Use>(Current); // pull end
            _wasRunning = running;
            if (!running) return;

            var fight = _plugin.ActiveFight();
            var elapsed = fight != null ? _plugin.ElapsedFor(fight) : _plugin.Timer.Elapsed;

            var now = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in MitWatch.Current())
            {
                now.Add(m.Name);
                if (_prevActive.Add(m.Name)) // newly applied this tick
                    Current.Add(new Use(elapsed, m.Name, m.Kind));
            }
            // Drop buffs that fell off so a re-cast later in the pull logs again.
            _prevActive.RemoveWhere(n => !now.Contains(n));
        }
        catch { }
    }
}
