using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// The complete mechanic timeline of a fight, every row its sheet knows about
// across ALL columns, not just your own slot, feeding the next-mits board with
// your presses attached where you have one.
public static class SheetTimeline
{
    public sealed class MechRow
    {
        public float Time;
        public string Mechanic = "";
        public int Hurt;    // 0 unknown, 1 light, 2 hurts, 3 deadly (custom sheets)
        public bool Buster; // custom sheets: lands on a tank, not the party
        // For rows with no mechanic label (bare user timers): the first line's
        // action, so the board never shows a nameless bar.
        public string Fallback = "";
        // Set on a scheduled boss-reposition row (the spot, e.g. "Middle"); drives
        // the cyan position row kind on the board.
        public string Position = "";
    }

    public static bool MechEquals(string a, string b)
        => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    public static List<MechRow> Build(FightProfile fight)
    {
        var rows = new List<MechRow>();
        var byMech = new Dictionary<string, List<MechRow>>(StringComparer.OrdinalIgnoreCase);

        MechRow RowFor(string mechanic, float time, float window)
        {
            var key = mechanic.Trim();
            if (!byMech.TryGetValue(key, out var list)) byMech[key] = list = new List<MechRow>();
            var row = list.FirstOrDefault(r => MathF.Abs(r.Time - time) < window);
            if (row == null)
            {
                row = new MechRow { Time = time, Mechanic = key };
                list.Add(row);
                rows.Add(row);
            }
            return row;
        }

        void AddLines(IEnumerable<MitLine> lines)
        {
            foreach (var l in lines)
            {
                var row = RowFor(l.Mechanic, l.Time, 1.6f);
                // Job extras ride ~1s ahead of their mechanic; plain sheet lines
                // own the row's time so the countdown lands on the hit itself.
                if (l.Jobs.Count == 0) row.Time = MathF.Max(row.Time, l.Time);
                if (row.Fallback.Length == 0 && !string.IsNullOrWhiteSpace(l.Action))
                    row.Fallback = l.Action;
            }
        }

        bool IsActive(string slot)
            => string.Equals(slot, fight.Slot, StringComparison.OrdinalIgnoreCase);

        if (Builtin.Has(fight.TerritoryId))
        {
            var slots = Builtin.Slots(fight.TerritoryId);
            foreach (var slot in slots)
            {
                if (IsActive(slot))
                    AddLines(fight.Lines);
                else if (fight.SavedSlots.TryGetValue(slot, out var saved) && saved.Count > 0)
                    AddLines(saved);
                else
                    AddLines(Builtin.BuildLines(fight.TerritoryId, slot)
                        .Where(b => !Builtin.IsDeleted(fight, slot, b)));
            }
            // The live plan can carry rows no bake has (user-added timers), and
            // an empty/unknown active slot still deserves its lines on the board.
            if (!slots.Any(IsActive)) AddLines(fight.Lines);
        }
        else if (fight.CustomSlots.Count > 0)
        {
            foreach (var slot in fight.CustomSlots)
            {
                if (IsActive(slot))
                    AddLines(fight.Lines);
                else if (fight.SavedSlots.TryGetValue(slot, out var saved) && saved.Count > 0)
                    AddLines(saved);
            }
            if (!fight.CustomSlots.Any(IsActive)) AddLines(fight.Lines);
        }
        else
        {
            AddLines(fight.Lines);
        }

        // Custom-sheet scaffold rows: mechanics exist (with their grades) even
        // before anyone wrote a mit into them.
        foreach (var cr in fight.CustomRows)
        {
            var row = RowFor(cr.Mechanic, cr.Time, 2f);
            row.Hurt = Math.Max(row.Hurt, cr.Hurt);
            row.Buster |= cr.Buster;
        }

        return rows.OrderBy(r => r.Time).ToList();
    }

    // ---- phase dividers ----------------------------------------------------

    public readonly record struct PhaseMark(float Time, string Label);

    private static readonly List<PhaseMark> NoMarks = new();

    // Every phase boundary a fight can put a name to.
    //
    // Nothing here needed fetching or baking: every timeline FrenMits ships
    // already tags each row with the phase it belongs to (Entry.Phase), because
    // the practice phase-jump needed exactly that. So the first row tagged "P3" is
    // where P3 starts, and each fight's own PhaseStarts already folds it up.
    //
    // Anchors are the fallback for anything with no tagged timeline. Where a fight
    // has both, the tags win: they're the finer answer (five or six phases against
    // an anchor list's two or three, since anchors exist to re-base the clock and
    // some phases deliberately have none).
    //
    // Duty timelines are skipped outright: they pack every encounter of an instance
    // onto one clock in 1000-second blocks, so a phase time from either source
    // would land in the wrong block, and a dungeon's bosses aren't phases anyway.
    public static List<PhaseMark> PhaseMarks(FightProfile fight)
    {
        if (fight.TimelineOnly) return NoMarks;

        var marks = new List<PhaseMark>();

        void Add(List<(string Name, float Time)> starts, Func<string, string>? title = null)
        {
            foreach (var (name, time) in starts)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var label = (title == null ? name : title(name)).Trim();
                if (label.Length > 0) marks.Add(new PhaseMark(time, label));
            }
        }

        var territory = fight.TerritoryId;
        // DMU spells its phases out ("Phase 3: Chaos & Exdeath"); the rest tag
        // theirs as plain P1/P2/P3, which is what people call them anyway.
        if (territory == Builtin.DmuTerritory) Add(DmuData.PhaseStarts(), DmuData.PhaseTitle);
        else if (territory == Builtin.FruTerritory) Add(FruData.PhaseStarts());
        else if (territory == Builtin.M12sTerritory) Add(M12sData.PhaseStarts());
        else if (IkuyaTimelines.Has(territory)) Add(IkuyaTimelines.PhaseStarts(territory));

        if (marks.Count == 0)
            foreach (var a in fight.BossAnchors)
                if (!string.IsNullOrWhiteSpace(a.Label))
                    marks.Add(new PhaseMark(a.Time, a.Label.Trim()));

        return marks;
    }

    // The phase that begins between two consecutive rows on the board, or "" when
    // none does. Drawing that name between the last row of one phase and the first
    // of the next turns "seven bars" into "three bars, then P2 starts".
    //
    // A mark landing exactly ON a row belongs to that row, so the divider sits
    // ABOVE it: that row is the first of the new phase, not the last of the old.
    public static string PhaseBetween(IReadOnlyList<PhaseMark> marks, float afterTime, float untilTime)
    {
        var label = "";
        // Two phases can begin inside one gap (a short phase nobody has a row
        // for). The LAST one is the phase the next row actually belongs to, and
        // the marks aren't sorted, so track the time rather than trusting order.
        var best = float.NegativeInfinity;
        for (var i = 0; i < marks.Count; i++)
        {
            var m = marks[i];
            if (m.Time <= afterTime || m.Time > untilTime) continue;
            if (m.Time < best) continue;
            best = m.Time;
            label = m.Label;
        }
        return label;
    }
}
