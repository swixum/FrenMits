using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FrenMits.Encounters;

// Baked boss timelines for nearly every instanced duty.
public static class UniversalTimelines
{
    private sealed class Zone
    {
        public List<(float Time, string Name)> Entries = new();
        public List<(float Time, uint Ability, bool Phase)> Syncs = new();
    }

    private static volatile Dictionary<uint, Zone>? _zones;
    private static readonly object LoadGate = new();

    // Unpacking this costs a frame, so spend it off-thread before a duty asks.
    public static void Warm()
        => Task.Run(() => { try { Load(); } catch { /* the read logs its own trouble */ } });

    // Whoever asks first loads it; everyone else waits on that one read.
    private static void Load()
    {
        if (_zones != null) return;
        lock (LoadGate)
        {
            if (_zones == null) ReadResource();
        }
    }

    // Built locally and published in one go at the end.
    private static void ReadResource()
    {
        var zones = new Dictionary<uint, Zone>();
        try
        {
            using var s = typeof(UniversalTimelines).Assembly
                .GetManifestResourceStream("FrenMits.universal_timelines.json.gz");
            if (s == null) { EncounterLog.Warn("[FrenMits] universal timelines resource missing"); _zones = zones; return; }
            using var gz = new GZipStream(s, CompressionMode.Decompress);
            using var r = new StreamReader(gz);
            var root = JObject.Parse(r.ReadToEnd());
            foreach (var prop in root.Properties())
            {
                if (!uint.TryParse(prop.Name, out var terr) || prop.Value is not JObject z) continue;
                var zone = new Zone();
                if (z["e"] is JArray es)
                    foreach (var a in es)
                        zone.Entries.Add(((float)a[0]!, (string)a[1]!));
                if (z["s"] is JArray ss)
                    foreach (var a in ss)
                        zone.Syncs.Add(((float)a[0]!, (uint)a[1]!, (int)a[2]! != 0));
                // The board walks in list order, so sort rather than trust.
                zone.Entries.Sort((a, b) => a.Time.CompareTo(b.Time));
                zone.Syncs.Sort((a, b) => a.Time.CompareTo(b.Time));
                zones[terr] = zone;
            }
            EncounterLog.Info($"[FrenMits] universal timelines loaded: {zones.Count} duties");
        }
        catch (Exception e)
        {
            EncounterLog.Error("[FrenMits] universal timelines failed to load", e);
        }
        _zones = zones;
    }

    public static bool Has(uint territory)
    {
        Load();
        return _zones!.ContainsKey(territory);
    }

    // Every duty that ships a timeline, for checking the set.
    public static IEnumerable<uint> Territories()
    {
        Load();
        return _zones!.Keys;
    }

    // A fresh timeline-only fight for this duty, never saved.
    public static FightProfile? Build(uint territory)
    {
        Load();
        if (!_zones!.TryGetValue(territory, out var z)) return null;
        var f = new FightProfile
        {
            TerritoryId = territory,
            Name = DutyName(territory),
            Category = "Other",
            TimelineOnly = true,
        };
        foreach (var (t, name) in z.Entries)
            f.Lines.Add(new MitLine { Time = t, Mechanic = name, Action = "", Sound = false });
        foreach (var (t, id, phase) in z.Syncs)
            f.SyncPoints.Add(new SyncPoint { Ability = id, Time = t, IsPhase = phase, Label = "auto" });
        // Only an ability's FIRST anchor may re-base the clock.
        SyncAnchors.Guard(f.SyncPoints, SyncAnchors.EncounterStarts(f.Lines.Select(l => l.Time)));
        return f;
    }

    // True when a duty's timeline counts from a block base rather than the
    // pull, which is how the long field-op instances are written.
    public static bool UsesBlockTimes(uint territory)
    {
        Load();
        return _zones!.TryGetValue(territory, out var z)
               && z.Entries.Count > 0 && z.Entries[0].Time >= 1000f;
    }

    // How many mechanics this duty's timeline carries.
    public static int RowCount(uint territory)
    {
        Load();
        return _zones!.TryGetValue(territory, out var z) ? z.Entries.Count : 0;
    }

    // An editable sheet seeded from this duty's timeline, columns and all.
    public static FightProfile? BuildSheet(uint territory)
    {
        Load();
        if (!_zones!.TryGetValue(territory, out var z)) return null;
        var f = new FightProfile
        {
            TerritoryId = territory,
            Name = DutyName(territory),
            Category = "Custom",
            CustomSlots = SlotNames.Standard.ToList(),
        };
        // Mechanics are scaffold rows, so the grid is plannable at once.
        foreach (var (t, name) in z.Entries)
            f.CustomRows.Add(new CustomRow { Time = t, Mechanic = name });
        foreach (var (t, id, phase) in z.Syncs)
            f.SyncPoints.Add(new SyncPoint { Ability = id, Time = t, IsPhase = phase, Label = "auto" });
        SyncAnchors.Guard(f.SyncPoints, SyncAnchors.EncounterStarts(z.Entries.Select(e => e.Time)));
        return f;
    }

    // The duty's display name, supplied by the host.
    public static Func<uint, string> DutyNameOf { get; set; } = _ => "Duty timeline";

    private static string DutyName(uint territory) => DutyNameOf(territory);
}
