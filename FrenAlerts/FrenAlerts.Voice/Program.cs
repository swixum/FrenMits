using KokoroSharp;
using KokoroSharp.Core;

namespace FrenAlerts.Voice;

internal static class Program
{
    private const double StaleSeconds = 4.0;

    private const string VoiceName = "af_heart";

    private static KokoroTTS? _tts;
    private static KokoroVoice? _voice;

    private static int Main(string[] args)
    {
        var checking = args.Contains("--check");

        try
        {
            Load();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"load failed: {ex.Message}");
            return 2;
        }

        Console.Out.WriteLine("ready");
        Console.Out.Flush();

        if (checking) return 0;

        Speak();
        return 0;
    }

    private static void Load()
    {
        var here = AppContext.BaseDirectory;

        var local = Path.Combine(here, "model", "kokoro.onnx");
        _tts = File.Exists(local) ? KokoroTTS.LoadModel(local) : KokoroTTS.LoadModel(KModel.float16);

        var voices = Path.Combine(here, "voices");
        if (Directory.Exists(voices)) KokoroVoiceManager.LoadVoicesFromPath(voices);

        _voice = KokoroVoiceManager.GetVoice(VoiceName);
        if (_voice is null) throw new InvalidOperationException($"no voice named {VoiceName}");
    }

    private static void Speak()
    {
        while (Console.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;

            // Each line arrives stamped by the parent, so this can tell a call that
            // is late from one that only looks late because synthesis took a moment.
            var (at, text) = Split(line);
            if (text.Length == 0) continue;

            if (at > 0 && Now() - at > StaleSeconds) continue;

            try
            {
                _tts!.StopPlayback();
                _tts.SpeakFast(text, _voice!);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"speak failed: {ex.Message}");
            }
        }
    }

    // "<unix seconds>\t<text>", with the timestamp optional so a person can drive
    // this by hand to hear what a call sounds like.
    private static (double At, string Text) Split(string line)
    {
        var tab = line.IndexOf('\t');
        if (tab < 0) return (0, line.Trim());

        _ = double.TryParse(line[..tab], out var at);
        return (at, line[(tab + 1)..].Trim());
    }

    private static double Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
}
