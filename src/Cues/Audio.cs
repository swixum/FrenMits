using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace FrenMits.Cues;

// Text-to-speech through Windows SAPI or Edge voices.
// One worker thread owns the speech engine; the game's thread only ever enqueues.
public class Audio : IDisposable
{
    // Curated Edge voices, tagged by gender for the UI.
    public static readonly (string Id, string Name, bool Female)[] EdgeVoices =
    {
        ("en-US-AriaNeural",        "Aria (US)",        true),
        ("en-US-JennyNeural",       "Jenny (US)",       true),
        ("en-US-MichelleNeural",    "Michelle (US)",    true),
        ("en-GB-SoniaNeural",       "Sonia (UK)",       true),
        ("en-GB-LibbyNeural",       "Libby (UK)",       true),
        ("en-AU-NatashaNeural",     "Natasha (AU)",     true),
        ("en-CA-ClaraNeural",       "Clara (CA)",       true),
        ("en-IE-EmilyNeural",       "Emily (IE)",       true),
        ("en-IN-NeerjaNeural",      "Neerja (IN)",      true),
        ("en-US-GuyNeural",         "Guy (US)",         false),
        ("en-US-ChristopherNeural", "Christopher (US)", false),
        ("en-US-EricNeural",        "Eric (US)",        false),
        ("en-US-RogerNeural",       "Roger (US)",       false),
        ("en-US-SteffanNeural",     "Steffan (US)",     false),
        ("en-GB-RyanNeural",        "Ryan (UK)",        false),
        ("en-AU-WilliamNeural",     "William (AU)",     false),
    };

    private sealed record Job(long Seq, string Text, int Rate, int Volume, bool Edge, string Voice, bool ListVoices);

    // Small and lossy on purpose: a full queue drops the cue rather than block a frame.
    private readonly BlockingCollection<Job> _jobs = new(64);
    private readonly Thread _worker;

    private object? _voice;       // SAPI.SpVoice COM object, touched only by the worker
    private bool _ttsUnavailable;
    private string _currentVoice = "";
    private volatile List<string>? _voiceNames;
    private int _voicesRequested;

    // Cue ordering: only a newer cue plays.
    private long _speakSeq;
    private long _playedSeq;
    private volatile bool _disposed;

    // Unload cancels any fetch still on the wire.
    private readonly CancellationTokenSource _shutdown = new();

    public Audio()
    {
        _worker = new Thread(WorkLoop) { IsBackground = true, Name = "FrenMits.Audio" };
        _worker.Start();
    }

    // True if seq is the newest cue played.
    private static bool TryAdvance(ref long played, long seq)
    {
        while (true)
        {
            var cur = Interlocked.Read(ref played);
            if (seq < cur) return false;
            if (Interlocked.CompareExchange(ref played, seq, cur) == cur) return true;
        }
    }

    // Last TTS result, shown in the Audio tab.
    public string LastTtsStatus { get; private set; } = "";

    // Small WAV cache so a repeated call is instant.
    private readonly Dictionary<string, byte[]> _edgeCache = new();
    private readonly LinkedList<string> _edgeOrder = new();
    private const int EdgeCacheMax = 128;

    // Speaks through Edge when useEdge, else SAPI. Never blocks the caller.
    public void Speak(string text, int rate, int volume, bool useEdge, string voice)
    {
        if (_disposed || string.IsNullOrWhiteSpace(text)) return;
        var seq = Interlocked.Increment(ref _speakSeq);
        try { _jobs.TryAdd(new Job(seq, text, rate, volume, useEdge, voice, false)); }
        catch (InvalidOperationException) { /* closed mid-add on unload */ }
    }

    // Names of every installed SAPI voice; filled by the worker, empty until then.
    public IReadOnlyList<string> VoiceNames()
    {
        if (_voiceNames is { } names) return names;
        if (!_disposed && Interlocked.CompareExchange(ref _voicesRequested, 1, 0) == 0)
            try { _jobs.TryAdd(new Job(0, "", 0, 0, false, "", ListVoices: true)); }
            catch (InvalidOperationException) { /* closed mid-add on unload */ }
        return Array.Empty<string>();
    }

    // ---- the worker ----

    // The outermost frame on this thread. A reload can unload the load context
    // while the worker is still winding down, and what that throws surfaces at
    // the frame being entered - too early for a handler inside it to help. Only
    // a catch out here, one frame up, is guaranteed to see it, and anything that
    // escapes this thread reaches the runtime's unhandled hook and ends the game.
    private void WorkLoop()
    {
        try { Pump(); }
        catch { /* nothing may escape this thread */ }
    }

