namespace FrenAlerts.Engine.Scripts;

// The macro text their Dancing Mad shotcalls ship with, ported.
//
// Three of that fight's calls resolve to one of several answers, and the group needs
// the answer in party chat rather than only on the caller's screen: which gaze is
// real, whether the fire or the water is the one to dodge, and which of the two
// mystery elements is lying. These are the lines their plugin fills in for those.
//
// Filled in rather than forced: a line somebody has already written is left alone,
// and the whole seed is applied once and remembered by name, so editing one and
// restarting does not put theirs back.
public static class ShotcallSeed
{
    public const string Name = "shotcall_macro_text_v3";

    // Trigger, the output key inside it, and the words.
    public static readonly (string Trigger, string Key, string Text)[] Lines =
    [
        ("DMU P4 Shotcall Gaze", "realGaze1", "Gaze1: Look OUT."),
        ("DMU P4 Shotcall Gaze", "fakeGaze1", "Gaze1: Look INSIDE."),
        ("DMU P4 Shotcall Gaze", "realGaze2", "Gaze2: Look OUT."),
        ("DMU P4 Shotcall Gaze", "fakeGaze2", "Gaze2: Look INSIDE."),
        ("DMU P4 Shotcall Chaos", "realInferno", "Fire is AOE (dodge)"),
        ("DMU P4 Shotcall Chaos", "fakeInferno", "Fire is DYNAMO (stay)"),
        ("DMU P4 Shotcall Chaos", "realTsunami", "Water is DYNAMO (stay)"),
        ("DMU P4 Shotcall Chaos", "fakeTsunami", "Water is AOE (dodge)"),
        ("DMU P4 Shotcall Mystery Magic", "trueIceTrueThunder",
            "TRUE ice (Cones) / TRUE lightning (Lines)"),
        ("DMU P4 Shotcall Mystery Magic", "fakeIceTrueThunder",
            "FAKE ice (Cones) / TRUE lightning (Lines)"),
        ("DMU P4 Shotcall Mystery Magic", "trueIceFakeThunder",
            "TRUE ice (Cones) / FAKE lightning (Lines)"),
        ("DMU P4 Shotcall Mystery Magic", "fakeIceFakeThunder",
            "FAKE ice (Cones) / FAKE lightning (Lines)"),
    ];

    // Triggers they renamed, and what somebody's own macro text follows to. Without
    // this the words a raider wrote stay attached to a trigger id nothing fires any
    // more, and the call goes quiet with no way to see why.
    public static readonly (string Was, string Now)[] Renamed =
    [
        ("DMU P4 Kefka Says Cursed Shriek (Applied)", "DMU P4 Shotcall Gaze"),
        ("DMU P4 Tsunami/Inferno", "DMU P4 Shotcall Chaos"),
        ("DMU P4 Mystery Magic Ice and Thunder", "DMU P4 Shotcall Mystery Magic"),
    ];

    // Returns false when it has been applied before, so the caller can skip saving.
    public static bool Apply(ScriptOverrides overrides, ISet<string> applied)
    {
        if (!applied.Add(Name)) return false;

        foreach (var (trigger, key, text) in Lines)
        {
            var over = overrides.Ensure(trigger, key);
            if (string.IsNullOrEmpty(over.MacroText)) over.MacroText = text;
        }

        foreach (var (was, now) in Renamed) Move(overrides, was, now);

        return true;
    }

    // Whatever was set on the old id moves to the new one, and the old one is left
    // switched off rather than deleted: two copies posting the same line is the one
    // outcome worse than none.
    private static void Move(ScriptOverrides overrides, string was, string now)
    {
        foreach (var (trigger, key, _) in Lines)
        {
            if (trigger != now) continue;

            var old = overrides.Find(was, key);
            if (old is null) continue;

            if (old.MacroOn) overrides.Ensure(now, key).MacroOn = true;
            if (!string.IsNullOrEmpty(old.MacroText)) overrides.Ensure(now, key).MacroText = old.MacroText;

            old.MacroOn = false;
            old.MacroText = "";
        }
    }
}
