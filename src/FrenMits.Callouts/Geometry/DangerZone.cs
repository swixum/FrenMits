using System;

namespace FrenMits.Callouts;

// The ground a telegraphed action covers. Built from the game's own numbers plus
// where the caster stood, so it works on a fight nobody has ever seen.
//
// Heading follows the log: zero faces south and turns clockwise as it goes
// negative, so the caster's forward is (sin h, cos h).
public readonly record struct DangerZone(Spot Origin, float Heading, ActionShape Shape)
{
    // Cast types measured against real hits: 12 is a line out of the caster and
    // 3, 13 are cones out of the caster. Circles (type 2) are placed on a target
    // rather than the caster, so they need that target's spot as the origin.
    public bool CasterOrigin => FromCaster(Shape);

    // The same question before a zone exists, which is when the origin is still
    // being picked. One list, so the two answers cannot drift apart.
    public static bool FromCaster(ActionShape shape) => shape.CastType is 3 or 4 or 5 or 8 or 12 or 13;

    public bool Covers(Spot at) => Clearance(at) <= 0f;

    // How far a spot sits outside the danger, or zero when it is in it. Never
    // an overestimate: a curved edge is measured to its tangent, so a spot this
    // says is four yalms clear is at least four yalms clear. Room to spare is
    // what turns a legal answer into a survivable one.
    public float Clearance(Spot at)
    {
        if (!Shape.Known || !at.Known || !Origin.Known) return Clear;

        var dx = at.X - Origin.X;
        var dy = at.Y - Origin.Y;
        var distance = MathF.Sqrt(dx * dx + dy * dy);

        return Shape.CastType switch
        {
            2 or 5 or 6 or 7 => MathF.Max(0f, distance - Shape.Range),
            10 => OutsideRing(distance),
            4 or 8 or 12 => OutsideLine(dx, dy),
            3 or 13 => OutsideCone(dx, dy, distance),
            _ => Clear,
        };
    }

    // What an unmodelled shape reports. Nothing claims to be dangerous unless
    // its cast type is one we have measured.
    public const float Clear = float.MaxValue;

    // Why a spot is not in the danger, in the words a diagnostic needs. A miss
    // that cannot be explained is a miss nobody can fix.
    public string Why(Spot at)
    {
        if (!Shape.Known) return "no shape";
        if (!at.Known || !Origin.Known) return "no position";

        return Shape.CastType switch
        {
            2 or 5 or 6 or 7 => "past the radius",
            10 when Shape.Width <= 0f => "donut, hole unknown",
            10 => "inside the hole or past the rim",
            4 or 8 or 12 when Shape.Width <= 0f => "line, width unknown",
            4 or 8 or 12 => "beside or behind the line",
            3 or 13 when Shape.Angle <= 0f => "cone, spread unknown",
            3 or 13 => "outside the spread or past the range",
            _ => $"cast type {Shape.CastType} not modelled",
        };
    }

    // A donut whose hole nobody measured is not a donut, it is an unknown
    // shape. Treating it as solid would mark the middle dangerous when the
    // middle is the whole answer, and send people out of the one safe ring.
    private float OutsideRing(float distance)
    {
        if (Shape.Width <= 0f) return Clear;
        if (distance > Shape.Range) return distance - Shape.Range;
        if (distance < Shape.Width) return Shape.Width - distance;
        return 0f;
    }

    // A line reaches Range ahead and half of Width to each side. A line with no
    // width is an unknown shape for the same reason a cone with no spread is.
    private float OutsideLine(float dx, float dy)
    {
        if (Shape.Width <= 0f) return Clear;
        var half = Shape.Width / 2f;
        var forward = dx * MathF.Sin(Heading) + dy * MathF.Cos(Heading);
        var sideways = dx * MathF.Cos(Heading) - dy * MathF.Sin(Heading);

        var across = MathF.Max(MathF.Abs(sideways) - half, 0f);
        var along = MathF.Max(MathF.Max(-forward, forward - Shape.Range), 0f);
        return MathF.Sqrt(across * across + along * along);
    }

    // The spread comes from the action's own telegraph or from watching real
    // hits, so a 90 degree cone and a 270 degree one are not treated alike.
    //
    // A cone with no spread is an unknown shape, not a default one. Guessing
    // a middling spread is what had this naming the wrong half of the room for
    // abilities the cone model does not describe at all: measured against real
    // pulls, two of them would have spared under a seventh of the players they
    // really spared. Nothing is a better answer than a plausible shape.
    private float OutsideCone(float dx, float dy, float distance)
    {
        if (Shape.Angle <= 0f) return Clear;
        var half = Shape.Angle * MathF.PI / 360f;

        var off = MathF.Abs(Compass.Wrap(MathF.Atan2(dx, dy) - Heading));
        var beside = off > half ? distance * MathF.Sin(MathF.Min(off - half, MathF.PI / 2f)) : 0f;
        var beyond = distance > Shape.Range ? distance - Shape.Range : 0f;
        return MathF.Max(beside, beyond);
    }

    public static float Normalize(float radians) => Compass.Wrap(radians);
}
