using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FrenMits;

// Text-to-speech. Two engines:
//   * Windows SAPI voices (offline, via COM) — any voice installed on the system.
//   * Microsoft Edge "Read Aloud" online neural voices (free, no key, no install)
//     — the high-quality Aria/Guy/Jenny/Sonia/... voices, fetched as WAV and played
//     through winmm. Falls back to SAPI if the network call fails.
// Everything is best-effort and fails silently if the platform refuses.
public class Audio : IDisposable
{
    // Curated English Edge neural voices (the full catalog is hundreds across
    // languages; these are the useful ones for call-outs).
    public static readonly (string Id, string Label)[] EdgeVoices =
    {
        ("en-US-AriaNeural",        "Aria — US, female"),
        ("en-US-JennyNeural",       "Jenny — US, female"),
        ("en-US-MichelleNeural",    "Michelle — US, female"),
        ("en-US-AnaNeural",         "Ana — US, female (child)"),
        ("en-US-GuyNeural",         "Guy — US, male"),
        ("en-US-ChristopherNeural", "Christopher — US, male"),
        ("en-US-EricNeural",        "Eric — US, male"),
        ("en-US-RogerNeural",       "Roger — US, male"),
        ("en-US-SteffanNeural",     "Steffan — US, male"),
        ("en-GB-SoniaNeural",       "Sonia — UK, female"),
        ("en-GB-LibbyNeural",       "Libby — UK, female"),
        ("en-GB-RyanNeural",        "Ryan — UK, male"),
        ("en-AU-NatashaNeural",     "Natasha — AU, female"),
        ("en-AU-WilliamNeural",     "William — AU, male"),
        ("en-CA-ClaraNeural",       "Clara — CA, female"),
        ("en-IE-EmilyNeural",       "Emily — IE, female"),
        ("en-IN-NeerjaNeural",      "Neerja — IN, female"),
    };

    private object? _voice;       // SAPI.SpVoice COM object
    private bool _ttsUnavailable;
    private string _currentVoice = "";
    private List<string>? _voiceNames;

    // Small in-memory WAV cache so a repeated call-out (e.g. "Reprisal") is instant.
    private readonly Dictionary<string, byte[]> _edgeCache = new();
    private readonly LinkedList<string> _edgeOrder = new();
    private const int EdgeCacheMax = 128;

    // Speaks via the chosen engine. When useEdge is true, voice is an Edge voice id
    // (e.g. "en-US-AriaNeural"); otherwise it's a SAPI voice description.
    public void Speak(string text, int rate, int volume, bool useEdge, string voice)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        if (useEdge)
        {
            var t = text;
            Task.Run(() =>
            {
                try
                {
                    var wav = GetEdgeWav(t, voice, rate, volume);
                    if (wav != null) { PlayWav(wav); return; }
                }
                catch (Exception ex) { Service.Log.Warning(ex, "FrenMits: Edge TTS failed; using Windows voice"); }
                SpeakSapi(t, rate, volume, "");   // fallback
            });
            return;
        }

