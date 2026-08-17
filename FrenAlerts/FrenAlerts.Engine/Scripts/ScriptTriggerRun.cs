using Jint;
using System.Text.RegularExpressions;
using Jint.Native;
using Jint.Native.Object;

namespace FrenAlerts.Engine.Scripts;

// What a trigger of theirs says when it fires.
public enum ScriptCallLevel
{
    Info,
    Alert,
    Alarm,
}

public sealed record ScriptCall(
    string TriggerId, string Text, string Speech, ScriptCallLevel Level, double Seconds);

// One of their triggers, compiled once.
public sealed class CompiledScriptTrigger
{
    public required string Id { get; init; }
    public required Regex Regex { get; init; }
    public required string[] LineCodes { get; init; }
    public string Name { get; init; } = "";

    public JsValue? Condition { get; init; }
    public JsValue? Response { get; init; }
    public JsValue? AlarmText { get; init; }
    public JsValue? AlertText { get; init; }
    public JsValue? InfoText { get; init; }
    public JsValue? Tts { get; init; }
    public JsValue? PreRun { get; init; }
    public JsValue? Promise { get; init; }
    public JsValue? Run { get; init; }
    public JsValue? DelaySeconds { get; init; }
    public JsValue? DurationSeconds { get; init; }
    public JsValue? MacroText { get; init; }
    public JsValue? SuppressSeconds { get; init; }
    public JsValue OutputStrings { get; init; } = JsValue.Undefined;
    public bool Priority { get; init; }

    public bool Speaks =>
        Response is not null || AlarmText is not null || AlertText is not null
        || InfoText is not null || Tts is not null;
}

// Their triggers, run in their order.
//
// The order is the whole thing and it is theirs: match the line, ask the condition,
// refuse a re-fire inside the suppress window, run the collector, wait out the
// delay, then work out the words. Their fights are written expecting exactly that,
// and a trigger whose words are built before its delay reads the state of the wrong
// moment: several of theirs collect for three seconds and then say what they
// collected.
//
// The words are theirs too: a response builder if the trigger has one, otherwise
// alarm before alert before info, and a separate spoken line only where they ship
// one. Nothing here writes a call of its own.
public sealed class ScriptTriggerRunner(Jint.Engine js)
{
    // A call cannot be said twice inside this, however many lines matched, because
    // one mechanic routinely arrives as eight lines.
    private const double SameCallGuard = 3.0;

    // What a call stays up for when the trigger names no duration.
    private const double DefaultSeconds = 5.0;

    // Their spawn memory: the same actor spawning again inside this is the client
    // repeating itself, and the sweep runs at the second interval they use.
    private const double SpawnMemory = 10.0;
    private const double SpawnPrune = 30.0;

    private readonly List<CompiledScriptTrigger> _triggers = [];
    private readonly Dictionary<string, List<CompiledScriptTrigger>> _byCode = new(StringComparer.Ordinal);
    private readonly List<CompiledScriptTrigger> _anyCode = [];
    private readonly Dictionary<string, double> _lastFire = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _lastSaid = new(StringComparer.Ordinal);
    private readonly List<(double Due, CompiledScriptTrigger Trigger, ObjectInstance Matches)> _waiting = [];
    private readonly Dictionary<string, double> _spawnSeen = new(StringComparer.Ordinal);
    private double _spawnPruned;

    public IReadOnlyList<CompiledScriptTrigger> Triggers => _triggers;

    public int Matched { get; private set; }

    public int Fired { get; private set; }

    public string? Problem { get; private set; }

    // Where a finished call goes. Left to the host, because what it does with one is
    // a display question and this is the fight's answer, not the screen's.
    public Action<ScriptCall>? Say;

    // What somebody has changed about their shipped lines, and where a call that has
    // been given macro text goes. Both are theirs; both are off until asked for.
    public ScriptOverrides Overrides { get; } = new();

    public ScriptMacros Macros { get; } = new();

    // Their prelude asks the host for the overrides every time a line is built, so
    // this has to happen before any fight file runs.
    public void Bind() => Overrides.Bind(js);

    // Compiles one registered fight. Everything a trigger needs is read once here:
    // matching a line is hot, and asking the script for the same field every time is
    // how a boss mod costs frames.
    public void Compile(int setIndex) => Compile([setIndex]);

    // Several at once, because a zone is not always one fight: both halves of the
    // last savage tier register themselves against the same territory, and a host
    // that keeps one set per zone silently runs half the fight. Theirs walks every
    // registered set and asks each whether it belongs here, which comes to the same
    // thing and is why they never hit it.
    public void Compile(IReadOnlyList<int> setIndexes)
    {
        _triggers.Clear();
        _byCode.Clear();
        _anyCode.Clear();
        ClearPending();
        Problem = null;

        foreach (var index in setIndexes) CompileSet(index);
    }

