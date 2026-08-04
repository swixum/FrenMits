using System;

namespace FrenMits.Encounters;

// The logging seam for encounter data and algorithms, wired to Dalamud by the
// host at startup and silent everywhere else (the offline test host).
public static class EncounterLog
{
    public static Action<string> Info { get; set; } = _ => { };
    public static Action<string> Warn { get; set; } = _ => { };
    public static Action<string, Exception> Error { get; set; } = (_, _) => { };

    // Deliberately swallowed errors, wired to Swallowed.Report for its throttling.
    public static Action<string, Exception> Failed { get; set; } = (_, _) => { };
}
