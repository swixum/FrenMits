using Jint;
using Jint.Native;

namespace FrenAlerts.Engine.Scripts;

// Their fights, run through the loop that stands beside them.
//
// The C# half is deliberately thin: hand an event across as the fields their triggers
// read, take back the list of triggers that want to speak, hold each one for its own
// delay, then ask for the words. Everything with judgement in it, and everything that
// touches their functions, happens in JavaScript.
//
// The same three things the other path has, because they are ours rather than theirs:
// the guard against saying one call twice, the overrides somebody has set, and the
// chat queue.
public sealed class ScriptLoopRunner(Jint.Engine js)
{
    // A call cannot be said twice inside this, however many events matched, because
    // one mechanic routinely arrives as eight of them.
    private const double SameCallGuard = 3.0;

    // What a call stays up for when the trigger names no duration.
    private const double DefaultSeconds = 5.0;

    private readonly List<(double Due, string Handle)> _waiting = [];
    private readonly Dictionary<string, double> _lastSaid = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _spawnSeen = new(StringComparer.Ordinal);
    private double _spawnPruned;

    // Their spawn memory: the same actor spawning again inside this is the client
    // repeating itself, and the sweep runs at the interval they use.
    private const double SpawnMemory = 10.0;
    private const double SpawnPrune = 30.0;

    public Action<ScriptCall>? Say;

    public ScriptOverrides Overrides { get; } = new();

    public ScriptMacros Macros { get; } = new();

    public int Matched { get; private set; }

    public int Fired { get; private set; }

    public string? Problem { get; private set; }

    // Loaded before their files, because their prelude reads the overrides while it
    // builds every line and the loop is what their fights register into.
    public void Bind()
    {
        Overrides.Bind(js);
        js.Execute(ScriptLoop.Driver);
    }

    // Which fights are live here. A zone is not always one of them: both halves of
    // the last savage tier claim the same territory.
    public void Watch(IReadOnlyList<int> setIndexes)
    {
        js.Execute($"__zones = [{string.Join(",", setIndexes)}];");
        Forget();
    }

    public int TriggerCount()
    {
        var count = js.Evaluate("__triggersHere().length");
        return count.IsNumber() ? (int)count.AsNumber() : 0;
    }

    public void Forget()
    {
        _waiting.Clear();
        _lastSaid.Clear();
        _spawnSeen.Clear();
        try { js.Execute("__forget();"); }
        catch (Exception ex) { Problem = ex.Message; }
    }

    // One event, offered to every trigger of every fight loaded here.
    public void Feed(in GameEvent e, string sourceName = "", string targetName = "")
    {
        if (ScriptFields.TypeOf(e.Kind) is not { } type) return;
        if (e.Kind == EventKind.ActorSpawn && !FreshSpawn(e.SourceId, e.Time)) return;

        Offer(e, type, ScriptFields.For(e, sourceName, targetName));

        // The same event under the other name the game writes it as, for the fights
        // that read that one. Second rather than first, so a set answered by both
        // keeps their order: the tether call is the one their file leads with.
        if (ScriptFields.AlsoTypeOf(e.Kind) is { } also)
            Offer(e, also, ScriptFields.AlsoFor(e));
    }

    private void Offer(in GameEvent e, string type, Dictionary<string, object?> fields)
    {
        try
        {
            var wanted = js.Invoke("__match", type, fields, e.Time);
            if (!wanted.IsArray()) return;

            var rows = wanted.AsArray();
            var count = (uint)rows.Get("length").AsNumber();
            for (var i = 0u; i < count; i++)
            {
                var item = rows.Get(i.ToString());
                if (!item.IsObject()) continue;
                var row = item.AsObject();

                Matched++;
                var handle = row.Get("handle").ToString();
                var delay = row.Get("delay").AsNumber();

                if (delay > 0.01) _waiting.Add((e.Time + delay, handle));
                else Speak(handle, e.Time);
            }
        }
        catch (Exception ex)
        {
            Problem = ex.Message;
        }
    }

    // Anything whose delay has run out, and the chat queue with it.
    public void Tick(double now)
    {
        Macros.Tick(now);

        for (var i = _waiting.Count - 1; i >= 0; i--)
        {
            if (_waiting[i].Due > now) continue;
            var handle = _waiting[i].Handle;
            _waiting.RemoveAt(i);
            Speak(handle, now);
        }
    }

    private void Speak(string handle, double now)
    {
        try
        {
            Overrides.Mode = "text";
            var said = js.Invoke("__say", handle);
            if (!said.IsObject()) return;
            var row = said.AsObject();

            var id = row.Get("id").ToString();
            var text = Line(row.Get("text"));
            var speech = Line(row.Get("speech"));

            // Their sentinel: a channel switched off is silent, not empty.
            if (text == ScriptOverrides.Off) text = "";
            if (speech == ScriptOverrides.Off) speech = "";

            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(speech)) return;

            // One mechanic arrives as one event per player, so the same words from
            // the same trigger inside three seconds are the same call.
            var key = id + "" + (string.IsNullOrEmpty(text) ? speech : text);
            if (_lastSaid.TryGetValue(key, out var last) && now - last < SameCallGuard) return;
            _lastSaid[key] = now;

            var hold = row.Get("hold").IsNumber() ? row.Get("hold").AsNumber() : 0;
            if (hold <= 0.01) hold = DefaultSeconds;

            var level = (int)row.Get("level").AsNumber() switch
            {
                2 => ScriptCallLevel.Alarm,
                1 => ScriptCallLevel.Alert,
                _ => ScriptCallLevel.Info,
            };

            if (Overrides.MacroFor(id) && !string.IsNullOrWhiteSpace(text)) Macros.Arm(text, now);

            Fired++;
            Say?.Invoke(new ScriptCall(id, text, ScriptSpeech.Spell(speech), level, hold));
        }
        catch (Exception ex)
        {
            Problem = ex.Message;
        }
    }

    private static string Line(JsValue value) => value.IsString() ? value.AsString() : "";

    // The client re-sends a spawn for an actor that is already there, and their fights
    // count adds: two for one add is a call that thinks there are twice as many.
    private bool FreshSpawn(uint actorId, double now)
    {
        var key = actorId.ToString("X8");

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
}
