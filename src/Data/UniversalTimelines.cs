using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace FrenMits;

// Baked boss timelines for (nearly) every instanced duty in the game (per
// territory, the bosses' named casts plus resync anchors), powering a "runs
// everywhere" Next Mits board of what the boss is about to do with no mits or
// audio, resynced off the bosses' own casts.
public static class UniversalTimelines
{
    private sealed class Zone
    {
        public List<(float Time, string Name)> Entries = new();
        public List<(float Time, uint Ability, bool Phase)> Syncs = new();
    }

    private static Dictionary<uint, Zone>? _zones;

    // Built into a LOCAL dictionary and published in one go at the end. Assigning
    // _zones first and filling it afterwards means anything reading it during the
    // load sees a half-populated table and decides the duty has no timeline - the
    // plugin only ever loads from the framework thread, so it never hit this, but
    // it is one line to make the field mean what it says.
    private static void Load()
    {
        if (_zones != null) return;
        var zones = new Dictionary<uint, Zone>();
        try
        {
            using var s = typeof(UniversalTimelines).Assembly
                .GetManifestResourceStream("FrenMits.universal_timelines.json.gz");
            if (s == null) { Service.Log?.Warning("[FrenMits] universal timelines resource missing"); _zones = zones; return; }
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
                // The board walks entries in list order; don't trust the file
                // to be time-sorted (older bakes weren't for branching fights).
                zone.Entries.Sort((a, b) => a.Time.CompareTo(b.Time));
                zone.Syncs.Sort((a, b) => a.Time.CompareTo(b.Time));
                zones[terr] = zone;
            }
            Service.Log?.Information($"[FrenMits] universal timelines loaded: {zones.Count} duties");
        }
        catch (Exception e)
        {
            Service.Log?.Error(e, "[FrenMits] universal timelines failed to load");
        }
        _zones = zones;
    }

    public static bool Has(uint territory)
    {
        Load();
        return _zones!.ContainsKey(territory);
    }

    // Every duty that ships a timeline, so the whole set can be checked at once
    // rather than one territory at a time.
    public static IEnumerable<uint> Territories()
    {
        Load();
        return _zones!.Keys;
    }

    // A fresh IN-MEMORY timeline-only fight for this duty, rebuilt on territory
    // change and never saved, carrying only mechanic names so the board lists
    // them while the call overlay and audio stay silent.
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
        // Only an ability's FIRST anchor may re-base the clock. A duty gives phase
        // anchors a 20000s forward window to reach each boss's own block, so one
        // that re-bases on an ability an EARLIER boss also casts drags the board a
        // whole boss or more ahead, mid fight. See SyncAnchors.Guard.
        SyncAnchors.Guard(f.SyncPoints, SyncAnchors.EncounterStarts(f.Lines.Select(l => l.Time)));
        return f;
    }

    private static string DutyName(uint territory)
    {
        try
        {
            var t = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()?.GetRowOrDefault(territory);
            var cfc = t?.ContentFinderCondition.ValueNullable;
            var name = cfc?.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name))
                return char.ToUpperInvariant(name![0]) + name[1..];
        }
        catch
        {
            // fall through to the generic label
        }
        return "Duty timeline";
    }
}
