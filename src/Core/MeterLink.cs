using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Ipc;
using Newtonsoft.Json.Linq;

namespace FrenMits;

// The pipe to the parser: the in-process message gate first, then the overlay
// WebSocket, everything queued for the framework thread to drain.
public class MeterLink : IDisposable
{
    public enum LinkStatus { Off, Searching, Ipc, Socket }

    private const string Receiver = "FrenMits.MeterEvents";
    private const string Subscribe = "{\"call\":\"subscribe\",\"events\":[\"CombatData\",\"LogLine\"]}";
    private const string IpcListening = "IINACT.Server.Listening";
    private const string IpcSubscribe = "IINACT.CreateSubscriber";
    private const string IpcUnsubscribe = "IINACT.Unsubscribe";
    private const string IpcSend = "IINACT.IpcProvider." + Receiver;

    private readonly Plugin _plugin;
    private readonly ICallGateProvider<JObject, bool> _gate;
    private readonly ConcurrentQueue<JObject> _queue = new();
    private const int MaxQueued = 20000;

    public LinkStatus Status { get; private set; } = LinkStatus.Off;
    public string LastError { get; private set; } = "";

    private DateTime _nextAttempt = DateTime.MinValue;
    private DateTime _nextHealthCheck = DateTime.MaxValue;

    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private Task? _wsTask;
    private volatile bool _wsUp;

    public MeterLink(Plugin plugin)
    {
        _plugin = plugin;
        _gate = Service.PluginInterface.GetIpcProvider<JObject, bool>(Receiver);
        _gate.RegisterFunc(msg =>
        {
            if (_queue.Count < MaxQueued) _queue.Enqueue(msg);
            return true;
        });
    }

    public bool TryDequeue(out JObject msg) => _queue.TryDequeue(out msg!);

    // Called every framework tick while the meter is enabled: connects when
    // down (rate-limited) and keeps an eye on a link that can die silently.
    public void EnsureStarted()
    {
        var now = DateTime.UtcNow;
        if (Status == LinkStatus.Socket && !_wsUp) Status = LinkStatus.Searching;

        if (Status is LinkStatus.Off or LinkStatus.Searching)
        {
            if (now < _nextAttempt) return;
            _nextAttempt = now + TimeSpan.FromSeconds(10);
            Connect();
            return;
        }

        // The message gate has no disconnect signal, so re-ask the parser
        // occasionally whether it is still there at all.
        if (Status == LinkStatus.Ipc && now >= _nextHealthCheck)
        {
            _nextHealthCheck = now + TimeSpan.FromSeconds(30);
            if (!IpcAlive())
            {
                Status = LinkStatus.Searching;
                _nextAttempt = now + TimeSpan.FromSeconds(5);
            }
        }
    }

    // Drop the link and search again on the next tick (settings changed).
    public void RetryNow()
    {
        EnsureStopped();
        _nextAttempt = DateTime.MinValue;
        Status = LinkStatus.Searching;
    }

    public void EnsureStopped()
    {
        if (Status == LinkStatus.Off) return;
        if (Status == LinkStatus.Ipc)
        {
            try
            {
                Service.PluginInterface.GetIpcSubscriber<string, bool>(IpcUnsubscribe).InvokeFunc(Receiver);
            }
            catch { /* the parser may already be gone */ }
        }
        StopSocket();
        _queue.Clear();
        Status = LinkStatus.Off;
    }

    private void Connect()
    {
        Status = LinkStatus.Searching;
        var mode = _plugin.Config.MeterConnection; // 0 auto, 1 in-process, 2 WebSocket
        if (mode is 0 or 1 && TryIpc()) return;
        if (mode is 0 or 2) StartSocket();
    }

    private bool IpcAlive()
    {
        try
        {
            return Service.PluginInterface.GetIpcSubscriber<bool>(IpcListening).InvokeFunc();
        }
        catch { return false; }
    }

    private bool TryIpc()
    {
        if (!IpcAlive()) return false;
        try
        {
            if (!Service.PluginInterface.GetIpcSubscriber<string, bool>(IpcSubscribe).InvokeFunc(Receiver))
                return false;
            Service.PluginInterface.GetIpcSubscriber<JObject, bool>(IpcSend)
                .InvokeAction(JObject.Parse(Subscribe));
            Status = LinkStatus.Ipc;
            _nextHealthCheck = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            Service.Log.Information("[FrenMits] meter: parser link up (in-process).");
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    private void StartSocket()
    {
        StopSocket();
        _cts = new CancellationTokenSource();
        _ws = new ClientWebSocket();
        var address = _plugin.Config.MeterSocketAddress;
        _wsTask = Task.Run(() => RunSocket(address, _ws, _cts.Token));
    }

    private async Task RunSocket(string address, ClientWebSocket ws, CancellationToken token)
    {
        try
        {
            await ws.ConnectAsync(new Uri(address), token);
            await ws.SendAsync(Encoding.UTF8.GetBytes(Subscribe), WebSocketMessageType.Text, true, token);
            _wsUp = true;
            Status = LinkStatus.Socket;
            Service.Log.Information($"[FrenMits] meter: parser link up ({address}).");

            var buffer = new ArraySegment<byte>(new byte[8192]);
            using var ms = new MemoryStream();
            while (!token.IsCancellationRequested)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(buffer, token);
                    ms.Write(buffer.Array!, buffer.Offset, result.Count);
                } while (!result.EndOfMessage);
                if (result.MessageType == WebSocketMessageType.Close) break;

                try
                {
                    var msg = JObject.Parse(Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length));
                    if (_queue.Count < MaxQueued) _queue.Enqueue(msg);
                }
                catch { /* one malformed frame must not kill the link */ }
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            _wsUp = false; // EnsureStarted notices and schedules a reconnect
        }
    }

    private void StopSocket()
    {
        _wsUp = false;
        try { _cts?.Cancel(); } catch { }
        try { _ws?.Dispose(); } catch { }
        try { _wsTask?.Wait(500); } catch { }
        _cts = null;
        _ws = null;
        _wsTask = null;
    }

    public void Dispose()
    {
        EnsureStopped();
        _gate.UnregisterFunc();
    }
}
