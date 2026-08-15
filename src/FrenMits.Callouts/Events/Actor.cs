using System;

namespace FrenMits.Callouts;

// A spot on the arena floor. Z is elevation and rarely matters.
public readonly record struct Spot(float X, float Y, float Z)
{
    public static readonly Spot Nowhere = new(float.NaN, float.NaN, float.NaN);

    public bool Known => !float.IsNaN(X) && !float.IsNaN(Y);

    // Flat distance, ignoring elevation, which is what mechanics care about.
    public float DistanceTo(Spot other)
        => MathF.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));
}

// Who acted or who was hit, and where they stood at the time.
public readonly record struct Actor(uint Id, string Name, uint NameId, Spot At, float Heading)
{
    public static readonly Actor Nobody = new(0, "", 0, Spot.Nowhere, 0f);

    public bool Known => Id != 0 || Name.Length > 0;

    // Player object ids start at 0x10000000; everything above that is spawned.
    public bool IsPlayer => (Id & 0xF0000000u) == 0x10000000u;
}
