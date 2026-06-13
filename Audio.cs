using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace FrenMits;

// Optional audio cues. TTS uses the built-in Windows SAPI voice via COM (no
// extra dependencies); the beep is a generated sine WAV played through winmm.
// Both are best-effort and fail silently if the platform refuses.
public class Audio : IDisposable
{
    private object? _voice;       // SAPI.SpVoice COM object
    private bool _ttsUnavailable;

    public void Speak(string text, int rate, int volume)
    {
        if (string.IsNullOrWhiteSpace(text) || _ttsUnavailable) return;
        try
        {
            _voice ??= CreateVoice();
            if (_voice is null) return;
            dynamic v = _voice;
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

    public void Beep(float frequency, int milliseconds, int volumePercent)
    {
        try
        {
            var wav = GenerateSineWav(frequency, milliseconds, Math.Clamp(volumePercent, 0, 100) / 100f);
            Task.Run(() =>
            {
                try { PlaySound(wav, IntPtr.Zero, SndMemory); } // synchronous on this task
                catch { /* ignore */ }
            });
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: beep failed");
        }
    }

    // 16-bit mono PCM WAV with a short fade in/out to avoid clicks.
    private static byte[] GenerateSineWav(float freq, int ms, float amplitude)
    {
        const int sampleRate = 44100;
        var samples = Math.Max(1, sampleRate * ms / 1000);
        var fade = Math.Min(samples / 4, sampleRate / 200);
        amplitude = Math.Clamp(amplitude, 0f, 1f) * 0.9f;

        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        var dataBytes = samples * 2;

        w.Write(new[] { 'R', 'I', 'F', 'F' });
        w.Write(36 + dataBytes);
        w.Write(new[] { 'W', 'A', 'V', 'E' });
        w.Write(new[] { 'f', 'm', 't', ' ' });
        w.Write(16);                 // fmt chunk size
        w.Write((short)1);           // PCM
        w.Write((short)1);           // mono
        w.Write(sampleRate);
        w.Write(sampleRate * 2);     // byte rate
        w.Write((short)2);           // block align
        w.Write((short)16);          // bits per sample
        w.Write(new[] { 'd', 'a', 't', 'a' });
        w.Write(dataBytes);

        for (var i = 0; i < samples; i++)
        {
            var env = 1f;
            if (i < fade) env = i / (float)fade;
            else if (i > samples - fade) env = (samples - i) / (float)fade;
            var sample = MathF.Sin(2f * MathF.PI * freq * i / sampleRate) * amplitude * env;
            w.Write((short)(sample * short.MaxValue));
        }

        w.Flush();
        return stream.ToArray();
    }

    private const uint SndMemory = 0x0004; // SND_MEMORY (synchronous when SND_ASYNC absent)

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern bool PlaySound(byte[] data, IntPtr hModule, uint flags);

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
