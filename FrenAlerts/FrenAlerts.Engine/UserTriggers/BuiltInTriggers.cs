using System.Numerics;

namespace FrenAlerts.Engine.UserTriggers;

// The sets that ship with the plugin, ported from theirs.
//
// Four sets of the calls that are not fight specific: who used a mitigation, who
// used an invuln, and the two debuffs that kill somebody who does not notice them.
// Every action id, colour, scale and duration is theirs.
//
// Off by default, the same way theirs ship. Somebody who installs a boss mod did
// not ask to be told about every Reprisal in the party.
public static class BuiltInTriggers
{
    // Bumped when the shipped list changes, so a config written against an older one
    // can be topped up without overwriting what somebody edited.
    public const int Revision = 7;

    private static readonly Vector4 Green = new(0.4f, 0.92f, 0.45f, 1f);
    private static readonly Vector4 Blue = new(0.45f, 0.8f, 1f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.8f, 0.3f, 1f);
    private static readonly Vector4 Red = new(1f, 0.3f, 0.3f, 1f);

    public static List<UserTriggerSet> Build() =>
    [
        new()
        {
            Id = "builtin.general.utility",
            Name = "Tank & Raid Utility",
            Category = "General",
            BuiltIn = true,
            Enabled = false,
            Triggers =
            [
                Cast("builtin.provoke", "Provoke", 7533, "Provoke {player}", Green),
                Cast("builtin.shirk", "Shirk", 7537, "Shirk {player}", Green, RoleMask.Tank),
                Cast("builtin.reprisal", "Reprisal", 7535, "Reprisal {player}", Blue),
                Cast("builtin.addle", "Addle", 7560, "Addle {player}", Blue),
                Cast("builtin.feint", "Feint", 7549, "Feint {player}", Blue),
            ],
        },
        new()
        {
            Id = "builtin.general.invulns",
            Name = "Tank Invulns",
            Category = "General",
            BuiltIn = true,
            Enabled = false,
            Triggers =
            [
                Cast("builtin.hallowed", "Hallowed Ground", 30, "Hallowed Ground {player}", Gold),
                Cast("builtin.holmgang", "Holmgang", 43, "Holmgang {player}", Gold),
                Cast("builtin.livingdead", "Living Dead", 3638, "Living Dead {player}", Gold),
                Cast("builtin.superbolide", "Superbolide", 16152, "Superbolide {player}", Gold),
            ],
        },
        new()
        {
            Id = "builtin.personal.danger",
            Name = "Danger Debuffs (on you)",
            Category = "Personal",
            BuiltIn = true,
            Enabled = false,
            Triggers =
            [
                Status("builtin.doom", "Doom", "Doom", "Doom on YOU", Red),
                Status("builtin.dmgdown", "Damage Down", "Damage Down", "Damage Down", Gold),
            ],
        },
    ];

    // Matched by action id rather than by name, so it reads the same in every
    // language. The name is kept as the pattern anyway, which is what the editor
    // shows somebody who opens it.
    private static UserTrigger Cast(
        string id, string name, uint actionId, string text, Vector4 color,
        RoleMask self = RoleMask.None) =>
        new()
        {
            Id = id,
            Name = name,
            On = TriggerMatch.Cast,
            MatchById = true,
            DataId = actionId,
            Pattern = name,
            Source = SourceFilter.Anyone,
            SelfRoles = self,
            Text = text,
            TtsText = text,
            TtsEnabled = true,
            TextEnabled = true,
            Color = color,
            Scale = 1.6f,
            Duration = 3f,
        };

    private static UserTrigger Status(
        string id, string name, string pattern, string text, Vector4 color) =>
        new()
        {
            Id = id,
            Name = name,
            On = TriggerMatch.StatusGain,
            Pattern = pattern,
            OnlyOnSelf = true,
            Source = SourceFilter.Anyone,
            Text = text,
            TtsText = text,
            TtsEnabled = true,
            TextEnabled = true,
            Color = color,
            Scale = 2f,
            Duration = 4f,
        };
}
