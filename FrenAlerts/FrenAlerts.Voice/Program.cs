using KokoroSharp;
using KokoroSharp.Core;
using KokoroSharp.Processing;

namespace FrenAlerts.Voice;

internal static class Program
{
    private const double StaleSeconds = 4.0;

    private const string DefaultVoice = "af_heart";

    // Marks a line that changes how it speaks rather than something to speak. The
    // first field of a spoken line is a timestamp, so this can never be mistaken
    // for one.
    private const string SetPrefix = "set";

    private static KokoroTTS? _tts;
    private static KokoroVoice? _voice;
    private static KokoroTTSPipelineConfig _how = new();

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

        // Loaded by hand rather than left to the library's own search, so a missing
        // folder says which folder instead of naming a path that is not on the disk.
        var voices = Path.Combine(here, "voices");
        if (!Directory.Exists(voices))
            throw new DirectoryNotFoundException($"no voices folder at {voices}");
        KokoroVoiceManager.LoadVoicesFromPath(voices);

        _voice = Pick(DefaultVoice)
                 ?? KokoroVoiceManager.Voices.FirstOrDefault()
                 ?? throw new InvalidOperationException($"no voices in {voices}");
    }

    private static void Speak()
    {
        while (Console.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;

            if (line.StartsWith(SetPrefix + "\t", StringComparison.Ordinal))
            {
                Configure(line.Split('\t'));
                continue;
            }

            // Each line arrives stamped by the parent, so this can tell a call that
            // is late from one that only looks late because synthesis took a moment.
            var (at, text) = Split(line);
            if (text.Length == 0) continue;

            if (at > 0 && Now() - at > StaleSeconds) continue;

            try
            {
                _tts!.StopPlayback();
                _tts.SpeakFast(text, _voice!, _how);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"speak failed: {ex.Message}");
            }
        }
    }

    // "set<tab>voice<tab>speed<tab>volume". A field that will not read stays as it
    // was, because a bad number is not worth dropping the other two settings over.
    private static void Configure(string[] fields)
    {
        try
        {
            if (fields.Length > 1 && Pick(fields[1]) is { } voice) _voice = voice;

            if (fields.Length > 2 && float.TryParse(fields[2], out var speed) && speed > 0)
                _how = new KokoroTTSPipelineConfig { Speed = Math.Clamp(speed, 0.5f, 2f) };

            if (fields.Length > 3 && float.TryParse(fields[3], out var volume))
                _tts!.SetVolume(Math.Clamp(volume, 0f, 1f));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"settings failed: {ex.Message}");
        }
    }

    // A voice that is not there keeps the current one talking, which is better than
    // going quiet because a name was mistyped.
    private static KokoroVoice? Pick(string name)
    {
        try
        {
            return KokoroVoiceManager.GetVoice(name);
        }
        catch
        {
            return null;
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
