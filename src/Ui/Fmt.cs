using System;

namespace FrenMits.Ui;

// The plugin's m:ss time text, shared by every window.
public static class Fmt
{
    // Nearest-second m:ss, the overlay countdown style.
    public static string MmssRound(float seconds)
    {
        var s = (int)MathF.Round(seconds);
        return $"{s / 60}:{s % 60:00}";
    }

    // Floor m:ss, so 1:59.9 still reads 1:59.
    public static string MmssFloor(float seconds)
    {
        var s = (int)seconds;
        return $"{s / 60}:{s % 60:00}";
    }

    // Nearest-second m:ss with a minus for pre-pull rows.
    public static string MmssSigned(float seconds)
    {
        var s = (int)MathF.Round(seconds);
        var sign = s < 0 ? "-" : "";
        s = Math.Abs(s);
        return $"{sign}{s / 60}:{s % 60:00}";
    }
}
