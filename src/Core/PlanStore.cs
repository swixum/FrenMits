using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace FrenMits;

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
    private static readonly HashSet<string> JobAbbrs = new(Jobs.Abbreviations, StringComparer.OrdinalIgnoreCase);

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
            dirty |= SplitLineList(fight.Lines);
            foreach (var key in fight.SavedSlots.Keys.ToList())
                dirty |= SplitLineList(fight.SavedSlots[key]);
        }
        // Persist the split so it only runs once.
        if (dirty) Save(fights);
    }

    public static bool SplitLineList(List<MitLine> lines)
    {
        var dirty = false;
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line.Action)) continue;
            var segments = SplitTopLevel(line.Action);
            if (segments.Count <= 1) continue;

            // Replace this one line with N lines, one per action segment.
            lines.RemoveAt(i);
            for (var s = 0; s < segments.Count; s++)
            {
                var seg = segments[s].Trim();
                if (seg.Length == 0) continue;
                var jobs = ExtractJobGate(ref seg);
                var newLine = new MitLine
                {
                    Time = line.Time,
                    Mechanic = line.Mechanic,
                    Action = seg,
                    Jobs = jobs.Count > 0 ? jobs : new List<string>(line.Jobs),
                    Enabled = line.Enabled,
                    Custom = line.Custom,
                    OffsetSeconds = line.OffsetSeconds,
                    OffsetManual = line.OffsetManual,
                    CoverUntil = line.CoverUntil,
                    LeadOverride = line.LeadOverride,
                    Tts = s == 0 ? line.Tts : "",       // only the first segment inherits TTS
                    Sound = line.Sound,
                    Color = line.Color,
                    IconId = s == 0 ? line.IconId : 0u,  // only the first segment inherits icon
                };
                lines.Insert(i + s, newLine);
            }
            dirty = true;
        }
        return dirty;
    }

    /// <summary>
    /// Splits an action string at top-level '+' characters, respecting
    /// parenthesised groups so "(WAR/PLD)" stays intact.
    /// </summary>
    private static List<string> SplitTopLevel(string action)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < action.Length; i++)
        {
            var c = action[i];
            if (c == '(') depth++;
            else if (c == ')') { if (depth > 0) depth--; }
            else if (depth == 0 && c == '+')
            {
                parts.Add(action[start..i]);
                start = i + 1;
            }
        }
        parts.Add(action[start..]);
        return parts;
    }

    /// <summary>
    /// If the segment contains a parenthesised job gate like "(WAR/PLD)",
    /// extracts the job abbreviations into a list and strips the gate from
    /// the segment string. Returns an empty list when there's no gate.
    /// </summary>
    private static List<string> ExtractJobGate(ref string segment)
    {
        var jobs = new List<string>();
        var i = segment.IndexOf('(');
        if (i < 0) return jobs;
        var j = segment.IndexOf(')', i + 1);
        if (j < 0) return jobs;

        var inside = segment.Substring(i + 1, j - i - 1);
        var tokens = inside.Split('/');
        var allJobs = tokens.Length > 0;
        foreach (var t in tokens)
        {
            var tok = t.Trim();
            if (tok.Length == 0 || !JobAbbrs.Contains(tok)) { allJobs = false; break; }
        }
        if (!allJobs) return jobs;

        // Valid job gate found — extract and strip.
        foreach (var t in tokens)
            jobs.Add(t.Trim().ToUpperInvariant());

        segment = (segment[..i] + segment[(j + 1)..]).Trim();
        return jobs;
    }
}
