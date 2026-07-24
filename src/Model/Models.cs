using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// One encounter's mitigation timeline, firing only when the player is in the
// matching territory.
[Serializable]
public class FightProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New fight";
    public uint TerritoryId { get; set; }
    public bool Enabled { get; set; } = true;

    // Sidebar group: "Ultimate", "Savage", "Extreme", "Raids", "Other".
    public string Category { get; set; } = "";

    // Added on the cue clock (Plugin.CueClockFor), NOT the sheet clock, so it
    // survives resync: + fires every call earlier, - later.
    public float TimerOffset { get; set; }

    // The tank pairing picked in the Tank busters extras card (e.g. "WAR/DRK"),
    // remembered per fight so the dropdown stays where you set it.
    public string TankPairing { get; set; } = "";

    // The active slot's lines (what the overlay reads + the line table edits).
    public List<MitLine> Lines { get; set; } = new();

    // Tombstones for sheet-baked lines the user deleted, so ApplySlot's top-up
    // and the sheet re-bakes don't resurrect them.
    public List<DeletedCall> DeletedCalls { get; set; } = new();

    // Per-mechanic notes shown in the Sheet View's footer strip (the in-game
    // version of the Ikuya sheet's notes).
    public List<SheetNote> Notes { get; set; } = new();

    // The built-in sheet slot currently selected for this fight (e.g. "D1", "WHM").
    public string Slot { get; set; } = "";

    // Per-slot saved line sets, so each slot keeps its own edits.
    public Dictionary<string, List<MitLine>> SavedSlots { get; set; } = new();

    // Set once the built-in timeline has been auto-loaded for this profile.
    public bool AutoLoaded { get; set; }

    // Resync anchors: when one of these abilities is cast, the timer snaps so
    // the ability resolves at Time, correcting phase drift.
    public List<SyncPoint> SyncPoints { get; set; } = new();

    // Cast-free safety net: when a boss with this NameId first appears, the
    // clock snaps to Time.
    public List<BossAnchor> BossAnchors { get; set; } = new();

    // Timeline-only: an auto-generated boss timeline for a duty with no sheet
    // (never saved to the config).
    public bool TimelineOnly { get; set; }

    // Custom sheets (non-builtin fights): the column layout of a user-made
    // sheet.
    public List<string> CustomSlots { get; set; } = new();

    // Scaffold rows for custom sheets: mechanics that exist before anyone has
    // written a mit into them (a row needs no lines to be plannable).
    public List<CustomRow> CustomRows { get; set; } = new();

    // Untargetable/downtime windows this fight owns (custom sheets): derived from
    // an imported log's cast gaps, on the same pull clock as the rows/anchors.
    public List<DowntimeWindow> CustomDowntimes { get; set; } = new();

    // Derived; ignored by the serializer so share codes and plan snapshots
    // don't carry every line twice.
    //
    // Cached, because the overlay, the board, the mit tuner and the server-bar
    // entry each ask for this EVERY frame and a bare OrderBy sorted a fresh copy
    // for every ask. Only three things can change the order - the list being
    // swapped wholesale (SwapCustomSlot), a line added/removed, or a line
    // retimed - so the fingerprint below covers all of them exactly (bit-exact
    // on Time, so even a hairline retime in the editor invalidates).
    [Newtonsoft.Json.JsonIgnore]
    public IReadOnlyList<MitLine> OrderedLines
    {
        get
        {
            var lines = Lines;
            var stamp = lines.Count;
            unchecked
            {
                foreach (var l in lines)
                    stamp = stamp * 31 + BitConverter.SingleToInt32Bits(l.Time);
            }
            if (_orderedSrc != lines || _orderedStamp != stamp)
            {
                // OrderBy, not List.Sort: it's a STABLE sort, so lines sharing a
                // time keep the order they were baked in, exactly as before.
                _ordered = lines.OrderBy(l => l.Time).ToList();
                _orderedSrc = lines;
                _orderedStamp = stamp;
            }
            return _ordered;
        }
    }

    private List<MitLine>? _orderedSrc;
    private List<MitLine> _ordered = new();
    private int _orderedStamp;
}

// A deleted sheet call, remembered so no re-bake brings it back.
[Serializable]
public class DeletedCall
{
    public string Slot { get; set; } = "";
    public float Time { get; set; }
    public string Mechanic { get; set; } = "";
    public string Action { get; set; } = "";
}

