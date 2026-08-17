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

    // The calls that named their own place. Empty on every install that has not
    // written a trigger asking for one, and drawn nowhere until then.
    public PlacedCalls Placed { get; private set; } = null!;

    // Built after the runner, since it reads the board the runner owns.
    public CooldownOverlay? CooldownOverlay { get; private set; }

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
        Placed = new PlacedCalls(Config, _fonts, Board);
        _windows.AddWindow(Overlay);
        _windows.AddWindow(Placed);
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
            Seating = () => Config.SeatOverride,
            ScriptStrat = id => Config.ScriptStratFor(id),
            // The same two the board asks before it takes a call, so a sound cannot
            // come out of a fight that is muted or a plugin that is switched off.
            Audible = () => Config.AlertsEnabled
                            && !Config.IsMuted(Service.ClientState.TerritoryType),
        };
        // The group's seating, read on every party poll. Handed to the party read
        // rather than to the runner, because that is the one place seats are worked
        // out, and dropped again on the way out so a reload leaves nothing holding a
        // config that has gone.
        Game.PartySlots.Seats = () => Config.PartySeats;
        Game.PartySlots.Book = () => Config.PartyBook;
        // The triggers somebody wrote, handed over as the one list both sides hold:
        // the page edits these objects and the config saves the same ones, so there
        // is no copy to keep in step.
        _runner.Mine.Use(Config.TriggerSets, Config.BuiltInRevision, out var revision);
        Config.TriggerSets = _runner.Mine.Sets;
        if (Config.BuiltInRevision != revision)
        {
            Config.BuiltInRevision = revision;
            Config.Save();
        }

        // The cooldowns somebody set up, handed over as one list the same way, and
        // their own window to draw them in.
        _runner.Cooldowns.Use(Config.Cooldowns);
        Config.Cooldowns = _runner.Cooldowns.Entries;
        CooldownOverlay = new CooldownOverlay(Config, _runner.Cooldowns, () => _runner.Now);
        _windows.AddWindow(CooldownOverlay);

        // So the window can report the fight that is actually loaded rather than
        // the list of fights that exist.
        ConfigWindow.Runner = _runner;
        ConfigWindow.Cooldowns = CooldownOverlay;
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
            // Here rather than in the constructor, for the same reason the plan is:
            // a slow load freezes the game on update, and this opens a file.
            if (Config.KeepRecording && !_runner.Diary.On)
            {
                _runner.OpenDiary();
                Service.Log.Information("Fren Alerts: recording, as it was left on.");
            }
        }
        _fonts.Tick();
        _fonts.WarmIfNeeded(Config);
        _windows.Draw();
        // Speech follows the config on the frame, so ticking it takes effect on
        // the next call rather than the next zone or the next reload.
        _runner.MineEnabled = Config.UserTriggersEnabled;
        // Followed on the frame like the voice is, so changing how often the tracker
        // shows takes effect while the settings are open rather than on the next zone.
        _runner.Cooldowns.Board.Visibility =
            (Engine.UserTriggers.CooldownVisibility)Config.CooldownVisibility;
        _runner.Voice.Enabled = Config.VoiceEnabled;
        _runner.Voice.Volume = Config.VoiceVolume;
        _runner.Voice.Speed = Config.VoiceSpeed;
        _runner.Voice.UseLocal = Config.UseLocalVoice;
        _runner.Voice.LocalVoiceName = Config.LocalVoiceName;
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
        Game.PartySlots.Seats = null;
        Game.PartySlots.Book = null;
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

        if (args.Trim().Equals("record", StringComparison.OrdinalIgnoreCase))
        {
            if (_runner.Diary.On)
            {
                var path = _runner.WriteDiary();
                _runner.CloseDiary();
                // Turned off by hand means off next time too, or the one thing that
                // switches it off would be undone by the next reload.
                Config.KeepRecording = false;
                Config.Save();
                Service.ChatGui.Print(path is null
                    ? "Recording off. Nothing happened while it was on."
                    : $"Recording off, written to {path}");
            }
            else
            {
                _runner.OpenDiary();
                // Remembered, so a night of replays does not need this typed again
                // after every reload.
                Config.KeepRecording = true;
                // Asking for it once is what puts the control in the window. Until
                // then the row is not there at all, so an install that never wanted
                // a recorder never sees one.
                Config.Diagnostics = true;
                Config.Save();
                Service.ChatGui.Print(
                    "Recording on. Do the pull, then run this again to write the file. "
                    + "Each pull is written as it ends, so it is safe to leave on. "
                    + "There is now a switch for it on the plugin's home page.");
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
                (_runner.Scripted
                    ? $"Imported calls: {_runner.ScriptMatched} matched, {_runner.ScriptFired} said. "
                    : "") +
                (_runner.ScriptProblem.Length > 0 ? $"Script problem: {_runner.ScriptProblem}. " : "") +
                (_runner.Mine.Total > 0
                    ? $"Your triggers: {_runner.Mine.Live} of {_runner.Mine.Total} on, "
                      + $"{_runner.Mine.Fired} said. "
                    : "") +
                // Said out loud, so a recorder left running is never a surprise.
                (_runner.Diary.On ? $" Recording, {_runner.Diary.Lines} lines." : "") +
                (_runner.ControlAvailable ? "" : " Direction calls unavailable.") +
                (_runner.AbilitiesAvailable ? "" : " Calls that fire on a hit unavailable.") +
                (_runner.ParserConnected ? "" : " No parser: head marker calls are off."));
            return;
        }
        ConfigWindow.IsOpen = !ConfigWindow.IsOpen;
        if (ConfigWindow.IsOpen) ConfigWindow.BringToFront();
    }
}
