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
    public RecapButtonWindow RecapButtonWindow { get; }
    public RecapWindow RecapWindow { get; }

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

        // Migrate the old M12S placeholder zone (1320) to the real one (1327).
        foreach (var f in Config.Fights)
            if (f.TerritoryId == 1320)
            {
                f.TerritoryId = Builtin.M12sTerritory;
                f.Category = "Savage";
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

        // Bake a default slot for any built-in that's still empty (freshly seeded,
        // or seeded empty by an older version that only baked on zone-in), so its
        // mits show up front instead of reading "(0)". Your own edits and any slot
        // you've already loaded are left untouched.
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
        RecapButtonWindow = new RecapButtonWindow(this);
        RecapWindow = new RecapWindow(this);
        Windows.AddWindow(ConfigWindow);
        Windows.AddWindow(OverlayWindow);
        Windows.AddWindow(TimelineWindow);
        Windows.AddWindow(MitBarWindow);
        Windows.AddWindow(RecapButtonWindow);
        Windows.AddWindow(RecapWindow);
        OverlayWindow.IsOpen = true;
        TimelineWindow.IsOpen = true;
        MitBarWindow.IsOpen = true;
        RecapButtonWindow.IsOpen = true;

        Service.CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Fren Mits. /fm sync = zero the timer, /fm test = toggle test mode, /fm reset = clear the timer."
        });
        Service.CommandManager.AddHandler(CommandAlias, new CommandInfo(OnCommand));

        try { _dtr = Service.DtrBar.Get("Fren Mits"); }
        catch (Exception ex) { Service.Log.Warning(ex, "FrenMits: DTR entry failed"); }

        Service.PluginInterface.UiBuilder.Draw += DrawUi;
        Service.PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        Service.PluginInterface.UiBuilder.OpenMainUi += OpenConfig;
        Service.Framework.Update += OnFrameworkUpdate;
        Service.ClientState.TerritoryChanged += OnTerritoryChanged;

        // Cover the case where the plugin loads while already inside a boss room.
        if (Builtin.Has(Service.ClientState.TerritoryType))
            AutoLoadForTerritory(Service.ClientState.TerritoryType);

        // Diagnostic: if this ever logs "#2" (or higher) while only one copy should be
        // running, the plugin is double-loaded — which would double every audio cue.
        var n = System.Threading.Interlocked.Increment(ref _liveInstances);
        Service.Log.Information($"[FrenMits] init — live instance #{n}");
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

        var slot = !string.IsNullOrEmpty(fight.Slot) ? fight.Slot : PreferredDefaultSlot(territory);

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

    // Replay state (desk testing). When ReplayFight is set the normal pipeline runs
    // against the recording instead of the live instance: ActiveFight resolves to
    // it, the live timer/territory gates step aside, and ReplayCutsceneActive
    // drives InCutscene from the recorded cutscene windows.
    public static FightProfile? ReplayFight;
    public static bool ReplayCutsceneActive;
    public static bool Replaying => ReplayFight != null;

    private bool _frameErrorLogged;

    private void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework _)
    {
        // Never let a per-frame hiccup (e.g. a stale game object) escape into
        // Dalamud's tick loop. Log the first one, then stay quiet.
        try
        {
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

    // The fight clock the overlay/cues read: pull time + per-fight offset + phase.
    public float ElapsedFor(FightProfile fight)
        => Timer.Elapsed + fight.TimerOffset + PhaseOffsetFor(fight);

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
        var elapsed = ElapsedFor(fight);
        var next = fight.OrderedLines
            .Where(l => l.Enabled && l.AppliesTo(job) && l.Time - elapsed > 0)
            .Select(l => (l, remaining: l.Time - elapsed))
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
            default:
                ConfigWindow.Toggle();
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
    public FightProfile? ActiveFight()
    {
        if (Replaying) return ReplayFight;
        var territory = Service.ClientState.TerritoryType;
        foreach (var fight in Config.Fights)
            if (fight.Enabled && fight.TerritoryId == territory)
                return fight;
        return null;
    }

    public void Dispose()
    {
        // Never leave replay state latched across a reload.
        ReplayFight = null;
        ReplayCutsceneActive = false;

        Service.Log.Information($"[FrenMits] dispose — live instances now {System.Threading.Interlocked.Decrement(ref _liveInstances)}");
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