// A mechanic row on a custom sheet: just a name and a time.
[Serializable]
public class CustomRow
{
    public float Time { get; set; }
    public string Mechanic { get; set; } = "";

    // How hard the hit is unmitigated: 0 unknown, 1 light, 2 hurts, 3 deadly.
    public int Hurt { get; set; }

    // Tank buster: the hit lands on one tank or two, not the party.
    public bool Buster { get; set; }
}

// A note attached to one mechanic row on the sheet, matched by mechanic label +
// nearby time (a bulk re-time moves it along with the row).
[Serializable]
public class SheetNote
{
    public float Time { get; set; }
    public string Mechanic { get; set; } = "";
    public string Text { get; set; } = "";
}

[Serializable]
public class BossAnchor
{
    public uint NameId { get; set; }
    public float Time { get; set; }
    public string Label { get; set; } = "";
}

// A lull learned from a pull: at Start seconds into the fight the boss went
// untargetable and stayed that way for Duration seconds.
[Serializable]
public class DowntimeWindow
{
    public float Start { get; set; }
    public float Duration { get; set; }
    // The boss HP fraction the phase must be pushed below by Start (its DPS check).
    public float TargetHp { get; set; } = -1f;

    // Hardcoded-table only: this window's TIME is uncertain (cactbot couldn't pin
    // it), so refine Start/Duration from live pulls.
    [Newtonsoft.Json.JsonIgnore]
    public bool Learn { get; set; }

    // This lull is an actual cutscene (not just a plain untargetable transition).
    public bool Cutscene { get; set; }
}

[Serializable]
public class SyncPoint
{
    public uint Ability { get; set; } // action id
    public float Time { get; set; }   // seconds from pull when it resolves
    public bool IsPhase { get; set; }  // phase-start anchor: wide window, re-bases the clock
    public string Label { get; set; } = "";
}

// A single timeline call-out: at Time seconds into the fight, the listed Jobs
// should use Action (for the Mechanic).
[Serializable]
public class MitLine
{
    public float Time { get; set; }
    public string Mechanic { get; set; } = "";
    public string Action { get; set; } = "";

    // Job abbreviations this line applies to (e.g. "WAR", "SCH").
    public List<string> Jobs { get; set; } = new();
    public bool Enabled { get; set; } = true;

    // True for a line a user added themselves (not from a built-in sheet bake).
    public bool Custom { get; set; }

    // Per-line offset on the CUE clock: + fires this one call earlier, - later.
    public float OffsetSeconds { get; set; }

    // True when the offset was set BY HAND (the per-line offset slider), so the
    // timing solver leaves it alone.
    public bool OffsetManual { get; set; }

    // Multi-hit coverage: this call must still be ACTIVE at this plan time (the
    // last hit it covers).
    public float CoverUntil { get; set; }

    // Where this call actually fires on the cue clock.
    public float CueTime => Time - OffsetSeconds;

    // Per-line overrides (0 / empty = use the global setting).
    public float LeadOverride { get; set; }   // warning lead seconds; 0 = global
    public string Tts { get; set; } = "";      // custom spoken text; empty = Action
    public bool Sound { get; set; } = true;    // play the audio cue for this line
    public uint Color { get; set; }            // ABGR text color; 0 = default
    public uint IconId { get; set; }           // pinned game icon id; 0 = infer from action

    public bool AppliesTo(string? jobAbbr)
        => (Jobs.Count == 0 || (jobAbbr != null && JobListHas(jobAbbr)))
           // Job gates written INSIDE the action text ("Party Mit (WAR/PLD)")
           // also count: on a DRK that call is someone else's press.
           && (string.IsNullOrEmpty(jobAbbr) || string.IsNullOrWhiteSpace(Action) || ActionFor(jobAbbr).Length > 0);

