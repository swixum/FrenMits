using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace FrenMits;

// Fight plans live in their own file next to the config, rather than inside it.
public static class PlanStore
{
    public const string FileName = "plans.json";

    private static string PlanPath => Path.Combine(
        Service.PluginInterface.GetPluginConfigDirectory(), FileName);

    // Exactly what's on disk, so a save that changes nothing costs no disk at all
    // (Save runs on every plan edit, and most of them touch one line).
    private static string _onDisk = "";

    // Set when the file EXISTS but wouldn't load.
    public static bool Broken { get; private set; }

    // The plans on disk, or null when there is no plan file yet.
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
            return list;
        }
        catch (Exception ex)
        {
            // Keep an unreadable file intact and stop writing for the session.
            Backup(file, ".corrupt.bak");
            Service.Log?.Error(ex,
                $"FrenMits: plans.json exists ({json.Length} bytes) but failed to parse. " +
                "Backed up; plan saving is OFF for this session so nothing overwrites it.");
            Broken = true;
            return null;
        }
    }

    // Which copy to believe when the config STILL carries fights and a plan
    // file exists as well.
    public static bool PreferConfigCopy(bool planFileExists, int legacyCount, bool configIsNewer)
        => legacyCount > 0 && (!planFileExists || configIsNewer);

    // True when the config file was written more recently than the plan file.
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
            // Write beside it and swap, so power loss or a crash mid-write leaves
            // the previous plans intact rather than a half-written file.
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

    // Taken once, immediately before the first save that leaves plans out of
    // the config.
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
}
