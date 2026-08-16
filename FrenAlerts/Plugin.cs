using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FrenAlerts.Engine;
using FrenAlerts.Engine.Alerts;
using FrenAlerts.Ui;

namespace FrenAlerts;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/frenalerts";
    private const string CommandAlias = "/fa";

    private readonly WindowSystem _windows = new("FrenAlerts");
    private readonly FontManager _fonts = new();

    public Configuration Config { get; }

    // What is on screen right now; the fights push into it, the overlay reads it.
    public AlertBoard Board { get; } = new();

    public ConfigWindow ConfigWindow { get; }
    public AlertOverlay Overlay { get; }

    private readonly Game.Runner _runner;

    // Nothing heavy here: a slow constructor freezes the game on update.
    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();
        Config = Configuration.Load();

        Board.Decide = call =>
        {
            if (!Config.AlertsEnabled) return null;
            if (Config.IsMuted(Service.ClientState.TerritoryType)) return null;
            // The common case is a call nobody has touched, and that one costs a
            // dictionary lookup and nothing else.
            if (Config.EditFor(call.Key) is not { } edit) return call;
            return CallEdits.Apply(call, edit, Ui.FightCatalog.ShippedText(call.Key));
        };

        Overlay = new AlertOverlay(Config, _fonts, Board);
        ConfigWindow = new ConfigWindow(Config, _fonts, Board, Overlay);
        Overlay.OpenSettings = () => { ConfigWindow.IsOpen = true; ConfigWindow.BringToFront(); };
        _windows.AddWindow(Overlay);
        _windows.AddWindow(ConfigWindow);

        Service.PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        Service.PluginInterface.UiBuilder.OpenMainUi += OpenConfig;

        // The short one is the one on show. Both work and always have, but the
        // installer prints the command beside its help text, and the long name with
        // its two subcommands spelled out filled the row with things nobody types.
        Service.CommandManager.AddHandler(CommandAlias, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Fren Alerts",
        });
        Service.CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            ShowInHelp = false,
        });
        _runner = new Game.Runner(Board)
        {
            Switched = id => Config.CallSwitch(Ui.FightCatalog.CallOf(id)),
            Strat = (territory, key) => Config.StratFor(territory, key),
        };
        // So the window can report the fight that is actually loaded rather than
        // the list of fights that exist.
        ConfigWindow.Runner = _runner;
        // The screen counts in the fight's seconds rather than the wall's: paused in
        // a replay a call holds instead of ageing out, and at four times speed the
        // countdown reaches zero when the mechanic does.
        Board.Clock = () => _runner.Now;

        // Last, and only once everything it touches exists. Frames are drawn while
        // this constructor is still running, so subscribing any earlier hands the
        // game a draw against half-built fields: on the first install that landed
        // between here and the runner being assigned, and every frame threw.
        Service.PluginInterface.UiBuilder.Draw += OnDraw;

        Service.Log.Information($"Fren Alerts {PluginVersion} loaded, engine {EngineInfo.Version}.");
    }

    private static string PluginVersion =>
        typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "unknown";

    private bool _planRead;

    private void OnDraw()
    {
        if (!_planRead)
        {
            _planRead = true;
            Service.Log.Information(_runner.LoadPlan());
        }
        _fonts.Tick();
        _fonts.WarmIfNeeded(Config);
        _windows.Draw();
        // Speech follows the config on the frame, so ticking it takes effect on
        // the next call rather than the next zone or the next reload.
        _runner.Voice.Enabled = Config.VoiceEnabled;
        _runner.Voice.Volume = Config.VoiceVolume;
        // The write a drag has been holding, landed the moment it stops.
        Config.Flush();
    }

    private void OpenConfig()
    {
        ConfigWindow.IsOpen = true;
        ConfigWindow.BringToFront();
    }

    public void Dispose()
    {
        // First off, so no event arrives while the rest is being torn down.
        _runner.Dispose();
        Service.CommandManager.RemoveHandler(CommandAlias);
        Service.CommandManager.RemoveHandler(Command);
        Service.PluginInterface.UiBuilder.OpenMainUi -= OpenConfig;
        Service.PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        Service.PluginInterface.UiBuilder.Draw -= OnDraw;
        _windows.RemoveAllWindows();
        ConfigWindow.Dispose();
        // Anything a drag was still holding, before the handles go.
        Config.Flush(force: true);
        _fonts.Dispose();
        Board.Clear();
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("plan", StringComparison.OrdinalIgnoreCase))
        {
            Service.ChatGui.Print(_runner.LoadPlan());
            return;
        }

        if (args.Trim().Equals("voice", StringComparison.OrdinalIgnoreCase))
        {
            Service.ChatGui.Print(
                (_runner.Voice.Unavailable
                    ? "Speech is not available on this machine. "
                    : $"System voice: {(Config.VoiceEnabled ? "on" : "off")}, " +
                      $"{_runner.Voice.Spoken} said, {_runner.Voice.Dropped} dropped. ") +
                _runner.LocalVoice.Describe());
            return;
        }

        if (args.Trim().Equals("probe", StringComparison.OrdinalIgnoreCase))
        {
            _runner.Markers.Enabled = !_runner.Markers.Enabled;
            if (_runner.Markers.Enabled)
            {
                Service.ChatGui.Print(
                    "Marker probe on. Do one pull, then run this again to write the file.");
            }
            else
            {
                var path = _runner.Markers.Write();
                Service.ChatGui.Print(path is null
                    ? "Marker probe off. Nothing was recorded."
                    : $"Marker probe off, {_runner.Markers.Rows} rows written to {path}");
                _runner.Markers.Forget();
            }
            return;
        }

        if (args.Trim().Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            Service.ChatGui.Print(
                $"Fren Alerts {PluginVersion}, engine {EngineInfo.Version}. " +
                $"Fight: {_runner.Fight}, {_runner.SpeakingCount} of {_runner.TriggerCount} " +
                $"speaking, {_runner.PlanCalls} from your plan, {_runner.TethersSeen} tethers seen. " +
                (_runner.InReplay ? "Replay. " : "") +
                $"Pull {_runner.Pulls}{(_runner.InPull ? ", running" : "")}, " +
                (_runner.PhasesKnown ? $"phase {_runner.Phase}, " : "") +
                $"{_runner.AbilitiesSeen} hits, {_runner.MarkersSeen} head markers read. " +
                // Counted, so "the prop calls are quiet" has an answer that is not a
                // guess: zero here means nothing is reading the arena in this zone.
                $"Arena: {_runner.ArenaSeen} read, {_runner.ArenaTracking} tracked" +
                (_runner.ArenaDropped > 0 ? $", {_runner.ArenaDropped} dropped" : "") + ". " +
                (_runner.HasTimeline
                    ? $"Timeline: {_runner.TimelineMechanics} mechanics, "
                      + (_runner.TimelineRunning
                          ? $"at {_runner.TimelineAt:0}s, {_runner.TimelineResyncs} resyncs, "
                            + $"drift {_runner.TimelineDrift:+0.0;-0.0;0}s."
                          : "not anchored yet.")
                    : "No timeline for this fight.") +
                // Only where the fight has any, so it is silent in the six that do not.
                (_runner.YellsExpected == 0
                    ? ""
                    : $" {_runner.YellsKnown} of {_runner.YellsExpected} boss lines known.") +
                (_runner.ControlAvailable ? "" : " Direction calls unavailable.") +
                (_runner.AbilitiesAvailable ? "" : " Calls that fire on a hit unavailable.") +
                (_runner.ParserConnected ? "" : " No parser: head marker calls are off."));
            return;
        }
        ConfigWindow.IsOpen = !ConfigWindow.IsOpen;
        if (ConfigWindow.IsOpen) ConfigWindow.BringToFront();
    }
}