    // Plain loop instead of LINQ Contains: this runs for every line of the fight,
    // every frame, in four places (overlay, board, tuner, cues), and the LINQ
    // overload boxes the list's enumerator on each ask.
    private bool JobListHas(string jobAbbr)
    {
        for (var i = 0; i < Jobs.Count; i++)
            if (string.Equals(Jobs[i], jobAbbr, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // The parts of this call that apply to a job, honoring job qualifiers in the
    // text: "Reprisal + Party Mit (GNB/DRK)" is "Reprisal" for a WAR, unchanged
    // for a DRK.
    public string ActionFor(string? jobAbbr)
    {
        if (string.IsNullOrWhiteSpace(Action) || string.IsNullOrEmpty(jobAbbr)) return Action;
        // Fast path for the overwhelming majority of calls: a gate is always
        // written as a parenthetical, so with no '(' anywhere nothing can be
        // dropped and the split below would just rebuild the same string. This
        // runs per line per frame, so the array it saves matters.
        if (Action.IndexOf('(') < 0) return Action;
        List<string>? kept = null;
        var dropped = false;
        foreach (var raw in Action.Split('+'))
        {
            var seg = raw.Trim();
            if (seg.Length == 0) continue;
            if (SegmentAppliesTo(seg, jobAbbr!)) (kept ??= new()).Add(seg);
            else dropped = true;
        }
        if (!dropped) return Action; // common case: nothing gated, keep verbatim
        return kept == null ? "" : string.Join(" + ", kept);
    }

    // Derived from the master job table so a new job added there is recognized
    // as a gate token here automatically - a second hand-kept list drifted.
    private static readonly HashSet<string> JobAbbrs = new(FrenMits.Jobs.Abbreviations, StringComparer.OrdinalIgnoreCase);

    private static bool SegmentAppliesTo(string segment, string job)
    {
        var i = segment.IndexOf('(');
        while (i >= 0)
        {
            var j = segment.IndexOf(')', i + 1);
            if (j < 0) break;
            var tokens = segment.Substring(i + 1, j - i - 1).Split('/');
            var allJobs = tokens.Length > 0;
            var mine = false;
            foreach (var t in tokens)
            {
                var tok = t.Trim();
                if (tok.Length == 0 || !JobAbbrs.Contains(tok)) { allJobs = false; break; }
                if (string.Equals(tok, job, StringComparison.OrdinalIgnoreCase)) mine = true;
            }
            if (allJobs && !mine) return false;
            i = segment.IndexOf('(', j + 1);
        }
        return true;
    }

    // True when any segment of the action carries a job gate like "(WAR/PLD)",
    // meaning the call belongs to specific jobs we may not be able to identify.
    public bool HasJobGate()
    {
        if (string.IsNullOrWhiteSpace(Action) || Action.IndexOf('(') < 0) return false;
        foreach (var raw in Action.Split('+'))
        {
            var seg = raw.Trim();
            if (seg.Length > 0 && JobGateOf(seg).Length > 0) return true;
        }
        return false;
    }

    // The normalized job gate ("PLD/WAR") on the segment of `action` naming
    // `mit`, or "" when that segment is ungated.
    public static string JobTagFor(string action, string mit)
    {
        foreach (var raw in action.Split('+'))
        {
            var seg = raw.Trim();
            if (seg.Length == 0 || seg.IndexOf(mit, StringComparison.OrdinalIgnoreCase) < 0) continue;
            return JobGateOf(seg);
        }
        return "";
    }

    // The segment's job gate, normalized (upper-cased, sorted, '/'-joined), or
    // "" when its parentheticals aren't job lists.
    private static string JobGateOf(string segment)
    {
        var i = segment.IndexOf('(');
        while (i >= 0)
        {
            var j = segment.IndexOf(')', i + 1);
            if (j < 0) break;
            var tokens = segment.Substring(i + 1, j - i - 1).Split('/');
            var jobs = new List<string>();
            var all = tokens.Length > 0;
            foreach (var t in tokens)
            {
                var tok = t.Trim().ToUpperInvariant();
                if (tok.Length == 0 || !JobAbbrs.Contains(tok)) { all = false; break; }
                jobs.Add(tok);
            }
            if (all) { jobs.Sort(StringComparer.Ordinal); return string.Join("/", jobs); }
            i = segment.IndexOf('(', j + 1);
        }
        return "";
    }

    public string TimeText
    {
        get
        {
            var t = (int)MathF.Round(Time);
            var sign = t < 0 ? "-" : "";
            t = Math.Abs(t);
            return $"{sign}{t / 60}:{t % 60:00}";
        }
    }
}