    private void CompileSet(int setIndex)
    {
        try
        {
            var set = js.Evaluate($"triggerSets[{setIndex}]");
            if (!set.IsObject()) return;

            var triggers = set.AsObject().Get("triggers");
            if (!triggers.IsArray()) return;

            var array = triggers.AsArray();
            var count = (uint)array.Get("length").AsNumber();
            for (var i = 0u; i < count; i++)
            {
                var item = array.Get(i.ToString());
                if (item.IsObject()) CompileOne(item.AsObject());
            }
        }
        catch (Exception ex)
        {
            Problem = ex.Message;
        }
    }

    private void CompileOne(ObjectInstance t)
    {
        var type = Str(t, "type");
        var net = t.Get("netRegex");
        if (type is null || !net.IsObject()) return;

        var regex = ScriptNetRegex.Build(type, net.AsObject());
        if (regex is null) return;

        var trigger = new CompiledScriptTrigger
        {
            Id = Str(t, "id") ?? $"{type} #{_triggers.Count}",
            Name = Str(t, "name") ?? "",
            Regex = regex,
            LineCodes = ScriptNetRegex.LineCodesFor(type),
            Condition = Fn(t, "condition"),
            Response = Fn(t, "response"),
            AlarmText = Opt(t, "alarmText"),
            AlertText = Opt(t, "alertText"),
            InfoText = Opt(t, "infoText"),
            Tts = Opt(t, "tts"),
            PreRun = Fn(t, "preRun"),
            Promise = Fn(t, "promise"),
            Run = Fn(t, "run"),
            DelaySeconds = Opt(t, "delaySeconds"),
            DurationSeconds = Opt(t, "durationSeconds"),
            MacroText = Opt(t, "macroText"),
            SuppressSeconds = Opt(t, "suppressSeconds"),
            OutputStrings = t.Get("outputStrings"),
            Priority = t.Get("priority").IsBoolean() && t.Get("priority").AsBoolean(),
        };

        _triggers.Add(trigger);

        if (trigger.LineCodes.Length == 0) { _anyCode.Add(trigger); return; }
        foreach (var code in trigger.LineCodes)
        {
            if (!_byCode.TryGetValue(code, out var list)) _byCode[code] = list = [];
            list.Add(trigger);
        }
    }

    // One line, offered to the triggers that could want it.
    public void Process(string line, double now)
    {
        if (_triggers.Count == 0) return;
        if (!FreshSpawn(line, now)) return;

        var code = ScriptLines.CodeOf(line);
        if (code is not null && _byCode.TryGetValue(code, out var candidates)) Run(candidates, line, now);
        if (_anyCode.Count > 0) Run(_anyCode, line, now);
    }

    private void Run(List<CompiledScriptTrigger> candidates, string line, double now)
    {
        foreach (var trigger in candidates)
        {
            try
            {
                var match = trigger.Regex.Match(line);
                if (!match.Success) continue;

                Matched++;

                var data = Data();
                if (data is null) return;

                var matches = BuildMatches(match);
                if (trigger.Condition is not null
                    && !Jint.Runtime.TypeConverter.ToBoolean(Invoke(trigger.Condition, data, matches)))
                    continue;

                // Suppressed on the match rather than on the call, so a trigger that
                // collects and says nothing is throttled the same way.
                var suppress = Number(trigger.SuppressSeconds, data, matches);
                if (suppress > 0 && _lastFire.TryGetValue(trigger.Id, out var last)
                    && now - last < suppress)
                    continue;

                _lastFire[trigger.Id] = now;

                // Before the delay on purpose: a collector reads the moment the line
                // arrived, not the moment the call is due.
                if (trigger.PreRun is not null) Invoke(trigger.PreRun, data, matches);

                var delay = Number(trigger.DelaySeconds, data, matches);
                if (delay > 0.01) _waiting.Add((now + delay, trigger, matches));
                else Execute(trigger, matches, now);
            }
            catch (Exception ex)
            {
                Problem = $"{trigger.Id}: {ex.Message}";
            }
        }
    }

    // The same add, spawning twice.
    //
    // The client re-sends a spawn for an actor that is already there, and their fights
    // count adds: two lines for one Spiny Plume is a call that thinks there are twice
    // as many as the arena holds. Everything that is not a spawn goes straight
    // through; a spawn is passed once and then ignored for ten seconds.
    private bool FreshSpawn(string line, double now)
    {
        if (!line.StartsWith("03|", StringComparison.Ordinal)) return true;

        var fields = line.Split('|');
        if (fields.Length < 3) return true;

        var key = fields[2];

        if (now - _spawnPruned > SpawnPrune)
        {
            _spawnPruned = now;
            foreach (var (seen, at) in _spawnSeen)
                if (now - at > SpawnMemory) _spawnSeen.Remove(seen);
        }

        if (_spawnSeen.TryGetValue(key, out var last) && now - last < SpawnMemory) return false;

        _spawnSeen[key] = now;
        return true;
    }

