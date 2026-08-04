using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Planning;

// Plan snapshots, written to the plugin config directory.
public sealed class SnapshotStore
{
    private readonly Configuration _config;

    public SnapshotStore(Configuration config) => _config = config;

    public sealed class PlanBackup
    {
        public string Reason = "";
        public string FightName = "";
        public DateTime When;
        public FightProfile Fight = null!;
    }

    public readonly record struct SnapshotInfo(string File, DateTime When, string Reason);

    private static string Dir => System.IO.Path.Combine(
        Service.PluginInterface.GetPluginConfigDirectory(), "snapshots");

    // A fight id is a bare guid, so anything else can't reach the file name.
    private static string FileKey(string? id)
    {
        var chars = (id ?? "").Where(char.IsLetterOrDigit).Take(64).ToArray();
        return chars.Length > 0 ? new string(chars) : "unknown";
    }

    public void Save(FightProfile fight, string reason)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Dir);
            var file = System.IO.Path.Combine(Dir,
                $"{FileKey(fight.Id)}_{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
            System.IO.File.WriteAllText(file, Newtonsoft.Json.JsonConvert.SerializeObject(
                new PlanBackup { Reason = reason, FightName = fight.Name, When = DateTime.Now, Fight = fight }));

            // Keep the newest 12 per fight.
            var mine = System.IO.Directory.GetFiles(Dir, $"{FileKey(fight.Id)}_*.json")
                .OrderByDescending(f => f).ToList();
            foreach (var old in mine.Skip(12)) System.IO.File.Delete(old);
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: snapshot failed");
        }
    }

    public List<SnapshotInfo> List(string fightId)
    {
        var list = new List<SnapshotInfo>();
        try
        {
            if (!System.IO.Directory.Exists(Dir)) return list;
            foreach (var file in System.IO.Directory.GetFiles(Dir, $"{FileKey(fightId)}_*.json")
                         .OrderByDescending(f => f))
            {
                try
                {
                    var b = Newtonsoft.Json.JsonConvert.DeserializeObject<PlanBackup>(
                        System.IO.File.ReadAllText(file));
                    if (b != null) list.Add(new SnapshotInfo(file, b.When, b.Reason));
                }
                // One bad file shouldn't hide the rest, but say so.
                catch (Exception ex) { Swallowed.Report("plan snapshot read", ex); }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: snapshot list failed");
        }
        return list;
    }

    // Snapshots left behind by deleted fights of this duty.
    public List<SnapshotInfo> ListOrphans(uint territory, string excludeFightId)
    {
        var list = new List<SnapshotInfo>();
        try
        {
            if (territory == 0 || !System.IO.Directory.Exists(Dir)) return list;
            foreach (var file in System.IO.Directory.GetFiles(Dir, "*.json")
                         .OrderByDescending(f => f))
            {
                if (excludeFightId.Length > 0
                    && System.IO.Path.GetFileName(file).StartsWith(FileKey(excludeFightId) + "_")) continue;
                try
                {
                    var b = Newtonsoft.Json.JsonConvert.DeserializeObject<PlanBackup>(
                        System.IO.File.ReadAllText(file));
                    if (b?.Fight != null && b.Fight.TerritoryId == territory)
                        list.Add(new SnapshotInfo(file, b.When, b.Reason + " [previous sheet]"));
                }
                // One bad file shouldn't hide the rest, but say so.
                catch (Exception ex) { Swallowed.Report("plan snapshot read", ex); }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: orphan snapshot scan failed");
        }
        return list;
    }

    // Restore a snapshot over the target fight.
    public string Restore(FightProfile target, string file)
    {
        try
        {
            var b = Newtonsoft.Json.JsonConvert.DeserializeObject<PlanBackup>(
                System.IO.File.ReadAllText(file));
            if (b?.Fight == null) return "That snapshot couldn't be read.";

            target.Lines = b.Fight.Lines ?? new();
            target.SavedSlots = b.Fight.SavedSlots ?? new();
            target.DeletedCalls = b.Fight.DeletedCalls ?? new();
            target.Notes = b.Fight.Notes ?? new();
            target.Slot = b.Fight.Slot;
            target.TimerOffset = b.Fight.TimerOffset;
            if (!Builtin.Has(target.TerritoryId))
            {
                target.SyncPoints = b.Fight.SyncPoints ?? new();
                target.BossAnchors = b.Fight.BossAnchors ?? new();
                // Columns only when present, so an old snapshot can't wipe them.
                if (b.Fight.CustomSlots is { Count: > 0 })
                {
                    target.CustomSlots = b.Fight.CustomSlots;
                    target.CustomRows = b.Fight.CustomRows ?? new();
                    target.CustomDowntimes = b.Fight.CustomDowntimes ?? new();
                }
            }
            // Restore the active-slot alias.
            if (!string.IsNullOrEmpty(target.Slot) && target.SavedSlots.ContainsKey(target.Slot))
                target.SavedSlots[target.Slot] = target.Lines;
            // Old snapshots carry legacy slot names, so standardize them.
            SlotNames.NormalizeFight(target);
            _config.Save();
            return $"Restored the {b.When:MMM d, h:mm tt} snapshot ({b.Reason}).";
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: snapshot restore failed");
            return "That snapshot couldn't be read.";
        }
    }
}
