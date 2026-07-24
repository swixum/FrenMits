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
    public SnapshotStore Snapshots { get; }
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
        Snapshots = new SnapshotStore(Config);
        FrenMits.Windows.Theme.Colorblind = Config.ColorblindMode; // status palette follows the setting

        // Versioned migrations (v2..v23) live in ConfigMigrations.
        ConfigMigrations.Run(this);

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
        // button to click.
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
        // "(Ultimate)" suffix for the short code, matching the others).
        foreach (var f in Config.Fights)
        {
            if (f.Name == "Dancing Mad (Ultimate)") { f.Name = Builtin.Name(Builtin.DmuTerritory); seeded = true; }
            else if (f.Name == "Futures Rewritten (Ultimate)") { f.Name = Builtin.Name(Builtin.FruTerritory); seeded = true; }
        }

        if (seeded) Config.Save();

        // NOTE: the default-slot prebake and the "already inside a boss room"
        // auto-load both need main-thread game state, so they are deferred to the
        // first Framework.Update tick (see RunFirstTickInit()).

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
        // running, the plugin is double-loaded, which would double every audio cue.
        var n = System.Threading.Interlocked.Increment(ref _liveInstances);
        Service.Log.Information($"[FrenMits] init - live instance #{n}");
    }

    private static int _liveInstances;

    // Load the saved config defensively: if the file exists but won't deserialize,
    // keep it intact (backed up) and suppress saves for the session instead of
    // clobbering the user's real settings with defaults.
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

    // Seamless auto-load: on entering a boss room we support, top up the fight's
    // lines with the latest baked timeline (adding only what's missing) and refresh
    // the resync anchors, keeping every edit the user has made.
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
        // that has no fight of its own.
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
    // lines).
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

    // Switch which sheet column is "yours" for a fight: builtin fights load the
    // slot's plan through ApplySlot (keeping each slot's saved edits), custom
    // sheets swap the saved lists directly.
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
        AutoTime(fight);

        Service.Log.Information($"FrenMits auto-load: territory {territory}, slot {fight.Slot}, +{added} lines.");
    }

    // Run the cooldown-aware offset solver over a fight's active slot, so its
    // presses fire early enough to cover their hits and keep the recast ready for
    // the next mechanic.
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
        // hand a fresh 8.0 job the main tank's calls.
        if (LocalPlayer is { } p && Jobs.ByRowId(p.ClassJob.RowId) is null) return "";
        return Builtin.DefaultSlotForJob(territory, ActiveJobAbbreviation());
    }

    // Local player via the object table (index 0); IClientState.LocalPlayer was
    // removed in this Dalamud build.
    public static Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter? LocalPlayer
        => Service.ObjectTable[0] as Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter;

    // True while a cutscene is playing (phase-transition cutscenes in ultimates) so
    // call-outs and cues are suppressed: you can't act, and the clock self-corrects
    // on the next resync anchor when it ends.
    public static bool InCutscene =>
        Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene]
        || Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WatchingCutscene78]
        || Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInCutSceneEvent];

    // The cutscene state everything gameplay-facing should use: the raw game
    // flags occasionally STICK after a cutscene ends, so a cutscene reading true
    // for 3+ minutes straight is treated as stuck.
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

    // True while actually in a pull, when the HUD displays force-lock (see each
    // window's EffectiveLocked) so a stray drag can't grab them mid-fight.
    public static bool InCombat =>
        Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat];

    // Downtime: mid-pull, the boss is present but not targetable (a phase
    // transition, it jumped away, or a cutscene).
    public bool DowntimeActive { get; private set; }
    // Measured in GAME time - accumulated at the replay's own speed and frozen
    // while it's paused, not on the wall clock - so a lull learned from a 2x (or
    // paused) replay records its real in-game length, not the real seconds you
    // spent watching it.
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

    private void UpdateDowntime(float gameDt)
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

    // Watching a Duty Recorder replay (e.g. via A Realm Recorded), where the
    // spectator never gets a combat flag so the timer auto-starts from the
    // replay's own casts (SyncEngine.TryPlaybackAutoStart).
    public static bool InDutyPlayback =>
        Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.DutyRecorderPlayback];

    // The game's simulation-speed multiplier (1 normal, 0 paused, 2 for 2x during
    // a replay), read to keep the timeline and alerts in step with the replay
    // instead of ticking on real time.
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

    // Game-state-dependent startup that can't run in the constructor (loader thread),
    // so it runs once on the first Framework.Update tick where ObjectTable /
    // ClientState access is safe.
    private void RunFirstTickInit()
    {
        // Bake a default slot for any built-in that's still empty (freshly seeded,
        // or seeded empty by an older version that only baked on zone-in), so its
        // mits show up front instead of reading "(0)".
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

            // A REAL pull always outranks Test mode, since left on it would keep
            // the overlays unlocked and visible through cutscenes mid-fight, so
            // combat switches it off.
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
                // ticking).
                if (InCutscene && !CutsceneStuck)
                {
                    CutsceneStuck = true;
                    Service.Log.Warning("[FrenMits] Combat started while the cutscene flag was on; treating the flag as stuck so the overlay shows.");
                }
            }
            _wasInCombatForTest = inCombatNow;

            // Leaving a Duty Recorder playback: the spectator never gets combat
            // flags, so the replay-started timer would keep ticking on the menus
            // forever.
            if (_wasInDutyPlayback && !InDutyPlayback && Timer.Running)
            {
                Timer.Reset();
                Service.Log.Information("[FrenMits] Playback ended; timer stopped.");
            }
            _wasInDutyPlayback = InDutyPlayback;

            // Keep the timeline in step with a Duty Recorder replay: real time
            // keeps running while playback is paused (or sped up), so nudge the
            // clock by realDelta * (1 - gameSpeed).
            var nowUtc = DateTime.UtcNow;
            var realDt = (float)(nowUtc - _lastPlaybackTick).TotalSeconds;
            _lastPlaybackTick = nowUtc;
            if (InDutyPlayback && Timer.Running && realDt > 0f && realDt < 1f)
                Timer.ShiftStart(realDt * (1f - ReplayGameSpeed()));

            // This frame's GAME-time delta: real seconds scaled by the sim speed
            // (1 in live play, 0 while a replay is paused, 2 at 2x).
            var gameDt = realDt > 0f && realDt < 1f ? realDt * ReplayGameSpeed() : 0f;

            RefreshAutoFight();
            Timer.Update();
            UpdateDowntime(gameDt);
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
    // resync engine never re-arms on its own.
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

    // A phase anchor landing inside Phase 2's segment proves the door is down even
    // when the kill was never seen (late join, reconnect, replay chapter jump).
    // Flip the latch and pull the offset back out of the raw timer in the same
    // frame, so ElapsedFor is unchanged NOW and the next pull starts on the
    // Phase 2 clock immediately instead of calling Phase 1 until the first anchor.
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

    // Practice: a fight to preview out of its zone (set by the phase-jump), used
    // only in Test Mode when the current zone isn't a real fight.
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

    // Cheap per-frame check: (re)build the auto fight when the territory changes.
    private int _autoFightsStamp = -1;

    private void RefreshAutoFight()
    {
        var territory = Service.ClientState.TerritoryType;
        // Re-check when the zone changes OR the fights list does (adding a
        // sheet mid-instance stands the auto timeline down; deleting the only
        // sheet brings it back).
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
