using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FrenMits;

// The meter's rolling diag file, alive without a pull.
public sealed class MeterDiag
{
    private readonly List<string> _buf = new();
    private string _path = "";
    private DateTime _nextFlush = DateTime.MinValue;

    public void Note(string what)
    {
        _buf.Add($"[{DateTime.Now:HH:mm:ss.f}] {what}");
        if (_buf.Count > 2000) _buf.RemoveAt(0);
    }

    // Called once a frame; writes batch so a busy fight is cheap.
    public void Update()
    {
        if (_buf.Count == 0 || DateTime.UtcNow < _nextFlush) return;
        _nextFlush = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        Flush();
    }

    public void Flush()
    {
        if (_buf.Count == 0) return;
        try
        {
            if (_path.Length == 0)
            {
                var dir = Path.Combine(Service.PluginInterface.GetPluginConfigDirectory(), "diagnostics");
                Directory.CreateDirectory(dir);
                _path = Path.Combine(dir, $"meter-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                // Keep the newest handful of session files.
                foreach (var f in new DirectoryInfo(dir).GetFiles("meter-*.txt").OrderByDescending(f => f.Name).Skip(9))
                    try { f.Delete(); } catch { /* ignore */ }
                Service.Log.Information($"[FrenMits] meter diagnostics: {_path}");
            }
            File.AppendAllLines(_path, _buf);
            _buf.Clear();
        }
        catch
        {
            _buf.Clear();
        }
    }
}
