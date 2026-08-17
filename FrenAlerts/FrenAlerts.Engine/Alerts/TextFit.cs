namespace FrenAlerts.Engine;

// Shortening a line to the room it actually has.
//
// The config window draws its rows with ImGui.TextUnformatted, which neither wraps
// nor clips: a line wider than the panel is drawn straight off the side of it. Every
// line written by hand was measured and made to fit, and that held right up until a
// line was built rather than written. "Your answer "{chosen}" is not one this fight
// offers any more" is 65 characters plus whatever somebody typed into a box that
// takes 192, and a file path in a trigger has no length at all.
//
// So the width is measured instead of budgeted. The caller passes the room and a way
// to measure text in it, which is ImGui's own CalcTextSize on the font actually being
// drawn with, and gets back something that fits.
public static class TextFit
{
    // Three dots rather than the one character, which is prettier and is not known to
    // be in the font. The glyph range Dalamud loads is a fact about their build, this
    // plugin renders no U+2026 anywhere today, and a missing glyph draws as a box.
    public const string Cut = "...";

    // The longest head of the text that fits in the room, with the marker on the end
    // of it. Unchanged when the whole thing already fits, and empty when there is not
    // even room for the marker: half a word and no sign that anything was dropped is
    // worse than nothing, because it reads as the whole answer.
    public static string Fit(string text, float room, Func<string, float> widthOf)
    {
        if (text.Length == 0 || room <= 0f) return "";
        if (widthOf(text) <= room) return text;
        if (widthOf(Cut) > room) return "";

        // The widest head that still fits once the marker is on it. Measured rather
        // than divided: a proportional font makes "iii" and "WWW" the same count of
        // characters and nothing like the same width.
        var lo = 0;
        var hi = text.Length - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (widthOf(text[..mid] + Cut) <= room) lo = mid;
            else hi = mid - 1;
        }

        // Trimmed, so it reads "the fight offers..." rather than "the fight offers ...".
        return text[..lo].TrimEnd() + Cut;
    }
}