    private void Pump()
    {
        try
        {
            foreach (var job in _jobs.GetConsumingEnumerable())
            {
                if (_disposed) break;
                try { Run(job); }
                catch { /* one bad cue must not end the voice */ }
            }
        }
        catch { /* collection torn down on unload */ }
        finally { Cleanup(); }
    }

    private void Run(Job job)
    {
        if (job.ListVoices) { FillVoiceNames(); return; }
        // A newer cue is already queued behind this one, so skip ahead.
        if (job.Seq < Interlocked.Read(ref _speakSeq)) return;

        if (job.Edge)
        {
            try
            {
                var mp3 = GetEdgeWav(job.Text, job.Voice, job.Rate, job.Volume);
                if (mp3 is { Length: > 64 })
                {
                    if (job.Seq == Interlocked.Read(ref _speakSeq))
                        LastTtsStatus = $"Online OK - {job.Voice}";
                    PlayMp3(mp3, job.Seq);
                    return;
                }
                if (job.Seq == Interlocked.Read(ref _speakSeq))
                    LastTtsStatus = $"Online: no audio [{_edgeDiag}] - using Windows voice";
            }
            catch (Exception ex)
            {
                if (job.Seq == Interlocked.Read(ref _speakSeq))
                    LastTtsStatus = $"Online failed: {ex.Message} - using Windows voice";
                Service.Log.Warning(ex, "FrenMits: Edge TTS failed; using Windows voice");
            }
            SpeakSapi(job.Text, job.Rate, job.Volume, "", job.Seq);   // fallback
            return;
        }

        LastTtsStatus = "Windows voice";
        SpeakSapi(job.Text, job.Rate, job.Volume, job.Voice, job.Seq);
    }

    // Worker-only from here down: COM and the player live on this one thread.
    private void Cleanup()
    {
        try
        {
            if (_voice is not null && Marshal.IsComObject(_voice))
                Marshal.ReleaseComObject(_voice);
        }
        catch { /* ignore */ }
        _voice = null;
        // Disposed through IDisposable, never the real types - see the fields.
        try { _output?.Dispose(); } catch { /* ignore */ }
        try { _reader?.Dispose(); } catch { /* ignore */ }
        try { _readerMs?.Dispose(); } catch { /* ignore */ }
        _output = null; _reader = null; _readerMs = null;
    }

    // ---- Windows SAPI ----

    private void SpeakSapi(string text, int rate, int volume, string voiceName, long seq)
    {
        if (_ttsUnavailable || _disposed) return;
        Service.Log.Information($"[FrenMits] SAPI.Speak '{text}'");
        try
        {
            // A newer cue was asked for, so drop this one.
            if (seq < Interlocked.Read(ref _speakSeq)) return;
            if (!TryAdvance(ref _playedSeq, seq)) return; // newer cue already played

            _voice ??= CreateVoice();
            if (_voice is null) return;
            dynamic v = _voice;
            ApplyVoice(v, voiceName);
            v.Rate = Math.Clamp(rate, -10, 10);
            v.Volume = Math.Clamp(volume, 0, 100);
            // Async plus purge: interrupt, then speak.
            v.Speak(text, 3u);
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: TTS speak failed");
        }
    }

