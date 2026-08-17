using System.Numerics;
using System.Text.RegularExpressions;

namespace FrenAlerts.Engine.Alerts;

// One run of a call's words that shares a colour.
public readonly record struct CallPiece(string Text, Vector4? Color);

public static partial class CallText
{
    [GeneratedRegex(@"<(blue|red|green|yellow|white|orange)>(.*?)</\1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Tagged();

    // The arrow a sequence is written with, drawn as an arrow.
    //
    // Their fights write one thing following another as "Spread => Stack", and two
    // characters standing in for a glyph looks like two characters standing in for a
    // glyph. swix asked for the real one.
    //
    // Here rather than in the fights themselves. There are 169 of them across nine
    // English files, those files are replaced wholesale whenever their fights are pulled
    // again, and 169 edits would go with them. One place instead, and a fight pulled in
    // tomorrow arrives with it already done. It catches the handful written `->` too,
    // which are the same thing spelled a second way.
    //
    // The screen gets the arrow. The voice gets a word, because a voice handed any of
    // these reads out the punctuation: "Bait Puddle equals greater than Spread".
    public const string Arrow = "→";

    // The symbol only, with the spacing left as written, so a line reads on screen the
    // way whoever wrote it spaced it.
    //
    // Matched rather than replaced twice: Replace("=>") on Dancing Mad's "==>" left the
    // spare character behind and drew "= →".
    [GeneratedRegex(@"=+>|-+>")]
    private static partial Regex Drawn();

    private static string Arrows(string text) =>
        text.Contains('>') ? Drawn().Replace(text, Arrow) : text;

    // What the voice says where the screen shows a symbol.
    //
    // The words the symbols already mean, which is what somebody reading the line aloud
    // would say. An arrow is one thing after another: "Bait Puddle then Spread". A plus
    // is two things at once: "Spread East and Avoid Cleave".
    //
    // All three spellings of the arrow, because the screen's own substitution is not the
    // only way one arrives: a fight pulled in tomorrow can carry it already written.
    public const string Said = "then";
    public const string Also = "and";

    // One or more dashes, because Dancing Mad writes one of them "==>" and matching
    // only the two characters left the spare one behind: "Counterclockwise = then".
    [GeneratedRegex(@"\s*(?:=+>|-+>|→)\s*")]
    private static partial Regex Pointing();

    // Spaces both sides, which is how all 124 of them are written across the fights.
    // A plus with a word hard against it is somebody's shorthand rather than a join,
    // and "HP+" read as "HP and" would be worse than leaving it be.
    [GeneratedRegex(@"\s+\+\s+")]
    private static partial Regex Joining();

    public static string Speak(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        text = Pointing().Replace(text, $" {Said} ");
        text = Joining().Replace(text, $" {Also} ");
        return text.Trim();
    }

    // A call split into its coloured runs.
    //
    // Their words can colour a piece of themselves, written as <red>this</red>, and a
    // call that shows the tags is a call reading out punctuation nobody wrote. Split
    // here so the screen draws the runs without them.
    public static List<CallPiece> Pieces(string text)
    {
        var pieces = new List<CallPiece>();
        if (string.IsNullOrEmpty(text))
        {
            pieces.Add(new CallPiece("", null));
            return pieces;
        }

        // Before the tags are read, so a run's own words carry it too. No tag contains
        // either arrow, so nothing here can damage one.
        text = Arrows(text);

        var at = 0;
        foreach (Match hit in Tagged().Matches(text))
        {
            if (hit.Index > at) pieces.Add(new CallPiece(text[at..hit.Index], null));
            pieces.Add(new CallPiece(hit.Groups[2].Value, CallLook.Tag(hit.Groups[1].Value)));
            at = hit.Index + hit.Length;
        }

        if (at < text.Length) pieces.Add(new CallPiece(text[at..], null));
        if (pieces.Count == 0) pieces.Add(new CallPiece(text, null));

        return pieces;
    }

    // The same words with the tags taken out, for measuring what is about to be drawn.
    //
    // Not for reading aloud, whatever this used to say. Every voice call site hands over
    // `Call.Spoken` or `Call.Speech` and none of them come through here, which is the
    // whole reason the arrow above can be a drawing decision on its own.
    public static string Plain(string text)
    {
        var plain = new System.Text.StringBuilder(text.Length);
        foreach (var piece in Pieces(text)) plain.Append(piece.Text);
        return plain.ToString();
    }

    public static string Sentence(string text)
    {
        if (text.Length == 0) return text;

        var first = text[0];
        if (!char.IsLetter(first) || char.IsUpper(first)) return text;

        return char.ToUpperInvariant(first) + text[1..];
    }
}