    // Anything whose delay has run out. Walked backwards so a call can be taken off
    // the list while it is being read.
    public void Tick(double now)
    {
        Macros.Tick(now);

        for (var i = _waiting.Count - 1; i >= 0; i--)
        {
            if (_waiting[i].Due > now) continue;

            var (_, trigger, matches) = _waiting[i];
            _waiting.RemoveAt(i);
            try { Execute(trigger, matches, now); }
            catch (Exception ex) { Problem = $"{trigger.Id}: {ex.Message}"; }
        }
    }

    // A pull ending drops whatever was still waiting: a call from the pull before is
    // worse than no call at all.
    public void ClearPending()
    {
        _waiting.Clear();
        _spawnSeen.Clear();
        _lastFire.Clear();
        _lastSaid.Clear();
    }

    private void Execute(CompiledScriptTrigger trigger, ObjectInstance matches, double now)
    {
        var data = Data();
        if (data is null) return;

        var output = js.Invoke(js.GetValue("makeOutput"), trigger.OutputStrings, "en", trigger.Id);

        if (trigger.Promise is not null)
        {
            try { Invoke(trigger.Promise, data, matches, output); }
            catch (Exception ex) { Problem = $"{trigger.Id} promise: {ex.Message}"; }
        }

        // Their order, and it is not a preference: a trigger that ships all three
        // means the loudest one. Built once per channel, because a reworded line is
        // spliced in while it is being built rather than afterwards.
        Overrides.Mode = "text";
        var (alarm, alert, info) = Words(trigger, data, matches, output);

        var shown = alarm ?? alert ?? info;

        string? spoken;
        if (trigger.Tts is not null)
        {
            Overrides.Mode = "tts";
            spoken = Text(trigger.Tts, data, matches, output);
        }
        else if (!Overrides.Touched(trigger.Id)) spoken = shown;
        else
        {
            // Overridden somewhere, so the spoken line is built again in its own
            // channel: theirs lets somebody reword what is said without touching
            // what is shown.
            Overrides.Mode = "tts";
            var (spokenAlarm, spokenAlert, spokenInfo) = Words(trigger, data, matches, output);
            spoken = spokenAlarm ?? spokenAlert ?? spokenInfo;
        }

        string? macro = null;
        if (Overrides.MacroFor(trigger.Id))
        {
            Overrides.Mode = "macro";
            if (trigger.MacroText is not null) macro = Text(trigger.MacroText, data, matches, output);
            else
            {
                var (macroAlarm, macroAlert, macroInfo) = Words(trigger, data, matches, output);
                macro = macroAlarm ?? macroAlert ?? macroInfo;
            }
        }

        Overrides.Mode = "text";

        // After the words, because several of theirs clear the very state the line
        // was about to read.
        if (trigger.Run is not null) Invoke(trigger.Run, data, matches, output);

        if (macro is not null && macro != ScriptOverrides.Off) Macros.Arm(macro, now);

        // Their sentinel: a channel switched off is silent, not empty.
        if (shown == ScriptOverrides.Off) shown = null;
        if (spoken == ScriptOverrides.Off) spoken = null;

        if (shown is null && spoken is null) return;

        // One mechanic arrives as one line per player, so the same words from the
        // same trigger inside three seconds are the same call.
        var key = trigger.Id + "" + (shown ?? spoken);
        if (_lastSaid.TryGetValue(key, out var said) && now - said < SameCallGuard) return;
        _lastSaid[key] = now;

        var seconds = Number(trigger.DurationSeconds, data, matches);
        if (seconds <= 0.01) seconds = DefaultSeconds;

        var level = alarm is not null ? ScriptCallLevel.Alarm
            : alert is not null ? ScriptCallLevel.Alert
            : ScriptCallLevel.Info;

        Fired++;
        Say?.Invoke(new ScriptCall(
            trigger.Id, shown ?? "", ScriptSpeech.Spell(spoken ?? shown ?? ""), level, seconds));
    }

    // The three lines a trigger could say, in their own order. A response builder
    // answers all three at once; a plain trigger carries them as three fields.
    private (string? Alarm, string? Alert, string? Info) Words(
        CompiledScriptTrigger trigger, ObjectInstance data, ObjectInstance matches, JsValue output)
    {
        if (trigger.Response is null)
            return (Text(trigger.AlarmText, data, matches, output),
                    Text(trigger.AlertText, data, matches, output),
                    Text(trigger.InfoText, data, matches, output));

        var response = Invoke(trigger.Response, data, matches, output);
        if (!response.IsObject()) return (null, null, null);

        var built = response.AsObject();
        return (FromResponse(built.Get("alarmText"), data, matches, output),
                FromResponse(built.Get("alertText"), data, matches, output),
                FromResponse(built.Get("infoText"), data, matches, output));
    }

