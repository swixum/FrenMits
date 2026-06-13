using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

public enum JobRole
{
    Tank,
    Healer,
    Melee,
    PhysicalRanged,
    Caster
}

public readonly record struct JobInfo(uint RowId, string Abbreviation, string Name, JobRole Role);

// Static job table keyed by ClassJob RowId so we never depend on Lumina row
// shape changing between game patches.
public static class Jobs
{
    public static readonly IReadOnlyList<JobInfo> All = new List<JobInfo>
    {
        new(19, "PLD", "Paladin", JobRole.Tank),
        new(21, "WAR", "Warrior", JobRole.Tank),
        new(32, "DRK", "Dark Knight", JobRole.Tank),
        new(37, "GNB", "Gunbreaker", JobRole.Tank),

        new(24, "WHM", "White Mage", JobRole.Healer),
        new(28, "SCH", "Scholar", JobRole.Healer),
        new(33, "AST", "Astrologian", JobRole.Healer),
        new(40, "SGE", "Sage", JobRole.Healer),

        new(20, "MNK", "Monk", JobRole.Melee),
        new(22, "DRG", "Dragoon", JobRole.Melee),
        new(30, "NIN", "Ninja", JobRole.Melee),
        new(34, "SAM", "Samurai", JobRole.Melee),
        new(39, "RPR", "Reaper", JobRole.Melee),
        new(41, "VPR", "Viper", JobRole.Melee),

        new(23, "BRD", "Bard", JobRole.PhysicalRanged),
        new(31, "MCH", "Machinist", JobRole.PhysicalRanged),
        new(38, "DNC", "Dancer", JobRole.PhysicalRanged),

        new(25, "BLM", "Black Mage", JobRole.Caster),
        new(27, "SMN", "Summoner", JobRole.Caster),
        new(35, "RDM", "Red Mage", JobRole.Caster),
        new(42, "PCT", "Pictomancer", JobRole.Caster),
        new(36, "BLU", "Blue Mage", JobRole.Caster),
    };

    public static readonly string[] Abbreviations = All.Select(j => j.Abbreviation).ToArray();

    public static JobInfo? ByRowId(uint rowId)
    {
        foreach (var j in All)
            if (j.RowId == rowId) return j;
        return null;
    }

    public static JobInfo? ByAbbreviation(string? abbr)
    {
        if (string.IsNullOrWhiteSpace(abbr)) return null;
        foreach (var j in All)
            if (string.Equals(j.Abbreviation, abbr, StringComparison.OrdinalIgnoreCase)) return j;
        return null;
    }

    public static IEnumerable<string> AbbreviationsForRole(JobRole role)
        => All.Where(j => j.Role == role).Select(j => j.Abbreviation);
}
