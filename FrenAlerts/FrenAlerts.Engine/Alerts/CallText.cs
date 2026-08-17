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
    // The screen only, and deliberately. The voice is handed `Call.Spoken`, which never
    // comes through here, so what is read out during a pull is byte for byte what it was
    // before this existed.
    public const string Arrow = "→";

    private static string Arrows(string text) =>
        text.Contains('>') ? text.Replace("=>", Arrow).Replace("->", Arrow) : text;

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
