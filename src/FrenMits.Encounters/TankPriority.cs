using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Encounters;

// Resolves which physical MT/OT column a priority-governed tank line belongs
// to for this player. During a PriorityPhase window, a Tank-kind MT/OT line
// (Rampart, invulns, Short Mit, ...) means "priority 1 / priority 2" ranked
// by job, not literal enmity - Party-kind lines (Reprisal, Party Mit, ...)
// are untouched and always keep their literal MT/OT slot.
public static class TankPriority
{
    private static bool IsTankSlot(string slot)
        => string.Equals(slot, "MT", StringComparison.OrdinalIgnoreCase)
           || string.Equals(slot, "OT", StringComparison.OrdinalIgnoreCase);

    // Swap-eligible: everything except Party-kind (Reprisal, Party Mit, ...) -
    // the sheet's own exclusion list. Checking "not Party" instead of "is
    // Tank" avoids depending on MitTypes' Tank word list covering every
    // generic planning phrase ("40% + Short Mit" has no single recognized
    // ability word, but it's still a personal buster call, not a party one).
    private static bool IsSwapEligible(MitLine l) => MitTypes.Classify(l.Action, l.Mechanic) != MitTypes.Kind.Party;

    // The local and co-tank jobs, supplied by the host. Defaults to an
    // unresolvable pairing, which Resolve reads as "leave the literal slot".
    public static Func<(string? Local, string? CoTank)> TankJobs { get; set; } = () => (null, null);

