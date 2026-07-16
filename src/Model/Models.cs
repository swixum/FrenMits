using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits;

// One encounter's mitigation timeline. Only fires when the player is in the
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

    // The active slot's lines (what the overlay reads + the line table edits).
    public List<MitLine> Lines { get; set; } = new();

    // Tombstones for sheet-baked lines the user deleted, so ApplySlot's top-up
    // and the sheet re-bakes don't resurrect them. Slot-scoped, like the lines
    // themselves. Cleared by Reset to sheet / the Restore button.
    public List<DeletedCall> DeletedCalls { get; set; } = new();

    // Per-mechanic notes shown in the Sheet View's footer strip (the in-game
    // version of the Ikuya sheet's notes). Fight-wide, shared with the plan code.
    public List<SheetNote> Notes { get; set; } = new();

    // The built-in sheet slot currently selected for this fight (e.g. "D1", "WHM").
    // Drives the seamless auto-load when you enter the zone. Empty = infer from job.
    public string Slot { get; set; } = "";

    // Per-slot saved line sets, so each slot keeps its own edits. Switching the
    // slot picker swaps Lines to that slot's set (never mixes two slots together).
    public Dictionary<string, List<MitLine>> SavedSlots { get; set; } = new();

    // Set once the built-in timeline has been auto-loaded for this profile.
    public bool AutoLoaded { get; set; }

    // Resync anchors: when one of these abilities is cast, the timer snaps so
    // the ability resolves at Time, correcting phase drift.
    public List<SyncPoint> SyncPoints { get; set; } = new();

    // Cast-free safety net: when a boss with this NameId first appears, the
    // clock snaps to Time. Ideal for phases with no public ability timeline.
    public List<BossAnchor> BossAnchors { get; set; } = new();

    // Custom sheets (non-builtin fights): the column layout of a user-made
    // sheet. Non-empty = this fight shows in Sheet View like an official one.
    public List<string> CustomSlots { get; set; } = new();

    // Scaffold rows for custom sheets: mechanics that exist before anyone has
    // written a mit into them (a row needs no lines to be plannable).
    public List<CustomRow> CustomRows { get; set; } = new();

    // Derived; ignored by the serializer so share codes and plan snapshots
    // don't carry every line twice.
    [Newtonsoft.Json.JsonIgnore]
    public IEnumerable<MitLine> OrderedLines => Lines.OrderBy(l => l.Time);
}

// A deleted sheet call, remembered so no re-bake brings it back. Matched by the
// spoken action (or mechanic when neither line has an action) within a wide time
// window, so a sheet update that re-times or renames the mechanic still can't
// resurrect it.
[Serializable]
public class DeletedCall
{
    public string Slot { get; set; } = "";
    public float Time { get; set; }
    public string Mechanic { get; set; } = "";
    public string Action { get; set; } = "";
}

// A mechanic row on a custom sheet: just a name and a time. Lines reference it
// loosely (mechanic label + nearby time), same as notes.
[Serializable]
public class CustomRow
{
    public float Time { get; set; }
    public string Mechanic { get; set; } = "";
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

    // Job abbreviations this line applies to (e.g. "WAR", "SCH"). Empty = all jobs.
    public List<string> Jobs { get; set; } = new();
    public bool Enabled { get; set; } = true;

    // True for a line a user added themselves (not from a built-in sheet bake).
    // A re-bake of a built-in fight keeps these and only replaces the baked lines,
    // so custom timers people add survive sheet updates.
    public bool Custom { get; set; }

    // Per-line offset on the CUE clock: + fires this one call earlier, - later.
    // The plan time (Time) stays put; only when the call fires/shows moves, so
    // resync and sheet updates are unaffected. 0 = no shift.
    public float OffsetSeconds { get; set; }

    // Multi-hit coverage: this call must still be ACTIVE at this plan time (the
    // last hit it covers). 0 = covers only its own moment. Sheet View computes
    // the valid press window from this plus the buff's duration.
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
        => Jobs.Count == 0 || (jobAbbr != null && Jobs.Contains(jobAbbr, StringComparer.OrdinalIgnoreCase));

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
