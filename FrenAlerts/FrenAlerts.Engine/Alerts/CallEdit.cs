namespace FrenAlerts.Engine.Alerts;

public sealed class CallEdit
{
    public bool Off { get; set; }

    public bool? On { get; set; }

    public string? Text { get; set; }

    public CallLevel? Level { get; set; }

    // Shown but not said. Separate from Off, which stops the call happening at all:
    // this is for the ones worth a glance and not worth a sentence.
    public bool Silent { get; set; }

    public bool IsDefault =>
        !Off && On is null && Text is null && Level is null && !Silent;

    public bool Speaks(bool shipped) => On ?? (Off ? false : shipped);

    public CallEdit Copy() => new()
    {
        Off = Off, On = On, Text = Text, Level = Level, Silent = Silent,
    };
}

public static class CallEdits
{
    public const string Target = "{target}";

    public static Call? Apply(Call call, CallEdit? edit, string shipped = "")
    {
        if (edit is null || edit.IsDefault) return call;
        if (edit.Off) return null;

        var text = call.Text;
        if (edit.Text is { Length: > 0 } wanted)
        {
            text = wanted.Contains(Target, StringComparison.Ordinal)
                ? Retarget(shipped, call.Text, wanted) ?? text
                : wanted;
        }

        // Time and Hold are carried through untouched: what an edit can change is
        // what the call says and how loudly, never when it happens.
        return call with
        {
            Text = text,
            Level = edit.Level ?? call.Level,
        };
    }

    public static string? Retarget(string shipped, string shown, string wanted)
    {
        var at = shipped.IndexOf(Target, StringComparison.Ordinal);
        if (at < 0) return null;

        var before = shipped[..at];
        var after = shipped[(at + Target.Length)..];
        if (shown.Length < before.Length + after.Length) return null;
        if (!shown.StartsWith(before, StringComparison.Ordinal)) return null;
        if (!shown.EndsWith(after, StringComparison.Ordinal)) return null;

        var name = shown[before.Length..(shown.Length - after.Length)];
        return wanted.Replace(Target, name, StringComparison.Ordinal);
    }
}
