using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace FrenMits;

// Fight plans live in their own file next to the config, rather than inside it.
//
// They were 99.6% of what the config weighed - about 700KB of plans against 6KB
// of actual settings - and Configuration.Save() is called from nearly 200 places,
// so before this every checkbox re-serialized and rewrote every plan you own.
// Ticking a box now writes 6KB.
//
// Owning the serializer is also what lets the plan file drop the "$type" marker
// Dalamud's config serializer stamps on every single line, and honour the
// ShouldSerialize rules on MitLine.
public static class PlanStore
{
    public const string FileName = "plans.json";

    private static string PlanPath => Path.Combine(
        Service.PluginInterface.GetPluginConfigDirectory(), FileName);

    // Exactly what's on disk, so a save that changes nothing costs no disk at all
    // (Save runs on every plan edit, and most of them touch one line).
    private static string _onDisk = "";

    // Set when the file EXISTS but wouldn't load. Mirrors Configuration.SuppressSave:
    // a plan file we can't read is one we must not overwrite, because the copy on
    // disk is the only one the user still has.
    public static bool Broken { get; private set; }

    // The plans on disk, or null when there is no plan file yet (a fresh install,
    // or a profile that predates the split and still carries them in its config).
    //
    // Null is deliberately NOT the same as an empty list: empty means "this user
    // deleted all their fights", and must not trigger the migration below.
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
            // Keep the unreadable file intact and stop writing for the session, so
            // it stays recoverable instead of being replaced by whatever ended up
            // in memory.
            Backup(file, ".corrupt.bak");
            Service.Log?.Error(ex,
                $"FrenMits: plans.json exists ({json.Length} bytes) but failed to parse. " +
                "Backed up; plan saving is OFF for this session so nothing overwrites it.");
            Broken = true;
            return null;
        }
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

    // Taken once, immediately before the first save that leaves plans out of the
    // config. Rolling back to an older FrenMits would otherwise find no fights,
    // since that build only ever looked inside the config for them.
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
