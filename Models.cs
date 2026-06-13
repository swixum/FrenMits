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

    // Added to the combat-synced elapsed time. Use it to nudge the whole sheet
    // earlier/later if your sheet's t=0 differs from combat start.
    public float TimerOffset { get; set; }

    // The active slot's lines (what the overlay reads + the line table edits).
    public List<MitLine> Lines { get; set; } = new();

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

    public IEnumerable<MitLine> OrderedLines => Lines.OrderBy(l => l.Time);
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

    // Per-line overrides (0 / empty = use the global setting).
    public float LeadOverride { get; set; }   // warning lead seconds; 0 = global
    public string Tts { get; set; } = "";      // custom spoken text; empty = Action
    public bool Sound { get; set; } = true;    // play the audio cue for this line
    public uint Color { get; set; }            // ABGR text colour; 0 = default
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
