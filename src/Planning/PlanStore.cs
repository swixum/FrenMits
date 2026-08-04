using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace FrenMits.Planning;

// Fight plans live in their own file next to the config.
public static class PlanStore
{
    public const string FileName = "plans.json";

    private static string PlanPath => Path.Combine(
        Service.PluginInterface.GetPluginConfigDirectory(), FileName);

    // Exactly what's on disk, so an unchanged save costs nothing.
    private static string _onDisk = "";

    // Set when the file exists but wouldn't load.
    public static bool Broken { get; private set; }

    // The plans on disk, or null when there is no file yet.
    public static List<FightProfile>? Load()
    {
        string json;
        var file = PlanPath;
        try
        {
            if (!File.Exists(file)) return null;
            json = File.ReadAllText(file);
        }
        catch (Exception ex)
        {
            Service.Log?.Error(ex, "FrenMits: plans.json could not be read");
            Broken = true;
            return null;
        }

        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            var list = JsonConvert.DeserializeObject<List<FightProfile>>(json);
            if (list == null) throw new InvalidDataException("plans.json parsed to null");
            _onDisk = json;
            SplitCombinedActions(list);
            return list;
        }
        catch (Exception ex)
        {
            // Keep an unreadable file and stop writing this session.
            Backup(file, ".corrupt.bak");
            Service.Log?.Error(ex,
                $"FrenMits: plans.json exists ({json.Length} bytes) but failed to parse. " +
                "Backed up; plan saving is OFF for this session so nothing overwrites it.");
            Broken = true;
            return null;
        }
    }

    // Which copy to believe when both carry fights.
    public static bool PreferConfigCopy(bool planFileExists, int legacyCount, bool configIsNewer)
        => legacyCount > 0 && (!planFileExists || configIsNewer);

    // True when the config was written after the plan file.
    public static bool ConfigIsNewerThanPlans()
    {
        try
        {
            var cfg = Service.PluginInterface.ConfigFile;
            var plans = PlanPath;
            if (cfg is not { Exists: true } || !File.Exists(plans)) return false;
            return File.GetLastWriteTimeUtc(cfg.FullName) > File.GetLastWriteTimeUtc(plans);
        }
        catch (Exception ex)
        {
            Service.Log?.Warning(ex, "FrenMits: could not compare config and plan file times");
            return false;
        }
    }

    public static bool Exists()
    {
        try { return File.Exists(PlanPath); }
        catch { return false; }
    }

    public static void Save(List<FightProfile>? fights)
    {
        if (fights == null || Broken || Configuration.SuppressSave) return;
        try
        {
            var json = JsonConvert.SerializeObject(fights);
            if (string.Equals(json, _onDisk, StringComparison.Ordinal)) return;

            var dir = Service.PluginInterface.GetPluginConfigDirectory();
            Directory.CreateDirectory(dir);
            var file = PlanPath;
            // Write beside it and swap, so a crash can't half-write.
            var tmp = file + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(file)) File.Replace(tmp, file, null);
            else File.Move(tmp, file);
            _onDisk = json;
        }
        catch (Exception ex)
        {
            Service.Log?.Error(ex, "FrenMits: plans.json could not be written");
        }
    }

    // Taken once, right before plans leave the config.
    public static void BackupConfigBeforeSplit()
    {
        try
        {
            var cfg = Service.PluginInterface.ConfigFile;
            if (cfg is { Exists: true }) Backup(cfg.FullName, ".pre-plan-split.bak");
        }
        catch (Exception ex)
        {
            Service.Log?.Warning(ex, "FrenMits: could not back up the config before the plan split");
        }
    }

    private static void Backup(string file, string suffix)
    {
        try
        {
            var bak = file + suffix;
            if (!File.Exists(bak)) File.Copy(file, bak);
        }
        catch (Exception ex)
        {
            Service.Log?.Warning(ex, $"FrenMits: could not back up {file}");
        }
    }

    // ---- One-time migration: split "A + B" action strings into individual MitLine objects ----

    // Job abbreviations for extracting inline (WAR/PLD) gates.

    /// <summary>
    /// Walks every fight's Lines and SavedSlots; any MitLine whose Action
    /// contains a top-level '+' is replaced by N independent lines, one per
    /// action segment. Inline job gates like "(WAR/PLD)" are extracted into
    /// the new line's Jobs list and stripped from the action name.
    /// </summary>
    private static void SplitCombinedActions(List<FightProfile> fights)
    {
        var dirty = false;
        foreach (var fight in fights)
        {
            dirty |= LineSplit.SplitLineList(fight.Lines);
            foreach (var key in fight.SavedSlots.Keys.ToList())
                dirty |= LineSplit.SplitLineList(fight.SavedSlots[key]);
        }
        // Persist the split so it only runs once.
        if (dirty) Save(fights);
    }


}