    private void FillVoiceNames()
    {
        var names = new List<string>();
        try
        {
            _voice ??= CreateVoice();
            if (_voice is not null)
            {
                dynamic v = _voice;
                dynamic tokens = v.GetVoices();
                int count = tokens.Count;
                for (var i = 0; i < count; i++)
                {
                    try { names.Add((string)tokens.Item(i).GetDescription()); }
                    catch { /* skip malformed token */ }
                }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: enumerating TTS voices failed");
        }
        _voiceNames = names;
    }

    private void ApplyVoice(dynamic v, string voiceName)
    {
        if (string.IsNullOrWhiteSpace(voiceName) || voiceName == _currentVoice) return;
        try
        {
            dynamic tokens = v.GetVoices();
            int count = tokens.Count;
            for (var i = 0; i < count; i++)
            {
                dynamic token = tokens.Item(i);
                if (string.Equals((string)token.GetDescription(), voiceName, StringComparison.OrdinalIgnoreCase))
                {
                    v.Voice = token;
                    _currentVoice = voiceName;
                    return;
                }
            }
        }
        catch { /* keep current voice */ }
    }

    private object? CreateVoice()
    {
        try
        {
            var type = Type.GetTypeFromProgID("SAPI.SpVoice");
            if (type is null) { _ttsUnavailable = true; return null; }
            return Activator.CreateInstance(type);
        }
        catch
        {
            _ttsUnavailable = true;
            return null;
        }
    }

    // ---- Edge online voices ----

    private byte[]? GetEdgeWav(string text, string voice, int rate, int volume)
    {
        var key = $"{voice}|{rate}|{volume}|{text}";
        if (_edgeCache.TryGetValue(key, out var hit)) return hit;

        var wav = FetchEdge(text, voice, rate, volume);
        if (wav != null && !_edgeCache.ContainsKey(key))
        {
            _edgeCache[key] = wav;
            _edgeOrder.AddLast(key);
            if (_edgeOrder.Count > EdgeCacheMax)
            {
                var oldest = _edgeOrder.First!.Value;
                _edgeOrder.RemoveFirst();
                _edgeCache.Remove(oldest);
            }
        }
        return wav;
    }

    // Bump this and the User-Agent when Edge starts 403ing.
    private const string EdgeToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const string EdgeVersion = "1-143.0.3650.75";

    // Last fetch diagnostic, shown when no audio comes back.
    private string _edgeDiag = "";

    private byte[]? FetchEdge(string text, string voice, int rate, int volume)
    {
        if (string.IsNullOrWhiteSpace(voice)) voice = "en-US-AriaNeural";
        var paths = new StringBuilder();
        var lastText = "";

        using var ws = new ClientWebSocket();
        try
        {
            ws.Options.SetRequestHeader("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0");
            ws.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
        }
        catch { /* some runtimes restrict these headers; the endpoint still accepts the request */ }

        var url =
            "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1" +
            $"?TrustedClientToken={EdgeToken}&Sec-MS-GEC={EdgeSecToken()}&Sec-MS-GEC-Version={EdgeVersion}" +
            $"&ConnectionId={Guid.NewGuid():N}";

        // Ten seconds, or sooner if the plugin is unloading.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        ws.ConnectAsync(new Uri(url), cts.Token).GetAwaiter().GetResult();

        var ts = DateTime.UtcNow.ToString(
            "ddd MMM dd yyyy HH:mm:ss 'GMT+0000 (Coordinated Universal Time)'", CultureInfo.InvariantCulture);

        var configMsg =
            "X-Timestamp:" + ts + "\r\nContent-Type:application/json; charset=utf-8\r\nPath:speech.config\r\n\r\n" +
            "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"false\"," +
            "\"wordBoundaryEnabled\":\"false\"},\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}";
        SendText(ws, configMsg, cts.Token);

        var ssml =
            "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>" +
            $"<voice name='{voice}'><prosody pitch='+0Hz' rate='{EdgeRate(rate)}' volume='{EdgeVolume(volume)}'>" +
            $"{XmlEscape(text)}</prosody></voice></speak>";
        var ssmlMsg =
            "X-RequestId:" + Guid.NewGuid().ToString("N") +
            "\r\nContent-Type:application/ssml+xml\r\nX-Timestamp:" + ts + "Z\r\nPath:ssml\r\n\r\n" + ssml;
        SendText(ws, ssmlMsg, cts.Token);

        using var audio = new MemoryStream();
        var buf = new byte[16384];
        var done = false;
        while (!done)
        {
            using var msgStream = new MemoryStream();
            WebSocketReceiveResult r;
            do
            {
                r = ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token).GetAwaiter().GetResult();
                if (r.MessageType == WebSocketMessageType.Close)
                {
                    paths.Append($"close({r.CloseStatus}:{r.CloseStatusDescription}) ");
                    done = true;
                    break;
                }
                msgStream.Write(buf, 0, r.Count);
            }
            while (!r.EndOfMessage);
            if (done) break;

            var msg = msgStream.ToArray();
            if (r.MessageType == WebSocketMessageType.Text)
            {
                var s = Encoding.UTF8.GetString(msg);
                var pi = s.IndexOf("Path:", StringComparison.Ordinal);
                if (pi >= 0) paths.Append(s.Substring(pi + 5, Math.Min(20, s.Length - pi - 5)).Split('\r')[0]).Append(' ');
                var bi = s.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (bi >= 0 && bi + 4 < s.Length) lastText = s[(bi + 4)..];
                if (s.Contains("Path:turn.end")) done = true;
            }
            else if (msg.Length > 2)
            {
                // Binary frame: 2-byte header length, header, audio.
                int headerLen = (msg[0] << 8) | msg[1];
                int start = 2 + headerLen;
                if (start < msg.Length) audio.Write(msg, start, msg.Length - start);
            }
        }

        try { ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", cts.Token).GetAwaiter().GetResult(); }
        catch { /* ignore */ }

        var wav = audio.ToArray();
        _edgeDiag = $"paths=[{paths.ToString().Trim()}] audio={wav.Length}B"
                    + (lastText.Length > 0 ? $" msg={lastText[..Math.Min(120, lastText.Length)]}" : "");
        return wav.Length > 44 ? wav : null;
    }

    // Rolling token Edge wants; doubles lose precision here.
    private static string EdgeSecToken()
    {
        long seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 11644473600L; // -> Windows epoch
        seconds -= seconds % 300L;                                               // round to 5 min
        long winTicks = seconds * 10_000_000L;                                   // 100-ns units (fits in long)
        var s = winTicks.ToString(CultureInfo.InvariantCulture) + EdgeToken;
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(s));
        var sb = new StringBuilder(64);
        foreach (var b in hash) sb.Append(b.ToString("X2"));
        return sb.ToString();
    }

