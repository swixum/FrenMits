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

    // A call split into its coloured runs.
    //
    // Their words can colour a piece of themselves, written as <red>this</red>, and a
    // call that shows the tags is a call reading out punctuation nobody wrote. Split
    // here so the screen draws the runs and the voice reads the words without them.
    public static List<CallPiece> Pieces(string text)
    {
        var pieces = new List<CallPiece>();
        if (string.IsNullOrEmpty(text))
        {
            pieces.Add(new CallPiece("", null));
            return pieces;
        }

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

    // The same words with the tags taken out, for measuring and for reading aloud.
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