    // Every line one trigger can say, and the words it ships with.
    //
    // Their own way of listing them, and it is not a read of the trigger's fields: a
    // response builder writes its strings onto the output object as it runs, so the
    // only way to see the keys is to run it once and ask what it left behind. This is
    // what an override editor offers somebody who wants to reword one line of a call
    // rather than all of it.
    public IReadOnlyList<(string Key, string Shipped)> Outputs(string triggerId)
    {
        var found = new List<(string, string)>();

        var trigger = _triggers.Find(t => t.Id == triggerId);
        var data = Data();
        if (trigger is null || data is null) return found;

        try
        {
            var output = js.Invoke(js.GetValue("makeOutput"), trigger.OutputStrings, "en", trigger.Id);
            var matches = js.Invoke(js.GetValue("__newObj")).AsObject();

            Overrides.Mode = "text";
            if (trigger.Response is not null)
            {
                try { Invoke(trigger.Response, data, matches, output); }
                catch { /* a builder that needs a real match still leaves its keys */ }
            }

            var strings = output.AsObject().Get("responseOutputStrings");
            if (!strings.IsObject()) return found;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (key, property) in strings.AsObject().GetOwnProperties())
            {
                var name = key.ToString();
                if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;
                found.Add((name, English(property.Value)));
            }
        }
        catch (Exception ex)
        {
            Problem = $"{triggerId} outputs: {ex.Message}";
        }

        return found;
    }

    // The shipped English of one output string, or nothing for the ones that are
    // built from a function rather than written down.
    private static string English(JsValue? value)
    {
        if (value is null || value.IsUndefined() || value.IsNull() || Callable(value)) return "";
        if (value.IsString()) return value.AsString();
        if (!value.IsObject()) return "";

        var en = value.AsObject().Get("en");
        return en.IsString() ? en.AsString() : "";
    }

    // Their per-pull state, which their own triggers read and write as `data`.
    private ObjectInstance? Data()
    {
        var data = js.GetValue("__data");
        return data.IsObject() ? data.AsObject() : null;
    }

    // The named fields of a match, as a plain object their code can read. Their own
    // helper builds it, so it is the same kind of object the rest of their script
    // makes.
    private ObjectInstance BuildMatches(Match match)
    {
        var matches = js.Invoke(js.GetValue("__newObj")).AsObject();
        foreach (var name in match.Groups.Keys)
        {
            if (int.TryParse(name, out _)) continue;
            var group = match.Groups[name];
            if (group.Success) matches.Set(name, group.Value);
        }
        return matches;
    }

    private string? Text(JsValue? field, ObjectInstance data, ObjectInstance matches, JsValue output)
    {
        if (field is null || field.IsUndefined() || field.IsNull()) return null;
        return Resolve(Callable(field) ? Invoke(field, data, matches, output) : field);
    }

    private string? FromResponse(JsValue field, ObjectInstance data, ObjectInstance matches, JsValue output)
    {
        if (field.IsUndefined() || field.IsNull()) return null;
        return Callable(field) ? Text(field, data, matches, output) : Resolve(field);
    }

    // A line is either the words themselves or their locale object, which this build
    // only ever reads English out of.
    private static string? Resolve(JsValue value)
    {
        if (value.IsUndefined() || value.IsNull()) return null;

        if (value.IsString())
        {
            var text = value.AsString();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        if (!value.IsObject()) return null;

        var en = value.AsObject().Get("en");
        return en.IsString() ? en.AsString() : null;
    }

    private double Number(JsValue? field, ObjectInstance data, ObjectInstance matches)
    {
        if (field is null || field.IsUndefined() || field.IsNull()) return 0;
        var value = Callable(field) ? Invoke(field, data, matches, JsValue.Undefined) : field;
        return value.IsNumber() ? value.AsNumber() : 0;
    }

    private JsValue Invoke(JsValue fn, params object[] args)
    {
        var result = js.Invoke(fn, args);
        try { return result.UnwrapIfPromise(); }
        catch { return JsValue.Undefined; }
    }

    private static bool Callable(JsValue value) => value is Jint.Native.Function.Function;

    private static string? Str(ObjectInstance o, string key)
    {
        var value = o.Get(key);
        return value.IsString() ? value.AsString() : null;
    }

    private static JsValue? Opt(ObjectInstance o, string key)
    {
        var value = o.Get(key);
        return value.IsUndefined() ? null : value;
    }

    private static JsValue? Fn(ObjectInstance o, string key)
    {
        var value = o.Get(key);
        return Callable(value) ? value : null;
    }
}
