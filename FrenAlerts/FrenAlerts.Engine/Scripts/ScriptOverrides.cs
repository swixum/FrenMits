using Jint;

namespace FrenAlerts.Engine.Scripts;

// What somebody has changed about one of their shipped calls.
//
// Their fights ship with words nobody here wrote, so the way to disagree with a line
// is to override it rather than to edit the file: keep their fights replaceable, keep
// the change. One of these per trigger and output key, and a key nobody has touched
// has no entry at all.
public sealed class OutputOverride
{
    public bool TextOn { get; set; } = true;

    public bool TtsOn { get; set; } = true;

    public string Text { get; set; } = "";

    public string Tts { get; set; } = "";

    // Off by default, because a call that types itself into party chat is a thing to
    // ask for rather than to discover.
    public bool MacroOn { get; set; }

    public string MacroText { get; set; } = "";

    public bool IsDefault =>
        TextOn && TtsOn && !MacroOn
        && string.IsNullOrEmpty(Text) && string.IsNullOrEmpty(Tts)
        && string.IsNullOrEmpty(MacroText);
}

// The overrides, and the two functions their prelude asks for.
//
// Their `makeOutput` proxy calls `__ov(triggerId, key, mode)` every time a line is
// built and splices whatever comes back over the shipped one, so this is the hook a
// reworded call goes through. `\0OFF` is their sentinel for "say nothing on this
// channel", and it has to survive all the way out to the caller rather than being
// turned into an empty string on the way.
public sealed class ScriptOverrides
{
    // Theirs, kept exactly: a line that comes back as this is a channel switched off,
    // not a line with no words in it.
    public const string Off = "\0OFF";

    // Their key: the trigger and the output string it belongs to, joined by a
    // character no name can contain.
    private const char Join = '';

    private readonly Dictionary<string, OutputOverride> _byKey = new(StringComparer.Ordinal);

    // Which channel is being built right now, which their proxy reads back through
    // `__ovMode`. Set by the runner around each resolution.
    public string Mode { get; set; } = "text";

    public int Count => _byKey.Count;

    public static string KeyOf(string triggerId, string outputKey) => triggerId + Join + outputKey;

    public OutputOverride? Find(string triggerId, string outputKey) =>
        _byKey.GetValueOrDefault(KeyOf(triggerId, outputKey));

    public OutputOverride Ensure(string triggerId, string outputKey)
    {
        var key = KeyOf(triggerId, outputKey);
        if (!_byKey.TryGetValue(key, out var found)) _byKey[key] = found = new OutputOverride();
        return found;
    }

    public void Remove(string triggerId, string outputKey) => _byKey.Remove(KeyOf(triggerId, outputKey));

    // A trigger with anything overridden at all, which is what decides whether the
    // spoken line is resolved a second time rather than reusing the shown one.
    public bool Touched(string triggerId)
    {
        var prefix = triggerId + Join;
        foreach (var key in _byKey.Keys)
            if (key.StartsWith(prefix, StringComparison.Ordinal)) return true;
        return false;
    }

    public bool MacroFor(string triggerId)
    {
        var prefix = triggerId + Join;
        foreach (var (key, value) in _byKey)
            if (value.MacroOn && key.StartsWith(prefix, StringComparison.Ordinal)) return true;
        return false;
    }

    public IEnumerable<(string Key, OutputOverride Value)> All()
    {
        foreach (var (key, value) in _byKey) yield return (key, value);
    }

    // Their answer, verbatim, including which cases return nothing and which return
    // the sentinel: nothing means "use what shipped", the sentinel means "say nothing".
    public string Answer(string triggerId, string outputKey, string mode)
    {
        var found = Find(triggerId, outputKey);

        if (mode == "macro")
        {
            if (found is null || !found.MacroOn) return Off;
            // Their own placeholder: a macro with no words of its own sends the line
            // the call already says.
            return string.IsNullOrEmpty(found.MacroText) ? "{default}" : found.MacroText;
        }

        if (found is null) return "";
        if (mode == "tts") return found.TtsOn ? found.Tts : Off;
        return found.TextOn ? found.Text : Off;
    }

    public void Bind(Jint.Engine js)
    {
        js.SetValue("__ovMode", () => Mode);
        js.SetValue("__ov", (string triggerId, string key, string mode) => Answer(triggerId, key, mode));
    }
}
