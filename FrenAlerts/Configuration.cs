using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Configuration;
using FrenAlerts.Engine;
using FrenAlerts.Engine.Alerts;
using Newtonsoft.Json;

namespace FrenAlerts;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // ---- the master switch ----
    public bool AlertsEnabled { get; set; } = true;

    // Shows a sample call so the overlay can be dragged where you want it.
    public bool TestMode { get; set; }

    private HashSet<uint> _mutedTerritories = new();

    public HashSet<uint> MutedTerritories
    {
        get => _mutedTerritories;
        set => _mutedTerritories = value ?? new HashSet<uint>();
    }

    public bool IsMuted(uint territory) => _mutedTerritories.Contains(territory);

    private Dictionary<string, CallEdit> _callEdits = new();

    public Dictionary<string, CallEdit> CallEdits
    {
        get => _callEdits;
        set => _callEdits = value ?? new Dictionary<string, CallEdit>();
    }

    public CallEdit? EditFor(string key) =>
        _callEdits.TryGetValue(key, out var e) ? e : null;

    public bool AllCallsOn { get; set; } = true;

    private bool? Untouched => AllCallsOn ? true : null;

    public bool IsCallOn(string key, bool shipped) =>
        EditFor(key) is { } edit ? edit.Speaks(AllCallsOn || shipped) : AllCallsOn || shipped;

    // What the runner asks per call as a fight loads: true on, false off, null to
    // leave the pack's own answer alone.
    public bool? SwitchFor(string key) =>
        EditFor(key) is { } edit ? edit.On ?? (edit.Off ? false : Untouched) : Untouched;

    // The same answer, for a trigger the pages have no say over: a fight module's
    // own calls are left exactly as they were written.
    public bool? CallSwitch(string key) =>
        key.Length == 0 ? null : SwitchFor(key);

    public void SetCallOn(string key, bool shipped, bool on)
    {
        var edit = EditFor(key)?.Copy() ?? new CallEdit();
        // The older flag cannot disagree with the new one, so it is always cleared
        // and On is the single answer from here.
        edit.Off = false;
        edit.On = on == (AllCallsOn || shipped) ? null : on;
        SetEdit(key, edit);
    }

    public bool IsEdited(string key) => EditFor(key) is { IsDefault: false };

    private Dictionary<string, string> _strats = new();

    // Which answer the group uses for a mechanic that has several, keyed by fight
    // and setting so two fights can use the same word for different things.
    public Dictionary<string, string> Strats
    {
        get => _strats;
        set => _strats = value ?? new Dictionary<string, string>();
    }

    private static string StratKey(ushort territory, string key) => $"{territory}/{key}";

    // The group's answer, or the setting's own default when they have not said.
    // An answer that is no longer offered reads as the default rather than being
    // passed through, so a renamed option cannot leave a fight matching nothing.
    public string StratFor(ushort territory, string key)
    {
        var setting = Strategies.Find(territory, key);
        if (setting is null) return "";
        if (!_strats.TryGetValue(StratKey(territory, key), out var chosen)) return setting.Default;
        return setting.Options.Any(o => o.Value == chosen) ? chosen : setting.Default;
    }

    public void SetStrat(ushort territory, string key, string value)
    {
        var setting = Strategies.Find(territory, key);
        if (setting is null) return;

        if (value == setting.Default) _strats.Remove(StratKey(territory, key));
        else if (setting.Options.Any(o => o.Value == value)) _strats[StratKey(territory, key)] = value;
        Save();
    }

    public bool StratIsSet(ushort territory, string key) =>
        _strats.ContainsKey(StratKey(territory, key));

    // The one place an edit is written, so the "back to default drops it" rule
    // cannot be forgotten at a call site.
    public void SetEdit(string key, CallEdit edit)
    {
        if (edit.IsDefault) _callEdits.Remove(key);
        else _callEdits[key] = edit;
        Save();
    }

    public void ClearEdit(string key)
    {
        if (_callEdits.Remove(key)) Save();
    }

    public int ClearEdits(IEnumerable<string> keys)
    {
        var gone = keys.Count(k => _callEdits.Remove(k));
        if (gone > 0) Save();
        return gone;
    }

    // ---- the call on screen ----
    // One line, read left to right: icon, what to do, and how long you have.
    public Vector2 OverlayPosition { get; set; } = new(0.5f, 0.35f);
    public bool OverlayLocked { get; set; }
    public float CallFontSizePx { get; set; } = 40f;
    public int CallTextAlign { get; set; } = 1;              // 0 left, 1 center, 2 right
    public bool ShowCallIcon { get; set; } = true;
    public float CallIconScale { get; set; } = 1f;           // against the text height
    public bool ShowCountdown { get; set; } = true;          // the (3) after the words

    // One color per level, so how bad it is reads before the words do.
    public uint ColorInfo { get; set; } = 0xFFFFD47F;        // #7FD4FF light blue
    public uint ColorAlert { get; set; } = 0xFF3BC5FF;       // #FFC53B amber
    public uint ColorAlarm { get; set; } = 0xFF5C5CFF;       // #FF5C5C red

    public bool TextShadow { get; set; } = true;
    public bool TextOutline { get; set; }
    public bool PulseWhenClose { get; set; } = true;
    public bool ShowBackground { get; set; }
    public uint BackgroundColor { get; set; } = 0xB0000000;

    // ---- voice ----
    public bool VoiceEnabled { get; set; }
    public float VoiceVolume { get; set; } = 0.7f;
    public float VoiceSpeed { get; set; } = 1f;
    public bool UseLocalVoice { get; set; } = true;
    public string LocalVoiceName { get; set; } = Engine.Alerts.VoiceCatalog.Default;

    // ---- diagnostics ----

    // Whether this machine has ever asked for the pull recorder. False everywhere
    // it has not been asked for, which is everywhere it was not wanted: the
    // recorder writes a file to disk, and a debug surface is not something to hand
    // to somebody who installed this to be told where to stand.
    //
    // The chat command is the only way to set it. Once set, the window carries the
    // control, because switching it off mid-replay is the thing worth having.
    public bool Diagnostics { get; set; }

    // Whether the recorder should come back on by itself next time.
    //
    // Off everywhere it has not been asked for, same as the switch above: this only
    // becomes true when somebody turns the recorder on by hand, and it is what makes
    // a night of replays survive a reload without typing the command every time.
    public bool KeepRecording { get; set; }

    // ---- the config window itself ----
    public uint AccentColor { get; set; } = Ui.Theme.DefaultAccent;
    public float UiScale { get; set; } = 1f;
    public bool ColorblindMode { get; set; }

    private const long QuietMs = 400;

    private bool _dirty;
    private long _dirtyAt;

    // Set when the file on disk failed to load: the defaults are in memory and
    // writing them over a config that might still be readable would lose it.
    [JsonIgnore] public static bool SuppressSave;

    [JsonIgnore] public static DateTime LastSavedAt = DateTime.MinValue;

    [JsonIgnore] public bool SavePending => _dirty;

    public void Save()
    {
        _dirty = true;
        _dirtyAt = Environment.TickCount64;
    }

    public void Flush(bool force = false)
    {
        if (!_dirty || SuppressSave) return;
        if (!force && Environment.TickCount64 - _dirtyAt < QuietMs) return;
        _dirty = false;
        try
        {
            Service.PluginInterface.SavePluginConfig(this);
            LastSavedAt = DateTime.Now;
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "could not save the config");
        }
    }

    public static Configuration Load()
    {
        try
        {
            return Service.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        }
        catch (Exception ex)
        {
            // Defaults for this session rather than no plugin, and nothing is
            // written back over whatever is there until it is read again.
            Service.Log.Error(ex, "the config file could not be read; running on defaults");
            SuppressSave = true;
            return new Configuration();
        }
    }
}
