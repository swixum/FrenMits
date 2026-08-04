using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Ipc;
using Newtonsoft.Json.Linq;

namespace FrenMits.Meter;

// The pipe to the parser, in-process gate first, socket second.
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

    // The socket address actually in use, which may not be the configured scheme.
    public string ActiveAddress { get; private set; } = "";

    private DateTime _nextAttempt = DateTime.MinValue;
    private DateTime _nextHealthCheck = DateTime.MaxValue;

    private CancellationTokenSource? _cts;
    private Task? _wsTask;
    private volatile bool _wsUp;

    // Bumped on every stop or start; a stale attempt's writes are refused.
    private volatile int _attempt;

    // When the last message arrived, from either link, for the quiet-socket watch.
    private long _lastMsgTicks = DateTime.UtcNow.Ticks;

    // Log lines flow almost constantly, so a socket this quiet is dead.
    private static readonly TimeSpan QuietLimit = TimeSpan.FromSeconds(60);

    public MeterLink(Plugin plugin)
    {
        _plugin = plugin;
        _gate = Service.PluginInterface.GetIpcProvider<JObject, bool>(Receiver);
        _gate.RegisterFunc(msg =>
        {
            Volatile.Write(ref _lastMsgTicks, DateTime.UtcNow.Ticks);
            if (_queue.Count < MaxQueued) _queue.Enqueue(msg);
            return true;
        });
    }

    public bool TryDequeue(out JObject msg) => _queue.TryDequeue(out msg!);

    // Connects when down and watches for a silent death.
    public void EnsureStarted()
    {
        var now = DateTime.UtcNow;
        if (Status == LinkStatus.Socket && !_wsUp) Status = LinkStatus.Searching;

        // A half-open socket never errors, so quiet is the only tell.
        if (Status == LinkStatus.Socket && _wsUp
            && now.Ticks - Volatile.Read(ref _lastMsgTicks) > QuietLimit.Ticks)
        {
            Service.Log.Warning($"[FrenMits] meter: parser link quiet for {QuietLimit.TotalSeconds:0}s; reconnecting.");
            RetryNow();
            return;
        }

        if (Status is LinkStatus.Off or LinkStatus.Searching)
        {
            if (now < _nextAttempt) return;
            _nextAttempt = now + TimeSpan.FromSeconds(10);
            Connect();
            return;
        }

        // The gate has no disconnect signal, so ask if it's alive.
        if (Status == LinkStatus.Ipc && now >= _nextHealthCheck)
        {
            _nextHealthCheck = now + TimeSpan.FromSeconds(30);
            if (!IpcAlive())
            {
                // Drop the registration too, or a socket fallback would feed twice.
                TryUnsubscribe();
                Status = LinkStatus.Searching;
                _nextAttempt = now + TimeSpan.FromSeconds(5);
            }
        }
    }

    // Drop the link and search again next tick.
    public void RetryNow()
    {
        EnsureStopped();
        _nextAttempt = DateTime.MinValue;
        Status = LinkStatus.Searching;
    }

    public void EnsureStopped()
    {
        if (Status == LinkStatus.Off) return;
        if (Status == LinkStatus.Ipc) TryUnsubscribe();
        StopSocket();
        _queue.Clear();
        Status = LinkStatus.Off;
    }

    private static void TryUnsubscribe()
    {
        try
        {
            Service.PluginInterface.GetIpcSubscriber<string, bool>(IpcUnsubscribe).InvokeFunc(Receiver);
        }
        catch { /* the parser may already be gone */ }
    }

    private void Connect()
    {
        Status = LinkStatus.Searching;
        // A fresh attempt starts its quiet clock from here.
        Volatile.Write(ref _lastMsgTicks, DateTime.UtcNow.Ticks);
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
            // A registration left over from a dropped link must not deliver twice.
            TryUnsubscribe();
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
        var address = _plugin.Config.MeterSocketAddress;
        // Whatever this generation writes is valid only while it is the newest.
        var gen = _attempt;
        var token = _cts.Token;
        _wsTask = Task.Run(() => RunSocket(address, gen, token));
    }

    // A parser with SSL on serves wss, so try the other scheme before giving up.
    private async Task RunSocket(string address, int gen, CancellationToken token)
    {
        // Whatever came up last time goes first, as long as it's the same endpoint.
        var first = _worked.Length > 0 && (_worked == address || _worked == OtherScheme(address))
            ? _worked : address;
        if (await Pump(first, gen, token) || token.IsCancellationRequested) return;

        var firstError = LastError;
        if (OtherScheme(first) is { } alt && await Pump(alt, gen, token)) return;
        // Neither worked, so report the address that was actually configured.
        if (firstError.Length > 0 && gen == _attempt) LastError = firstError;
    }

    // The same endpoint under the other scheme, null if it isn't a WebSocket address.
    public static string? OtherScheme(string address)
    {
        if (address.StartsWith("wss://", StringComparison.OrdinalIgnoreCase)) return "ws://" + address[6..];
        if (address.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)) return "wss://" + address[5..];
        return null;
    }

    // The address that last came up, so a reconnect doesn't relearn the scheme.
    private string _worked = "";

    // One address, connect to close; true once the link has been up.
    private async Task<bool> Pump(string address, int gen, CancellationToken token)
    {
        if (token.IsCancellationRequested || gen != _attempt) return false;
        var up = false;
        // Local to this attempt: cancellation is the only outside hand on it.
        using var ws = new ClientWebSocket();
        try
        {
            var uri = new Uri(address);
            // A local parser signs its own certificate, and this never leaves the machine.
            if (uri.Scheme == "wss" && uri.IsLoopback)
                ws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            // Pings, so a dead TCP path shows up as an error instead of silence.
            ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

            await ws.ConnectAsync(uri, token);
            // A stop raced the connect, so land dead rather than flip the status back up.
            token.ThrowIfCancellationRequested();
            await ws.SendAsync(Encoding.UTF8.GetBytes(Subscribe), WebSocketMessageType.Text, true, token);
            if (gen != _attempt) return false;
            up = true;
            _wsUp = true;
            _worked = address;
            ActiveAddress = address;
            LastError = "";
            Status = LinkStatus.Socket;
            Volatile.Write(ref _lastMsgTicks, DateTime.UtcNow.Ticks);
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
                    // A stranded attempt must not feed the new session's queue.
                    if (gen != _attempt) break;
                    Volatile.Write(ref _lastMsgTicks, DateTime.UtcNow.Ticks);
                    if (_queue.Count < MaxQueued) _queue.Enqueue(msg);
                }
                catch { /* one malformed frame must not kill the link */ }
            }
        }
        catch (Exception ex)
        {
            // A link we closed ourselves is not a failure worth showing.
            if (!token.IsCancellationRequested && gen == _attempt) LastError = $"{address} - {ex.Message}";
        }
        finally
        {
            // Only the current generation may report the link down.
            if (gen == _attempt) _wsUp = false; // EnsureStarted notices and schedules a reconnect
        }
        return up;
    }

    private void StopSocket()
    {
        // Strands whatever attempt is still in flight; its writes are refused from here.
        _attempt++;
        _wsUp = false;
        ActiveAddress = "";
        try { _cts?.Cancel(); } catch { }
        // No wait here: cancellation ends the task, and a wait stalls the game's thread.
        _cts = null;
        _wsTask = null;
    }

    public void Dispose()
    {
        EnsureStopped();
        _gate.UnregisterFunc();
    }
}