    // Our -10..10 rate maps onto Edge percent.
    private static string EdgeRate(int rate)
    {
        var pct = Math.Clamp(rate, -10, 10) * 5;
        return (pct >= 0 ? "+" : "") + pct.ToString(CultureInfo.InvariantCulture) + "%";
    }

    // Volume 0..100 maps onto Edge relative volume.
    private static string EdgeVolume(int volume)
    {
        var pct = Math.Clamp(volume, 0, 100) - 100;
        return (pct >= 0 ? "+" : "") + pct.ToString(CultureInfo.InvariantCulture) + "%";
    }

    private static void SendText(ClientWebSocket ws, string msg, CancellationToken ct) =>
        ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)),
            WebSocketMessageType.Text, true, ct).GetAwaiter().GetResult();

    private static string XmlEscape(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
        .Replace("\"", "&quot;").Replace("'", "&apos;");

    // ---- MP3 playback ----

    // One shared output, so a new clip stops the old.
    // Typed as IDisposable so the shutdown path names no NAudio type: naming one
    // makes the JIT resolve the assembly, which throws once a reload has torn the
    // load context down. Null fields touch nothing, and a player that did play is
    // already resident, so neither case needs a load. PlayMp3 below is the only
    // place the real types appear, and it never runs during shutdown.
    private IDisposable? _output;
    private IDisposable? _reader;
    private MemoryStream? _readerMs;

    // Decodes the MP3 and plays it, non-blocking.
    private void PlayMp3(byte[] mp3, long seq)
    {
        Service.Log.Information($"[FrenMits] Edge.PlayMp3 ({mp3.Length}B)");
        try
        {
            // Don't resurrect the player after Dispose.
            if (_disposed) return;
            // A newer cue already played, so drop this one.
            if (!TryAdvance(ref _playedSeq, seq)) return;

            // Disposing the previous output stops it.
            try { _output?.Dispose(); } catch { /* ignore */ }
            try { _reader?.Dispose(); } catch { /* ignore */ }
            try { _readerMs?.Dispose(); } catch { /* ignore */ }

            _readerMs = new MemoryStream(mp3);
            var reader = new NAudio.Wave.Mp3FileReader(_readerMs);
            var output = new NAudio.Wave.WaveOutEvent();
            output.Init(reader);
            output.Play();
            _reader = reader;
            _output = output;
        }
        catch (Exception ex)
        {
            LastTtsStatus = $"Online OK but playback error: {ex.Message}";
            Service.Log.Warning(ex, "FrenMits: MP3 playback failed");
        }
    }

    public void Dispose()
    {
        // Flag first so in-flight work bails at its next gate.
        _disposed = true;
        try { _shutdown.Cancel(); } catch { /* ignore */ }
        try { _jobs.CompleteAdding(); } catch { /* ignore */ }
        // An idle worker is out in microseconds; a busy one finishes off-thread instead of stalling a frame.
        try
        {
            if (!_worker.Join(25))
                System.Threading.Tasks.Task.Run(() => { try { _worker.Join(5000); } catch { /* ignore */ } });
        }
        catch { /* ignore */ }
    }
}
