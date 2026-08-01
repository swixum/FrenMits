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

    // Dancing Mad's territory, kept for the preset button.
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

        // Slot names run through the standard on every load.
        var slotsRenamed = false;
        foreach (var f in Config.Fights)
            slotsRenamed |= SlotNames.NormalizeFight(f);
        // Rename pinned columns too, or old pins stop matching.
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

        // Meter columns from a pre-Replace build carry doubles.
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

        // Migrate the two built-ins that were renamed.
        foreach (var f in Config.Fights)
        {
            if (f.Name == "Dancing Mad (Ultimate)") { f.Name = Builtin.Name(Builtin.DmuTerritory); seeded = true; }
            else if (f.Name == "Futures Rewritten (Ultimate)") { f.Name = Builtin.Name(Builtin.FruTerritory); seeded = true; }
        }

        if (seeded) Config.Save();

        AdoptSupersededSheets();

        // Deferred to the first tick, since both need game state.

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
            // The server-bar countdown doubles as a button.
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

        // If this ever logs a second instance, cues would double.
        var n = System.Threading.Interlocked.Increment(ref _liveInstances);
        Service.Log.Information($"[FrenMits] init - live instance #{n}");
    }

    private static int _liveInstances;

    // Load defensively: a bad file is kept and saves suppressed.
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
            // The file is there but unreadable, so not a first run.
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

    // Point Fights at the plan file, moving them out once.
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

        // Never written back, so drop it either way.
        var legacy = config.LegacyFights;
        config.LegacyFights = null;

        if (PlanStore.PreferConfigCopy(plans != null, legacy?.Count ?? 0, PlanStore.ConfigIsNewerThanPlans()))
        {
            // The first load after the split, or a rollback coming forward.
            config.Fights = legacy!;
            PlanStore.BackupConfigBeforeSplit();
            PlanStore.Save(config.Fights);
            Service.Log?.Information(
                $"FrenMits: took {config.Fights.Count} fight plans from the config into {PlanStore.FileName}.");
            return;
        }

        if (plans != null) config.Fights = plans;
    }

    // On entering a boss room, top up lines and refresh anchors.
    private void OnTerritoryChanged(uint territory)
    {
        // A replay clock has no combat flag, so leaving stops it.
        if (Timer.Live && !InCombat) Timer.Reset();

        // Re-entering the instance resets the door-boss phase.
        _phaseTwo = false;
        _trackedBossEntity = 0;
        _trackedBossLastHp = 0;
        // A practice preview never survives a zone change.
        PreviewFight = null;
        try { AutoLoadForTerritory(territory); }
        catch (Exception ex) { Service.Log.Error(ex, "FrenMits: auto-load failed"); }

        // Opt-in check-in, once per entry, for fights with a sheet.
        if (Config.ShowSlotPopupOnEntry)
        {
            var sheetFight = Config.Fights.FirstOrDefault(f => f.Enabled && f.TerritoryId == territory
                && (Builtin.Has(f.TerritoryId) || f.CustomSlots.Count > 0));
            if (sheetFight != null) SlotPopupWindow.OpenFor(sheetFight);
        }
    }

    // Full refresh: rebake every built-in, discarding edits.
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

    // The migrations only ever snapshot through here.
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

    // Apply a canonical role to every fight that has a sheet.
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

    // Stash the current column and make the target's list live.
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

        // Prefer the enabled profile, which is what drives the fight.
        var fight = Config.Fights.FirstOrDefault(f => f.Enabled && f.TerritoryId == territory)
                    ?? Config.Fights.FirstOrDefault(f => f.TerritoryId == territory);
        if (fight == null)
        {
            fight = new FightProfile { Name = Builtin.Name(territory), TerritoryId = territory };
            Config.Fights.Add(fight);
        }
        if (!fight.Enabled) return;

        // Fall back to a default, so a dead slot can't bake the fight.
        var slot = !string.IsNullOrEmpty(fight.Slot)
                   && Builtin.Slots(territory).Contains(fight.Slot, StringComparer.OrdinalIgnoreCase)
            ? fight.Slot
            : PreferredDefaultSlot(territory);

        // No safe guess, so the popup asks instead.
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

    // Run the offset solver over a fight's active slot.
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

    // Erase every offset the auto-timer wrote, everywhere.
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

    // Custom sheets follow the sidebar pick unless you chose one.
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

    // Default slot for a fight with none: the role, else the job.
    private string PreferredDefaultSlot(uint territory)
    {
        var roleSlot = Builtin.RoleSlot(territory, Config.RoleSelection);
        if (!string.IsNullOrEmpty(roleSlot)) return roleSlot!;
        // A player on a job missing from the Jobs table gets no guess.
        if (LocalPlayer is { } p && Jobs.ByRowId(p.ClassJob.RowId) is null) return "";
        return Builtin.DefaultSlotForJob(territory, ActiveJobAbbreviation());
    }

    // Local player via the object table, since the property is gone.
    public static Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter? LocalPlayer
        => Service.ObjectTable[0] as Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter;

    // True while a cutscene plays, so calls are suppressed.
    public static bool InCutscene =>
        Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene]
        || Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene78]
        || Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInCutSceneEvent];

    // The cutscene state to use, since the raw flags stick.
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

    // Inside an instanced duty, as opposed to the open world.
    public static bool InDuty =>
        Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty]
        || Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty56]
        || Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty95];

    // True while in a pull, when the displays force-lock.
    public static bool InCombat =>
        Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat];

    // Downtime: the boss is present but not targetable.
    public bool DowntimeActive { get; private set; }
    // Measured in game time, so a 2x replay still reads right.
    public float DowntimeElapsed => _downtimeElapsed;
    // Seconds until targetable, -1 while still measuring.
    public float DowntimeRemaining => _downtimeRemaining;
    private float _downtimeElapsed;
    private float _downtimeRemaining = -1f;
    private float _downtimeStartElapsed;
    private float _downtimeKnownEnd = -1f;

    // The boss's health as a fraction, -1 with no boss.
    public float BossHpFraction { get; private set; } = -1f;

    // Players still up, -1 when uncounted.
    public int PlayersStanding { get; private set; } = -1;

    // Whoever this pull is about, which files its timeline.
    public uint CurrentBossNameId { get; private set; }
    public string CurrentBossName => _currentBossName;
    private string _currentBossName = "";
    private uint _currentBossMaxHp;

    // True where a timeline has to be learned, not looked up.
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

    // A raid boss's health bar, told from trash by the lower floor.
    public const uint BossHpFloor = 1_000_000;
    private const uint BossHpPlayerMultiple = 15;

    public static bool BossSized(uint maxHp, uint playerMaxHp)
        => maxHp > (playerMaxHp > 0 ? Math.Min(BossHpFloor, playerMaxHp * BossHpPlayerMultiple) : BossHpFloor);

    // Who in your own party is up, not the whole zone.
    private static int StandingInParty()
    {
        var party = Service.PartyList;
        // Nobody to read: unknown beats calling it a wipe.
        if (party.Length == 0)
            return LocalPlayer is { } me ? (me.CurrentHp > 0 ? 1 : 0) : -1;
        var up = 0;
        foreach (var m in party)
            if (m.CurrentHP > 0) up++;
        return up;
    }

    private void UpdateDowntime(float gameDt)
    {
        // The boss sweep walks the object table, so gate it.
        IBattleNpc? boss = null;
        IBattleNpc? targetable = null;
        IBattleNpc? biggest = null;
        var playerMaxHp = LocalPlayer?.MaxHp ?? 0u;
        var standing = Timer.Running ? StandingInParty() : -1;
        if (Timer.Running)
            foreach (var o in Service.ObjectTable)
            {
                if (o is not IBattleNpc n || (byte)n.BattleNpcKind != 5) continue;
                if (BossSized(n.MaxHp, playerMaxHp))
                {
                    // The DPS-gate readout wants a raid boss, so trash stays out.
                    if (boss is null || n.MaxHp > boss.MaxHp) boss = n;
                    // The targetable enemy is the boss, not the biggest.
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
            // Downtime means nothing boss-sized to hit.
            if (CutsceneActive) down = true;
            else if (boss != null && targetable == null) down = true;
        }

        // Tick the lull in game time, so replay speed can't skew it.
        if (down && DowntimeActive) _downtimeElapsed += gameDt;

        var fight = ActiveFight();
        var clock = fight != null ? ElapsedFor(fight) : Timer.Elapsed;
        if (down && !DowntimeActive)
        {
            // Just started: stamp it and recall when the boss is back.
            _downtimeElapsed = 0f;
            _downtimeStartElapsed = clock;
            _downtimeKnownEnd = LookupTargetable(fight?.TerritoryId, clock);
            Diag.Note(_downtimeKnownEnd > 0f
                ? $"downtime START clock={clock:0.0}  targetable={_downtimeKnownEnd:0.0}"
                : $"downtime START clock={clock:0.0}  (no window)");
        }
        else if (!down && DowntimeActive)
        {
            // Just ended: refine any learnable window from the measurement.
            if (fight != null) MaybeLearnDowntime(fight.TerritoryId, _downtimeStartElapsed, DowntimeElapsed);
            Diag.Note($"downtime END   clock={clock:0.0}  lasted={DowntimeElapsed:0.0}s");
            _downtimeKnownEnd = -1f;
        }
        DowntimeActive = down;
        _downtimeRemaining = down && _downtimeKnownEnd > 0f
            ? MathF.Max(0f, _downtimeKnownEnd - clock) : -1f;
    }

    // When the boss is targetable again near start, else -1.
    private float LookupTargetable(uint? territory, float start)
        => territory is { } t
            ? Downtimes.TargetableAt(Downtimes.Effective(t, Config.LearnedDowntimes), start)
            : -1f;

    // Record a measurement only for a window marked learnable.
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

    // Watching a replay, where nobody gets a combat flag.
    public static bool InDutyPlayback =>
        Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.DutyRecorderPlayback];

    // The simulation speed: 1 normal, 0 paused, 2 for double.
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

    // Startup that can't run in the constructor.
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

        // Covers loading while already inside a boss room.
        if (Builtin.Has(Service.ClientState.TerritoryType))
            AutoLoadForTerritory(Service.ClientState.TerritoryType);
    }

    private void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework _)
    {
        // Never let a per-frame hiccup escape into the tick loop.
        try
        {
            if (!_firstTickDone) { _firstTickDone = true; RunFirstTickInit(); }

            UpdateCutsceneStuck();

            // A real pull outranks Test mode.
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

                // A pull can't begin in a cutscene, so the flag is stuck.
                if (InCutscene && !CutsceneStuck)
                {
                    CutsceneStuck = true;
                    Service.Log.Warning("[FrenMits] Combat started while the cutscene flag was on; treating the flag as stuck so the overlay shows.");
                }
            }
            _wasInCombatForTest = inCombatNow;

            // Leaving a replay, where no combat flag would stop the timer.
            if (_wasInDutyPlayback && !InDutyPlayback && Timer.Running)
            {
                Timer.Reset();
                Service.Log.Information("[FrenMits] Playback ended; timer stopped.");
            }
            _wasInDutyPlayback = InDutyPlayback;

            // Keep the timeline in step with a paused or sped-up replay.
            var nowUtc = DateTime.UtcNow;
            var realDt = (float)(nowUtc - _lastPlaybackTick).TotalSeconds;
            _lastPlaybackTick = nowUtc;
            if (InDutyPlayback && Timer.Running && realDt > 0f && realDt < 1f)
                Timer.ShiftStart(realDt * (1f - ReplayGameSpeed()));

            // This frame's game-time delta, scaled by the sim speed.
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
            // Rate-limited, since a recurring throw would kill the engines.
            FrameErrorCount++;
            LastFrameErrorAt = DateTime.UtcNow;
            if ((DateTime.UtcNow - _lastFrameErrLog).TotalSeconds >= 60)
            {
                _lastFrameErrLog = DateTime.UtcNow;
                Service.Log.Error(ex, $"FrenMits: framework update error (x{FrameErrorCount} this session)");
            }
        }
    }

    // ---- cutscene boundary ----
    private bool _wasInCutscene;

    // The party's countdown, fed to the clock so calls go live.
    private const uint NoCountdown = uint.MaxValue;   // 0 is a real initiator id (unresolved)
    private uint _countdownFrom = NoCountdown;

    private void UpdateCountdown()
    {
        if (!Config.StartOnCountdown || !InDuty)
        { Timer.CancelCountdown(); _countdownFrom = NoCountdown; return; }

        var cd = Countdown.Read();
        if (!cd.Active)
        {
            // Called off rather than run out, which Cancel tells apart.
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

    // ---- door-boss phases ----
    private bool _phaseTwo;
    private uint _trackedBossEntity;
    private uint _trackedBossLastHp;

    private void UpdatePhase()
    {
        // Only relevant for the door-boss territory.
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

        // Boss health hit zero, so Phase 1 is cleared.
        if (_trackedBossLastHp > 0 && boss.CurrentHp == 0)
            _phaseTwo = true;
        _trackedBossLastHp = boss.CurrentHp;
    }

    // A phase anchor in Phase 2's segment proves the door is down.
    public void OnPhaseAnchor(FightProfile fight, SyncPoint sp)
    {
        if (_phaseTwo || fight.TerritoryId != Builtin.M12sTerritory) return;
        if (sp.Time < M12sData.Phase2Offset) return;
        Timer.SetElapsed(Timer.Elapsed - M12sData.Phase2Offset);
        _phaseTwo = true;
        Service.Log.Information($"[FrenMits] Phase 2 latched from anchor '{sp.Label}'.");
    }

    // Extra seconds on a fight's clock for the current phase.
    public float PhaseOffsetFor(FightProfile fight)
        => _phaseTwo && fight.TerritoryId == Builtin.M12sTerritory ? M12sData.Phase2Offset : 0f;

    // The sheet clock: where the fight actually is on the timeline.
    public float ElapsedFor(FightProfile fight)
        => Timer.Elapsed + PhaseOffsetFor(fight);

    // The call schedule: sheet clock plus the fight's offset.
    public float CueClockFor(FightProfile fight)
        => ElapsedFor(fight) + fight.TimerOffset;

    // Next-up mit on the server-info bar.
    private void UpdateDtr()
    {
        if (_dtr == null) return;
        if (!Config.ShowDtrBar || !Timer.Live || ActiveFight() is not { } fight || fight.TimelineOnly
            // Same silence rules as the overlay and cues.
            || CutsceneActive || Cues.Holding)
        {
            _dtr.Shown = false;
            return;
        }

        var job = ActiveJobAbbreviation();
        var elapsed = CueClockFor(fight);
        // A single pass for the soonest call, since this runs per tick.
        MitLine? next = null;
        var nextRemaining = 0f;
        // Time order, so tied calls pick the same one as before.
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
            // Before any window draws: age handles and warm the sizes.
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

    // A command's answer, in the log and in front of the player.
    private static void Chat(string message)
    {
        Service.Log.Information($"[FrenMits] {message}");
        try { Service.ChatGui.Print($"[FrenMits] {message}"); }
        catch (Exception ex) { Swallowed.Report("chat print", ex); }
    }

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
            // Record what the parser feeds, so a bad pull can be replayed.
            case "meterrec":
                if (MeterFeed.Recording)
                {
                    var saved = MeterFeed.Stop();
                    Chat(saved.Length > 0 ? $"Meter feed saved: {saved}" : "Nothing was recorded.");
                }
                else
                {
                    MeterFeed.Start();
                    Chat("Recording the meter feed. Run /fm meterrec again to save it.");
                }
                break;
            case "meterplay":
                var newest = MeterFeed.Newest();
                Chat(newest.Length == 0
                    ? "No recordings yet - run /fm meterrec before a pull."
                    : $"{System.IO.Path.GetFileName(newest)}: {Meter.Replay(newest)}");
                break;
            // The meter's session diag file, kept off unless turned on here.
            case "meterdiag":
                Config.MeterDiagFile = !Config.MeterDiagFile;
                Config.SaveSettings();
                Chat(Config.MeterDiagFile
                    ? "Meter diag file on (this PC only)."
                    : "Meter diag file off.");
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

    // The job the overlay follows: an override, or the live job.
    public string? ActiveJobAbbreviation()
    {
        if (!string.Equals(Config.JobSelection, "Auto", StringComparison.OrdinalIgnoreCase))
            return Config.JobSelection;

        var job = LocalPlayer?.ClassJob.RowId;
        return job is { } rowId ? Jobs.ByRowId(rowId)?.Abbreviation : null;
    }

    // Practice: a fight to preview out of its zone.
    public static FightProfile? PreviewFight;

    public FightProfile? ActiveFight()
    {
        var territory = Service.ClientState.TerritoryType;
        foreach (var fight in Config.Fights)
            if (fight.Enabled && fight.TerritoryId == territory)
                return fight;
        // A practice phase-jump beats the universal timeline.
        if (Config.TestMode && PreviewFight != null) return PreviewFight;
        // No sheet here, so the baked universal timeline steps in.
        if (Config.UniversalTimelines && _autoFight != null && _autoFight.TerritoryId == territory)
            return _autoFight;
        // Nothing baked either, so fall back on what we've learned.
        if (Config.LearnTimelines && _learnedFight != null && _learnedFight.TerritoryId == territory)
            return _learnedFight;
        return null;
    }

    // The learned timeline, rebuilt only when the boss changes.
    private FightProfile? _learnedFight;
    private uint _learnedFor;

    // True while this is a projection off the pull in progress.
    private bool _learnedIsLive;
    private int _livePullCasts = -1;

    // Latched during the pull, since the boss despawns first.
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

        // A stored timeline wins over reading the live pull.
        if (_learnedFight != null && !_learnedIsLive) return;
        if (boss == 0 || !LearningHere || !Timer.Running) return;
        // Re-read as the boss reveals more of its loop.
        if (Sync.LastPull.Count == _livePullCasts) return;
        _livePullCasts = Sync.LastPull.Count;

        var built = TimelineLearner.BuildFromLivePull(
            territory, _currentBossName, boss, CapturedCasts());
        if (built != null) { _learnedFight = built; _learnedIsLive = true; }
    }

    // A pull ended: fold it in and drop the live projection.
    private void CommitFinishedPull()
    {
        var running = Timer.Running;
        var ended = _wasRunningForLearn && !running;
        _wasRunningForLearn = running;
        if (!ended) return;

        try
        {
            if (!Config.LearnTimelines || _learnBossNameId == 0 || Sync.LastPull.Count == 0) return;

            // LearnPull, since the capture spans the whole instance.
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
            // Always, or learning quietly ends for the session.
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

    // This pull's enemy casts, with who cast them.
    private List<TimelineLearner.PullCast> CapturedCasts()
    {
        var casts = new List<TimelineLearner.PullCast>(Sync.LastPull.Count);
        foreach (var cp in Sync.LastPull)
            if (!cp.IsBoss)
                casts.Add(new TimelineLearner.PullCast(cp.Id, cp.Time, ActionNames.Of(cp.Id), cp.CasterNameId));
        return casts;
    }

    // The timeline-only fight for this territory, never saved.
    private FightProfile? _autoFight;
    private uint _autoFightTerritory = uint.MaxValue;

    // Rebuild the auto fight when the territory changes.
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
        // Enabled is ignored: a disabled profile means stay silent.
        _autoFight = Config.Fights.Any(f => f.TerritoryId == territory)
            ? null
            : UniversalTimelines.Build(territory);
        if (_autoFight != null)
            Service.Log.Information($"[FrenMits] universal timeline armed for \"{_autoFight.Name}\" ({territory}).");
    }

    // Practice jump: park the clock just before a phase's calls.
    public void PracticeJump(FightProfile fight, float time)
    {
        PreviewFight = fight;
        if (!Config.TestMode) { Config.TestMode = true; Config.Save(); }
        var raw = time - 6f - fight.TimerOffset - PhaseOffsetFor(fight);
        Timer.SetElapsed(MathF.Max(0f, raw));
        // SetElapsed doesn't bump Generation, so re-arm by hand.
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
