using System.Numerics;

namespace FrenAlerts.Engine.UserTriggers;

// A trigger somebody made themselves, ported from theirs field for field.
//
// The shipped fights are one half of what their plugin does; this is the other. A
// raider writes "when this cast starts, say this, then nine seconds later say that",
// and every knob below is one they exposed: which kind of event, matched how, from
// whom, gated on what, and what it does when it fires.
//
// Kept whole rather than trimmed. A trigger somebody wrote against their editor is
// only portable if every field it can carry means the same thing here, and the field
// nobody uses is exactly the one the next person's trigger turns out to need.
public sealed class UserTrigger
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public bool Enabled { get; set; } = true;

    public string Name { get; set; } = "New trigger";

    public string Group { get; set; } = "";

    public TriggerMatch On { get; set; } = TriggerMatch.Cast;

    public string Pattern { get; set; } = "";

    public bool UseRegex { get; set; }

    public SourceFilter Source { get; set; }

    public bool OnlyOnSelf { get; set; }

    // A name can be translated and a cast can be renamed between patches; an id is
    // neither, which is why theirs offers both and why an id wins where it is set.
    public bool MatchById { get; set; }

    public uint DataId { get; set; }

    public float FightTime { get; set; }

    public RoleFilter SourceRole { get; set; }

    public RoleFilter TargetRole { get; set; }

    public string SourceName { get; set; } = "";

    public string TargetName { get; set; } = "";

    public RoleMask SelfRoles { get; set; }

    public List<NumCondition> NumConditions { get; set; } = [];

    public List<VarCondition> VarConditions { get; set; } = [];

    public List<VarAction> SetVars { get; set; } = [];

    public float Cooldown { get; set; }

    public bool NoReentry { get; set; }

    public Concurrency Concurrency { get; set; }

    public string SoundPath { get; set; } = "";

    public bool UseEventDuration { get; set; }

    public bool ShowCountdown { get; set; }

    public ClearRule ClearOn { get; set; } = new();

    public bool AnyZone { get; set; } = true;

    public List<uint> Zones { get; set; } = [];

    public float DelaySeconds { get; set; }

    public bool TtsEnabled { get; set; } = true;

    public string TtsText { get; set; } = "";

    public bool TextEnabled { get; set; } = true;

    public string Text { get; set; } = "";

    public float Duration { get; set; } = 4f;

    public bool ShowIcon { get; set; }

    public uint IconId { get; set; }

    public float Scale { get; set; } = 2f;

    public Vector4 Color { get; set; } = new(1f, 0.9f, 0.2f, 1f);

    public bool Background { get; set; } = true;

    public bool OverridePos { get; set; }

    public float PosX { get; set; } = 0.5f;

    public float PosY { get; set; } = 0.32f;

    public List<FollowUp> FollowUps { get; set; } = [];

    public UserTrigger Clone()
    {
        var copy = (UserTrigger)MemberwiseClone();
        copy.FollowUps = FollowUps.ConvertAll(f => f.Clone());
        copy.Zones = [.. Zones];
        copy.NumConditions = NumConditions.ConvertAll(c => c.Clone());
        copy.VarConditions = VarConditions.ConvertAll(c => c.Clone());
        copy.SetVars = SetVars.ConvertAll(v => v.Clone());
        copy.ClearOn = ClearOn.Clone();
        return copy;
    }
}

