using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Encounters;

// One encounter's mit timeline, fired inside its territory.
[Serializable]
public class FightProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New fight";
    public uint TerritoryId { get; set; }
    public bool Enabled { get; set; } = true;

    // Sidebar group: "Ultimate", "Savage", "Extreme", "Raids", "Custom", "Other".
    public string Category { get; set; } = "";

    // A custom sheet that outranks the official one for its zone.
    public bool PreferOverOfficial { get; set; }

    // Added on the cue clock, so it survives resync.
    public float TimerOffset { get; set; }

    // The tank pairing picked for this fight, remembered.
    public string TankPairing { get; set; } = "";
    
    // The simulated job picked for this fight, remembered (when UseSetup is unchecked).
    public string SimulatedJob { get; set; } = "";

    // The active slot's lines (what the overlay reads + the line table edits).
    public List<MitLine> Lines { get; set; } = new();

    // Tombstones for deleted lines, so a re-bake can't revive them.
    public List<DeletedCall> DeletedCalls { get; set; } = new();

    // Per-mechanic notes shown in the Sheet View footer.
    public List<SheetNote> Notes { get; set; } = new();

    // The built-in sheet slot selected for this fight.
    public string Slot { get; set; } = "";

    // Per-slot saved line sets, so each slot keeps its own edits.
    public Dictionary<string, List<MitLine>> SavedSlots { get; set; } = new();

    // Set once the built-in timeline has been auto-loaded for this profile.
    public bool AutoLoaded { get; set; }

    // Resync anchors: a cast of one snaps the timer onto Time.
    public List<SyncPoint> SyncPoints { get; set; } = new();

    // Cast-free safety net: this boss appearing snaps the clock.
    public List<BossAnchor> BossAnchors { get; set; } = new();

    // An auto-generated timeline for a duty with no sheet.
    public bool TimelineOnly { get; set; }

    // The column layout of a user-made sheet.
    public List<string> CustomSlots { get; set; } = new();

    // Scaffold rows: mechanics that exist before any mit does.
    public List<CustomRow> CustomRows { get; set; } = new();

    // Downtime windows this fight owns, from an imported log.
    public List<DowntimeWindow> CustomDowntimes { get; set; } = new();

    // PriorityPhase.Start values manually flipped from their auto-resolved
    // priority-1/priority-2 pick (see TankPriority).
    public List<float> SwappedPriorityPhases { get; set; } = new();

    // Derived, and not serialized so codes don't carry lines twice.
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
                // A stable sort, so lines sharing a time keep their order.
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

    // The last moment this fight has anything to say, across every column.
    public float LastMoment()
    {
        var last = 0f;
        foreach (var l in Lines) if (l.Time > last) last = l.Time;
        foreach (var r in CustomRows) if (r.Time > last) last = r.Time;
        foreach (var slot in SavedSlots.Values)
            foreach (var l in slot) if (l.Time > last) last = l.Time;
        return last;
    }

    // The profile that fires in a territory. Officials outrank Custom
    // sheets unless the Custom one is marked preferred.
    public static FightProfile? Active(IEnumerable<FightProfile> fights, uint territory, Func<uint, bool> hasOfficial)
    {
        FightProfile? official = null, custom = null;
        foreach (var fight in fights)
        {
            if (!fight.Enabled || fight.TerritoryId != territory) continue;
            if (fight.Category == "Custom" && hasOfficial(fight.TerritoryId)) custom ??= fight;
            else official ??= fight;
        }
        if (custom != null && (official == null || custom.PreferOverOfficial)) return custom;
        return official;
    }
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

    // How hard the hit is: 0 unknown, 1 light, 2 hurts, 3 deadly.
    public int Hurt { get; set; }

    // Tank buster: the hit lands on one tank or two, not the party.
    public bool Buster { get; set; }

    // The fight's timer running out.
    public bool Enrage { get; set; }

    public bool ShouldSerializeEnrage() => Enrage;
}

// A note on one row, matched by mechanic and nearby time.
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

// A lull learned from a pull, with when it began and how long.
[Serializable]
public class DowntimeWindow
{
    public float Start { get; set; }
    public float Duration { get; set; }
    // The boss health this phase must be pushed below by Start.
    public float TargetHp { get; set; } = -1f;

    // This window's time is uncertain, so refine it from pulls.
    [Newtonsoft.Json.JsonIgnore]
    public bool Learn { get; set; }

