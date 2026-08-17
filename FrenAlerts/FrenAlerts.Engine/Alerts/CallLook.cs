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

    public static float OutlineWidth(float px) => MathF.Max(2f, px * 0.055f);

    // The colours behind a call, alpha carried in from the fade.
    public static Vector4 ShadowColor(float alpha) => new(0f, 0f, 0f, 0.3f * alpha);
    public static Vector4 BackTop(float alpha) => new(0.08f, 0.08f, 0.11f, 0.72f * alpha);
    public static Vector4 BackBottom(float alpha) => new(0.02f, 0.02f, 0.04f, 0.78f * alpha);
    public const float BorderAlpha = 0.7f;
    public const float BorderWidth = 1.5f;
    public const float ShadowDrop = 3f;

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
