namespace FrenAlerts.Engine.Scripts;

// What a fight of theirs looks like on its own page.
//
// Here rather than in the plugin because it is the answer to "what does this fight
// call", and that answer has to be checkable against their real files offline. It used
// to be written twice, once in the host and once in the test that was supposed to be
// holding the host to account, and the two only agreed for as long as somebody
// remembered to change both.
public static class ScriptListing
{
    // Every trigger in the compiled zone, in their own order.
    public static IReadOnlyList<ScriptShownCall> For(ScriptTriggerRunner runner, ScriptFights fights) =>
        [.. runner.Triggers.Select(t => new ScriptShownCall(t.Id, t.Speaks, LinesOf(runner, fights, t.Id)))];

    // What one trigger can say, as lines a page can list and reword.
    //
    // Keyed off their own output strings, because a rewording is stored against the key
    // and their words are the only thing that carries one. Narrowed to the keys this
    // trigger reaches, because their tables are shared between the triggers of a
    // mechanic and listing the table lists mostly words this one never says. Then
    // grouped by the words themselves: two keys that ship the same line are one line to
    // read and one thing to reword.
    public static IReadOnlyList<ScriptShownLine> LinesOf(
        ScriptTriggerRunner runner, ScriptFights fights, string id)
    {
        var declared = runner.Outputs(id).Where(o => o.Shipped.Length > 0).ToList();

        // Nothing keyed came back, so whatever it says can be shown and not reworded. A
        // mechanic with its words and no edit box beats a mechanic with a name and
        // nothing under it.
        if (declared.Count == 0)
            return [.. runner.Says(id).Select(w => new ScriptShownLine([], w))];

        var reaches = fights.ReachesOutputs(id).ToHashSet(StringComparer.Ordinal);
        var kept = reaches.Count == 0
            ? declared
            : declared.Where(o => reaches.Contains(o.Key)).ToList();

        // It reaches keys and none of them are keys it declares, so the two answers are
        // about different things and the scan is the one to distrust. A long list of the
        // right lines beats a short list of the wrong ones.
        if (kept.Count == 0) kept = declared;

        var order = new List<string>();
        var keysOf = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (key, text) in kept)
        {
            if (!keysOf.TryGetValue(text, out var keys))
            {
                keysOf[text] = keys = [];
                order.Add(text);
            }
            keys.Add(key);
        }

        return [.. order.Select(text => new ScriptShownLine(keysOf[text], text))];
    }
}
