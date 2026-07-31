using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FrenMits.Windows;

namespace FrenMits;

public sealed class Plugin : IDalamudPlugin, IMigrationHost
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
    public DamageCapture Damage { get; }
    public Meter Meter { get; }
    public FFLogsClient FFLogs { get; } = new();
    public MitRecap Recap { get; }
    public SnapshotStore Snapshots { get; }
    public Diagnostics Diag { get; }
    public readonly WindowSystem Windows = new("FrenMits");
    public ConfigWindow ConfigWindow { get; }
    public OverlayWindow OverlayWindow { get; }
    public TimelineWindow TimelineWindow { get; }
    public MitBarWindow MitBarWindow { get; }
    public CombatTimerWindow CombatTimerWindow { get; }
    public MeterWindow MeterWindow { get; }
    public PrepWindow PrepWindow { get; }
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
        // Plans come from their own file now.
        LoadPlans(Config);
        Config.LearnedFights ??= new();
        Snapshots = new SnapshotStore(Config);
        FrenMits.Windows.Theme.Colorblind = Config.ColorblindMode; // status palette follows the setting

        // Versioned migrations (v2..v23) live in ConfigMigrations.
        ConfigMigrations.Run(this);

        // Slot names run through the standard on every load: cheap and idempotent.
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

        // Meter columns saved by a pre-Replace build carry doubled entries.
        var colsFixed = Configuration.DedupeMeterColumns(Config.MeterColumns);
        colsFixed |= Configuration.DedupeMeterColumns(Config.MeterHealColumns);
        if (colsFixed) Config.SaveSettings();

        // Auto-add any built-in fight the user hasn't been shown yet.
        Config.SeededTerritories ??= new();
        var seeded = false;
        foreach (var (territory, name, category, _) in Builtin.Fights)
        {
            if (Config.SeededTerritories.Contains(territory)) continue;
            Config.SeededTerritories.Add(territory);
            if (Config.Fights.All(f => f.TerritoryId != territory))
                Config.Fights.Add(new FightProfile { Name = name, TerritoryId = territory, Category = category });
            seeded = true;
        }

        // Migrate the two built-ins that were renamed (dropped the redundant
        // "(Ultimate)" suffix for the short code, matching the others).
        foreach (var f in Config.Fights)
        {
            if (f.Name == "Dancing Mad (Ultimate)") { f.Name = Builtin.Name(Builtin.DmuTerritory); seeded = true; }
            else if (f.Name == "Futures Rewritten (Ultimate)") { f.Name = Builtin.Name(Builtin.FruTerritory); seeded = true; }
        }

        if (seeded) Config.Save();

        AdoptSupersededSheets();

        // Deferred to the first Framework.Update tick: both need main-thread game
        // state.

        Cues = new CueEngine(this, Audio);
        Sync = new SyncEngine(this);
        Damage = new DamageCapture(this);
        Meter = new Meter(this);
        Recap = new MitRecap(this);
        Diag = new Diagnostics(this);
        ConfigWindow = new ConfigWindow(this);
        OverlayWindow = new OverlayWindow(this);
        TimelineWindow = new TimelineWindow(this);
        MitBarWindow = new MitBarWindow(this);
        CombatTimerWindow = new CombatTimerWindow(this);
        MeterWindow = new MeterWindow(this);
        PrepWindow = new PrepWindow(this);
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
        Windows.AddWindow(MeterWindow);
        Windows.AddWindow(PrepWindow);
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
        MeterWindow.IsOpen = true;
        PrepWindow.IsOpen = true;
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
        // running, the plugin is double-loaded, which would double every audio cue.
        var n = System.Threading.Interlocked.Increment(ref _liveInstances);
        Service.Log.Information($"[FrenMits] init - live instance #{n}");
    }

    private static int _liveInstances;

    // Load defensively: a file that won't deserialize is kept and saves are suppressed.
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
            // The file is there but unreadable, do NOT treat this as a first run.
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

    // Point Config.Fights at the plan file, moving them out of the config the
    // first time a profile that predates the split is loaded.
    private static void LoadPlans(Configuration config)
    {
        var plans = PlanStore.Load();

        if (PlanStore.Broken)
        {
            // The plan file is there but unreadable.
            if (config.LegacyFights is { Count: > 0 }) config.Fights = config.LegacyFights;
            Configuration.SuppressSave = true;
            Service.Log?.Error(
                $"FrenMits: {PlanStore.FileName} could not be read. Running WITHOUT saving anything this " +
                "session so both copies of your plans stay recoverable.");
            return;
        }

        // Never written back whatever happens next, so drop it either way.
        var legacy = config.LegacyFights;
        config.LegacyFights = null;

        if (PlanStore.PreferConfigCopy(plans != null, legacy?.Count ?? 0, PlanStore.ConfigIsNewerThanPlans()))
        {
            // Either the normal first load after the split, or a rollback that
            // edited plans in the old build and came forward again.
            config.Fights = legacy!;
            PlanStore.BackupConfigBeforeSplit();
            PlanStore.Save(config.Fights);
            Service.Log?.Information(
                $"FrenMits: took {config.Fights.Count} fight plans from the config into {PlanStore.FileName}.");
            return;
        }

        if (plans != null) config.Fights = plans;
    }

    // On entering a boss room, top up the fight's lines and refresh the anchors.
    private void OnTerritoryChanged(uint territory)
    {
        // A replay-started clock has no combat flag to stop it; leaving the
        // playback (or any zone) out of combat shuts it down.
        if (Timer.Live && !InCombat) Timer.Reset();

        // Leaving / re-entering the instance resets the door-boss phase to 1.
        _phaseTwo = false;
        _trackedBossEntity = 0;
        _trackedBossLastHp = 0;
        // A practice preview never survives a zone change.
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

    // Full refresh: rebake every built-in fresh, discarding saved per-slot edits.
    public int ResetAllBuiltins()
    {
        var n = 0;
        foreach (var f in Config.Fights)
        {
            if (!Builtin.Has(f.TerritoryId)) continue;
            if (f.Lines.Count > 0 || f.SavedSlots.Count > 0)
                Snapshots.Save(f, "before Refresh from sheet");
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

    // IMigrationHost: the migrations only ever snapshot through here.
    public void SnapshotFight(FightProfile fight, string reason) => Snapshots.Save(fight, reason);

    private void AdoptSupersededSheets()
    {
        var adopted = ConfigMigrations.AdoptSupersededSheets(Config, (f, why) =>
        {
            try { Snapshots.Save(f, why); }
            catch (Exception ex) { Swallowed.Report("snapshot superseded sheet", ex); }
        });
        if (adopted > 0)
        {
            Config.Save();
            Service.Log.Information($"FrenMits: {adopted} sheet(s) now ship officially; "
                                    + "replaced the custom copies (snapshots kept).");
        }
    }

    // Apply a canonical role to every fight that has a sheet (the sidebar's
    // YOUR ROLE and the entry popup both route here).
    public void SetRoleForAll(string role)
    {
        Config.RoleSelection = role;
        foreach (var f in Config.Fights)
        {
            if (Builtin.Has(f.TerritoryId))
            {
                var slot = Builtin.RoleSlot(f.TerritoryId, role);
                if (!string.IsNullOrEmpty(slot)) { Builtin.ApplySlot(f, slot!); AutoTime(f); }
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

    // Switch which sheet column is yours for a fight.
    public void SetSlot(FightProfile fight, string slot)
    {
        if (Builtin.Has(fight.TerritoryId))
        {
            Builtin.ApplySlot(fight, slot);
            AutoTime(fight);
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
        AutoTime(fight);
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

        // No safe guess, so don't bake someone else's seat; the popup asks instead.
        if (slot.Length == 0)
        {
            Service.Log.Information($"FrenMits auto-load: territory {territory}, unknown job - waiting for a slot pick.");
            return;
        }

        var added = Builtin.ApplySlot(fight, slot);
        Config.DmuSlot = fight.Slot;
        Config.Save();
        AutoTime(fight);

        Service.Log.Information($"FrenMits auto-load: territory {territory}, slot {fight.Slot}, +{added} lines.");
    }

    // Run the cooldown-aware offset solver over a fight's active slot.
    public void AutoTime(FightProfile? fight)
    {
        if (!Config.AutoCooldownTiming || fight == null || fight.Lines.Count == 0) return;
        try
        {
            var hits = SheetTimeline.Build(fight).Select(r => r.Time).ToList();
            var changed = TimingSolver.Solve(fight, hits, Config.CooldownLeadSeconds);
            if (changed > 0)
            {
                if (!string.IsNullOrEmpty(fight.Slot)) fight.SavedSlots[fight.Slot] = fight.Lines;
                Config.Save();
                Service.Log.Information($"FrenMits auto-time: {fight.Name}/{fight.Slot}, {changed} offsets solved.");
            }
        }
        catch (Exception ex) { Service.Log.Warning($"FrenMits auto-time failed: {ex.Message}"); }
    }

    // Erase every offset/coverage the auto-timer wrote - across every fight and
    // saved slot - so turning the feature off returns each plan to its own timing.
    public void ClearSolvedOffsets()
    {
        var changed = false;
        void Clear(List<MitLine>? lines)
        {
            if (lines == null) return;
            foreach (var l in lines)
                if (!l.OffsetManual && (l.OffsetSeconds != 0f || l.CoverUntil != 0f))
                {
                    l.OffsetSeconds = 0f;
                    l.CoverUntil = 0f;
                    changed = true;
                }
        }
        foreach (var f in Config.Fights)
        {
            Clear(f.Lines);
            if (f.SavedSlots != null)
                foreach (var slot in f.SavedSlots.Values) Clear(slot);
        }
        if (changed)
        {
            Config.Save();
            Service.Log.Information("FrenMits: auto cooldown timing off - cleared solver offsets.");
        }
    }

    // Custom sheets follow the sidebar Role/Job on zone-in, unless you picked a column.
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

    // PreferredDefaultSlot against an arbitrary column list.
    private string PreferredDefaultSlotIn(System.Collections.Generic.IReadOnlyList<string> slots)
    {
        var roleSlot = Builtin.RoleSlotIn(slots, Config.RoleSelection);
        if (!string.IsNullOrEmpty(roleSlot)) return roleSlot!;
        if (LocalPlayer is { } p && Jobs.ByRowId(p.ClassJob.RowId) is null) return "";
        return Builtin.DefaultSlotForJobIn(slots, ActiveJobAbbreviation());
    }

    // Default slot for a fight with none picked: the global role pick, else by job.
    private string PreferredDefaultSlot(uint territory)
    {
        var roleSlot = Builtin.RoleSlot(territory, Config.RoleSelection);
        if (!string.IsNullOrEmpty(roleSlot)) return roleSlot!;
        // A player on a job missing from the Jobs table gets no guess.
        if (LocalPlayer is { } p && Jobs.ByRowId(p.ClassJob.RowId) is null) return "";
        return Builtin.DefaultSlotForJob(territory, ActiveJobAbbreviation());
    }

    // Local player via the object table (index 0); IClientState.LocalPlayer was
    // removed in this Dalamud build.
    public static Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter? LocalPlayer
        => Service.ObjectTable[0] as Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter;

    // True while a cutscene is playing, so calls and cues are suppressed.
    public static bool InCutscene =>
        Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene]
        || Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene78]
        || Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInCutSceneEvent];

    // The cutscene state everything gameplay-facing should use; the raw flags stick.
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

    // The running assembly version, e.g. "1.0.0.121".
    public static string PluginVersion =>
        typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    // Inside an instanced duty (any of the three bound-by-duty flags the game
    // uses), as opposed to the open world.
    public static bool InDuty =>
        Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty]
        || Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty56]
        || Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty95];

    // True while actually in a pull, when the HUD displays force-lock (see each
    // window's EffectiveLocked) so a stray drag can't grab them mid-fight.
    public static bool InCombat =>
        Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat];

    // Downtime: mid-pull, the boss is present but not targetable (a phase
    // transition, it jumped away, or a cutscene).
    public bool DowntimeActive { get; private set; }
    // Measured in game time, so a lull learned from a 2x replay records its real
    // length.
    public float DowntimeElapsed => _downtimeElapsed;
    // Seconds left until targetable, once this lull has been seen before (learned);
    // -1 the very first time, when we're still measuring it.
    public float DowntimeRemaining => DowntimeActive && _downtimeKnownDur > 0f
        ? MathF.Max(0f, _downtimeKnownDur - DowntimeElapsed) : -1f;
    private float _downtimeElapsed;
    private float _downtimeStartElapsed;
    private float _downtimeKnownDur = -1f;

    // The current boss's HP as a 0..1 fraction (-1 when there's no boss).
    public float BossHpFraction { get; private set; } = -1f;

    // Players still on their feet, or -1 when nothing was counted this frame.
    // Zero while the party is still in combat is what a wipe looks like.
    public int PlayersStanding { get; private set; } = -1;

    // Whoever this pull is about, by NameId: the key a learned timeline is filed
    // under, and how a 3-boss dungeon keeps three separate timelines.
    public uint CurrentBossNameId { get; private set; }
    public string CurrentBossName => _currentBossName;
    private string _currentBossName = "";
    private uint _currentBossMaxHp;

    // True where a timeline has to be learned rather than looked up: in a duty,
    // with no sheet of its own and nothing baked for it.
    public bool LearningHere { get; private set; }

    private bool ComputeLearningHere()
    {
        if (!Config.LearnTimelines) return false;
        if (!InDuty) return false;
        var territory = Service.ClientState.TerritoryType;
        if (UniversalTimelines.Has(territory)) return false;
        foreach (var f in Config.Fights)
            if (f.TerritoryId == territory) return false;
        return true;
    }

    private void UpdateDowntime(float gameDt)
    {
        // The boss sweep walks the whole object table, so it's gated on a running
        // clock.
        IBattleNpc? boss = null;
        IBattleNpc? targetable = null;
        IBattleNpc? biggest = null;
        var standing = Timer.Running ? 0 : -1;
        if (Timer.Running)
            foreach (var o in Service.ObjectTable)
            {
                // In a duty the players in the object table are the party, and
                // a wipe is what empties this count.
                if (o is Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter
                    { MaxHp: > 0, CurrentHp: > 0 }) standing++;
                if (o is not IBattleNpc n || (byte)n.BattleNpcKind != 5) continue;
                if (n.MaxHp > 1_000_000)
                {
                    // The DPS-gate readout wants a raid boss, so it keeps the HP floor.
                    if (boss is null || n.MaxHp > boss.MaxHp) boss = n;
                    // A fight can carry huge untargetable extras (clones, set
                    // pieces, a corpse not yet despawned); the one you can hit is
                    // the boss, not the biggest.
                    if (n is { IsTargetable: true, CurrentHp: > 0 }
                        && (targetable is null || n.MaxHp > targetable.MaxHp)) targetable = n;
                }
                // Learning wants whoever the fight is about, at any level.
                if (n.NameId != 0 && (biggest is null || n.MaxHp > biggest.MaxHp)) biggest = n;
            }
        var hpOf = targetable ?? boss;
        BossHpFraction = hpOf is { MaxHp: > 0 } ? (float)hpOf.CurrentHp / hpOf.MaxHp : -1f;
        PlayersStanding = standing;
        if (biggest != null)
        {
            CurrentBossNameId = biggest.NameId;
            _currentBossName = biggest.Name.ToString();
            _currentBossMaxHp = biggest.MaxHp;
        }

        var down = false;
        if (Timer.Running)
        {
            // Downtime means there's nothing boss-sized to hit, not that the
            // biggest actor happens to be untargetable.
            if (CutsceneActive) down = true;
            else if (boss != null && targetable == null) down = true;
        }

        // Tick the lull's length in game-time so replay speed / pauses can't skew it.
        if (down && DowntimeActive) _downtimeElapsed += gameDt;

        if (down && !DowntimeActive)
        {
            // Just started: stamp it and recall its hardcoded length for the banner.
            _downtimeElapsed = 0f;
            var f = ActiveFight();
            _downtimeStartElapsed = f != null ? ElapsedFor(f) : Timer.Elapsed;
            _downtimeKnownDur = LookupDowntime(f?.TerritoryId, _downtimeStartElapsed);
        }
        else if (!down && DowntimeActive)
        {
            // Just ended: refine the TIME of any learnable window (one cactbot
            // couldn't pin) from what we just measured.
            if (ActiveFight() is { } f) MaybeLearnDowntime(f.TerritoryId, _downtimeStartElapsed, DowntimeElapsed);
            _downtimeKnownDur = -1f;
        }
        DowntimeActive = down;
    }

    // The known length of the lull starting near `start` (-1 if none).
    private float LookupDowntime(uint? territory, float start)
    {
        if (territory is not { } t) return -1f;
        foreach (var w in Downtimes.Effective(t, Config.LearnedDowntimes))
            if (MathF.Abs(w.Start - start) < 8f) return w.Duration;
        return -1f;
    }

    // Record a measured Start/Duration ONLY when it matches a learnable hardcoded
    // window (Learn=true) - the few transitions cactbot leaves uncertain.
    private void MaybeLearnDowntime(uint territory, float start, float dur)
    {
        if (dur < 1.5f) return; // ignore blips
        var target = Downtimes.For(territory).FirstOrDefault(w => w.Learn && MathF.Abs(w.Start - start) < 25f);
        if (target == null) return;
        var key = territory.ToString();
        if (!Config.LearnedDowntimes.TryGetValue(key, out var list))
            Config.LearnedDowntimes[key] = list = new();
        var existing = list.FirstOrDefault(x => MathF.Abs(x.Start - target.Start) < 25f);
        if (existing != null) { existing.Start = start; existing.Duration = dur; }
        else list.Add(new DowntimeWindow { Start = start, Duration = dur });
        Config.Save();
    }

    // Watching a Duty Recorder replay, where the spectator never gets a combat flag.
    public static bool InDutyPlayback =>
        Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.DutyRecorderPlayback];

    // The game's simulation-speed multiplier: 1 normal, 0 paused, 2 for 2x.
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
        catch (Exception ex) { Swallowed.Report("replay speed read", ex); return 1f; }
    }

    private DateTime _lastPlaybackTick = DateTime.UtcNow;

    private bool _firstTickDone;
    private bool _wasInDutyPlayback;
    private DateTime _lastFrameErrLog = DateTime.MinValue;
    public int FrameErrorCount { get; private set; }
    public DateTime LastFrameErrorAt { get; private set; } = DateTime.MinValue;
    private bool _wasInCombatForTest; // edge detector for the Test-mode auto-off

    // Startup that can't run in the constructor, so it runs on the first tick.
    private void RunFirstTickInit()
    {
        // Bake a default slot for any built-in that's still empty.
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
        // Dalamud's tick loop.
        try
        {
            if (!_firstTickDone) { _firstTickDone = true; RunFirstTickInit(); }

            UpdateCutsceneStuck();

            // A real pull always outranks Test mode, so combat switches it off.
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

                // A pull can never begin inside a cutscene, so this proves the flag is
                // stuck.
                if (InCutscene && !CutsceneStuck)
                {
                    CutsceneStuck = true;
                    Service.Log.Warning("[FrenMits] Combat started while the cutscene flag was on; treating the flag as stuck so the overlay shows.");
                }
            }
            _wasInCombatForTest = inCombatNow;

            // Leaving a Duty Recorder playback, where no combat flag would ever stop
            // the timer.
            if (_wasInDutyPlayback && !InDutyPlayback && Timer.Running)
            {
                Timer.Reset();
                Service.Log.Information("[FrenMits] Playback ended; timer stopped.");
            }
            _wasInDutyPlayback = InDutyPlayback;

            // Keep the timeline in step with a replay that is paused or sped up.
            var nowUtc = DateTime.UtcNow;
            var realDt = (float)(nowUtc - _lastPlaybackTick).TotalSeconds;
            _lastPlaybackTick = nowUtc;
            if (InDutyPlayback && Timer.Running && realDt > 0f && realDt < 1f)
                Timer.ShiftStart(realDt * (1f - ReplayGameSpeed()));

            // This frame's GAME-time delta: real seconds scaled by the sim speed
            // (1 in live play, 0 while a replay is paused, 2 at 2x).
            var gameDt = realDt > 0f && realDt < 1f ? realDt * ReplayGameSpeed() : 0f;

            RefreshAutoFight();
            UpdateCountdown();   // arm the clock on a countdown before Update reads it
            Timer.Update();
            UpdateDowntime(gameDt);
            UpdateLearning();
            Recap.Update();
            HandleCutsceneBoundary();
            UpdatePhase();
            Diag.Update();   // open/close the pull record before the engines log into it
            Sync.Update();
            Cues.Update();
            Meter.Update();
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
    // Phase cutscenes pause the action but not our clock, and combat never drops.
    private bool _wasInCutscene;

    // The party's pull countdown, fed to the clock so the board and the calls
    // are already live and already right as the numbers run down.
    private const uint NoCountdown = uint.MaxValue;   // 0 is a real initiator id (unresolved)
    private uint _countdownFrom = NoCountdown;

    private void UpdateCountdown()
    {
        if (!Config.StartOnCountdown || !InDuty)
        { Timer.CancelCountdown(); _countdownFrom = NoCountdown; return; }

        var cd = Countdown.Read();
        if (!cd.Active)
        {
            // Called off, rather than run out: CancelCountdown tells the two apart
            // by whether the zero it is holding has passed.
            Timer.CancelCountdown();
            _countdownFrom = NoCountdown;
            return;
        }

        if (_countdownFrom != cd.InitiatorId)
        {
            _countdownFrom = cd.InitiatorId;
            var who = Countdown.InitiatorName(cd.InitiatorId);
            Service.Log.Information($"[FrenMits] Countdown{(who.Length > 0 ? $" from {who}" : "")}: "
                                  + $"{cd.Remaining:0.0}s; the clock is live.");
        }
        Timer.SetCountdown(cd.Remaining);
    }

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
    // from 0.
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

        // Boss HP fell to zero => Phase 1 cleared, latched until the zone changes.
        if (_trackedBossLastHp > 0 && boss.CurrentHp == 0)
            _phaseTwo = true;
        _trackedBossLastHp = boss.CurrentHp;
    }

    // A phase anchor inside Phase 2's segment proves the door is down.
    public void OnPhaseAnchor(FightProfile fight, SyncPoint sp)
    {
        if (_phaseTwo || fight.TerritoryId != Builtin.M12sTerritory) return;
        if (sp.Time < M12sData.Phase2Offset) return;
        Timer.SetElapsed(Timer.Elapsed - M12sData.Phase2Offset);
        _phaseTwo = true;
        Service.Log.Information($"[FrenMits] Phase 2 latched from anchor '{sp.Label}'.");
    }

    // Extra seconds added to a fight's clock for the current phase (door bosses).
    public float PhaseOffsetFor(FightProfile fight)
        => _phaseTwo && fight.TerritoryId == Builtin.M12sTerritory ? M12sData.Phase2Offset : 0f;

    // The sheet clock: where the fight actually is on the timeline.
    public float ElapsedFor(FightProfile fight)
        => Timer.Elapsed + PhaseOffsetFor(fight);

    // The call schedule the overlay/cues/DTR/upcoming list read: sheet clock plus
    // the fight's timer offset.
    public float CueClockFor(FightProfile fight)
        => ElapsedFor(fight) + fight.TimerOffset;

    // Next-up mit on the server-info bar.
    private void UpdateDtr()
    {
        if (_dtr == null) return;
        if (!Config.ShowDtrBar || !Timer.Live || ActiveFight() is not { } fight || fight.TimelineOnly
            // Same silence rules as the overlay and cues: the clock is known-drifted.
            || CutsceneActive || Cues.Holding)
        {
            _dtr.Shown = false;
            return;
        }

        var job = ActiveJobAbbreviation();
        var elapsed = CueClockFor(fight);
        // Single pass for the soonest call: this runs every tick, and the LINQ
        // chain it replaces sorted the entire plan just to read the front of it.
        MitLine? next = null;
        var nextRemaining = 0f;
        // Time order (now a cached sort), so two calls tied on the clock pick the
        // same one the old OrderBy did.
        foreach (var l in fight.OrderedLines)
        {
            var remaining = l.CueTime - elapsed;
            if (remaining <= 0 || !l.Enabled || !l.AppliesTo(job)) continue;
            if (next == null || remaining < nextRemaining) { next = l; nextRemaining = remaining; }
        }

        if (next == null)
        {
            _dtr.Shown = false;
            return;
        }

        var label = string.IsNullOrWhiteSpace(next.Action) ? next.Mechanic : next.ActionFor(job);
        _dtr.Text = $" {label} {(int)MathF.Ceiling(nextRemaining)}s";
        _dtr.Shown = true;
    }

    private bool _drawErrorLogged;

    private void DrawUi()
    {
        try
        {
            // Before any window draws: age out retired font handles, and make
            // sure the sizes actually configured are already building.
            Fonts.Tick();
            Fonts.WarmIfNeeded(Config);
        }
        catch (Exception ex) { Swallowed.Report("font warm", ex); }

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

    // Practice: a fight to preview out of its zone (set by the phase-jump), used
    // only in Test Mode when the current zone isn't a real fight.
    public static FightProfile? PreviewFight;

    public FightProfile? ActiveFight()
    {
        var territory = Service.ClientState.TerritoryType;
        foreach (var fight in Config.Fights)
            if (fight.Enabled && fight.TerritoryId == territory)
                return fight;
        // A practice phase-jump beats the universal timeline.
        if (Config.TestMode && PreviewFight != null) return PreviewFight;
        // No sheet for this duty: the baked universal timeline (board + combat
        // timer only) steps in, so a timeline runs in every instanced duty.
        if (Config.UniversalTimelines && _autoFight != null && _autoFight.TerritoryId == territory)
            return _autoFight;
        // Nothing baked for this duty: fall back to what we've learned about the
        // boss actually in front of us.
        if (Config.LearnTimelines && _learnedFight != null && _learnedFight.TerritoryId == territory)
            return _learnedFight;
        return null;
    }

    // The learned timeline for the boss currently being fought, rebuilt only when
    // the boss changes (so a 3-boss dungeon swaps timelines as you go).
    private FightProfile? _learnedFight;
    private uint _learnedFor;

    // True while _learnedFight is a projection off the pull IN PROGRESS rather
    // than something read back from disk.
    private bool _learnedIsLive;
    private int _livePullCasts = -1;

    // Latched during the pull, because by the time combat drops the boss has
    // already despawned and CurrentBossNameId has gone back to zero.
    private uint _learnBossNameId;
    private string _learnBossName = "";
    private uint _learnBossMaxHp;
    private bool _wasRunningForLearn;

    private void UpdateLearning()
    {
        LearningHere = ComputeLearningHere();
        LatchLearnBoss();
        RefreshLearnedFight();
        CommitFinishedPull();
    }

    private void LatchLearnBoss()
    {
        if (!LearningHere || CurrentBossNameId == 0) return;
        if (_currentBossMaxHp <= _learnBossMaxHp) return;
        _learnBossMaxHp = _currentBossMaxHp;
        _learnBossNameId = CurrentBossNameId;
        _learnBossName = _currentBossName;
    }

    private void RefreshLearnedFight()
    {
        if (!Config.LearnTimelines) { ClearLearnedFight(); return; }
        var territory = Service.ClientState.TerritoryType;
        var boss = CurrentBossNameId;

        if (boss != _learnedFor)
        {
            _learnedFor = boss;
            _livePullCasts = -1;
            _learnedIsLive = false;
            _learnedFight = boss == 0 || UniversalTimelines.Has(territory)
                ? null
                : TimelineLearner.Build(Config, boss, territory);
            if (_learnedFight != null)
                Service.Log.Information(
                    $"[FrenMits] learned timeline armed for \"{_learnedFight.Name}\" "
                    + $"({_learnedFight.Lines.Count} casts).");
        }

        // A timeline already learned for this boss always wins; only fall back to
        // reading the live pull when there's nothing stored.
        if (_learnedFight != null && !_learnedIsLive) return;
        if (boss == 0 || !LearningHere || !Timer.Running) return;
        // Re-read as the boss reveals more of its loop, not once and then frozen.
        if (Sync.LastPull.Count == _livePullCasts) return;
        _livePullCasts = Sync.LastPull.Count;

        var built = TimelineLearner.BuildFromLivePull(
            territory, _currentBossName, boss, CapturedCasts());
        if (built != null) { _learnedFight = built; _learnedIsLive = true; }
    }

    // A pull just ended: fold what the boss did into what we already knew, and
    // drop any live projection, whose times only meant anything for that pull.
    private void CommitFinishedPull()
    {
        var running = Timer.Running;
        var ended = _wasRunningForLearn && !running;
        _wasRunningForLearn = running;
        if (!ended) return;

        try
        {
            if (!Config.LearnTimelines || _learnBossNameId == 0 || Sync.LastPull.Count == 0) return;

            // LearnPull, not Learn: the capture spans the whole instance, so the
            // boss's own engagement has to be cut out and rebased first.
            if (TimelineLearner.LearnPull(Config, _learnBossNameId, _learnBossName,
                    Sync.LastPullTerritory, CapturedCasts()))
            {
                Config.Save();
                Service.Log.Information(
                    $"[FrenMits] learned timeline updated for \"{_learnBossName}\" from this pull.");
            }
        }
        catch (Exception ex) { Swallowed.Report("timeline learning", ex); }
        finally
        {
            // Always, even on the early returns above, or learning quietly ends for the
            // session.
            _learnBossNameId = 0;
            _learnBossName = "";
            _learnBossMaxHp = 0;
            if (_learnedIsLive) ClearLearnedFight();
            _learnedFor = 0;   // re-read from disk next frame, with this pull folded in
        }
    }

    private void ClearLearnedFight()
    {
        _learnedFight = null;
        _learnedFor = 0;
        _learnedIsLive = false;
        _livePullCasts = -1;
    }

    // This pull's enemy casts, with who cast them (boss-appearance markers are
    // bookkeeping, not mechanics).
    private List<TimelineLearner.PullCast> CapturedCasts()
    {
        var casts = new List<TimelineLearner.PullCast>(Sync.LastPull.Count);
        foreach (var cp in Sync.LastPull)
            if (!cp.IsBoss)
                casts.Add(new TimelineLearner.PullCast(cp.Id, cp.Time, ActionNames.Of(cp.Id), cp.CasterNameId));
        return casts;
    }

    // The in-memory timeline-only fight for the current territory (never saved).
    private FightProfile? _autoFight;
    private uint _autoFightTerritory = uint.MaxValue;

    // Cheap per-frame check: (re)build the auto fight when the territory changes.
    private int _autoFightsStamp = -1;

    private void RefreshAutoFight()
    {
        var territory = Service.ClientState.TerritoryType;
        // Re-check when the zone changes or the fights list does.
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
        // SetElapsed doesn't bump Generation, so the fresh-pull check never re-arms.
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
        Meter.Dispose();
        Damage.Dispose();
        Windows.RemoveAllWindows();
        ConfigWindow.Dispose();
        Fonts.Dispose();
        Audio.Dispose();
    }
}
