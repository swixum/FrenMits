using System.Collections.Generic;

namespace FrenMits;

public class FightDefinition
{
    public uint TerritoryId { get; set; }
    public string Name { get; set; } = "";
    public List<MechanicEvent> Timeline { get; set; } = new();
    public List<MechanicAction> DefaultActions { get; set; } = new();
    public List<SyncPoint> SyncPoints { get; set; } = new();
    public List<PhaseStartData> PhaseStarts { get; set; } = new();
    public List<CustomRow> CustomRows { get; set; } = new();
    public List<BossAnchor> BossAnchors { get; set; } = new();
    // Windows where the tank slot's Tank-kind lines mean "priority 1 / priority 2"
    // (ranked by JobPriority) instead of literal MT/OT enmity.
    public List<PriorityPhase> PriorityPhases { get; set; } = new();
}

// MT-slot Tank-kind lines go to whichever tank's job ranks first in JobPriority
// (OT gets the other), for lines timed within [Start, End). Party-kind lines
// (Reprisal, Party Mit, ...) are never affected - those stay literal MT/OT.
public class PriorityPhase
{
    public float Start { get; set; }
    public float End { get; set; }
    public List<string> JobPriority { get; set; } = new(); // most-priority job first
}

public class PhaseStartData
{
    public float Time { get; set; }
    public string Name { get; set; } = "";
}

public class MechanicEvent
{
    public float Time { get; set; }
    public string Mechanic { get; set; } = "";
    // Future-proofing: damage type, etc. can go here.
}

public class MechanicAction
{
    public float Time { get; set; }
    public string Mechanic { get; set; } = "";
    public string Slot { get; set; } = "";
    public string Action { get; set; } = "";
    public List<string> Jobs { get; set; } = new();
    // The Mechanic names a personal timer (a summoner's Ifrit/Titan/Garuda
    // cycle), not a boss cast, so it has no Timeline entry and must stay out
    // of the sheet's official mechanic list. The line itself still bakes -
    // whoever plays the job needs the call.
    public bool Hidden { get; set; }
}
