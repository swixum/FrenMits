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
    public FFLogsClient FFLogs { get; } = new();
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
    public MiniSheetWindow MiniSheetWindow { get; }
    public SlotPopupWindow SlotPopupWindow { get; }

    private readonly IDtrBarEntry? _dtr;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();

        Config = LoadConfig();
        Config.Fights ??= new();
        FrenMits.Windows.Theme.Colorblind = Config.ColorblindMode; // status palette follows the setting

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

        // v16: Dancing Mad re-baked to the Ikuya sheet v5.0 (P3 Reprisal/Addle
        // moves, P4 healer reshuffle, P5 Forsaken hits renamed and reassigned).
        // The smart merge keeps custom lines AND carries per-line tweaks
        // (offsets, disabled state, sounds, colors, press windows) onto the
        // updated calls. Each fight is snapshotted first, so History can restore
        // the pre-update plan.
        if (Config.Version < 16)
        {
            foreach (var f in Config.Fights)
                if (f.TerritoryId == Builtin.DmuTerritory)
                    SnapshotPlan(f, "before the sheet v5.0 update");
            SmartRebakeDmu();
            Config.Version = 16;
            Config.Save();
        }

        // v17: restore the WHM Asylum calls the v16 bake dropped. Asylum was
        // never on the Ikuya sheet; it is a FrenMits addition timed from an
        // FFLogs clear (see DmuData's header note), and the v5.0 sync wrongly
        // treated it as a sheet removal. Re-run the smart re-bake; the
        // containment-aware sweep replaces a v16 "Divine Caress" cleanly with
        // "Divine Caress + Asylum" instead of doubling it, and offsets carry.
        if (Config.Version < 17)
        {
            foreach (var f in Config.Fights)
                if (f.TerritoryId == Builtin.DmuTerritory)
                    SnapshotPlan(f, "before restoring the WHM Asylum calls");
            SmartRebakeDmu();
            Config.Version = 17;
            Config.Save();
        }

        // v18: the sheet v5.0 also reworked the per-pairing tank tabs (explicit
        // ability names instead of "90s/40%/Short Mit", a new Black Holes IV
        // row, the P2 Wings of Destruction/Ultimate Embrace rows split) and
        // re-timed several BRD/MNK/PLD job-mitigation anchors. Upgrade lines
        // users already added from those cards: extras move to their new times
        // in place, and tank plans matching the old bake are swapped for the
        // new one with per-line tweaks carried over. Edited lines are kept.
        if (Config.Version < 18)
        {
            foreach (var f in Config.Fights)
                if (f.TerritoryId == Builtin.DmuTerritory)
                    SnapshotPlan(f, "before the v5.0 tank and job-mitigation update");
            UpgradeDmuTankAndExtraLines();
            Config.Version = 18;
            Config.Save();
        }

        // v19: the sheet's "Ultimate Embrance" typo (P2, 3:41) is now baked
        // corrected as "Ultimate Embrace". Re-run the smart re-bake so existing
        // plans pick up the fixed name; per-line tweaks carry over as usual.
        if (Config.Version < 19)
        {
            foreach (var f in Config.Fights)
                if (f.TerritoryId == Builtin.DmuTerritory)
                    SnapshotPlan(f, "before the Ultimate Embrace typo fix");
            SmartRebakeDmu();
            Config.Version = 19;
            Config.Save();
        }

        // v20: migrate the old M12S placeholder zone (1320) to the real one
        // (1327). Gated so it runs once - 1320 is a real duty, and an ungated
        // remap would silently steal any custom sheet a user builds there.
        if (Config.Version < 20)
        {
            foreach (var f in Config.Fights)
                if (f.TerritoryId == 1320)
                {
                    f.TerritoryId = Builtin.M12sTerritory;
                    f.Category = "Savage";
                }
            Config.Version = 20;
            Config.Save();
        }

        // Slot names run through the standard (T1/T2, M1/M2, R1/R2 - see
        // SlotNames) on EVERY load: cheap, idempotent, and it also catches
        // fights imported from plan codes made on older versions.
        var slotsRenamed = false;
        foreach (var f in Config.Fights)
            slotsRenamed |= SlotNames.NormalizeFight(f);
        // Pinned Sheet View columns are plain strings in the config; rename
        // them too or pre-standard pins ("MT", "D3") silently stop matching.
        for (var i = 0; i < Config.SheetPinnedSlots.Count; i++)
        {
            var canon = SlotNames.Canon(Config.SheetPinnedSlots[i]);
            if (!string.Equals(canon, Config.SheetPinnedSlots[i], StringComparison.Ordinal))
            { Config.SheetPinnedSlots[i] = canon; slotsRenamed = true; }
        }
        for (var i = Config.SheetPinnedSlots.Count - 1; i > 0; i--)
            if (Config.SheetPinnedSlots.Take(i).Contains(Config.SheetPinnedSlots[i], StringComparer.OrdinalIgnoreCase))
            { Config.SheetPinnedSlots.RemoveAt(i); slotsRenamed = true; }
        if (slotsRenamed) Config.Save();

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
        MiniSheetWindow = new MiniSheetWindow(this);
        SlotPopupWindow = new SlotPopupWindow(this);
        WhatsNewWindow = new WhatsNewWindow(this);
        Windows.AddWindow(ConfigWindow);
        Windows.AddWindow(OverlayWindow);
        Windows.AddWindow(TimelineWindow);
        Windows.AddWindow(MitBarWindow);
        Windows.AddWindow(CombatTimerWindow);
        Windows.AddWindow(RecapButtonWindow);
        Windows.AddWindow(RecapWindow);
        Windows.AddWindow(SheetViewWindow);
        Windows.AddWindow(MiniSheetWindow);
        Windows.AddWindow(SlotPopupWindow);
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
            HelpMessage = "Open Fren Mits. /fm sheet = the all-slots sheet view, /fm mini = the pocket mit tuner, /fm sync = zero the timer, /fm test = toggle test mode, /fm reset = clear the timer, /fm p4 = practice-jump to a phase."
        });
        Service.CommandManager.AddHandler(CommandAlias, new CommandInfo(OnCommand));

        try
        {
            _dtr = Service.DtrBar.Get("Fren Mits");
            // The server-bar countdown doubles as a button: click = Sheet View.
            _dtr.Tooltip = "Fren Mits: the next call. Click to open Sheet View.";
            _dtr.OnClick = _ =>
            {
                var f = ActiveFight();
                SheetViewWindow.Open(
                    f != null && (Builtin.Has(f.TerritoryId) || f.CustomSlots.Count > 0) ? f : null);
            };
        }
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
        // A replay-started clock has no combat flag to stop it; leaving the
        // playback (or any zone) out of combat shuts it down.
        if (Timer.Running && !InCombat) Timer.Reset();

        // Leaving / re-entering the instance resets the door-boss phase to 1.
        _phaseTwo = false;
        _trackedBossEntity = 0;
        _trackedBossLastHp = 0;
        // A practice preview never survives a zone change: without this, Test
        // mode left on could route the previewed fight's plan into any zone
        // that has no fight of its own. (Test mode itself stays on: sample-call
        // placement across teleports is legitimate.)
        PreviewFight = null;
        try { AutoLoadForTerritory(territory); }
        catch (Exception ex) { Service.Log.Error(ex, "FrenMits: auto-load failed"); }

        // Opt-in slot check-in: once per entry, only for fights that have a
        // sheet (official, or a custom one the user built).
        if (Config.ShowSlotPopupOnEntry)
        {
            var sheetFight = Config.Fights.FirstOrDefault(f => f.Enabled && f.TerritoryId == territory
                && (Builtin.Has(f.TerritoryId) || f.CustomSlots.Count > 0));
            if (sheetFight != null) SlotPopupWindow.OpenFor(sheetFight);
        }
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

    // Snapshots left behind by DELETED fights of this duty (matched by the
    // territory stored inside each file). Reads every snapshot file, so it
    // only runs on demand from the History popup's finder button.
    public List<SnapshotInfo> ListOrphanSnapshots(uint territory, string excludeFightId)
    {
        var list = new List<SnapshotInfo>();
        try
        {
            if (territory == 0 || !System.IO.Directory.Exists(SnapshotDir)) return list;
            foreach (var file in System.IO.Directory.GetFiles(SnapshotDir, "*.json")
                         .OrderByDescending(f => f))
            {
                if (excludeFightId.Length > 0
                    && System.IO.Path.GetFileName(file).StartsWith(excludeFightId + "_")) continue;
                try
                {
                    var b = Newtonsoft.Json.JsonConvert.DeserializeObject<PlanBackup>(
                        System.IO.File.ReadAllText(file));
                    if (b?.Fight != null && b.Fight.TerritoryId == territory)
                        list.Add(new SnapshotInfo(file, b.When, b.Reason + " [previous sheet]"));
                }
                catch { /* one unreadable file shouldn't hide the rest */ }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: orphan snapshot scan failed");
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
            // Snapshots taken before the column standard carry MT/OT/D1-style
            // names; bring them onto the standard right away.
            SlotNames.NormalizeFight(target);
            Config.Save();
            return $"Restored the {b.When:MMM d, h:mm tt} snapshot ({b.Reason}).";
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: snapshot restore failed");
            return "That snapshot couldn't be read.";
        }
    }

    // Apply a canonical role to every fight that has a sheet (the sidebar's
    // YOUR ROLE and the entry popup both route here). Custom sheets speak the
    // same slot codes since the standard, so they follow the pick too.
    public void SetRoleForAll(string role)
    {
        Config.RoleSelection = role;
        foreach (var f in Config.Fights)
        {
            if (Builtin.Has(f.TerritoryId))
            {
                var slot = Builtin.RoleSlot(f.TerritoryId, role);
                if (!string.IsNullOrEmpty(slot)) Builtin.ApplySlot(f, slot!);
            }
            else if (f.CustomSlots.Count > 0)
            {
                // A sheet without a column for this role just keeps its pick.
                var slot = Builtin.RoleSlotIn(f.CustomSlots, role);
                if (slot != null) SwapCustomSlot(f, slot);
            }
        }
        Config.Save();
    }

    // Switch which sheet column is "yours" for a fight: builtin fights load the
    // slot's plan through ApplySlot (keeping each slot's saved edits), custom
    // sheets swap the saved lists directly.
    public void SetSlot(FightProfile fight, string slot)
    {
        if (Builtin.Has(fight.TerritoryId))
        {
            Builtin.ApplySlot(fight, slot);
            Config.Save();
            return;
        }
        SwapCustomSlot(fight, slot);
        Config.Save();
    }

    // The custom-sheet half of SetSlot: stash the current column, make the
    // target's saved list live (Lines IS SavedSlots[Slot], the alias invariant).
    private void SwapCustomSlot(FightProfile fight, string slot)
    {
        if (!string.IsNullOrEmpty(fight.Slot)) fight.SavedSlots[fight.Slot] = fight.Lines;
        fight.Slot = slot;
        fight.Lines = fight.SavedSlots.TryGetValue(slot, out var lines) ? lines : new System.Collections.Generic.List<MitLine>();
        fight.SavedSlots[slot] = fight.Lines;
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
            // Codes from older versions carry MT/OT/D1-style names; standardize
            // before matching so slots line up with the receiver's (normalized) data.
            SlotNames.NormalizeFight(fight);

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
    // previous bake (the DmuLegacy snapshot); those get replaced by the new bake,
    // but their per-line tweaks (offset, disabled state, sounds, colors, press
    // windows) are carried onto the matching new call first. Everything else -
    // anything flagged Custom, or that no longer matches the old bake - is kept,
    // so custom timers survive the sheet update.
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
        // The DMU data files stay keyed by their native MT/OT/D1-style labels.
        var native = SlotNames.ToLegacy(slot);
        var oldBaked = DmuLegacy.BuildLines(native);
        // Deleted calls stay deleted through a sheet re-bake too.
        var newBaked = DmuData.BuildLines(native)
            .Where(b => !Builtin.IsDeleted(fight, slot, b)).ToList();

        // Exact match against the previous bake (time + action + mechanic).
        static bool SameBaked(MitLine a, MitLine b)
            => MathF.Abs(a.Time - b.Time) < 0.6f
               && string.Equals(a.Action.Trim(), b.Action.Trim(), StringComparison.OrdinalIgnoreCase)
               && string.Equals(a.Mechanic.Trim(), b.Mechanic.Trim(), StringComparison.OrdinalIgnoreCase);

        // Mit parts of a combined call ("Divine Caress + Asylum" -> two parts),
        // for containment checks between bake versions.
        static string[] Parts(string action)
            => action.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        // Every mit named by `a` is also named by `b` (case-insensitive).
        static bool Covers(MitLine b, MitLine a)
            => Parts(a.Action).All(p => Parts(b.Action).Contains(p, StringComparer.OrdinalIgnoreCase));

        // "Shadows a real call": the same spoken action within a few seconds of a
        // current baked line. A fight never reuses one mit that close (its cooldown
        // is far longer), so anything that shadows a baked call is a stale or
        // duplicate line — drop it so nothing overlaps. Ignores the mechanic label
        // (the sheet renames/retimes those between versions). A line whose mits are
        // all contained in the baked call ("Divine Caress" vs the baked
        // "Divine Caress + Asylum") is redundant the same way.
        static bool Shadows(MitLine line, List<MitLine> baked)
            => baked.Any(b => MathF.Abs(b.Time - line.Time) < 6f
                              && (string.Equals(b.Action.Trim(), line.Action.Trim(), StringComparison.OrdinalIgnoreCase)
                                  || Covers(b, line)));

        // Keep a line only if it does NOT shadow a baked call (no overlap) AND it is
        // either a user-flagged custom or not a recognised old sheet-baked line.
        var customs = existing
            .Where(l => !Shadows(l, newBaked) && (l.Custom || !oldBaked.Any(b => SameBaked(l, b))))
            .ToList();

        foreach (var c in customs) c.Custom = true; // flag survivors so future updates keep them cleanly

        // Every line NOT kept above is a sheet-owned line being replaced (or a
        // shadowing edit of one). Before it goes, carry its per-line tweaks onto
        // the new baked call it corresponds to, so a sheet update never costs
        // anyone their offsets or settings. Match the exact same call first;
        // then the same action nearby (the sheet renames/retimes mechanics);
        // then the same base action ("Addle" matches "Addle (Exdeath)") nearby.
        // Each old line donates at most once.
        var donors = existing.Except(customs).ToList();
        var matched = new HashSet<MitLine>();

        static string BaseAction(string a)
        {
            var i = a.IndexOf('(');
            return (i > 0 ? a[..i] : a).Trim();
        }
        static void Carry(MitLine to, MitLine from)
        {
            to.OffsetSeconds = from.OffsetSeconds;
            to.OffsetManual = from.OffsetManual;
            to.CoverUntil = from.CoverUntil;
            to.Enabled = from.Enabled;
            to.LeadOverride = from.LeadOverride;
            to.Tts = from.Tts;
            to.Sound = from.Sound;
            to.Color = from.Color;
            to.IconId = from.IconId;
            if (from.Jobs.Count > 0 && to.Jobs.Count == 0) to.Jobs = new List<string>(from.Jobs);
        }

        foreach (var b in newBaked) // pass 1: identical calls keep their tweaks
        {
            var exact = donors.FirstOrDefault(d => SameBaked(d, b));
            if (exact == null) continue;
            donors.Remove(exact);
            matched.Add(b);
            Carry(b, exact);
        }
        foreach (var b in newBaked) // pass 2: moved / renamed calls
        {
            if (matched.Contains(b)) continue;
            var near = donors
                .Where(d => MathF.Abs(d.Time - b.Time) <= 30f
                            && (string.Equals(d.Action.Trim(), b.Action.Trim(), StringComparison.OrdinalIgnoreCase)
                                || string.Equals(BaseAction(d.Action), BaseAction(b.Action), StringComparison.OrdinalIgnoreCase)
                                || Covers(b, d)))
                .OrderBy(d => MathF.Abs(d.Time - b.Time))
                .FirstOrDefault();
            if (near == null) continue;
            donors.Remove(near);
            Carry(b, near);
        }

        var result = new List<MitLine>(newBaked);
        result.AddRange(customs);
        return result.OrderBy(l => l.Time).ToList();
    }

    // The BRD/MNK/PLD job-mitigation anchors that moved when they were re-timed
    // to sheet v5.0 rows (old time/mechanic -> new). Unchanged anchors are not
    // listed; a user-edited line matches nothing here and is left alone.
    private static readonly (string Job, string Action, float OldTime, string OldMech, float NewTime, string NewMech)[] DmuExtraMoves =
    {
        ("BRD", "Nature's Minne", 249, "Towers I", 250, "Towers I"),
        ("BRD", "Nature's Minne", 451, "Bowels of Agony (Chaos)", 450, "Bowels of Agony (Chaos)"),
        ("BRD", "Nature's Minne", 789, "Grand Cross", 793, "Grand Cross"),
        ("BRD", "Nature's Minne", 922, "Chaotic Flood", 928, "Chaotic Flood"),
        ("BRD", "Nature's Minne", 1046, "Fell Forces (3x)", 1062, "Forsaken (1st Hit)"),
        ("MNK", "Mantra", 237, "Forsaken", 236, "Forsaken"),
        ("MNK", "Mantra", 451, "Bowels of Agony (Chaos)", 450, "Bowels of Agony (Chaos)"),
        ("MNK", "Mantra", 544, "The Decisive Battle", 545, "The Decisive Battle"),
        ("MNK", "Mantra", 765, "Inferno/Tsunami", 769, "Inferno/Tsunami"),
        ("MNK", "Mantra", 905, "Ultima Repeater", 911, "Ultima Repeater"),
        ("PLD", "Passage of Arms", 342, "Light of Judgement", 343, "Light of Judgement"),
        ("PLD", "Passage of Arms", 609, "Shocking Impact", 609, "Shocking Impact/Shockwave"),
        ("PLD", "Passage of Arms", 789, "Grand Cross", 793, "Grand Cross"),
        ("PLD", "Passage of Arms", 922, "Chaotic Flood", 928, "Chaotic Flood"),
    };

    // One-time v18 upgrade: bring already-added DMU tank-buster plans and the
    // BRD/MNK/PLD job-mitigation lines up to the sheet v5.0 data. Safe to run
    // twice: already-upgraded lines match nothing and come out unchanged.
    private void UpgradeDmuTankAndExtraLines()
    {
        foreach (var f in Config.Fights)
        {
            if (f.TerritoryId != Builtin.DmuTerritory) continue;
            UpgradeDmuSet(f, f.Lines);
            foreach (var key in new List<string>(f.SavedSlots.Keys))
                UpgradeDmuSet(f, f.SavedSlots[key]);
        }
        Config.Save();
    }

    private static void UpgradeDmuSet(FightProfile fight, List<MitLine> lines)
    {
        // Job-mitigation extras: re-time in place, keeping every per-line tweak.
        foreach (var l in lines)
            foreach (var m in DmuExtraMoves)
                if (MathF.Abs(l.Time - m.OldTime) < 0.5f
                    && string.Equals(l.Action, m.Action, StringComparison.OrdinalIgnoreCase)
                    && l.Mechanic == m.OldMech
                    && l.Jobs.Contains(m.Job, StringComparer.OrdinalIgnoreCase))
                {
                    l.Time = m.NewTime;
                    l.Mechanic = m.NewMech;
                    break;
                }

        // Tank-buster plans (the card adds them as "Tank:" lines tagged to one job).
        foreach (var job in new[] { "WAR", "PLD", "DRK", "GNB" })
        {
            var mine = lines.Where(l => l.Mechanic.StartsWith("Tank:", StringComparison.Ordinal)
                                        && l.Jobs.Count == 1
                                        && string.Equals(l.Jobs[0], job, StringComparison.OrdinalIgnoreCase)).ToList();
            if (mine.Count == 0) continue;

            // Which pairing's old plan did these come from? Count exact matches
            // against each old bake; the remembered dropdown pick breaks ties.
            string? comp = null;
            var matched = new List<MitLine>();
            foreach (var c in TankMits.Comps(Builtin.DmuTerritory))
            {
                if (!TankMits.Jobs(c).Contains(job)) continue;
                var old = TankMitsLegacy.DmuFor(c, job);
                var hits = mine.Where(l => old.Any(e =>
                    MathF.Abs(l.Time - e.Time) < 0.5f
                    && l.Mechanic == $"Tank: {e.Mechanic}"
                    && l.Action == e.Action)).ToList();
                if (hits.Count > matched.Count
                    || (hits.Count == matched.Count && hits.Count > 0 && c == fight.TankPairing))
                {
                    comp = c;
                    matched = hits;
                }
            }
            if (comp == null || matched.Count == 0) continue; // fully hand-edited: hands off

            // Swap the unedited old lines for the new plan; edited lines stay,
            // and win over a new entry landing on the same moment.
            foreach (var l in matched) lines.Remove(l);
            var kept = lines.Where(l => l.Mechanic.StartsWith("Tank:", StringComparison.Ordinal)
                                        && l.Jobs.Count == 1
                                        && string.Equals(l.Jobs[0], job, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var e in TankMits.For(Builtin.DmuTerritory, comp, job))
            {
                if (kept.Any(k => MathF.Abs(k.Time - e.Time) < 1f)) continue;
                var donor = matched.FirstOrDefault(d => MathF.Abs(d.Time - e.Time) < 0.5f);
                lines.Add(new MitLine
                {
                    Time = e.Time,
                    Mechanic = $"Tank: {e.Mechanic}",
                    Action = e.Action,
                    Jobs = new List<string> { job },
                    Custom = true,
                    Enabled = donor?.Enabled ?? true,
                    OffsetSeconds = donor?.OffsetSeconds ?? 0f,
                    OffsetManual = donor?.OffsetManual ?? false,
                    CoverUntil = donor?.CoverUntil ?? 0f,
                    LeadOverride = donor?.LeadOverride ?? 0f,
                    Tts = donor?.Tts ?? "",
                    Sound = donor?.Sound ?? true,
                    Color = donor?.Color ?? 0,
                    IconId = donor?.IconId ?? 0,
                });
            }
        }

        var sorted = lines.OrderBy(l => l.Time).ToList();
        lines.Clear();
        lines.AddRange(sorted);
    }

    public void AutoLoadForTerritory(uint territory)
    {
        if (!Builtin.Has(territory)) { AutoSlotCustomSheet(territory); return; }

        // Prefer the enabled profile so this matches what ActiveFight will
        // actually drive when duplicates exist (first enabled wins there too).
        var fight = Config.Fights.FirstOrDefault(f => f.Enabled && f.TerritoryId == territory)
                    ?? Config.Fights.FirstOrDefault(f => f.TerritoryId == territory);
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

        // No safe guess (player is on a job the plugin doesn't know yet, likely
        // a new expansion's): don't bake someone else's seat - the entry popup
        // asks for a slot instead.
        if (slot.Length == 0)
        {
            Service.Log.Information($"FrenMits auto-load: territory {territory}, unknown job - waiting for a slot pick.");
            return;
        }

        var added = Builtin.ApplySlot(fight, slot);
        Config.DmuSlot = fight.Slot;
        Config.Save();

        Service.Log.Information($"FrenMits auto-load: territory {territory}, slot {fight.Slot}, +{added} lines.");
    }

    // Custom sheets follow the sidebar Role/Job on zone-in the same way
    // built-ins do - but only when no valid column is picked yet; a column you
    // chose by hand always stays.
    private void AutoSlotCustomSheet(uint territory)
    {
        var fight = Config.Fights.FirstOrDefault(f => f.Enabled && f.TerritoryId == territory && f.CustomSlots.Count > 0);
        if (fight == null) return;
        if (!string.IsNullOrEmpty(fight.Slot)
            && fight.CustomSlots.Contains(fight.Slot, StringComparer.OrdinalIgnoreCase)) return;

        var slot = PreferredDefaultSlotIn(fight.CustomSlots);
        if (slot.Length == 0) return; // no confident match: the entry popup asks

        SwapCustomSlot(fight, slot);
        Config.Save();
        Service.Log.Information($"FrenMits auto-slot: \"{fight.Name}\" -> {slot}.");
    }

    // PreferredDefaultSlot against an arbitrary column list: role pick first,
    // then best-guess by job; "" when there is nothing safe to guess (unknown
    // job, or the sheet has no matching column).
    private string PreferredDefaultSlotIn(System.Collections.Generic.IReadOnlyList<string> slots)
    {
        var roleSlot = Builtin.RoleSlotIn(slots, Config.RoleSelection);
        if (!string.IsNullOrEmpty(roleSlot)) return roleSlot!;
        if (LocalPlayer is { } p && Jobs.ByRowId(p.ClassJob.RowId) is null) return "";
        return Builtin.DefaultSlotForJobIn(slots, ActiveJobAbbreviation());
    }

    // Default slot for a fight with none picked yet: the global role pick (if set
    // and the fight has a slot for it) wins, so a chosen role sticks to fights you
    // haven't loaded yet; otherwise fall back to a best-guess by job.
    private string PreferredDefaultSlot(uint territory)
    {
        var roleSlot = Builtin.RoleSlot(territory, Config.RoleSelection);
        if (!string.IsNullOrEmpty(roleSlot)) return roleSlot!;
        // A logged-in player on a job missing from the Jobs table (a brand-new
        // job) gets NO guess: "" tells callers to skip the bake rather than
        // hand a fresh 8.0 job the main tank's calls. No player yet (login
        // screen) keeps the generic first-seat default as before.
        if (LocalPlayer is { } p && Jobs.ByRowId(p.ClassJob.RowId) is null) return "";
        return Builtin.DefaultSlotForJob(territory, ActiveJobAbbreviation());
    }

    // Local player via the object table (index 0); IClientState.LocalPlayer was
    // removed in this Dalamud build.
    public static Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter? LocalPlayer
        => Service.ObjectTable[0] as Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter;

    // True while a cutscene is playing (phase-transition cutscenes in ultimates) so
    // call-outs and cues are suppressed — you can't act, and the clock self-corrects
    // on the next resync anchor when it ends.
    public static bool InCutscene =>
        Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene]
        || Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene78]
        || Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInCutSceneEvent];

    // The cutscene state everything gameplay-facing should use. The raw game
    // flags occasionally STICK after a cutscene ends (a known game quirk);
    // frozen-forever meant a dead timer and hidden overlays until a restart.
    // A cutscene reading true for 3+ minutes straight is treated as stuck.
    public static bool CutsceneActive => InCutscene && !CutsceneStuck;
    public static bool CutsceneStuck { get; private set; }
    private DateTime? _csSince;

    private void UpdateCutsceneStuck()
    {
        if (!InCutscene)
        {
            _csSince = null;
            CutsceneStuck = false;
            return;
        }
        _csSince ??= DateTime.UtcNow;
        if (!CutsceneStuck && (DateTime.UtcNow - _csSince.Value).TotalSeconds > 180)
        {
            CutsceneStuck = true;
            Service.Log.Warning("[FrenMits] Cutscene flag has been on for 3+ minutes; treating it as stuck so the timer and overlays keep working.");
        }
    }

    // The running assembly version, e.g. "1.0.0.121". Used for the What's New gate.
    public static string PluginVersion =>
        typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    // True while actually in a pull. The HUD displays force-lock here (see each
    // window's EffectiveLocked) so a stray drag can't grab them mid-fight; you
    // reposition them out of combat or with Live preview.
    public static bool InCombat =>
        Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat];

    // Downtime: mid-pull, the boss is present but not targetable (a phase
    // transition, it jumped away, or a cutscene). The timeline flags it so a lull
    // reads as a lull, with a running timer of how long it's been going.
    public bool DowntimeActive { get; private set; }
    public float DowntimeElapsed => _downtimeStartUtc is { } s ? (float)(DateTime.UtcNow - s).TotalSeconds : 0f;
    // Seconds left until targetable, once this lull has been seen before (learned);
    // -1 the very first time, when we're still measuring it.
    public float DowntimeRemaining => DowntimeActive && _downtimeKnownDur > 0f
        ? MathF.Max(0f, _downtimeKnownDur - DowntimeElapsed) : -1f;
    private DateTime? _downtimeStartUtc;
    private float _downtimeStartElapsed;
    private float _downtimeKnownDur = -1f;

    // The current boss's HP as a 0..1 fraction (-1 when there's no boss). Feeds
    // the timeline's "push it or fail" skull near a phase gate.
    public float BossHpFraction { get; private set; } = -1f;

    private void UpdateDowntime()
    {
        IBattleNpc? boss = null;
        foreach (var o in Service.ObjectTable)
            if (o is IBattleNpc n && (byte)n.BattleNpcKind == 5 && n.MaxHp > 1_000_000
                && (boss is null || n.MaxHp > boss.MaxHp))
                boss = n;
        BossHpFraction = boss is { MaxHp: > 0 } ? (float)boss.CurrentHp / boss.MaxHp : -1f;

        var down = false;
        if (Timer.Running)
        {
            if (CutsceneActive) down = true;
            else if (boss is { IsTargetable: false }) down = true;
        }

        if (down && !DowntimeActive)
        {
            // Just started: stamp it and recall how long it ran last time.
            _downtimeStartUtc = DateTime.UtcNow;
            var f = ActiveFight();
            _downtimeStartElapsed = f != null ? ElapsedFor(f) : Timer.Elapsed;
            _downtimeKnownDur = LookupDowntime(f?.TerritoryId, _downtimeStartElapsed);
        }
        else if (!down && DowntimeActive)
        {
            // Just ended: learn its length for next time.
            if (ActiveFight() is { } f) RecordDowntime(f.TerritoryId, _downtimeStartElapsed, DowntimeElapsed);
            _downtimeStartUtc = null;
            _downtimeKnownDur = -1f;
        }
        DowntimeActive = down;
    }

    private float LookupDowntime(uint? territory, float start)
    {
        if (territory is not { } t || !Config.LearnedDowntimes.TryGetValue(t.ToString(), out var list))
            return -1f;
        foreach (var w in list) if (MathF.Abs(w.Start - start) < 8f) return w.Duration;
        return -1f;
    }

    private void RecordDowntime(uint territory, float start, float dur)
    {
        if (dur < 1.5f) return; // ignore blips
        var key = territory.ToString();
        if (!Config.LearnedDowntimes.TryGetValue(key, out var list))
            Config.LearnedDowntimes[key] = list = new();
        var w = list.FirstOrDefault(x => MathF.Abs(x.Start - start) < 8f);
        if (w != null) { w.Start = start; w.Duration = dur; }
        else list.Add(new DowntimeWindow { Start = start, Duration = dur });
        Config.Save();
    }

    // Watching a Duty Recorder replay (e.g. via A Realm Recorded). The spectator
    // never gets a combat flag, so the timer auto-starts from the replay's own
    // casts instead (SyncEngine.TryPlaybackAutoStart).
    public static bool InDutyPlayback =>
        Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.DutyRecorderPlayback];

    // The game's simulation-speed multiplier. 1 in normal play; the duty recorder
    // (and A Realm Recorded's controls) drive it during playback - 0 while paused,
    // 2 for 2x, 0.5 for half, and so on. We read it to keep the timeline and
    // alerts in step with the replay instead of ticking on real time.
    private static unsafe float ReplayGameSpeed()
    {
        try
        {
            var fw = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
            if (fw == null) return 1f;
            var s = fw->GameSpeedMultiplier;
            if (s < 0f || s > 100f) return 1f;   // garbage guard
            return s < 0.02f ? 0f : s;           // snap a near-zero (paused) to a hard stop
        }
        catch { return 1f; }
    }

    private DateTime _lastPlaybackTick = DateTime.UtcNow;

    private bool _firstTickDone;
    private bool _wasInDutyPlayback;
    private DateTime _lastFrameErrLog = DateTime.MinValue;
    public int FrameErrorCount { get; private set; }
    public DateTime LastFrameErrorAt { get; private set; } = DateTime.MinValue;
    private bool _wasInCombatForTest; // edge detector for the Test-mode auto-off

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
                var slot = PreferredDefaultSlot(fight.TerritoryId);
                if (slot.Length == 0) continue; // unknown job: no safe seat to guess
                Builtin.ApplySlot(fight, slot);
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

            UpdateCutsceneStuck();

            // A REAL pull always outranks Test mode. Left on, Test would keep
            // the overlays unlocked (click-catching) and visible through
            // cutscenes mid-fight, and a leftover practice preview could even
            // route another zone's plan into this pull. Placement is an
            // out-of-combat activity; combat switches it off.
            var inCombatNow = InCombat;
            if (inCombatNow && !_wasInCombatForTest)
            {
                if (Config.TestMode || PreviewFight != null)
                {
                    PreviewFight = null;
                    if (Config.TestMode)
                    {
                        Config.TestMode = false;
                        Config.Save();
                        Service.Log.Information("[FrenMits] Test mode switched off: a real pull started.");
                    }
                }

                // A pull can never BEGIN inside a cutscene, so combat starting
                // while the flag reads true proves the flag is stuck (the known
                // game quirk that hid the overlay while the server bar kept
                // ticking). Declare it immediately instead of waiting out the
                // 3-minute failsafe; the state self-clears when the flag drops.
                if (InCutscene && !CutsceneStuck)
                {
                    CutsceneStuck = true;
                    Service.Log.Warning("[FrenMits] Combat started while the cutscene flag was on; treating the flag as stuck so the overlay shows.");
                }
            }
            _wasInCombatForTest = inCombatNow;

            // Leaving a Duty Recorder playback: the spectator never gets combat
            // flags, so the replay-started timer would keep ticking on the menus
            // forever. Stop it the moment playback ends.
            if (_wasInDutyPlayback && !InDutyPlayback && Timer.Running)
            {
                Timer.Reset();
                Service.Log.Information("[FrenMits] Playback ended; timer stopped.");
            }
            _wasInDutyPlayback = InDutyPlayback;

            // Keep the timeline in step with a Duty Recorder replay: real time
            // keeps running while playback is paused (or sped up), so nudge the
            // clock by realDelta * (1 - gameSpeed). Paused (speed 0) freezes the
            // timeline and alerts; 2x/0.5x track the replay's pace. The delta is
            // measured on the SAME UtcNow clock Elapsed uses, so a pause freezes
            // it EXACTLY - UpdateDelta drifted a hair and let it creep down.
            var nowUtc = DateTime.UtcNow;
            var realDt = (float)(nowUtc - _lastPlaybackTick).TotalSeconds;
            _lastPlaybackTick = nowUtc;
            if (InDutyPlayback && Timer.Running && realDt > 0f && realDt < 1f)
                Timer.ShiftStart(realDt * (1f - ReplayGameSpeed()));

            RefreshAutoFight();
            Timer.Update();
            UpdateDowntime();
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
            // Rate-limited, not once-ever: a RECURRING throw here silently kills
            // every engine after the throw point, and we need the log to show it.
            FrameErrorCount++;
            LastFrameErrorAt = DateTime.UtcNow;
            if ((DateTime.UtcNow - _lastFrameErrLog).TotalSeconds >= 60)
            {
                _lastFrameErrLog = DateTime.UtcNow;
                Service.Log.Error(ex, $"FrenMits: framework update error (x{FrameErrorCount} this session)");
            }
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
        var inCs = CutsceneActive;
        if (!inCs && _wasInCutscene)
        {
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
    // resync engine; everything internal (sync matching, pull capture,
    // diagnostics) reads this one.
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
        if (!Config.ShowDtrBar || !Timer.Running || ActiveFight() is not { } fight || fight.TimelineOnly
            // Same silence rules as the overlay and cues: during a phase
            // cutscene (and until the post-cutscene resync lands) the clock is
            // known-drifted, so don't count calls down against it.
            || CutsceneActive || Cues.Holding)
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

        var label = string.IsNullOrWhiteSpace(next.l.Action) ? next.l.Mechanic : next.l.ActionFor(job);
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
            case "mini":
            case "tuner":
                MiniSheetWindow.IsOpen = !MiniSheetWindow.IsOpen;
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
        var territory = Service.ClientState.TerritoryType;
        foreach (var fight in Config.Fights)
            if (fight.Enabled && fight.TerritoryId == territory)
                return fight;
        // A practice phase-jump beats the universal timeline: the user explicitly
        // asked to preview a fight, and the auto board would otherwise swallow it
        // in every duty that has baked data.
        if (Config.TestMode && PreviewFight != null) return PreviewFight;
        // No sheet for this duty: the baked universal timeline (board + combat
        // timer only) steps in, so a timeline runs in every instanced duty.
        if (Config.UniversalTimelines && _autoFight != null && _autoFight.TerritoryId == territory)
            return _autoFight;
        return null;
    }

    // The in-memory timeline-only fight for the current territory (never saved).
    private FightProfile? _autoFight;
    private uint _autoFightTerritory = uint.MaxValue;

    // Cheap per-frame check: (re)build the auto fight when the territory
    // changes. Only duties with NO profile of their own get one, so a real
    // sheet or user fight always wins.
    private int _autoFightsStamp = -1;

    private void RefreshAutoFight()
    {
        var territory = Service.ClientState.TerritoryType;
        // Re-check when the zone changes OR the fights list does (adding a
        // sheet mid-instance stands the auto timeline down; deleting the only
        // sheet brings it back). The stamp folds in each fight's zone and
        // enabled flag so retargeting or toggling a fight re-evaluates too -
        // count alone missed both and left a stale board up.
        var stamp = Config.Fights.Count;
        foreach (var f in Config.Fights)
            stamp = stamp * 31 + (int)f.TerritoryId * 2 + (f.Enabled ? 1 : 0);
        if (territory == _autoFightTerritory && stamp == _autoFightsStamp) return;
        _autoFightTerritory = territory;
        _autoFightsStamp = stamp;
        // Enabled is deliberately ignored here: a profile you disabled means
        // "keep this duty silent", not "show me the generic board instead".
        _autoFight = Config.Fights.Any(f => f.TerritoryId == territory)
            ? null
            : UniversalTimelines.Build(territory);
        if (_autoFight != null)
            Service.Log.Information($"[FrenMits] universal timeline armed for \"{_autoFight.Name}\" ({territory}).");
    }

    // Practice phase-jump: preview a fight's phase by parking the clock ~6s before
    // its first call (Test Mode on so the overlay shows it anywhere).
    public void PracticeJump(FightProfile fight, float time)
    {
        PreviewFight = fight;
        if (!Config.TestMode) { Config.TestMode = true; Config.Save(); }
        var raw = time - 6f - fight.TimerOffset - PhaseOffsetFor(fight);
        Timer.SetElapsed(MathF.Max(0f, raw));
        // SetElapsed doesn't bump Generation and the clock lands mid-sheet, so
        // the fresh-pull check never re-arms: without this, jumping to the same
        // phase a second time would play no audio.
        Cues.Rearm();
    }

    public void StopPractice()
    {
        PreviewFight = null;
        Timer.Reset();
        if (Config.TestMode) { Config.TestMode = false; Config.Save(); }
    }

    public void Dispose()
    {
        Diag.FlushOnDispose();
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
