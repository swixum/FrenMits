using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FrenMits;

// A saved pull: the boss casts / appearances and cutscene windows captured during
// a real run, with times relative to the pull. Replayed later (ReplayEngine) to
// test that the timeline, resync and cutscene handling line up — no raid needed.
public sealed class PullRecording
{
    public string Name { get; set; } = "";
    public uint TerritoryId { get; set; }
    public string FightName { get; set; } = "";
    public float Duration { get; set; }
    public List<RecEvent> Events { get; set; } = new();

    public enum Kind { Cast, BossAppear, CutsceneStart, CutsceneEnd }

    public sealed class RecEvent
    {
        public float Time { get; set; }
        public Kind Type { get; set; }
        public uint Id { get; set; }
        public string Caster { get; set; } = "";
    }

    private static string Dir
    {
        get
        {
            var d = Path.Combine(Service.PluginInterface.GetPluginConfigDirectory(), "recordings");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static string SafeName(string name)
    {
        var s = string.Concat((string.IsNullOrWhiteSpace(name) ? "pull" : name)
            .Split(Path.GetInvalidFileNameChars()));
        return s.Length == 0 ? "pull" : s;
    }

    public void Save()
    {
        Events = Events.OrderBy(e => e.Time).ToList();
        File.WriteAllText(Path.Combine(Dir, SafeName(Name) + ".json"), JsonSerializer.Serialize(this, JsonOpts));
    }

    public static List<string> List()
    {
        try
        {
            return Directory.GetFiles(Dir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n!)
                .ToList()!;
        }
        catch { return new List<string>(); }
    }

    public static PullRecording? Load(string name)
    {
        try
        {
            var p = Path.Combine(Dir, SafeName(name) + ".json");
            return File.Exists(p) ? JsonSerializer.Deserialize<PullRecording>(File.ReadAllText(p)) : null;
        }
        catch { return null; }
    }

    public static void Delete(string name)
    {
        try
        {
            var p = Path.Combine(Dir, SafeName(name) + ".json");
            if (File.Exists(p)) File.Delete(p);
        }
        catch { /* ignore */ }
    }

    // Assemble a recording from the resync engine's current capture buffers.
    public static PullRecording FromCapture(string name, uint territory, string fightName,
        IEnumerable<SyncEngine.Capture> casts, IEnumerable<RecEvent> cutscenes)
    {
        var rec = new PullRecording { Name = name, TerritoryId = territory, FightName = fightName };
        foreach (var c in casts)
            rec.Events.Add(new RecEvent
            {
                Time = c.Time,
                Type = c.IsBoss ? Kind.BossAppear : Kind.Cast,
                Id = c.Id,
                Caster = c.Caster,
            });
        foreach (var cs in cutscenes)
            rec.Events.Add(new RecEvent { Time = cs.Time, Type = cs.Type, Id = 0, Caster = "cutscene" });
        rec.Events = rec.Events.OrderBy(e => e.Time).ToList();
        rec.Duration = rec.Events.Count > 0 ? rec.Events[^1].Time : 0f;
        return rec;
    }
}
