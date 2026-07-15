using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FrenMits.Windows;

namespace FrenMits;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/frenmits";
    private const string CommandAlias = "/fm";

    // Dancing Mad (Ultimate) instance territory (kept for the preset button).
    public const ushort DancingMadUltimateTerritory = Builtin.DmuTerritory;

    public Configuration Config { get; }
    public CombatTimer Timer { get; } = new();
    public FontManager Fonts { get; } = new();
    public Audio Audio { get; } = new();
    public CueEngine Cues { get; }
    public SyncEngine Sync { get; }
    public ReplayEngine Replay { get; }
    public MitReview Review { get; }
    public MitRecap Recap { get; }
    public Diagnostics Diag { get; }
    public readonly WindowSystem Windows = new("FrenMits");
    public ConfigWindow ConfigWindow { get; }
    public OverlayWindow OverlayWindow { get; }
    public TimelineWindow TimelineWindow { get; }
    public MitBarWindow MitBarWindow { get; }
    public CombatTimerWindow CombatTimerWindow { get; }
    public WhatsNewWindow WhatsNewWindow { get; }
    public RecapButtonWindow RecapButtonWindow { get; }
    public RecapWindow RecapWindow { get; }
    public SheetViewWindow SheetViewWindow { get; }

    private readonly IDtrBarEntry? _dtr;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();

        Config = LoadConfig();
        Config.Fights ??= new();

        // v2: split the upcoming list into its own timeline window and switch the
        // main call to the clean "Raidwide (3.3)" countdown shown 3s ahead.
        if (Config.Version < 2)
        {
            Config.HeadlineFormat = "{action} ({remaining})";
            Config.ShowCountdownNumber = false;
            Config.WarningSeconds = 3f;
            Config.Version = 2;
            Config.Save();
        }

        // v3: assign sidebar categories. Built-ins are ultimates; everything else
        // starts in "Other" and can be moved with the per-fight Category picker.
        if (Config.Version < 3)
        {
            foreach (var f in Config.Fights)
                if (string.IsNullOrEmpty(f.Category))
                    f.Category = Builtin.Has(f.TerritoryId) ? "Ultimate" : "Other";
            Config.Version = 3;
            Config.Save();
        }

        // v4: per-pull diagnostics on by default (local only). Flip existing
        // profiles on once; the toggle stays so it can still be turned off.
        if (Config.Version < 4)
        {
            Config.Diagnostics = true;
            Config.Version = 4;
            Config.Save();
        }

        // v5: the Ikuya sheet had a big v3.0 mit rework, so rebake all built-in
        // fights once to clear stale lines and start fresh on the new plan.
        if (Config.Version < 5)
        {
            ResetAllBuiltins();
            Config.Version = 5;
            Config.Save();
        }

        // v6: the legacy ultimate timelines (UCOB/UWU/TEA/DSR/TOP) were re-timed
        // from real FFLogs clears (the old cactbot-derived times were inflated
        // 2-4x). The shifts are far larger than the top-up's merge window, so a
        // plain re-load would leave stale duplicate lines — clean-rebake just
        // those five fights. DMU/FRU/M12S are unchanged, so any edits there stay.
        if (Config.Version < 6)
        {
            foreach (var f in Config.Fights)
            {
                if (!IkuyaTimelines.Has(f.TerritoryId)) continue;
                f.SavedSlots.Clear();
                if (!string.IsNullOrEmpty(f.Slot))
                    Builtin.ResetSlot(f, f.Slot);
                else { f.Lines.Clear(); f.AutoLoaded = false; }
            }
            Config.Version = 6;
            Config.Save();
        }

        // v7: Dancing Mad mits resynced to the Ikuya sheet v4.0 (action + timing
        // overwrites, line splits, new rows) and WHM Asylum added from FFLogs.
        // The shifts are far larger than the top-up's merge window, so a plain
        // re-load would leave stale/duplicate lines - clean-rebake just the DMU
        // built-in so everyone gets the new plan on update. Other built-ins are
        // unchanged, so any edits there stay.
        if (Config.Version < 7)
        {
            foreach (var f in Config.Fights)
            {
                if (f.TerritoryId != Builtin.DmuTerritory) continue;
                f.SavedSlots.Clear();
                if (!string.IsNullOrEmpty(f.Slot))
                    Builtin.ResetSlot(f, f.Slot);
                else { f.Lines.Clear(); f.AutoLoaded = false; }
            }
            Config.Version = 7;
            Config.Save();
        }

        // v8: the Ikuya sheet's v4.0 was edited in place after the v7 bake (P3 Black
        // Holes restructure, P4 Grand Cross reshuffle, P2/P5 tweaks). Re-bake DMU to
        // the new timeline, but KEEP custom lines people added - a smart merge that
        // only replaces the lines matching the previous bake (DmuLegacy snapshot).
        if (Config.Version < 8)
        {
            SmartRebakeDmu();
            Config.Version = 8;
            Config.Save();
        }

        // v9: an earlier DMU merge could leave overlapping / stale lines (it matched
        // on the mechanic label, which the sheet renames). Re-run the smart re-bake
        // with the hardened de-overlap so nothing doubles up, and flag surviving
        // custom lines so future sheet updates keep them cleanly.
        if (Config.Version < 9)
        {
            SmartRebakeDmu();
            Config.Version = 9;
            Config.Save();
        }

        // v10: ship the full sheet refresh to everyone - re-bake DMU to the latest
        // baked timings (the smart merge keeps every custom line people added).
        if (Config.Version < 10)
        {
            SmartRebakeDmu();
            Config.Version = 10;
            Config.Save();
        }

        // v11: a deliberate one-time CLEAN reset of Dancing Mad to the sheet, wiping
        // any custom lines too (to clear overlapping/stale data from earlier merges).
        // Custom lines added AFTER this are still kept by the smart re-bake going
        // forward (they get flagged Custom). Other built-ins are untouched.
        if (Config.Version < 11)
        {
            foreach (var f in Config.Fights)
            {
                if (f.TerritoryId != Builtin.DmuTerritory) continue;
                f.SavedSlots.Clear();
                if (!string.IsNullOrEmpty(f.Slot))
                    Builtin.ResetSlot(f, f.Slot);
                else { f.Lines.Clear(); f.AutoLoaded = false; }
            }
            Config.Version = 11;
            Config.Save();
        }

        // v12: the sheet was re-timed again (every row nudged 1-5s, a helper column
        // added, P5 enrage marker). Force another clean reset of Dancing Mad for
        // everyone so the new timings land cleanly. Custom lines added after still
        // survive the smart re-bake going forward.
        if (Config.Version < 12)
        {
            foreach (var f in Config.Fights)
            {
                if (f.TerritoryId != Builtin.DmuTerritory) continue;
                f.SavedSlots.Clear();
                if (!string.IsNullOrEmpty(f.Slot))
                    Builtin.ResetSlot(f, f.Slot);
                else { f.Lines.Clear(); f.AutoLoaded = false; }
            }
            Config.Version = 12;
            Config.Save();
        }

        // v13: hard reset Dancing Mad again so everyone is freshly baked from the
        // current sheet (now that generic mits resolve to each job's icon). Custom
        // lines added after this still survive the smart re-bake going forward.
        if (Config.Version < 13)
        {
            foreach (var f in Config.Fights)
            {
                if (f.TerritoryId != Builtin.DmuTerritory) continue;
                f.SavedSlots.Clear();
                if (!string.IsNullOrEmpty(f.Slot))
                    Builtin.ResetSlot(f, f.Slot);
                else { f.Lines.Clear(); f.AutoLoaded = false; }
            }
            Config.Version = 13;
            Config.Save();
        }

        // v14: hard reset Dancing Mad once more so the latest baked timeline is in
        // for everyone (pairs with calls now showing each job's real ability name).
        // Custom lines added after this still survive the smart re-bake going forward.
        if (Config.Version < 14)
        {
            foreach (var f in Config.Fights)
            {
                if (f.TerritoryId != Builtin.DmuTerritory) continue;
                f.SavedSlots.Clear();
                if (!string.IsNullOrEmpty(f.Slot))
                    Builtin.ResetSlot(f, f.Slot);
                else { f.Lines.Clear(); f.AutoLoaded = false; }
            }
            Config.Version = 14;
            Config.Save();
        }

        // Migrate the old M12S placeholder zone (1320) to the real one (1327).
        foreach (var f in Config.Fights)
            if (f.TerritoryId == 1320)
            {
                f.TerritoryId = Builtin.M12sTerritory;
                f.Category = "Savage";
            }

        // v15: stored fight names may carry an em dash from an older seed, which
        // the game font renders as an empty box. Normalize to a plain hyphen.
        if (Config.Version < 15)
        {
            foreach (var f in Config.Fights)
                if (f.Name.Contains('—'))
                    f.Name = f.Name.Replace('—', '-');
            Config.Version = 15;
            Config.Save();
        }

        // Auto-add any built-in fight the user hasn't been shown yet, so a newly
        // shipped fight (e.g. a fresh savage) appears directly on its tab with no
        // button to click. Tracked per-territory so a deleted built-in stays gone.
        Config.SeededTerritories ??= new();
        var seeded = false;
        foreach (var (territory, name, category) in Builtin.Fights)
        {
            if (Config.SeededTerritories.Contains(territory)) continue;
            Config.SeededTerritories.Add(territory);
            if (Config.Fights.All(f => f.TerritoryId != territory))
                Config.Fights.Add(new FightProfile { Name = name, TerritoryId = territory, Category = category });
            seeded = true;
        }

        // Migrate the two built-ins that were renamed (dropped the redundant
        // "(Ultimate)" suffix for the short code, matching the others). Only touches
        // the exact old default names, so a fight you renamed yourself is left alone.
        foreach (var f in Config.Fights)
        {
            if (f.Name == "Dancing Mad (Ultimate)") { f.Name = Builtin.Name(Builtin.DmuTerritory); seeded = true; }
            else if (f.Name == "Futures Rewritten (Ultimate)") { f.Name = Builtin.Name(Builtin.FruTerritory); seeded = true; }
        }

        if (seeded) Config.Save();

        // NOTE: the default-slot prebake and the "already inside a boss room"
        // auto-load both need live game state (the player's job via ObjectTable,
        // and the current territory). Dalamud only permits ObjectTable/ClientState
        // access on the game's main thread, but this constructor runs on a loader
        // thread — touching them here throws InvalidOperationException and aborts
        // the load. Both are deferred to the first Framework.Update tick instead,
        // which is guaranteed to run on the main thread. See RunFirstTickInit().

        Cues = new CueEngine(this, Audio);
        Sync = new SyncEngine(this);
        Replay = new ReplayEngine(this);
        Review = new MitReview(this);
        Recap = new MitRecap(this);
        Diag = new Diagnostics(this);
        ConfigWindow = new ConfigWindow(this);
        OverlayWindow = new OverlayWindow(this);
        TimelineWindow = new TimelineWindow(this);
        MitBarWindow = new MitBarWindow(this);
        CombatTimerWindow = new CombatTimerWindow(this);
        RecapButtonWindow = new RecapButtonWindow(this);
        RecapWindow = new RecapWindow(this);
        SheetViewWindow = new SheetViewWindow(this);
        WhatsNewWindow = new WhatsNewWindow(this);
        Windows.AddWindow(ConfigWindow);
        Windows.AddWindow(OverlayWindow);
        Windows.AddWindow(TimelineWindow);
        Windows.AddWindow(MitBarWindow);
        Windows.AddWindow(CombatTimerWindow);
        Windows.AddWindow(RecapButtonWindow);
        Windows.AddWindow(RecapWindow);
        Windows.AddWindow(SheetViewWindow);
        Windows.AddWindow(WhatsNewWindow);
        OverlayWindow.IsOpen = true;
        TimelineWindow.IsOpen = true;
        MitBarWindow.IsOpen = true;
        CombatTimerWindow.IsOpen = true;
        RecapButtonWindow.IsOpen = true;
        // Pop the "What's New" panel once after an update with notes.
        WhatsNewWindow.IsOpen = Config.LastWhatsNew != WhatsNewWindow.NotesVersion;

        Service.CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Fren Mits. /fm sheet = the all-slots sheet view, /fm sync = zero the timer, /fm test = toggle test mode, /fm reset = clear the timer, /fm p4 = practice-jump to a phase."
        });
        Service.CommandManager.AddHandler(CommandAlias, new CommandInfo(OnCommand));

        try { _dtr = Service.DtrBar.Get("Fren Mits"); }
        catch (Exception ex) { Service.Log.Warning(ex, "FrenMits: DTR entry failed"); }

        Service.PluginInterface.UiBuilder.Draw += DrawUi;
        Service.PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        Service.PluginInterface.UiBuilder.OpenMainUi += OpenConfig;
        Service.Framework.Update += OnFrameworkUpdate;
        Service.ClientState.TerritoryChanged += OnTerritoryChanged;

        // Diagnostic: if this ever logs "#2" (or higher) while only one copy should be
        // running, the plugin is double-loaded — which would double every audio cue.
        var n = System.Threading.Interlocked.Increment(ref _liveInstances);
        Service.Log.Information($"[FrenMits] init - live instance #{n}");
    }

    private static int _liveInstances;

    // Load the saved config defensively. GetPluginConfig() returns null both for a
    // genuine first run (no file) AND when an existing file fails to deserialize
    // (a partial write interrupted by a crash, an unresolved $type after a rename,
    // a transient read error mid-update). The old code couldn't tell the two apart:
    // it fell back to a fresh default config and the version migrations immediately
    // Save()'d it, overwriting the user's real settings for good. Now, if the file
    // exists but won't load, we keep it intact (backed up) and suppress saves for
    // the session instead of clobbering it — so a one-off hiccup can't wipe colours
    // and edits, and the original is recoverable.
    private static Configuration LoadConfig()
    {
        try
        {
            if (Service.PluginInterface.GetPluginConfig() is Configuration cfg)
                return cfg;
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "FrenMits: GetPluginConfig threw");
        }

        var file = Service.PluginInterface.ConfigFile;
        if (file is { Exists: true } && file.Length > 2)
        {
            // The file is there but unreadable — do NOT treat this as a first run.
            try
            {
                var bak = file.FullName + ".corrupt.bak";
                System.IO.File.Copy(file.FullName, bak, overwrite: true);
                Service.Log.Error(
                    $"FrenMits: config exists ({file.Length} bytes) but failed to load. Backed up to {bak}. " +
                    "Running on defaults WITHOUT saving over your file so it can be recovered.");
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "FrenMits: failed to back up unreadable config");
            }

            Configuration.SuppressSave = true;
        }

        return new Configuration();
    }

    // Seamless auto-load: on entering a boss room we support, top up the fight's
    // lines with the latest baked timeline (adding only what's missing) and refresh
    // the resync anchors — keeping every edit the user has made. Silent, no prompts.
    private void OnTerritoryChanged(uint territory)
    {
        // Leaving / re-entering the instance resets the door-boss phase to 1.
        _phaseTwo = false;
        _trackedBossEntity = 0;
        _trackedBossLastHp = 0;
        try { AutoLoadForTerritory(territory); }
        catch (Exception ex) { Service.Log.Error(ex, "FrenMits: auto-load failed"); }
    }

    // Full refresh: rebake every built-in fight's lines fresh from the current
    // sheet data, discarding saved per-slot edits (and any added potion/tank
    // lines). Used by the "Refresh from sheet" button and the one-time migration
    // after a big sheet update. Returns how many fights were rebaked.
    public int ResetAllBuiltins()
    {
        var n = 0;
        foreach (var f in Config.Fights)
        {
            if (!Builtin.Has(f.TerritoryId)) continue;
            if (f.Lines.Count > 0 || f.SavedSlots.Count > 0)
                SnapshotPlan(f, "before Refresh from sheet");
            f.SavedSlots.Clear();
            f.DeletedCalls.Clear();             // a full refresh un-deletes everything
            if (!string.IsNullOrEmpty(f.Slot))
                Builtin.ResetSlot(f, f.Slot);   // fresh bake of the active slot
            else
            {
                f.Lines.Clear();                // no slot yet: auto-load will bake on zone-in
                f.AutoLoaded = false;
            }
            n++;
        }
        Config.Save();
        return n;
    }

    // ---- plan snapshots ---------------------------------------------------
    // A snapshot is the whole FightProfile serialized to a file under the plugin
    // config directory, taken automatically before destructive operations and on
    // demand (Sheet View's History button). Pruned to the newest per fight.

    public sealed class PlanBackup
    {
        public string Reason = "";
        public string FightName = "";
        public DateTime When;
        public FightProfile Fight = null!;
    }

    public readonly record struct SnapshotInfo(string File, DateTime When, string Reason);

    private string SnapshotDir => System.IO.Path.Combine(
        Service.PluginInterface.GetPluginConfigDirectory(), "snapshots");

    public void SnapshotPlan(FightProfile fight, string reason)
    {
        try
        {
            System.IO.Directory.CreateDirectory(SnapshotDir);
            var file = System.IO.Path.Combine(SnapshotDir,
                $"{fight.Id}_{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
            System.IO.File.WriteAllText(file, Newtonsoft.Json.JsonConvert.SerializeObject(
                new PlanBackup { Reason = reason, FightName = fight.Name, When = DateTime.Now, Fight = fight }));

            // Keep the newest 12 per fight.
            var mine = System.IO.Directory.GetFiles(SnapshotDir, $"{fight.Id}_*.json")
                .OrderByDescending(f => f).ToList();
            foreach (var old in mine.Skip(12)) System.IO.File.Delete(old);
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: snapshot failed");
        }
    }

    public List<SnapshotInfo> ListSnapshots(string fightId)
    {
        var list = new List<SnapshotInfo>();
        try
        {
            if (!System.IO.Directory.Exists(SnapshotDir)) return list;
            foreach (var file in System.IO.Directory.GetFiles(SnapshotDir, $"{fightId}_*.json")
                         .OrderByDescending(f => f))
            {
                try
                {
                    var b = Newtonsoft.Json.JsonConvert.DeserializeObject<PlanBackup>(
                        System.IO.File.ReadAllText(file));
                    if (b != null) list.Add(new SnapshotInfo(file, b.When, b.Reason));
                }
                catch { /* one unreadable file shouldn't hide the rest */ }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: snapshot list failed");
        }
        return list;
    }

    // Restore a snapshot file over the target fight (full plan replace).
    public string RestoreSnapshot(FightProfile target, string file)
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
                // Columns only when the snapshot has them: a pre-sheet-era
                // snapshot must never wipe the fight's sheet layout.
                if (b.Fight.CustomSlots is { Count: > 0 })
                {
                    target.CustomSlots = b.Fight.CustomSlots;
                    target.CustomRows = b.Fight.CustomRows ?? new();
                }
            }
            // Restore the active-slot alias (Lines IS SavedSlots[slot] normally).
            if (!string.IsNullOrEmpty(target.Slot) && target.SavedSlots.ContainsKey(target.Slot))
                target.SavedSlots[target.Slot] = target.Lines;
            Config.Save();
            return $"Restored the {b.When:MMM d, h:mm tt} snapshot ({b.Reason}).";
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: snapshot restore failed");
            return "That snapshot couldn't be read.";
        }
    }

    // Decode a FRENMITS plan code and apply it: a same-territory code UPDATES the
    // existing profile in place (the sender's active slot only, notes merged);
    // anything else is added as a new fight. Shared by the fight page's "Import
    // from clipboard" and the Sheet View's Import button. Returns the touched
    // fight (null on failure), whether it was newly added, and a user message.
    public (FightProfile? Fight, bool IsNew, string Message) ImportPlanCode(string? clipboardText)
    {
        try
        {
            var text = (clipboardText ?? "").Trim();
            string json;
            if (text.StartsWith("FRENMITS2:"))
            {
                var data = Convert.FromBase64String(text["FRENMITS2:".Length..]);
                using var ms = new System.IO.MemoryStream(data);
                using var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress);
                using var outMs = new System.IO.MemoryStream();
                gz.CopyTo(outMs);
                json = System.Text.Encoding.UTF8.GetString(outMs.ToArray());
            }
            else if (text.StartsWith("FRENMITS1:"))
            {
                json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(text["FRENMITS1:".Length..]));
            }
            else
            {
                return (null, false, "No FrenMits plan code on the clipboard.");
            }

            var fight = Newtonsoft.Json.JsonConvert.DeserializeObject<FightProfile>(json);
            if (fight == null) return (null, false, "That plan code couldn't be read.");

            // A same-territory import UPDATES the existing profile instead of
            // adding a duplicate: a second profile for one territory never fires
            // (ActiveFight takes the first match), and a duplicate of a built-in
            // renders locked, with no way to delete it.
            var existing = fight.TerritoryId != 0
                ? Config.Fights.FirstOrDefault(f => f.TerritoryId == fight.TerritoryId)
                : null;
            if (existing != null)
            {
                SnapshotPlan(existing, $"before importing \"{fight.Name}\"");
                // Slot-scoped update: the import replaces the sender's ACTIVE slot
                // only. Wholesale-replacing SavedSlots/DeletedCalls would silently
                // wipe YOUR saved edits for every other slot in the fight.
                existing.Lines = fight.Lines;
                existing.TimerOffset = fight.TimerOffset;
                // Sheet notes MERGE: take the sender's note where they wrote one,
                // keep yours everywhere else (wholesale replace would wipe your
                // notes with a v131-era code's empty list).
                foreach (var n in fight.Notes)
                {
                    existing.Notes.RemoveAll(o =>
                        string.Equals(o.Mechanic.Trim(), n.Mechanic.Trim(), StringComparison.OrdinalIgnoreCase)
                        && MathF.Abs(o.Time - n.Time) < 4f);
                    existing.Notes.Add(n);
                }
                if (!string.IsNullOrEmpty(fight.Slot))
                {
                    existing.Slot = fight.Slot;
                    existing.SavedSlots[fight.Slot] = fight.Lines;
                    existing.DeletedCalls.RemoveAll(d =>
                        string.Equals(d.Slot, fight.Slot, StringComparison.OrdinalIgnoreCase));
                    existing.DeletedCalls.AddRange(fight.DeletedCalls.Where(d =>
                        string.Equals(d.Slot, fight.Slot, StringComparison.OrdinalIgnoreCase)));
                }
                if (!Builtin.Has(existing.TerritoryId))
                {
                    // Custom fights carry their hand-built anchors + sheet layout;
                    // built-ins keep the canonical baked ones (ApplySlot refreshes
                    // those anyway). Sheet columns only transfer when the sender
                    // actually HAS them: a pre-sheet-era code must never wipe the
                    // receiver's columns.
                    existing.Name = fight.Name;
                    existing.SyncPoints = fight.SyncPoints;
                    existing.BossAnchors = fight.BossAnchors;
                    if (fight.CustomSlots is { Count: > 0 })
                    {
                        existing.CustomSlots = fight.CustomSlots;
                        existing.CustomRows = fight.CustomRows ?? new();
                    }
                }
                Config.Save();
                return (existing, false, string.IsNullOrEmpty(fight.Slot)
                    ? $"Imported \"{fight.Name}\" into your existing \"{existing.Name}\"."
                    : $"Imported \"{fight.Name}\" into your existing \"{existing.Name}\" ({fight.Slot} slot; your other slots kept).");
            }

            fight.Id = Guid.NewGuid().ToString("N");
            Config.Fights.Add(fight);
            Config.Save();
            return (fight, true, $"Imported \"{fight.Name}\".");
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: import failed");
            return (null, false, "That plan code couldn't be read.");
        }
    }

    // Re-bake the Dancing Mad built-in from the (updated) sheet while KEEPING the
    // custom lines people added. A line is "old sheet-baked" if it matches the
    // previous bake (the DmuLegacy snapshot); those get replaced by the new bake.
    // Everything else - anything flagged Custom, or that no longer matches the old
    // bake - is kept, so custom timers survive the sheet update.
    public int SmartRebakeDmu()
    {
        var n = 0;
        foreach (var f in Config.Fights)
        {
            if (f.TerritoryId != Builtin.DmuTerritory) continue;

            if (!string.IsNullOrEmpty(f.Slot))
                f.Lines = MergeDmuSlot(f, f.Slot, f.Lines);
            foreach (var key in new List<string>(f.SavedSlots.Keys))
                f.SavedSlots[key] = MergeDmuSlot(f, key, f.SavedSlots[key]);

            f.SyncPoints = Builtin.SyncPoints(f.TerritoryId);
            f.BossAnchors = Builtin.BossAnchors(f.TerritoryId);
            n++;
        }
        if (n > 0) Config.Save();
        return n;
    }

    private static List<MitLine> MergeDmuSlot(FightProfile fight, string slot, List<MitLine> existing)
    {
        var oldBaked = DmuLegacy.BuildLines(slot);
        // Deleted calls stay deleted through a sheet re-bake too.
        var newBaked = DmuData.BuildLines(slot)
            .Where(b => !Builtin.IsDeleted(fight, slot, b)).ToList();

        // Exact match against the previous bake (time + action + mechanic).
        static bool SameBaked(MitLine a, MitLine b)
            => MathF.Abs(a.Time - b.Time) < 0.6f
               && string.Equals(a.Action.Trim(), b.Action.Trim(), StringComparison.OrdinalIgnoreCase)
               && string.Equals(a.Mechanic.Trim(), b.Mechanic.Trim(), StringComparison.OrdinalIgnoreCase);

        // "Shadows a real call": the same spoken action within a few seconds of a
        // current baked line. A fight never reuses one mit that close (its cooldown
        // is far longer), so anything that shadows a baked call is a stale or
        // duplicate line — drop it so nothing overlaps. Ignores the mechanic label
        // (the sheet renames/retimes those between versions).
        static bool Shadows(MitLine line, List<MitLine> baked)
            => baked.Any(b => MathF.Abs(b.Time - line.Time) < 6f
                              && string.Equals(b.Action.Trim(), line.Action.Trim(), StringComparison.OrdinalIgnoreCase));

        // Keep a line only if it does NOT shadow a baked call (no overlap) AND it is
        // either a user-flagged custom or not a recognised old sheet-baked line.
        var customs = existing
            .Where(l => !Shadows(l, newBaked) && (l.Custom || !oldBaked.Any(b => SameBaked(l, b))))
            .ToList();

        foreach (var c in customs) c.Custom = true; // flag survivors so future updates keep them cleanly

        var result = new List<MitLine>(newBaked);
        result.AddRange(customs);
        return result.OrderBy(l => l.Time).ToList();
    }

    public void AutoLoadForTerritory(uint territory)
    {
        if (!Builtin.Has(territory)) return;

        var fight = Config.Fights.FirstOrDefault(f => f.TerritoryId == territory);
        if (fight == null)
        {
            fight = new FightProfile { Name = Builtin.Name(territory), TerritoryId = territory };
            Config.Fights.Add(fight);
        }
        if (!fight.Enabled) return;

        // Fall back to a default if the saved slot is no longer valid (e.g. the
        // removed "Extras" slot), so the fight never ends up baked from a dead slot.
        var slot = !string.IsNullOrEmpty(fight.Slot)
                   && Builtin.Slots(territory).Contains(fight.Slot, StringComparer.OrdinalIgnoreCase)
            ? fight.Slot
            : PreferredDefaultSlot(territory);

        var added = Builtin.ApplySlot(fight, slot);
        Config.DmuSlot = fight.Slot;
        Config.Save();

        Service.Log.Information($"FrenMits auto-load: territory {territory}, slot {fight.Slot}, +{added} lines.");
    }

    // Default slot for a fight with none picked yet: the global role pick (if set
    // and the fight has a slot for it) wins, so a chosen role sticks to fights you
    // haven't loaded yet; otherwise fall back to a best-guess by job.
    private string PreferredDefaultSlot(uint territory)
    {
        var roleSlot = Builtin.RoleSlot(territory, Config.RoleSelection);
        return !string.IsNullOrEmpty(roleSlot) ? roleSlot! : Builtin.DefaultSlotForJob(territory, ActiveJobAbbreviation());
    }

    // Local player via the object table (index 0); IClientState.LocalPlayer was
    // removed in this Dalamud build.
    public static Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter? LocalPlayer
        => Service.ObjectTable[0] as Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter;

    // True while a cutscene is playing (phase-transition cutscenes in ultimates) so
    // call-outs and cues are suppressed — you can't act, and the clock self-corrects
    // on the next resync anchor when it ends.
    public static bool InCutscene =>
        ReplayCutsceneActive
        || Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene]
        || Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene78]
        || Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInCutSceneEvent];

    // The running assembly version, e.g. "1.0.0.121". Used for the What's New gate.
    public static string PluginVersion =>
        typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    // True while actually in a pull. The HUD displays force-lock here (see each
    // window's EffectiveLocked) so a stray drag can't grab them mid-fight; you
    // reposition them out of combat or with Live preview.
    public static bool InCombat =>
        Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat];

    // Replay state (desk testing). When ReplayFight is set the normal pipeline runs
    // against the recording instead of the live instance: ActiveFight resolves to
    // it, the live timer/territory gates step aside, and ReplayCutsceneActive
    // drives InCutscene from the recorded cutscene windows.
    public static FightProfile? ReplayFight;
    public static bool ReplayCutsceneActive;
    public static bool Replaying => ReplayFight != null;

    private bool _frameErrorLogged;
    private bool _firstTickDone;

    // Game-state-dependent startup that can't run in the constructor (loader thread).
    // Runs once on the first Framework.Update tick, which is on the main thread, so
    // ObjectTable / ClientState access here is safe.
    private void RunFirstTickInit()
    {
        // Bake a default slot for any built-in that's still empty (freshly seeded,
        // or seeded empty by an older version that only baked on zone-in), so its
        // mits show up front instead of reading "(0)". Your own edits and any slot
        // you've already loaded are left untouched. PreferredDefaultSlot reads the
        // live job off the object table, which is why this waits for the main thread.
        var prebaked = false;
        foreach (var fight in Config.Fights)
        {
            if (fight.Lines.Count == 0 && Builtin.Has(fight.TerritoryId))
            {
                Builtin.ApplySlot(fight, PreferredDefaultSlot(fight.TerritoryId));
                prebaked = true;
            }
        }
        if (prebaked) Config.Save();

        // Cover the case where the plugin loads while already inside a boss room.
        if (Builtin.Has(Service.ClientState.TerritoryType))
            AutoLoadForTerritory(Service.ClientState.TerritoryType);
    }

    private void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework _)
    {
        // Never let a per-frame hiccup (e.g. a stale game object) escape into
        // Dalamud's tick loop. Log the first one, then stay quiet.
        try
        {
            if (!_firstTickDone) { _firstTickDone = true; RunFirstTickInit(); }

            Timer.Update();
            Replay.Update();
            Review.Update();
            Recap.Update();
            HandleCutsceneBoundary();
            UpdatePhase();
            Diag.Update();   // open/close the pull record before the engines log into it
            Sync.Update();
            Cues.Update();
            UpdateDtr();
        }
        catch (Exception ex)
        {
            if (!_frameErrorLogged) { Service.Log.Error(ex, "FrenMits: framework update error"); _frameErrorLogged = true; }
        }
    }

    // ---- Cutscene boundary ------------------------------------------------
    // Phase-transition cutscenes in ultimates pause the action but NOT our wall
    // clock, and combat never drops (the timer freezes through them), so the
    // resync engine never re-arms on its own. When the cutscene ends we therefore
    // (1) re-arm resync so the new phase's first boss appearance / cast snaps the
    // clock back onto the timeline, and (2) hold cues until that snap lands so the
    // new phase doesn't open with calls fired against the drifted clock.
    private bool _wasInCutscene;

    private void HandleCutsceneBoundary()
    {
        var inCs = InCutscene;
        if (inCs && !_wasInCutscene)
        {
            // Log the cutscene window into the active recording so a replay can
            // reproduce it (skip the replay's own synthetic flag).
            if (Sync.Recording && !Replaying && ActiveFight() is { } rf)
                Sync.CutsceneMarks.Add(new PullRecording.RecEvent
                { Time = ElapsedFor(rf), Type = PullRecording.Kind.CutsceneStart });
        }
        else if (!inCs && _wasInCutscene)
        {
            if (Sync.Recording && !Replaying && ActiveFight() is { } rf)
                Sync.CutsceneMarks.Add(new PullRecording.RecEvent
                { Time = ElapsedFor(rf), Type = PullRecording.Kind.CutsceneEnd });

            Sync.Forget();
            if (Config.EnableSync)
                Cues.HoldForResync(Sync.PhaseSyncGeneration, 25.0);
        }
        _wasInCutscene = inCs;
    }

    // ---- Door-boss phase tracking ----------------------------------------
    // A door boss (e.g. M12S) is one instance with two phases, each its own combat
    // from 0. Once Phase 1 is killed you stay on Phase 2 until you leave the duty.
    // We watch the primary boss: when it dies, Phase 2 is locked on for this zone,
    // and ElapsedFor() shifts that fight's clock onto its Phase 2 segment.
    private bool _phaseTwo;
    private uint _trackedBossEntity;
    private uint _trackedBossLastHp;

    private void UpdatePhase()
    {
        // Only relevant for the door-boss territory; cheap no-op elsewhere.
        if (Service.ClientState.TerritoryType != Builtin.M12sTerritory)
            return;

        IBattleNpc? boss = null;
        foreach (var o in Service.ObjectTable)
            if (o is IBattleNpc n && n.MaxHp > 1_000_000
                && (boss is null || n.MaxHp > boss.MaxHp))
                boss = n;

        if (boss is null) { _trackedBossEntity = 0; return; }

        if (boss.EntityId != _trackedBossEntity)
        {
            _trackedBossEntity = boss.EntityId;
            _trackedBossLastHp = boss.CurrentHp;
            return;
        }

        // Boss HP fell to zero => Phase 1 cleared. Latches until the zone changes.
        if (_trackedBossLastHp > 0 && boss.CurrentHp == 0)
            _phaseTwo = true;
        _trackedBossLastHp = boss.CurrentHp;
    }

    // Extra seconds added to a fight's clock for the current phase (door bosses).
    public float PhaseOffsetFor(FightProfile fight)
        => _phaseTwo && fight.TerritoryId == Builtin.M12sTerritory ? M12sData.Phase2Offset : 0f;

    // The sheet clock: where the fight actually is on the timeline. Owned by the
    // resync engine; everything internal (sync matching, anchor capture, mit
    // review, recordings, diagnostics) reads this one.
    public float ElapsedFor(FightProfile fight)
        => Timer.Elapsed + PhaseOffsetFor(fight);

    // The call schedule the overlay/cues/DTR/upcoming list read: sheet clock plus
    // the fight's timer offset. The offset lives here and NOT on the sheet clock,
    // so a resync snap can never cancel it: +10 always fires every call 10s
    // earlier, resync on or off.
    public float CueClockFor(FightProfile fight)
        => ElapsedFor(fight) + fight.TimerOffset;

    // Next-up mit on the server-info bar.
    private void UpdateDtr()
    {
        if (_dtr == null) return;
        if (!Config.ShowDtrBar || !Timer.Running || ActiveFight() is not { } fight)
        {
            _dtr.Shown = false;
            return;
        }

        var job = ActiveJobAbbreviation();
        var elapsed = CueClockFor(fight);
        var next = fight.OrderedLines
            .Where(l => l.Enabled && l.AppliesTo(job) && l.CueTime - elapsed > 0)
            .Select(l => (l, remaining: l.CueTime - elapsed))
            .OrderBy(x => x.remaining)
            .FirstOrDefault();

        if (next.l == null)
        {
            _dtr.Shown = false;
            return;
        }

        var label = string.IsNullOrWhiteSpace(next.l.Action) ? next.l.Mechanic : next.l.Action;
        _dtr.Text = $" {label} {(int)MathF.Ceiling(next.remaining)}s";
        _dtr.Shown = true;
    }

    private bool _drawErrorLogged;

    private void DrawUi()
    {
        try { Windows.Draw(); }
        catch (Exception ex)
        {
            if (!_drawErrorLogged) { Service.Log.Error(ex, "FrenMits: draw error"); _drawErrorLogged = true; }
        }
    }

    private void OpenConfig() => ConfigWindow.IsOpen = true;

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "sync":
                Timer.SyncNow();
                break;
            case "reset":
                Timer.Reset();
                break;
            case "test":
                Config.TestMode = !Config.TestMode;
                Config.Save();
                break;
            case "sheet":
                if (SheetViewWindow.IsOpen) SheetViewWindow.IsOpen = false;
                else SheetViewWindow.Open();
                break;
            default:
                var pm = System.Text.RegularExpressions.Regex.Match(args.Trim().ToLowerInvariant(), @"^(?:phase|p)\s*(\d)$");
                if (pm.Success && (ActiveFight() ?? PreviewFight) is { } pf)
                {
                    var phases = Builtin.PhaseStarts(pf.TerritoryId);
                    var n = int.Parse(pm.Groups[1].Value);
                    if (n >= 1 && n <= phases.Count) PracticeJump(pf, phases[n - 1].Time);
                }
                else
                {
                    ConfigWindow.Toggle();
                }
                break;
        }
    }

    // Resolves the job the overlay should follow: explicit override or live job.
    public string? ActiveJobAbbreviation()
    {
        if (!string.Equals(Config.JobSelection, "Auto", StringComparison.OrdinalIgnoreCase))
            return Config.JobSelection;

        var job = LocalPlayer?.ClassJob.RowId;
        return job is { } rowId ? Jobs.ByRowId(rowId)?.Abbreviation : null;
    }

    // The fight whose territory matches where the player currently is.
    // Practice: a fight to preview out of its zone (set by the phase-jump). Used
    // only in Test Mode, and only when the current zone isn't a real fight.
    public static FightProfile? PreviewFight;

    public FightProfile? ActiveFight()
    {
        if (Replaying) return ReplayFight;
        var territory = Service.ClientState.TerritoryType;
        foreach (var fight in Config.Fights)
            if (fight.Enabled && fight.TerritoryId == territory)
                return fight;
        if (Config.TestMode && PreviewFight != null) return PreviewFight;
        return null;
    }

    // Practice phase-jump: preview a fight's phase by parking the clock ~6s before
    // its first call (Test Mode on so the overlay shows it anywhere).
    public void PracticeJump(FightProfile fight, float time)
    {
        PreviewFight = fight;
        if (!Config.TestMode) { Config.TestMode = true; Config.Save(); }
        var raw = time - 6f - fight.TimerOffset - PhaseOffsetFor(fight);
        Timer.SetElapsed(MathF.Max(0f, raw));
    }

    public void StopPractice()
    {
        PreviewFight = null;
        Timer.Reset();
        if (Config.TestMode) { Config.TestMode = false; Config.Save(); }
    }

    public void Dispose()
    {
        // Never leave replay state latched across a reload.
        ReplayFight = null;
        ReplayCutsceneActive = false;

        Service.Log.Information($"[FrenMits] dispose - live instances now {System.Threading.Interlocked.Decrement(ref _liveInstances)}");
        Service.Framework.Update -= OnFrameworkUpdate;
        Service.ClientState.TerritoryChanged -= OnTerritoryChanged;
        Service.PluginInterface.UiBuilder.Draw -= DrawUi;
        Service.PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        Service.PluginInterface.UiBuilder.OpenMainUi -= OpenConfig;

        Service.CommandManager.RemoveHandler(Command);
        Service.CommandManager.RemoveHandler(CommandAlias);

        _dtr?.Remove();
        Windows.RemoveAllWindows();
        ConfigWindow.Dispose();
        Fonts.Dispose();
        Audio.Dispose();
    }
}
