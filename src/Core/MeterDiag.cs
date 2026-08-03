using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FrenMits;

// The meter's rolling diag file, alive without a pull.
public sealed class MeterDiag
{
    private readonly List<string> _buf = new();
    private readonly object _gate = new();
    private string _path = "";
    private DateTime _nextFlush = DateTime.MinValue;

    public void Note(string what)
    {
        _buf.Add($"[{DateTime.Now:HH:mm:ss.f}] {what}");
        if (_buf.Count > 2000) _buf.RemoveAt(0);
    }

    // Called once a frame; batches go to a task so the disk never touches the game's thread.
    public void Update()
    {
        if (_buf.Count == 0 || DateTime.UtcNow < _nextFlush) return;
        _nextFlush = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        var batch = _buf.ToArray();
        _buf.Clear();
        System.Threading.Tasks.Task.Run(() => Write(batch));
    }

    // The last words on dispose, worth one synchronous write.
    public void Flush()
    {
        if (_buf.Count == 0) return;
        var batch = _buf.ToArray();
        _buf.Clear();
        Write(batch);
    }

    private void Write(string[] batch)
    {
        lock (_gate)
        {
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
                File.AppendAllLines(_path, batch);
            }
            catch
            {
                // Diagnostics must never hurt.
            }
        }
    }
}