// A second line, said either on a timer or when something else happens.
public sealed class FollowUp
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public FollowUpOn On { get; set; }

    public float Seconds { get; set; } = 9f;

    public string Pattern { get; set; } = "";

    public uint DataId { get; set; }

    public bool OnlyOnSelf { get; set; } = true;

    // Every condition, or any one of them. A stack that needs two debuffs is the
    // first; a spread that could arrive as either marker is the second.
    public bool RequireAll { get; set; } = true;

    public List<FollowCondition> Conditions { get; set; } = [];

    public bool TtsEnabled { get; set; } = true;

    public string TtsText { get; set; } = "";

    public bool TextEnabled { get; set; } = true;

    public string Text { get; set; } = "";

    public Vector4 Color { get; set; } = new(1f, 0.9f, 0.2f, 1f);

    public float Scale { get; set; } = 2f;

    public float Duration { get; set; } = 4f;

    public bool UseEventDuration { get; set; }

    public bool ShowCountdown { get; set; }

    public bool ShowIcon { get; set; }

    public uint IconId { get; set; }

    public bool IsConditional => On != FollowUpOn.Timer;

    // A step written the short way, with the pattern on the step itself, is the same
    // step written the long way with one condition. Filling it in here means the
    // matcher only ever has to read one of the two shapes.
    public void EnsureConditions()
    {
        if (On == FollowUpOn.Timer || Conditions.Count > 0) return;

        Conditions.Add(new FollowCondition
        {
            Pattern = Pattern,
            DataId = DataId,
            MatchById = DataId != 0 && On is FollowUpOn.Headmarker or FollowUpOn.Tether,
            OnlyOnSelf = OnlyOnSelf,
        });
    }

    public FollowUp Clone()
    {
        var copy = (FollowUp)MemberwiseClone();
        copy.Conditions = Conditions.ConvertAll(c => c.Clone());
        return copy;
    }
}

public sealed class FollowCondition
{
    public string Pattern { get; set; } = "";
    public uint DataId { get; set; }
    public bool MatchById { get; set; }
    public bool OnlyOnSelf { get; set; } = true;
    public bool UseRegex { get; set; }
    public SourceFilter Source { get; set; }
    public RoleFilter SourceRole { get; set; }
    public RoleFilter TargetRole { get; set; }

    public FollowCondition Clone() => (FollowCondition)MemberwiseClone();
}

// What takes a call back off the screen early, and how long to watch for it.
public sealed class ClearRule
{
    public bool Enabled { get; set; }
    public FollowUpOn On { get; set; } = FollowUpOn.Cast;
    public float Seconds { get; set; } = 12f;
    public string Pattern { get; set; } = "";
    public uint DataId { get; set; }
    public bool MatchById { get; set; }
    public bool OnlyOnSelf { get; set; }

    public ClearRule Clone() => (ClearRule)MemberwiseClone();
}

public sealed class NumCondition
{
    public NumField Field { get; set; }
    public NumOp Op { get; set; } = NumOp.Ge;
    public float Value { get; set; }

    public NumCondition Clone() => (NumCondition)MemberwiseClone();
}

public sealed class VarCondition
{
    public string Name { get; set; } = "";
    public NumOp Op { get; set; }
    public string Value { get; set; } = "";
    public bool Numeric { get; set; }

    public VarCondition Clone() => (VarCondition)MemberwiseClone();
}

public sealed class VarAction
{
    public string Name { get; set; } = "";
    public VarOp Op { get; set; }
    public string Value { get; set; } = "1";

    public VarAction Clone() => (VarAction)MemberwiseClone();
}

// A set of triggers that travel together, which is what a share code carries.
public sealed class UserTriggerSet
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "New set";
    public string Category { get; set; } = "General";
    public string Author { get; set; } = "";
    public bool BuiltIn { get; set; }
    public List<UserTrigger> Triggers { get; set; } = [];
}

public enum TriggerMatch : byte
{
    Any,
    Cast,
    StatusGain,
    StatusLose,
    Death,
    Headmarker,
    Tether,
    Chat,
    CastStart,
    Ability,
    CastEnd,
    FightTime,
}

public enum FollowUpOn : byte
{
    Timer,
    Cast,
    StatusGain,
    StatusLose,
    Headmarker,
    Tether,
    Death,
    Chat,
    CastStart,
    Ability,
    CastEnd,
}

public enum SourceFilter : byte
{
    Anyone,
    Enemy,
    You,
    Party,
}

public enum RoleFilter : byte
{
    Any,
    Tank,
    Healer,
    Dps,
}

[Flags]
public enum RoleMask : byte
{
    None = 0,
    Tank = 1,
    Healer = 2,
    Dps = 4,
}

public enum NumField : byte
{
    StackCount,
    Value,
    SourceHpPct,
    TargetHpPct,
    Param1,
    Param2,
    Param3,
    Param4,
}

public enum NumOp : byte
{
    Eq,
    Ne,
    Lt,
    Le,
    Gt,
    Ge,
}

public enum VarOp : byte
{
    Set,
    Increment,
}

// What a second call does when the first one is still up: wait for it, replace it,
// or sit beside it.
public enum Concurrency : byte
{
    Wait,
    Replace,
    Stack,
}