    // This lull is an actual cutscene.
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

// One call: at Time, these jobs should use this action.
[Serializable]
public class MitLine
{
    public float Time { get; set; }
    public string Mechanic { get; set; } = "";
    public string Action { get; set; } = "";

    // Job abbreviations this line applies to (e.g. "WAR", "SCH").
    public List<string> Jobs { get; set; } = new();
    public bool Enabled { get; set; } = true;

    // True for a line the user added themselves.
    public bool Custom { get; set; }

    // True for a generic baked job extra (e.g. Dismantle).
    [Newtonsoft.Json.JsonIgnore]
    public bool IsJobExtra { get; set; }

    // True for a line that is a personal override (not shared in the party plan).
    public bool Personal { get; set; }

    // Per-line offset on the CUE clock: + fires this one call earlier, - later.
    public float OffsetSeconds { get; set; }

    // True when set by hand, so the solver leaves it alone.
    public bool OffsetManual { get; set; }

    // Multi-hit coverage: still active at this plan time.
    public float CoverUntil { get; set; }

    // Where this call actually fires on the cue clock.
    [Newtonsoft.Json.JsonIgnore]
    public float CueTime => Time - OffsetSeconds;

    // An offset on this line, so CueTime is where the call belongs - voice,
    // overlay and every countdown alike. Not gated on OffsetManual: the sheet
    // pill and every board already show CueTime whatever wrote the offset, so
    // anything reading a different moment is disagreeing with what the user
    // sees. OffsetManual only tells the solver whose number it is.
    [Newtonsoft.Json.JsonIgnore]
    public bool HasCallOffset => OffsetSeconds != 0f;

    // Per-line overrides (0 / empty = use the global setting).
    public float LeadOverride { get; set; }   // warning lead seconds; 0 = global
    public string Tts { get; set; } = "";      // custom spoken text; empty = Action
    public bool Sound { get; set; } = true;    // play the audio cue for this line
    public uint Color { get; set; }            // ABGR text color; 0 = default
    public uint IconId { get; set; }           // pinned game icon id; 0 = infer from action

    // A line at its defaults writes nothing for these, which is most of them.
    public bool ShouldSerializeJobs() => Jobs is { Count: > 0 };
    public bool ShouldSerializeEnabled() => !Enabled;
    public bool ShouldSerializeSound() => !Sound;
    public bool ShouldSerializeCustom() => Custom;
    public bool ShouldSerializePersonal() => Personal;
    public bool ShouldSerializeOffsetSeconds() => OffsetSeconds != 0f;
    public bool ShouldSerializeOffsetManual() => OffsetManual;
    public bool ShouldSerializeCoverUntil() => CoverUntil != 0f;
    public bool ShouldSerializeLeadOverride() => LeadOverride != 0f;
    public bool ShouldSerializeTts() => !string.IsNullOrEmpty(Tts);
    public bool ShouldSerializeColor() => Color != 0;
    public bool ShouldSerializeIconId() => IconId != 0;
    public bool ShouldSerializeMechanic() => !string.IsNullOrEmpty(Mechanic);
    public bool ShouldSerializeAction() => !string.IsNullOrEmpty(Action);

    public bool AppliesTo(string? jobAbbr)
        => Jobs.Count == 0 || (jobAbbr != null && JobListHas(jobAbbr));

    // A plain loop, since this runs per line per frame.
    private bool JobListHas(string jobAbbr)
    {
        for (var i = 0; i < Jobs.Count; i++)
            if (string.Equals(Jobs[i], jobAbbr, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // A detached copy; edits to it never reach the original.
    public MitLine Clone()
    {
        var c = (MitLine)MemberwiseClone();
        c.Jobs = new List<string>(Jobs);
        return c;
    }

    // With one action per line, the action text is always the full action.
    public string ActionFor(string? jobAbbr) => Action;

    // True when this line has an explicit job restriction.
    public bool HasJobGate() => Jobs.Count > 0;

    // The normalized job tag for conflict tracking: the sorted job list, or "".
    public static string JobTagFor(string action, string mit)
    {
        // No inline gates in the new model; tags come from the Jobs list
        // and are handled by the caller.
        return "";
    }

    // Derived from Time, so never written.
    [Newtonsoft.Json.JsonIgnore]
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
