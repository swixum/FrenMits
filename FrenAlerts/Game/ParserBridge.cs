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

    public int Lines { get; private set; }

    private void TryConnect()
    {
        try
        {
            _mine = Service.PluginInterface.GetIpcProvider<JObject, bool>(Channel);
            _mine.RegisterFunc(OnMessage);

            _subscribe = Service.PluginInterface.GetIpcSubscriber<string, bool>(Subscribe);
            _unsubscribe = Service.PluginInterface.GetIpcSubscriber<string, bool>(Unsubscribe);

            // Throws when no parser is loaded, which is the ordinary case and is
            // caught below rather than logged as a failure.
            Connected = _subscribe.InvokeFunc(Channel);

            if (Connected) Ask();
            else Service.Log.Information("Fren Alerts: no parser answered; head marker calls are off.");
        }
        catch (Exception ex)
        {
            Connected = false;
            Service.Log.Debug($"Fren Alerts: no parser channel ({ex.GetType().Name}); head marker calls are off.");
        }
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
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "Fren Alerts: the parser channel opened but would not take a subscription.");
            Connected = false;
        }
    }

    private bool OnMessage(JObject message)
    {
        try
        {
            if ((string?)message["type"] != "LogLine") return true;
            if (message["line"] is not JArray line) return true;

            Lines++;

            // Cheap check before building anything: nearly every line is not a marker.
            if (line.Count < 7 || (string?)line[0] != LogLine.HeadMarkerKind) return true;

            var fields = new string[line.Count];
            for (var i = 0; i < line.Count; i++) fields[i] = (string?)line[i] ?? "";

            if (LogLine.Read(fields, _now()) is not { } e) return true;

            Reported++;
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
            if (Connected) _unsubscribe?.InvokeFunc(Channel);
        }
        catch
        {
            // The parser may already be gone, which is the usual case on logout.
        }
        _mine?.UnregisterFunc();
        _mine = null;
        Connected = false;
    }
}
