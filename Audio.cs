using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FrenMits;

// Optional audio cues. Text-to-speech uses the built-in Windows SAPI voices via
// COM (no extra dependencies), so any voice installed on the system — male or
// female — can be selected. Best-effort: fails silently if the platform refuses.
public class Audio : IDisposable
{
    private object? _voice;       // SAPI.SpVoice COM object
    private bool _ttsUnavailable;
    private string _currentVoice = "";
    private List<string>? _voiceNames;

    public void Speak(string text, int rate, int volume, string voiceName = "")
    {
        if (string.IsNullOrWhiteSpace(text) || _ttsUnavailable) return;
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
    // Cached after the first query; female voices appear here when installed.
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

    // Selects the SAPI voice whose description matches the saved name. No-op when
    // empty (keeps the system default) or already selected.
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
