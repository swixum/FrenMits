namespace FrenAlerts.Engine.Scripts;

// One of their lines, in somebody else's words.
//
// Their fights ship with words nobody here wrote, so a rewording is kept beside them
// rather than in them: their files stay replaceable and the change survives replacing
// them. Keyed by the trigger and the output key, because that is what their own
// override hook is keyed by, and one entry per key rather than per line on the page:
// their tables reach the same words under several names, and a page that lists those
// as one line writes one of these for each of them.
[Serializable]
public sealed class ScriptCallEdit
{
    public string Trigger { get; set; } = "";

    public string Key { get; set; } = "";

    // What it says on screen. Empty leaves their line alone.
    public string Text { get; set; } = "";

    // What is read out, where that should differ from the screen. Empty means it follows
    // the screen, which is decided when the edit is applied rather than stored here.
    public string Tts { get; set; } = "";

    public bool IsDefault => Text.Length == 0 && Tts.Length == 0;
}

public static class ScriptCallEdits
{
    // A ceiling on what one config can carry. Every fight of theirs put together is
    // around two thousand lines, so this is well past rewording all of them and is here
    // to stop a broken write growing the file without end.
    public const int Max = 8_000;

    // Ours written into their hook.
    //
    // The spoken line follows the screen unless somebody has said otherwise, and that is
    // not a nicety. Their runner resolves the spoken words in their own channel the
    // moment anything on a trigger is overridden, so a call reworded with nothing said
    // about the voice would appear in the new words and be read out in the old ones.
    public static void Apply(IEnumerable<ScriptCallEdit> edits, ScriptOverrides into)
    {
        foreach (var edit in edits)
        {
            if (edit.IsDefault || edit.Trigger.Length == 0 || edit.Key.Length == 0) continue;

            var over = into.Ensure(edit.Trigger, edit.Key);
            over.Text = edit.Text;
            over.Tts = edit.Tts.Length > 0 ? edit.Tts : edit.Text;
        }
    }
}
