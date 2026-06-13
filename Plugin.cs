using System;
using System.Linq;
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
    public readonly WindowSystem Windows = new("FrenMits");
    public ConfigWindow ConfigWindow { get; }
    public OverlayWindow OverlayWindow { get; }
    public TimelineWindow TimelineWindow { get; }

    private readonly IDtrBarEntry? _dtr;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();

        Config = Service.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
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

        // Ship ready-to-fill profiles for the built-in ultimates on first launch
        // so the plugin already targets the right encounters; the user just picks
        // their slot and loads the baked mits.
        if (Config.Fights.Count == 0)
            foreach (var (territory, name) in Builtin.Fights)
                Config.Fights.Add(new FightProfile { Name = name, TerritoryId = territory });

        Cues = new CueEngine(this, Audio);
        Sync = new SyncEngine(this);
        ConfigWindow = new ConfigWindow(this);
        OverlayWindow = new OverlayWindow(this);
        TimelineWindow = new TimelineWindow(this);
        Windows.AddWindow(ConfigWindow);
        Windows.AddWindow(OverlayWindow);
        Windows.AddWindow(TimelineWindow);
        OverlayWindow.IsOpen = true;
        TimelineWindow.IsOpen = true;

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
    }

    // Seamless auto-load: on entering a boss room we support, top up the fight's
    // lines with the latest baked timeline (adding only what's missing) and refresh
    // the resync anchors — keeping every edit the user has made. Silent, no prompts.
    private void OnTerritoryChanged(uint territory) => AutoLoadForTerritory(territory);

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

        var slot = !string.IsNullOrEmpty(fight.Slot)
            ? fight.Slot
            : Builtin.DefaultSlotForJob(territory, ActiveJobAbbreviation());

        var added = Builtin.ApplySlot(fight, slot);
        Config.DmuSlot = fight.Slot;
        Config.Save();

        Service.Log.Information($"FrenMits auto-load: territory {territory}, slot {fight.Slot}, +{added} lines.");
    }

    // Local player via the object table (index 0); IClientState.LocalPlayer was
    // removed in this Dalamud build.
    public static Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter? LocalPlayer
        => Service.ObjectTable[0] as Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter;

    private void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework _)
    {
        Timer.Update();
        Sync.Update();
        Cues.Update();
        UpdateDtr();
    }

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
        var elapsed = Timer.Elapsed + fight.TimerOffset;
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

    private void DrawUi() => Windows.Draw();

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
        var territory = Service.ClientState.TerritoryType;
        foreach (var fight in Config.Fights)
            if (fight.Enabled && fight.TerritoryId == territory)
                return fight;
        return null;
    }

    public void Dispose()
    {
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
