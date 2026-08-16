namespace FrenAlerts.Engine;

public readonly record struct Position(float X, float Y, float Elevation, float Heading)
{
    public static readonly Position None = new(float.NaN, float.NaN, float.NaN, float.NaN);

    // Angle from here to there, in the convention measured above.
    public static float Facing(Position from, Position to) =>
        MathF.Atan2(to.X - from.X, to.Y - from.Y);

    // A line that carried no position parses to None rather than to the origin,
    // which is a real spot on every arena and would read as the middle.
    public bool Known => !float.IsNaN(X);
}
