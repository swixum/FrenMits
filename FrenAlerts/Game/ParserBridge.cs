using Dalamud.Plugin.Ipc;
using FrenAlerts.Engine;
using Newtonsoft.Json.Linq;

namespace FrenAlerts.Game;

public sealed class ParserBridge : IDisposable
{
    private const string Channel = "FrenAlertsCalls";

    private const string Subscribe = "IINACT.CreateSubscriber";
    private const string Unsubscribe = "IINACT.Unsubscribe";
    private const string Inbox = "IINACT.IpcProvider." + Channel;

    private readonly Action<GameEvent> _emit;
    private readonly Func<double> _now;

    private ICallGateProvider<JObject, bool>? _mine;
    private ICallGateSubscriber<string, bool>? _subscribe;
    private ICallGateSubscriber<string, bool>? _unsubscribe;

    public ParserBridge(Action<GameEvent> emit, Func<double> now)
    {
        _emit = emit;
        _now = now;
        TryConnect();
    }

    public bool Connected { get; private set; }

    public int Reported { get; private set; }

    // Head markers alone, which is the one kind nothing else can answer and the one
    // the screen speaks about.
    public int MarkersReported { get; private set; }

    public int Lines { get; private set; }

    // Still trying to open the channel we send on.
    public bool Asking { get; private set; } = true;

    // The channel is open and head markers are on their way. Its own flag rather than
    // "no longer asking", because asking also stops when it gives up, and those two
    // are opposites: one means every head marker call works, the other means none do.
    public bool Reading { get; private set; }

    private const double AskEverySeconds = 1.0;
    private const int MaxAsks = 30;

    // A parser that was unloaded, or crashed, stops sending and says nothing about
    // it. Past this the client's own reads take the kinds back, because a silent
    // handover is the whole fight going quiet.
    //
    // Generous on purpose: a duty is thousands of lines a second, and even standing
    // still in a city there is chat and health. Only a parser that is gone is this
    // quiet for this long.
    private const double StaleSeconds = 20.0;

    private double _lastAsk = double.NegativeInfinity;
    private int _asks;

    // Written on the parser's thread, read on the frame. A double is one aligned
    // write, so the frame never sees half of one.
    private double _lastLine = double.NegativeInfinity;

    private void TryConnect()
    {
        try
        {
            _mine = Service.PluginInterface.GetIpcProvider<JObject, bool>(Channel);
            _mine.RegisterFunc(OnMessage);

            _subscribe = Service.PluginInterface.GetIpcSubscriber<string, bool>(Subscribe);
            _unsubscribe = Service.PluginInterface.GetIpcSubscriber<string, bool>(Unsubscribe);

            // Throws when no parser is loaded, which is the ordinary case.
            Connected = _subscribe.InvokeFunc(Channel);

            // A channel of this name already exists, which means an earlier load of
            // this plugin left one behind. Drop it and take the name again, or every
            // load from here on is refused and head markers stay off until the parser
            // itself is restarted.
            if (!Connected)
            {
                _unsubscribe.InvokeFunc(Channel);
                Connected = _subscribe.InvokeFunc(Channel);
                if (Connected)
                    Service.Log.Information("Fren Alerts: took back a parser channel left by an earlier load.");
            }

            if (!Connected)
                Service.Log.Information("Fren Alerts: no parser answered; head marker calls are off.");
        }
        catch (Exception ex)
        {
            Connected = false;
            Service.Log.Debug($"Fren Alerts: no parser channel ({ex.GetType().Name}); head marker calls are off.");
        }
    }

    // The parser registers the gate we send on some time after it accepts the
    // subscriber, not during it. Asking once from the constructor was always going to
    // lose that race, so it is asked again on the frame until it takes.
    public void Tick(double now)
    {
        if (Reading && now - _lastLine > StaleSeconds)
        {
            Reading = false;
            Service.Log.Information(
                "Fren Alerts: the parser went quiet, reading from the client again.");
        }

        if (!Connected || !Asking) return;
        if (now - _lastAsk < AskEverySeconds) return;

        _lastAsk = now;
        if (++_asks > MaxAsks)
        {
            Asking = false;
            Service.Log.Warning("Fren Alerts: the parser never opened its channel; head marker calls are off.");
            return;
        }
        Ask();
    }

    private void Ask()
    {
        try
        {
            var gate = Service.PluginInterface.GetIpcSubscriber<JObject, bool>(Inbox);
            gate.InvokeFunc(new JObject
            {
                ["call"] = "subscribe",
                ["events"] = new JArray("LogLine"),
            });
            Asking = false;
            Reading = true;
            // Or the staleness check fires on the next frame, before a single line
            // has had the chance to arrive.
            _lastLine = _now();
            Service.Log.Information("Fren Alerts: reading the fight from the parser.");
        }
        catch
        {
            // Not up yet. Tick asks again; this is a race rather than a failure, and
            // saying so every second would be noise.
        }
    }

    // Every line of the pull comes down this one callback, which is what it cost
    // before as well: the subscription was always for all of them and all but the
    // head markers were thrown away. So the reading below is the part that changed,
    // not the traffic.
    private bool OnMessage(JObject message)
    {
        try
        {
            if ((string?)message["type"] != "LogLine") return true;
            if (message["line"] is not JArray line) return true;

            var now = _now();
            Lines++;
            _lastLine = now;

            // Back from a quiet stretch. Lines arriving is the proof the channel is
            // alive, so nothing is re-subscribed: asking twice risks a parser that
            // adds the subscription rather than replacing it, and then every line
            // arrives twice and every call is said twice.
            if (!Reading && Connected && !Asking) Reading = true;

            if (line.Count < 3) return true;

            // Cheap check before anything is built: four lines in five are movement,
            // health and chat, and this is the only work they cause.
            var kind = LogLine.KindOf((string?)line[0] ?? "");
            if (!LiveCoverage.ParserOwned.Contains(kind)) return true;

            var fields = new string[line.Count];
            for (var i = 0; i < line.Count; i++) fields[i] = (string?)line[i] ?? "";

            if (LogLine.Read(kind, fields, now) is not { } e) return true;

            Reported++;
            if (kind == EventKind.HeadMarker) MarkersReported++;
            _emit(e);
        }
        catch
        {
        }
        return true;
    }

    public void Dispose()
    {
        try
        {
            // Always, not only when connected. A subscription left behind is a name
            // the parser refuses to hand out again, which is how one bad load turned
            // into head markers being off for every load after it.
            _unsubscribe?.InvokeFunc(Channel);
        }
        catch
        {
            // The parser may already be gone, which is the usual case on logout.
        }
        _mine?.UnregisterFunc();
        _mine = null;
        Connected = false;
        Reading = false;
    }
}