        SpeakSapi(text, rate, volume, voice);
    }

    // ---- Windows SAPI -----------------------------------------------------

    private void SpeakSapi(string text, int rate, int volume, string voiceName)
    {
        if (_ttsUnavailable) return;
        try
        {
            _voice ??= CreateVoice();
            if (_voice is null) return;
            dynamic v = _voice;
            ApplyVoice(v, voiceName);
            v.Rate = Math.Clamp(rate, -10, 10);
            v.Volume = Math.Clamp(volume, 0, 100);
            // SVSFlagsAsync (1) | SVSFPurgeBeforeSpeak (2): interrupt + speak async.
            v.Speak(text, 3u);
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: TTS speak failed");
        }
    }

    // Names of every installed SAPI voice (e.g. "Microsoft Zira", "Microsoft David").
    public IReadOnlyList<string> VoiceNames()
    {
        if (_voiceNames != null) return _voiceNames;
        _voiceNames = new List<string>();
        try
        {
            _voice ??= CreateVoice();
            if (_voice is null) return _voiceNames;
            dynamic v = _voice;
            dynamic tokens = v.GetVoices();
            int count = tokens.Count;
            for (var i = 0; i < count; i++)
            {
                try { _voiceNames.Add((string)tokens.Item(i).GetDescription()); }
                catch { /* skip malformed token */ }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: enumerating TTS voices failed");
        }
        return _voiceNames;
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

    // ---- Microsoft Edge online neural voices ------------------------------

    private byte[]? GetEdgeWav(string text, string voice, int rate, int volume)
    {
        var key = $"{voice}|{rate}|{volume}|{text}";
        lock (_edgeCache)
            if (_edgeCache.TryGetValue(key, out var hit)) return hit;

        var wav = FetchEdge(text, voice, rate, volume);
        if (wav != null)
            lock (_edgeCache)
            {
                if (!_edgeCache.ContainsKey(key))
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
            }
        return wav;
    }

    // Microsoft Edge "Read Aloud" voice service. Free, no key, no install.
    private const string EdgeToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const string EdgeVersion = "1-130.0.2849.68";

    private static byte[]? FetchEdge(string text, string voice, int rate, int volume)
    {
        if (string.IsNullOrWhiteSpace(voice)) voice = "en-US-AriaNeural";

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0");
        ws.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");

        var url =
            "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1" +
            $"?TrustedClientToken={EdgeToken}&Sec-MS-GEC={EdgeSecToken()}&Sec-MS-GEC-Version={EdgeVersion}" +
            $"&ConnectionId={Guid.NewGuid():N}";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        ws.ConnectAsync(new Uri(url), cts.Token).GetAwaiter().GetResult();

        var ts = DateTime.UtcNow.ToString(
            "ddd MMM dd yyyy HH:mm:ss 'GMT+0000 (Coordinated Universal Time)'", CultureInfo.InvariantCulture);

        var configMsg =
            "X-Timestamp:" + ts + "\r\nContent-Type:application/json; charset=utf-8\r\nPath:speech.config\r\n\r\n" +
            "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"false\"," +
            "\"wordBoundaryEnabled\":\"false\"},\"outputFormat\":\"riff-24khz-16bit-mono-pcm\"}}}}";
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
                if (r.MessageType == WebSocketMessageType.Close) { done = true; break; }
                msgStream.Write(buf, 0, r.Count);
            }
            while (!r.EndOfMessage);
            if (done) break;

            var msg = msgStream.ToArray();
            if (r.MessageType == WebSocketMessageType.Text)
            {
                if (Encoding.UTF8.GetString(msg).Contains("Path:turn.end")) done = true;
            }
            else if (msg.Length > 2)
            {
                // Binary frame: 2-byte big-endian header length, then header, then audio.
                int headerLen = (msg[0] << 8) | msg[1];
                int start = 2 + headerLen;
                if (start < msg.Length) audio.Write(msg, start, msg.Length - start);
            }
        }

        try { ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).GetAwaiter().GetResult(); }
        catch { /* ignore */ }

        var wav = audio.ToArray();
        return wav.Length > 44 ? wav : null;
    }

    // Rolling security token Microsoft now requires: SHA-256 of (winticks rounded to
    // 5 min) + the client token.
    private static string EdgeSecToken()
    {
        double ticks = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 11644473600d;
        ticks -= ticks % 300d;
        ticks *= 1e7;
        var s = ticks.ToString("F0", CultureInfo.InvariantCulture) + EdgeToken;
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(s));
        var sb = new StringBuilder(64);
        foreach (var b in hash) sb.Append(b.ToString("X2"));
        return sb.ToString();
    }

    // Our TTS rate slider is -10..10; map to Edge's percent (-50%..+50%).
    private static string EdgeRate(int rate)
    {
        var pct = Math.Clamp(rate, -10, 10) * 5;
        return (pct >= 0 ? "+" : "") + pct.ToString(CultureInfo.InvariantCulture) + "%";
    }

    // Volume 0..100 -> Edge relative volume (100 = default/loud, 0 = silent).
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

    // ---- WAV playback (winmm) ---------------------------------------------

    private const uint SndMemory = 0x0004; // SND_MEMORY (synchronous without SND_ASYNC)

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern bool PlaySound(byte[] data, IntPtr hModule, uint flags);

    // Plays a WAV buffer. Synchronous PlaySound on a worker task so the buffer stays
    // alive for the call and the game thread isn't blocked.
    private static void PlayWav(byte[] wav)
    {
        Task.Run(() =>
        {
            try { PlaySound(wav, IntPtr.Zero, SndMemory); }
            catch { /* ignore */ }
        });
    }

    public void Dispose()
    {
        try
        {
            if (_voice is not null && Marshal.IsComObject(_voice))
                Marshal.ReleaseComObject(_voice);
        }
        catch { /* ignore */ }
        _voice = null;
    }
}
