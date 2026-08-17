using System.Collections.Concurrent;
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

    // One line finishes before the next one starts.
    //
    // Every line used to StopPlayback first, so two calls landing together meant the
    // first was cut off part way through a word and only the second was ever heard.
    // Two mechanics at once is exactly when both lines matter.
    //
    // Reading and speaking are split, because reading has to keep up with the fight
    // whatever the speaking is doing: the parent writes to a pipe and a pipe that
    // nobody drains blocks the parent.
    private static readonly BlockingCollection<(double At, string Text)> Waiting = new(Queued);

    // What the queue holds before it starts throwing lines away. Eight is the parent's
    // own bound, and a queue past that is a fight nobody could listen to anyway.
    private const int Queued = 8;

    // Signalled when the library says a line is done, so this waits on the speaking
    // rather than on a guess at how long the words take.
    private static readonly SemaphoreSlim Finished = new(0, 1);

    private static void Speak()
    {
        _tts!.OnSpeechCompleted += _ => Done();
        _tts.OnSpeechCanceled += _ => Done();

        var speaking = new Thread(Speaking) { IsBackground = true, Name = "speaking" };
        speaking.Start();

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

            // Never Add, which blocks: the parent is on the other end of this pipe.
            if (!Waiting.TryAdd((at, text))) Console.Error.WriteLine("queue full, line dropped");
        }

        Waiting.CompleteAdding();
        speaking.Join(TimeSpan.FromSeconds(5));
    }

    // Released at most once per line, so a library that raises both completed and
    // canceled for the same one cannot leave a spare release behind to skip the next.
    private static void Done()
    {
        try
        {
            if (Finished.CurrentCount == 0) Finished.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    private static void Speaking()
    {
        foreach (var (at, text) in Waiting.GetConsumingEnumerable())
        {
            // Checked again here, not only on the way in: a line can sit behind a long
            // one and be about a mechanic that has already happened.
            if (at > 0 && Now() - at > StaleSeconds) continue;

            try
            {
                // Drain anything left over from the line before, so a stale release
                // cannot make this one look finished the moment it starts.
                Finished.Wait(0);
                _tts!.SpeakFast(text, _voice!, _how);

                // Bounded, because a completion that never arrives would otherwise
                // stop this thread saying anything ever again. Long enough for a line
                // nobody would write, and the fight moves on regardless.
                if (!Finished.Wait(TimeSpan.FromSeconds(15)))
                    Console.Error.WriteLine("gave up waiting for a line to finish");
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
