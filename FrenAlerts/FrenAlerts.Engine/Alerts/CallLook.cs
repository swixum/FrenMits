using System.Numerics;

namespace FrenAlerts.Engine.Alerts;

// How a call is drawn, carried over from the plugin the fights came from.
//
// Every number here is theirs: the size a call starts at, how it grows into place, how
// thick the outline is, where the icon sits, what the slab behind it is made of. Kept
// apart from the drawing so the arithmetic can be run rather than looked at, because
// what a screen does with these is the one thing a test cannot see.
//
// Theirs also runs a bar under a counted call with the seconds beside it. That is the
// one thing of theirs not carried over: the seconds are in brackets after the words
// instead, and nothing else moves while a call is up.
public static class CallLook
{
    // The size a call is drawn at before anything scales it, in points at 100%.
    public const float BasePx = 30f;

    // The gap between two calls in the stack.
    public const float StackGap = 12f;

    // How long a call takes to arrive, and how long it takes to leave.
    public const float PopSeconds = 0.18f;
    public const float FadeSeconds = 0.4f;

    // It arrives at 85% and grows to full, which is what makes one land rather than
    // appear.
    public const float PopFrom = 0.85f;

    // Sixteen offsets around the letter, so the outline is a ring rather than four
    // corners. Theirs, in their order.
    public static readonly (float X, float Y)[] Ring =
    [
        (-1f, 0f), (1f, 0f), (0f, -1f), (0f, 1f),
        (-0.7f, -0.7f), (0.7f, -0.7f), (-0.7f, 0.7f), (0.7f, 0.7f),
        (-0.4f, -0.92f), (0.4f, -0.92f), (-0.4f, 0.92f), (0.4f, 0.92f),
        (-0.92f, -0.4f), (0.92f, -0.4f), (-0.92f, 0.4f), (0.92f, 0.4f),
    ];

    // Smoothstep over the first fifth of a second.
    public static float Grown(float age)
    {
        var t = Math.Clamp(age / PopSeconds, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    // The size it is drawn at this frame: 85% growing to full.
    public static float ScaleAt(float age) => PopFrom + (1f - PopFrom) * Grown(age);

    // How solid it is: up with the pop, and away over the last fraction of a second.
    public static float AlphaAt(float age, float remaining)
    {
        var alpha = Grown(age);
        if (remaining < FadeSeconds) alpha *= Math.Clamp(remaining / FadeSeconds, 0f, 1f);
        return alpha;
    }

    public static bool WorthDrawing(float alpha) => alpha > 0.01f;

    // ---- the shapes around the words, all in fractions of the drawn size ----

    public const float IconSize = 0.95f;
    public const float IconGap = 0.24f;

    public const float PadX = 0.5f;
    public const float PadY = 0.28f;
    public const float Round = 0.28f;

    // ---- how much room a stack of them takes ----
    //
    // The slab is drawn past the text box rather than laid out, so nothing that sizes a
    // box around a call can work it out from the layout. Written here, once, because
    // three places need the same answer and each had its own guess: the overlay window,
    // the config window's two preview boxes. The previews used 2.9 and 1.6 times the
    // font size, which were close at the size they were picked at and cut the slab off
    // at any other.

    // How far the slab reaches past the text box, per side.
    public static float SlabX(float px) => px * PadX;
    public static float SlabY(float px) => px * PadY;

    // From one call's text box to the next. StackGap of clear air, plus the two slabs
    // between them where there is a background to draw.
    public static float StackStep(float px, bool withBackground) =>
        StackGap * (px / BasePx) + (withBackground ? 2f * SlabY(px) : 0f);

    // The layout height of a stack: the text boxes and what goes between them, with
    // the slab's overhang left out. Whatever hosts it pads by SlabY top and bottom,
    // and then the overhang has somewhere to go.
    //
    // itemSpacing is what the layout puts between two items by itself, which is on top
    // of the gap rather than instead of it.
    public static float StackHeight(int calls, float px, float lineHeight,
        float itemSpacing, bool withBackground)
    {
        if (calls <= 0) return 0f;
        return calls * lineHeight
               + (calls - 1) * (StackStep(px, withBackground) + itemSpacing);
    }

    public static float OutlineWidth(float px) => MathF.Max(2f, px * 0.055f);

    // The colours behind a call, alpha carried in from the fade.
    public static Vector4 ShadowColor(float alpha) => new(0f, 0f, 0f, 0.3f * alpha);
    public static Vector4 BackTop(float alpha) => new(0.08f, 0.08f, 0.11f, 0.72f * alpha);
    public static Vector4 BackBottom(float alpha) => new(0.02f, 0.02f, 0.04f, 0.78f * alpha);
    public const float BorderAlpha = 0.7f;
    public const float BorderWidth = 1.5f;
    public const float ShadowDrop = 3f;

    // ---- colours somebody can actually tell apart ----
    //
    // The three levels ship light blue, amber and red. Amber against red is the one
    // pair the common deficiencies collapse, and those two are Alert against Alarm:
    // "move" against "move now". Colorblind Mode swapped the window's own status dots
    // and left the calls alone, which is the half nobody looks at during a pull.
    //
    // Okabe-Ito, the same palette Theme already switches to, so the window and the
    // calls agree rather than each having their own idea of safe.
    public const uint SafeInfo = 0xFFE9B456;    // #56B4E9 sky blue
    public const uint SafeAlert = 0xFF009FE6;   // #E69F00 orange
    public const uint SafeAlarm = 0xFFA779CC;   // #CC79A7 reddish purple

    // The safe colour in place of the shipped one, and nothing else.
    //
    // A colour somebody picked is a colour somebody picked: turning the setting on
    // must not quietly throw away a palette they chose on purpose. So this only ever
    // replaces a default that is still sitting where it shipped.
    public static uint Safely(uint chosen, uint shipped, uint safe) =>
        chosen == shipped ? safe : chosen;

    // A word in a colour of its own, written into a call as <red>this</red>. Their
    // tags and their colours.
    public static Vector4? Tag(string tag) => tag.ToLowerInvariant() switch
    {
        "blue" => new Vector4(0.42f, 0.74f, 1f, 1f),
        "red" => new Vector4(1f, 0.38f, 0.34f, 1f),
        "green" => new Vector4(0.52f, 0.95f, 0.56f, 1f),
        "yellow" => new Vector4(1f, 0.86f, 0.36f, 1f),
        "orange" => new Vector4(1f, 0.62f, 0.2f, 1f),
        "white" => new Vector4(1f, 1f, 1f, 1f),
        _ => null,
    };
}