    private static int RankOf(IReadOnlyList<string> order, string job)
    {
        for (var i = 0; i < order.Count; i++)
            if (string.Equals(order[i], job, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    // "MT" (priority 1) or "OT" (priority 2): whichever column this player's
    // Tank-kind lines in `phase` should be read from. An unresolvable pairing
    // (missing job, mirrored jobs, no co-tank known) defaults to "MT" so a
    // solo session or an editor away from the duty still shows something.
    public static string Resolve(PriorityPhase phase, string? localJob, string? coTankJob, bool swap)
    {
        var picked = "MT";
        if (!string.IsNullOrEmpty(localJob) && !string.IsNullOrEmpty(coTankJob))
        {
            var mine = RankOf(phase.JobPriority, localJob!);
            var theirs = RankOf(phase.JobPriority, coTankJob!);
            if (mine >= 0 && theirs >= 0 && mine != theirs)
                picked = mine < theirs ? "MT" : "OT";
        }
        return swap ? (picked == "MT" ? "OT" : "MT") : picked;
    }

    // The PriorityPhase covering `time`, or null.
    public static PriorityPhase? PhaseAt(uint territory, float time)
        => Builtin.PriorityPhases(territory).FirstOrDefault(p => time >= p.Start && time < p.End);

    public static bool IsSwapped(FightProfile fight, PriorityPhase phase)
        => fight.SwappedPriorityPhases.Any(s => MathF.Abs(s - phase.Start) < 0.5f);

    public static void SetSwapped(FightProfile fight, PriorityPhase phase, bool swapped)
    {
        fight.SwappedPriorityPhases.RemoveAll(s => MathF.Abs(s - phase.Start) < 0.5f);
        if (swapped) fight.SwappedPriorityPhases.Add(phase.Start);
    }

    // Rewrite `lines` (already baked for `slot`) so the Tank-kind lines inside
    // a PriorityPhase window come from whichever column `resolveSlot` names
    // for that phase, instead of literal `slot`. Party-kind lines and
    // anything outside a priority window pass through untouched.
    //
    // `includeDeleted`: by default the borrowed ("other slot") half is
    // filtered by this player's own tombstones, matching a normal top-up
    // where `lines` itself already excludes deleted calls. Pass true when
    // the caller wants "does the sheet define anything here at all,
    // deleted or not" (e.g. deciding whether an old tombstone is still
    // meaningful) - with the default filter, a deleted borrowed line would
    // vanish from both sides at once and its own tombstone would look
    // stale, undoing the deletion on the next refresh.
    private static List<MitLine> ApplyCore(FightProfile fight, string slot, List<MitLine> lines,
        bool includeDeleted, Func<PriorityPhase, string> resolveSlot)
    {
        if (!IsTankSlot(slot)) return lines;
        var phases = Builtin.PriorityPhases(fight.TerritoryId);
        if (phases.Count == 0) return lines;

        PriorityPhase? PhaseFor(float t) => phases.FirstOrDefault(p => t >= p.Start && t < p.End);

        // Everything outside a priority window, plus Party-kind lines inside
        // one, are already correct as literally baked for this slot.
        var result = lines.Where(l => PhaseFor(l.Time) == null || !IsSwapEligible(l)).ToList();

        var otherSlot = string.Equals(slot, "MT", StringComparison.OrdinalIgnoreCase) ? "OT" : "MT";

        // Every Tank-kind priority-window call from both columns, tagged
        // with its literal source slot, so each moment can pick whichever
        // side resolveSlot says belongs here.
        var mine = lines.Where(l => PhaseFor(l.Time) != null && IsSwapEligible(l)).Select(l => (Slot: slot, Line: l));
        var other = Builtin.BuildLines(fight.TerritoryId, otherSlot)
            .Where(b => PhaseFor(b.Time) != null && IsSwapEligible(b) && (includeDeleted || !Builtin.IsDeleted(fight, slot, b)))
            .Select(b => (Slot: otherSlot, Line: b));

        var groups = mine.Concat(other)
            .GroupBy(x => (Time: MathF.Round(x.Line.Time, 1), Mech: x.Line.Mechanic.Trim().ToLowerInvariant()));
        foreach (var g in groups)
        {
            var phase = PhaseFor(g.First().Line.Time)!;
            var resolvedSlot = resolveSlot(phase);
            var pick = g.FirstOrDefault(x => string.Equals(x.Slot, resolvedSlot, StringComparison.OrdinalIgnoreCase));
            if (pick.Line != null) result.Add(pick.Line);
            // else: the resolved column has no call at this moment (an empty sheet cell) - drop it.
        }

        return result.OrderBy(l => l.Time).ToList();
    }

    // The single-player view (Fight Editor, the overlay): which column
    // *this player* actually reads, from live party job ranking plus a
    // manual per-phase override.
    public static List<MitLine> Apply(FightProfile fight, string slot, List<MitLine> lines, bool includeDeleted = false)
    {
        var (localJob, coTankJob) = TankJobs();
        return ApplyCore(fight, slot, lines, includeDeleted,
            phase => Resolve(phase, localJob, coTankJob, IsSwapped(fight, phase)));
    }

    // The Sheet View grid's PASSIVE tank column - `slot` is the one being
    // computed (MT or OT), `viewerSlot` is the fight's own active seat
    // (fight.Slot, which may not even be a tank; call this only for the
    // column that ISN'T fight.Slot). The grid shows both tank columns at
    // once, and they must never independently agree on the same pick - if
    // `viewerSlot` IS the other tank, its own column already shows Apply's
    // live-party pick (baked into fight.Lines), so this column shows the
    // complement of that same pick, not a second independent resolution
    // (which, since Resolve answers "which slot should this player read"
    // without knowing which slot is asking, would otherwise hand both
    // columns the identical answer instead of swapping them). When the
    // viewer isn't a tank, there's no pick to complement, so it falls back
    // to a plain MT<->OT exchange gated by the manual override.
    public static List<MitLine> ApplyGrid(FightProfile fight, string viewerSlot, string slot, List<MitLine> lines, bool includeDeleted = false)
    {
        if (!IsTankSlot(viewerSlot))
        {
            var flip = string.Equals(slot, "MT", StringComparison.OrdinalIgnoreCase) ? "OT" : "MT";
            return ApplyCore(fight, slot, lines, includeDeleted, phase => IsSwapped(fight, phase) ? flip : slot);
        }

        var (localJob, coTankJob) = TankJobs();
        return ApplyCore(fight, slot, lines, includeDeleted, phase =>
        {
            var viewerPick = Resolve(phase, localJob, coTankJob, IsSwapped(fight, phase));
            return string.Equals(viewerPick, "MT", StringComparison.OrdinalIgnoreCase) ? "OT" : "MT";
        });
    }
}
