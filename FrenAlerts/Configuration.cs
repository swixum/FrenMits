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

    // Shown but not said, per call. Asked on every call that reaches the board, so it
    // reads the edit already in hand rather than a second store.
    public bool IsSilent(string key) => key.Length > 0 && EditFor(key) is { Silent: true };

    public void SetSilent(string key, bool silent)
    {
        var edit = EditFor(key)?.Copy() ?? new CallEdit();
        edit.Silent = silent;
        SetEdit(key, edit);
    }

    private Dictionary<string, string> _strats = new();

    // Which answer the group uses for a mechanic that has several, keyed by fight
    // and setting so two fights can use the same word for different things.
    public Dictionary<string, string> Strats
    {
        get => _strats;
        set => _strats = value ?? new Dictionary<string, string>();
    }

    private static string StratKey(ushort territory, string key) => $"{territory}/{key}";

    // What an imported fight answers for a question it also asks, including its own
    // default when nobody has touched the row. Set once at startup and null when
    // nothing imported is loaded.
    // Never written to the file. It is a delegate, and the thing it closes over is this
    // config, so serialising it walks Target back to here and the whole save throws on a
    // circular reference. Every setting silently stopped saving the day this went in.
    [JsonIgnore] public Func<ushort, string, string>? ScriptAnswer;

    // The group's answer, or the setting's own default when they have not said.
    // An answer that is no longer offered reads as the default rather than being
    // passed through, so a renamed option cannot leave a fight matching nothing.

    public string StratFor(ushort territory, string key)
    {
        var setting = Strategies.Find(territory, key);
        if (setting is null) return "";

        // One row per question, settled in the engine so it is the same answer here,
        // on the page and in a test.
        _strats.TryGetValue(StratKey(territory, key), out var chosen);
        return Strategies.Answer(setting, ScriptAnswer?.Invoke(territory, key) ?? "",
            chosen ?? "");
    }

    public void SetStrat(ushort territory, string key, string value)
    {
        var setting = Strategies.Find(territory, key);
        if (setting is null) return;

        if (value == setting.Default) _strats.Remove(StratKey(territory, key));
        else if (setting.Options.Any(o => o.Value == value)) _strats[StratKey(territory, key)] = value;
        Save();
    }

    // Whether the row is showing something other than what it ships with, which is the
    // question the changed dot beside it is asking.
    //
    // Asked of StratFor rather than of the dictionary, so the dot cannot say one thing
    // while the box beside it shows another. It read "there is a key on disk", which was
    // already wrong for an option that had since been renamed: StratFor hands back the
    // default in that case, so the box drew the default and the dot said edited.
    //
    // Three sources feed that answer now, the imported fight's included, and only one of
    // them is a key on disk. Reading the answer itself is the one form that cannot fall
    // behind the next source somebody adds.
    public bool StratIsSet(ushort territory, string key) =>
        Strategies.Find(territory, key) is { } setting
        && StratFor(territory, key) != setting.Default;

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
    // Theirs, so a fresh install reads the size the fights were written against.
    // Anybody who has already set their own keeps it: this is only the default.
    public float CallFontSizePx { get; set; } = Engine.Alerts.CallLook.BasePx;
    public int CallTextAlign { get; set; } = 1;              // 0 left, 1 center, 2 right
    public bool ShowCallIcon { get; set; } = true;
    public float CallIconScale { get; set; } = 1f;           // against the text height
    public bool ShowCountdown { get; set; } = true;          // the (3) after the words

    // One color per level, so how bad it is reads before the words do.
    public uint ColorInfo { get; set; } = 0xFFFFD47F;        // #7FD4FF light blue
    public uint ColorAlert { get; set; } = 0xFF3BC5FF;       // #FFC53B amber
    public uint ColorAlarm { get; set; } = 0xFF5C5CFF;       // #FF5C5C red

    // The ring around the letters, which is the whole of how a call is read against a
    // bright floor. On by default, because theirs is.
    //
    // There used to be a second switch for a drop shadow. Carrying their look left the
    // two doing the same thing, so the shadow is gone and anybody who had it on keeps
    // the ring: Load turns this on once for them.
    public bool TextOutline { get; set; } = true;

    // Read by nothing now. Kept so a config written by an older build still loads, and
    // so the one-time turn-on below has something to read.
    public bool TextShadow { get; set; } = true;

    public bool PulseWhenClose { get; set; } = true;

    // The slab behind the words, theirs, on by default.
    public bool ShowBackground { get; set; } = true;
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

    // Which seat to read the calls as, when the game cannot say.
    //
    // Empty means work it out, which is right everywhere there is a party list. A
    // replay has none, so the eight players in the object table stand in for it and
    // the local player is always first among them: you are read as MT, H1, M1 or R1
    // and never as the second of your role. Every call that splits a pair then names
    // the other person's job.
    //
    // Cleared rather than remembered across a session by anything automatic: a seat
    // set by hand and forgotten would be worse in a real pull than the guess is.
    public string SeatOverride { get; set; } = "";

    // ---- who sits in each seat ----

    private Dictionary<string, string> _partySeats = new();

    // The group's own seating, by name, keyed by the seat.
    //
    // Seats are worked out from jobs, which is right only while the group's order
    // matches job order: with two melee, which one is M1 is the group's call. A seat
    // nobody names here keeps the worked-out answer, so an empty list is what an
    // install that never opens this should get.
    public Dictionary<string, string> PartySeats
    {
        get => _partySeats;
        set => _partySeats = Clean(value);
    }

    // Eight seats and no more, whatever the file on disk says. Read back rather than
    // trusted: a hand-edited config could otherwise grow this without limit and every
    // one of those keys would be looked up on every party poll.
    private static Dictionary<string, string> Clean(Dictionary<string, string>? raw)
    {
        var kept = new Dictionary<string, string>(8);
        if (raw is null) return kept;

        foreach (var slot in Audience.Slots)
            if (raw.TryGetValue(slot, out var name) && !string.IsNullOrWhiteSpace(name)
                && !kept.ContainsValue(name.Trim()))
                kept[slot] = name.Trim();

        return kept;
    }

    // One name to a seat and one seat to a name: seating somebody who is already sat
    // elsewhere empties the seat they came from, or the same person answers twice and
    // the seat they left is called for nobody.
    public void SetPartySeat(string slot, string name)
    {
        if (!Audience.IsSlot(slot)) return;
        var seat = slot.ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(name))
        {
            _partySeats.Remove(seat);
            Save();
            return;
        }

        var who = name.Trim();
        foreach (var other in Audience.Slots)
            if (other != seat && _partySeats.TryGetValue(other, out var sat)
                && string.Equals(sat, who, StringComparison.OrdinalIgnoreCase))
                _partySeats.Remove(other);

        _partySeats[seat] = who;
        Save();
    }

    public string PartySeatFor(string slot) =>
        _partySeats.GetValueOrDefault(slot.ToUpperInvariant(), "");

    // Back to working every seat out, which is where this started and what a group
    // that has changed shape wants rather than seven names that no longer sit there.
    public void ClearPartySeats()
    {
        _partySeats.Clear();
        Save();
    }

    private PartyBook _partyBook = new();

    // Who has run together, and how each group was seated. Learned from the party
    // read rather than set anywhere: two statics seat their melee differently and both
    // are right, so the answer is kept against the people it was used with.
    //
    // Bounded on the way in as well as on the way up, so a file somebody edited cannot
    // hand the party read a list it walks on every poll.
    public PartyBook PartyBook
    {
        get => _partyBook;
        set => _partyBook = value ?? new PartyBook();
    }

    // Whether the recorder should come back on by itself next time.
    //
    // Off everywhere it has not been asked for, same as the switch above: this only
    // becomes true when somebody turns the recorder on by hand, and it is what makes
    // a night of replays survive a reload without typing the command every time.
    public bool KeepRecording { get; set; }

    // ---- triggers somebody wrote themselves ----

    private List<Engine.UserTriggers.UserTriggerSet> _triggerSets = new();

    public List<Engine.UserTriggers.UserTriggerSet> TriggerSets
    {
        get => _triggerSets;
        set => _triggerSets = value ?? new List<Engine.UserTriggers.UserTriggerSet>();
    }

    // Which revision of the shipped sets this config has already been offered.
    //
    // Written down so a set somebody deleted stays deleted: without it, every
    // startup would hand back the four built-in sets they had just removed. A new
    // shipped set arrives by the number going up, not by the set being missing.
    public int BuiltInRevision { get; set; }

    // Whether hand-written triggers run at all, one switch above all the sets. Off
    // is what an install that only wants the fights should cost.
    public bool UserTriggersEnabled { get; set; } = true;

    // ---- how their fights are called ----

    private Dictionary<string, string> _scriptStrats = new();

    // Which way the group runs each mechanic their fights offer a choice on, keyed by
    // the strategy's own id. Empty means take whatever the fight defaults to, which
    // is what an install that never opens this should get.
    public Dictionary<string, string> ScriptStrats
    {
        get => _scriptStrats;
        set => _scriptStrats = value ?? new Dictionary<string, string>();
    }

    public string ScriptStratFor(string id) =>
        _scriptStrats.TryGetValue(id, out var value) ? value : "";

    // A choice back at the fight's own default is dropped rather than written, so a
    // strategy that is renamed or withdrawn cannot leave a dead answer behind.
    public void SetScriptStrat(string id, string value, string fallback)
    {
        if (string.IsNullOrEmpty(value) || value == fallback) _scriptStrats.Remove(id);
        else _scriptStrats[id] = value;
        Save();
    }

    // Every answer a fight asked for, back to that fight's own default, for the page's
    // back-to-default. One save rather than one per row, which is what setting them
    // back one at a time would cost.
    public int ClearScriptStrats(IEnumerable<string> ids)
    {
        var gone = ids.Count(id => _scriptStrats.Remove(id));
        if (gone > 0) Save();
        return gone;
    }

    private List<Engine.Scripts.ScriptCallEdit> _scriptCallEdits = new();

    // Their lines in somebody else's words. One entry per output key, which is what
    // their override hook is keyed by; the fight page lists several keys as one line
    // where they ship the same words and writes one of these for each.
    public List<Engine.Scripts.ScriptCallEdit> ScriptCallEdits
    {
        get => _scriptCallEdits;
        set
        {
            _scriptCallEdits = value ?? new List<Engine.Scripts.ScriptCallEdit>();
            _scriptEditAt = null;
        }
    }

    // Looked up per row on every frame the fight page draws, so it is not a walk of the
    // list. Dropped whenever the list moves, and rebuilt on the next question.
    private Dictionary<(string Trigger, string Key), Engine.Scripts.ScriptCallEdit>? _scriptEditAt;

    private Dictionary<(string, string), Engine.Scripts.ScriptCallEdit> ScriptEditIndex
    {
        get
        {
            if (_scriptEditAt is not null) return _scriptEditAt;

            var at = new Dictionary<(string, string), Engine.Scripts.ScriptCallEdit>();
            foreach (var edit in _scriptCallEdits) at[(edit.Trigger, edit.Key)] = edit;
            return _scriptEditAt = at;
        }
    }

    private List<string> _silentScriptCalls = new();

    // Their mechanics that are shown and not said, by trigger id.
    //
    // Kept as its own list rather than as a flag on a rewording: a rewording is per
    // line and is dropped the moment its box is emptied, and this is per mechanic and
    // has to outlive somebody clearing the words above it.
    public List<string> SilentScriptCalls
    {
        get => _silentScriptCalls;
        set
        {
            _silentScriptCalls = value ?? new List<string>();
            _silentScriptAt = null;
        }
    }

    private HashSet<string>? _silentScriptAt;

    private HashSet<string> SilentScriptIndex =>
        _silentScriptAt ??= new HashSet<string>(_silentScriptCalls, StringComparer.Ordinal);

    public bool IsScriptSilent(string trigger) =>
        trigger.Length > 0 && SilentScriptIndex.Contains(trigger);

    public void SetScriptSilent(string trigger, bool silent)
    {
        if (trigger.Length == 0) return;
        if (silent == IsScriptSilent(trigger)) return;

        if (silent) _silentScriptCalls.Add(trigger);
        else _silentScriptCalls.RemoveAll(t => string.Equals(t, trigger, StringComparison.Ordinal));

        _silentScriptAt = null;
        Save();
    }

    public Engine.Scripts.ScriptCallEdit? ScriptEditFor(string trigger, string key) =>
        ScriptEditIndex.TryGetValue((trigger, key), out var edit) ? edit : null;

    public bool IsScriptEdited(string trigger, string key) =>
        ScriptEditFor(trigger, key) is { IsDefault: false };

    // One line on the page, which is one or more of their keys, given new words.
    //
    // Every key that ships the line gets the same words, because the page showed them as
    // one line and rewording one of several identical lines silently is not what anybody
    // reading it asked for. Back to default drops the entries rather than storing empty
    // ones, so an untouched fight leaves nothing in the file.
    public void SetScriptEdit(string trigger, IEnumerable<string> keys, string text, string tts)
    {
        var wanted = text.Trim();
        var spoken = tts.Trim();

        foreach (var key in keys)
        {
            var edit = ScriptEditFor(trigger, key);
            if (wanted.Length == 0 && spoken.Length == 0)
            {
                if (edit is not null) _scriptCallEdits.Remove(edit);
                continue;
            }

            if (edit is null)
            {
                // The ceiling, so a runaway write cannot grow the config without end.
                // Silent because it is far past rewording every line of every fight.
                if (_scriptCallEdits.Count >= Engine.Scripts.ScriptCallEdits.Max) continue;
                _scriptCallEdits.Add(edit = new Engine.Scripts.ScriptCallEdit
                {
                    Trigger = trigger,
                    Key = key,
                });
            }

            edit.Text = wanted;
            edit.Tts = spoken;
        }

        _scriptEditAt = null;
        Save();
    }

    // Every rewording in a fight undone, for the page's own back-to-default.
    public int ClearScriptEdits(IEnumerable<string> triggers)
    {
        var mine = triggers.ToHashSet(StringComparer.Ordinal);
        var gone = _scriptCallEdits.RemoveAll(e => mine.Contains(e.Trigger));

        // The silenced ones go with the words. Left behind, a fight put back to
        // defaults keeps calls that are still mute with nothing on the page saying so,
        // which is what the strat list did before its own reset was fixed.
        var quiet = _silentScriptCalls.RemoveAll(mine.Contains);
        if (quiet > 0) _silentScriptAt = null;

        if (gone > 0 || quiet > 0)
        {
            if (gone > 0) _scriptEditAt = null;
            Save();
        }
        return gone;
    }

    // ---- the cooldown tracker ----

    private List<Engine.UserTriggers.CooldownEntry> _cooldowns = new();

    public List<Engine.UserTriggers.CooldownEntry> Cooldowns
    {
        get => _cooldowns;
        set => _cooldowns = value ?? new List<Engine.UserTriggers.CooldownEntry>();
    }

    // Off until somebody sets one up. An empty tracker draws nothing either way, and
    // a switch that is on with nothing behind it reads as broken.
    public bool CooldownsEnabled { get; set; }

    public Vector2 CooldownPosition { get; set; } = new(0.5f, 0.78f);

    // Always, in a duty, or in combat. Stored as the number the engine's own enum
    // uses, so the two cannot drift apart.
    public int CooldownVisibility { get; set; } = (int)Engine.UserTriggers.CooldownVisibility.InDuty;

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
            SaveProblem = "";
        }
        catch (Exception ex)
        {
            // Still dirty, so the next change tries again rather than the write being
            // dropped on the floor: a save that failed once used to be forgotten, and
            // what somebody typed was gone with it.
            _dirty = true;
            _dirtyAt = Environment.TickCount64;

            // Said once. This fired on every change for a whole evening and filled the
            // log with the same line, which is noise rather than a warning.
            if (SaveProblem.Length == 0)
            {
                SaveProblem = ex.Message;
                Service.Log.Error(ex, "could not save the config");
            }
        }
    }

    // Why the last save did not land, empty while it is landing. Read by the home page,
    // because a setting that will not stick is invisible until somebody notices one has
    // gone back on its own.
    [JsonIgnore] public string SaveProblem { get; private set; } = "";

    public static Configuration Load()
    {
        try
        {
            var config = Service.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            // Anybody who had the drop shadow on and the outline off had letters that
            // stood off the floor, and the shadow is gone. They get the ring instead
            // rather than a call that suddenly reads flat.
            if (config.TextShadow && !config.TextOutline)
            {
                config.TextOutline = true;
                config.TextShadow = false;
                config.Save();
            }

            return config;
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
