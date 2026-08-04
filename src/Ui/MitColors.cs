using System;

namespace FrenMits.Ui;

// The configured chip color for a mit kind. Keeps the settings lookup on the
// host side so the classifier itself stays free of Configuration.
public static class MitColors
{
    public static uint Color(MitTypes.Kind kind, Configuration c)
        => MitTypes.Color(kind, c.MitColorParty, c.MitColorTank, c.MitColorPersonal);
}
