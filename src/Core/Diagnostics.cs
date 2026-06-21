using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FrenMits;

// Per-pull diagnostics. While enabled, buffers resync / cue / phase events for the
// current pull and writes ONE structured text file per pull into
//   <pluginConfigDir>/diagnostics/yyyyMMdd-HHmmss.txt
// so the resync behaviour (especially the late phases) can be reviewed afterwards.
// Entirely local: nothing is sent anywhere. Opt-in via Config.Diagnostics.
public class Diagnostics
{
    private readonly Plugin _plugin;
    private readonly List<string> _lines = new();

    private bool _active;
    private bool _pendingEnd;
    private DateTime _falseAt;
    private DateTime _start;
    private string _header = "";
    private float _maxElapsed;

    public Diagnostics(Plugin plugin) => _plugin = plugin;

    public void Update()
    {
        if (!_plugin.Config.Diagnostics) return;

        var running = _plugin.Timer.Running;
        if (running)
        {
            _pendingEnd = false;
            if (!_active) Begin();
            if (_plugin.ActiveFight() is { } f)
                _maxElapsed = MathF.Max(_maxElapsed, _plugin.ElapsedFor(f));
        }
        else if (_active)
        {
            // Debounce a brief combat flicker so it doesn't split one pull in two;
            // only finalize after combat has stayed off for a few seconds.
            if (!_pendingEnd) { _pendingEnd = true; _falseAt = DateTime.Now; }
            else if ((DateTime.Now - _falseAt).TotalSeconds >= 5) End();
        }
    }

    private void Begin()
    {
        _lines.Clear();
        _maxElapsed = 0;
        _start = DateTime.Now;
        _active = true;
        var f = _plugin.ActiveFight();
        var job = _plugin.ActiveJobAbbreviation() ?? "?";
        _header = $"FrenMits pull diagnostics  v{typeof(Plugin).Assembly.GetName().Version}\n" +
                  $"fight={f?.Name ?? "?"}  slot={f?.Slot ?? "?"}  job={job}  start={_start:yyyy-MM-dd HH:mm:ss}";
        Log("PULL START");
    }

    private void End()
    {
        _active = false;
        _pendingEnd = false;
        var dur = (DateTime.Now - _start).TotalSeconds;
        if (dur < 20) return; // ignore false starts / instant resets

        Log($"PULL END  duration={dur:0.0}s  reached={_maxElapsed:0.0}s  " +
            $"avgDrift={_plugin.Sync.AvgDrift:+0.0;-0.0}s ({_plugin.Sync.DriftSamples} samples)");
        Flush();
    }

    // ---- event hooks (called from the engines) ---------------------------

    public void Sync(string detail, float clock, bool isPhase)
    {
        if (_active) Log($"{(isPhase ? "PHASE" : "sync ")}  {detail}");
    }

    public void Cue(string action, float time, float elapsed, int gen, string note)
    {
        if (_active) Log($"cue    '{action}'  time={time:0}  clock={elapsed:0.0}  gen={gen}{(note.Length > 0 ? "  " + note : "")}");
    }

    public void Note(string what)
    {
        if (_active) Log(what);
    }

    private void Log(string s)
    {
        var t = (DateTime.Now - _start).TotalSeconds;
        _lines.Add($"[{t,7:0.0}] {s}");
        if (_lines.Count > 4000) _lines.RemoveAt(0);
    }

    private void Flush()
    {
        try
        {
            var dir = Path.Combine(Service.PluginInterface.GetPluginConfigDirectory(), "diagnostics");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{_start:yyyyMMdd-HHmmss}.txt");
            File.WriteAllLines(path, new[] { _header, "" }.Concat(_lines));
            Service.Log.Information($"[FrenMits] diagnostics written: {path}");

            // Keep only the most recent 30 files.
            foreach (var f in new DirectoryInfo(dir).GetFiles("*.txt").OrderByDescending(f => f.Name).Skip(30))
                try { f.Delete(); } catch { /* ignore */ }
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: diagnostics write failed");
        }
    }
}
