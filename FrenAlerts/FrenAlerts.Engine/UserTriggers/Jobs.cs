using System.Numerics;

namespace FrenAlerts.Engine.UserTriggers;

// The job table, ported from theirs.
//
// Every number here is theirs: the class job id each icon is offset from, the colour
// per job and the role it belongs to. Kept because three different things need the
// same answer and would otherwise each carry their own half-right copy: a call that
// says a job out loud, a party list that colours by role, and an icon beside a name.
public static class Jobs
{
    public static readonly Vector4 TankColor = new(0.36f, 0.6f, 0.95f, 1f);
    public static readonly Vector4 HealerColor = new(0.36f, 0.82f, 0.45f, 1f);
    public static readonly Vector4 DpsColor = new(0.9f, 0.36f, 0.36f, 1f);
    public static readonly Vector4 OtherColor = new(0.62f, 0.62f, 0.66f, 1f);

    private static readonly Dictionary<string, JobFacts> Table = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PLD"] = new(JobRole.Tank, 19, Rgb(0xA8D2E6), "Paladin"),
        ["WAR"] = new(JobRole.Tank, 21, Rgb(0xCF2621), "Warrior"),
        ["DRK"] = new(JobRole.Tank, 32, Rgb(0xD126CC), "Dark Knight"),
        ["GNB"] = new(JobRole.Tank, 37, Rgb(0x796D30), "Gunbreaker"),
        ["WHM"] = new(JobRole.Healer, 24, Rgb(0xB0C0C4), "White Mage"),
        ["SCH"] = new(JobRole.Healer, 28, Rgb(0x8657FF), "Scholar"),
        ["AST"] = new(JobRole.Healer, 33, Rgb(0xE5C546), "Astrologian"),
        ["SGE"] = new(JobRole.Healer, 40, Rgb(0x80A0F0), "Sage"),
        ["MNK"] = new(JobRole.Dps, 20, Rgb(0xD69C00), "Monk"),
        ["DRG"] = new(JobRole.Dps, 22, Rgb(0x4164CD), "Dragoon"),
        ["NIN"] = new(JobRole.Dps, 30, Rgb(0xAF1964), "Ninja"),
        ["SAM"] = new(JobRole.Dps, 34, Rgb(0xE46D04), "Samurai"),
        ["RPR"] = new(JobRole.Dps, 39, Rgb(0x965A90), "Reaper"),
        ["VPR"] = new(JobRole.Dps, 41, Rgb(0x108210), "Viper"),
        ["BRD"] = new(JobRole.Dps, 23, Rgb(0x91BA5E), "Bard"),
        ["MCH"] = new(JobRole.Dps, 31, Rgb(0x6EE1D6), "Machinist"),
        ["DNC"] = new(JobRole.Dps, 38, Rgb(0xE2B0AF), "Dancer"),
        ["BLM"] = new(JobRole.Dps, 25, Rgb(0xA579D6), "Black Mage"),
        ["SMN"] = new(JobRole.Dps, 27, Rgb(0x2D9B78), "Summoner"),
        ["RDM"] = new(JobRole.Dps, 35, Rgb(0xE87B7B), "Red Mage"),
        ["PCT"] = new(JobRole.Dps, 42, Rgb(0xFC92E1), "Pictomancer"),
        ["BLU"] = new(JobRole.Dps, 36, Rgb(0x2459FF), "Blue Mage"),
    };

    public static JobFacts Get(string abbreviation) =>
        !string.IsNullOrEmpty(abbreviation) && Table.TryGetValue(abbreviation, out var facts)
            ? facts
            : new JobFacts(JobRole.Other, 0, OtherColor, "");

    public static Vector4 ColorOf(JobRole role) => role switch
    {
        JobRole.Tank => TankColor,
        JobRole.Healer => HealerColor,
        JobRole.Dps => DpsColor,
        _ => OtherColor,
    };

    public static IReadOnlyCollection<string> Known => Table.Keys;

    // The short code for a class job id, which is how their fights name a job.
    public static string CodeOf(uint classJob)
    {
        foreach (var (code, facts) in Table)
            if (facts.ClassJob == classJob) return code;
        return "";
    }

    private static Vector4 Rgb(uint rgb) => new(
        ((rgb >> 16) & 0xFF) / 255f,
        ((rgb >> 8) & 0xFF) / 255f,
        (rgb & 0xFF) / 255f,
        1f);
}

// The icon id is the class job id offset by their base, which is how the game
// numbers the job icons.
public readonly struct JobFacts(JobRole role, int classJobId, Vector4 color, string fullName)
{
    public readonly JobRole Role = role;

    public readonly uint ClassJob = classJobId > 0 ? (uint)classJobId : 0;

    public readonly uint IconId = classJobId > 0 ? (uint)(62000 + classJobId) : 0;

    public readonly Vector4 Color = color;

    public readonly string FullName = fullName;
}

public enum JobRole : byte
{
    Tank,
    Healer,
    Dps,
    Other,
}
