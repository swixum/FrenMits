using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Encounters;

// Optional job mit timers, derived from log clears.
public static class JobExtras
{
    // Steps: a sequence extra carries its action per entry.
    public sealed record Extra(string Job, string Action, float Recast, (int Time, string Mechanic)[] Lines,
        (int Time, string Summon)[]? Steps = null);

    // Baked zone schedules used to live here, hardcoded per job. That data
    // (BRD/MNK/PLD/DNC/MCH/RDM/PCT/SMN for Dancing Mad) now lives in
    // DancingMad(UMAD).json's DefaultActions, the sheet's single source of
    // truth. This dictionary is empty until another zone needs the same
    // baked-schedule mechanism.
    private static readonly Dictionary<uint, Extra[]> ByZone = new();

    public static IReadOnlyList<Extra> For(uint territory)
        => ByZone.TryGetValue(territory, out var e) ? e : Array.Empty<Extra>();

    public static Extra? For(uint territory, string? job)
        => string.IsNullOrEmpty(job)
            ? null
            : For(territory).FirstOrDefault(e => string.Equals(e.Job, job, StringComparison.OrdinalIgnoreCase));

    // Each job's optional extras, for sheets with no baked schedule.
    private static readonly (string Job, string Action, float Recast, int Level)[] Kit =
    {
        ("BRD", "Nature's Minne", 120f, 66),
        ("MNK", "Mantra", 90f, 42),
        ("PLD", "Passage of Arms", 120f, 70),
        ("DNC", "Curing Waltz", 60f, 52),
        ("DNC", "Improvisation", 120f, 80),
        ("MCH", "Dismantle", 120f, 62),
        ("RDM", "Magick Barrier", 120f, 86),
        ("PCT", "Tempera Grassa", 120f, 88),
    };

    // The level a duty syncs to, supplied by the host. 0 means no sync known.
    public static Func<uint, int> SyncLevelOf { get; set; } = _ => 0;

    // Every universal-kit extra for a custom sheet.
    public static IReadOnlyList<Extra> ForCustomSheet(FightProfile fight, string? job)
    {
        if (Builtin.Has(fight.TerritoryId)) return Array.Empty<Extra>();
        if (string.IsNullOrEmpty(job) || fight.CustomRows.Count == 0) return Array.Empty<Extra>();
        // Never suggest an ability the duty sync locks out.
        var sync = SyncLevelOf(fight.TerritoryId);
        var result = new List<Extra>();
        foreach (var kit in Kit.Where(k => string.Equals(k.Job, job, StringComparison.OrdinalIgnoreCase)))
        {
            if (sync > 0 && kit.Level > sync) continue;
            if (ComputeExtra(fight, kit) is { } e) result.Add(e);
        }
        return result;
    }

    // Place one kit ability across a custom sheet's rows.
    private static Extra? ComputeExtra(FightProfile fight, (string Job, string Action, float Recast, int Level) kit)
    {
        // Graded sheets place extras where the fight hurts.
        var pool = fight.CustomRows.Any(r => !r.Buster)
            ? fight.CustomRows.Where(r => !r.Buster).ToList()
            : fight.CustomRows;
        var candidates = pool.Any(r => r.Hurt > 0)
            ? pool.Where(r => r.Hurt > 0)
            : pool;
        var picked = new List<(float Time, string Mechanic)>();
        foreach (var row in candidates.OrderByDescending(r => r.Hurt).ThenBy(r => r.Time))
        {
            if (picked.Any(p => MathF.Abs(p.Time - row.Time) < kit.Recast)) continue;
            picked.Add((row.Time, row.Mechanic));
        }
        var lines = picked.OrderBy(p => p.Time)
            .Select(p => ((int)MathF.Round(p.Time), p.Mechanic)).ToArray();
        return lines.Length == 0 ? null : new Extra(kit.Job, kit.Action, kit.Recast, lines);
    }

    // Everything to offer for this job; baked wins a clash.
    public static IReadOnlyList<Extra> AllFor(FightProfile fight, string? job)
    {
        if (string.IsNullOrEmpty(job)) return Array.Empty<Extra>();
        var result = For(fight.TerritoryId)
            .Where(e => string.Equals(e.Job, job, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var e in ForCustomSheet(fight, job))
            if (!result.Any(r => string.Equals(r.Action, e.Action, StringComparison.OrdinalIgnoreCase)))
                result.Add(e);
        return result;
    }

    // A job-extra line looks exactly like this: yours, gated to one job, and
    // not a personal override of a shared row. Sheet View uses the same test
    // to tag "job extra" rows, so the two stay in lockstep.
    public static bool IsAutoExtra(MitLine line)
    {
        if (line.Jobs.Count == 0 || line.Personal) return false;
        if (line.Custom || line.IsJobExtra) return true;
        
        // Auto-heal / fallback for older saves without IsJobExtra:
        // Any baked line that restricts to a job other than the standard healers is a job extra.
        return line.Jobs.Any(j => j != "WHM" && j != "AST" && j != "SCH" && j != "SGE");
    }

    // Top up a fight's lines with every applicable job-extra schedule, the
    // same way Builtin.UpdateLines tops up the sheet's own baked calls: never
    // duplicate a call already there, and honor a tombstone left by deleting
    // one, so a removal sticks instead of reappearing next frame.
    public static bool EnsureAutoLines(FightProfile fight, string? job)
    {
        if (string.IsNullOrEmpty(job)) return false;
        var extras = AllFor(fight, job);
        if (extras.Count == 0) return false;

        var slot = fight.Slot;
        var added = false;

        void AddIfMissing(float time, string mechanic, string action, string tts)
        {
            if (fight.DeletedCalls.Any(d => string.Equals(d.Slot, slot, StringComparison.OrdinalIgnoreCase)
                    && MathF.Abs(d.Time - time) < 0.9f
                    && string.Equals(d.Action.Trim(), action.Trim(), StringComparison.OrdinalIgnoreCase)))
                return;
            if (fight.Lines.Any(l => string.Equals(l.Action.Trim(), action.Trim(), StringComparison.OrdinalIgnoreCase)
                    && l.Jobs.Contains(job, StringComparer.OrdinalIgnoreCase)
                    && MathF.Abs(l.Time - time) < 0.9f))
                return;
            fight.Lines.Add(new MitLine
            {
                Time = time,
                Mechanic = mechanic,
                Action = action,
                Tts = tts,
                Jobs = new List<string> { job },
                Enabled = true,
                Custom = true,
                Sound = true,
            });
            added = true;
        }

        foreach (var extra in extras)
        {
            if (extra.Steps is { Length: > 0 } steps)
            {
                // Grouped into bursts of up to three, same as the manual
                // "Grouped" add - one cue per burst instead of per summon.
                var bursts = new List<List<(int Time, string Summon)>>();
                foreach (var s in steps)
                {
                    if (bursts.Count == 0 || bursts[^1].Count >= 3 || s.Time - bursts[^1][^1].Time > 20f)
                        bursts.Add(new List<(int, string)>());
                    bursts[^1].Add(s);
                }
                foreach (var b in bursts)
                    AddIfMissing(b[0].Time, "Summon",
                        string.Join(" / ", b.Select(x => x.Summon)),
                        string.Join(", ", b.Select(x => x.Summon)));
            }
            else
            {
                foreach (var (time, mech) in extra.Lines)
                    AddIfMissing(time, mech, extra.Action, "");
            }
        }

        if (added) fight.Lines = fight.Lines.OrderBy(l => l.Time).ToList();
        return added;
    }
}
