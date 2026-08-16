namespace FrenAlerts.Engine;

// How a call refers to the player it is about.
public enum CallNaming
{
    NameAndSlot,
    NameOnly,
    SlotOnly,
}

// Who this player is, so a trigger can tell "on you" from "on someone".
public sealed class PlayerContext
{
    public uint MyId { get; set; }

    // MT/OT/H1/H2/M1/M2/R1/R2, the same slot standard the sheets use, so a plan
    // written for one party still resolves against a different one.
    public string MySlot { get; set; } = "";

    public CallNaming Naming { get; set; } = CallNaming.NameAndSlot;

    // Which answer the group uses for a mechanic that has several. Supplied by the
    // host, because the engine has no idea where a setting is stored. Unset returns
    // an empty string, which no option ever equals, so a call gated on a strat stays
    // quiet until somebody picks one.
    public Func<string, string> Strat { get; set; } = static _ => "";
}

// Everything a trigger gets to look at when it fires.
public readonly record struct TriggerContext(
    GameEvent Event, PlayerContext Player, ActorBook Actors, PartyContext Party, FightState State)
{
    public int Nth => State.Count(Event.Kind, Event.Id);

    // The group's answer for one of this fight's strat settings.
    public string Strat(string key) => Player.Strat(key);

    public bool Running(string key, string option) => Player.Strat(key) == option;

    public int Phase => State.Phase;

    // True on the first event of a mechanic, false on the rest of the same burst.
    public bool FirstOfBurst => State.NewBurst;

    public bool HasRealTarget => Event.TargetId != 0 && Event.TargetId != Event.SourceId;

    public bool FiredBy(Aim aim) => aim switch
    {
        Aim.Anyone => true,
        Aim.Me => SourceIsMe,
        Aim.NotMe => !SourceIsMe,
        Aim.Enemy => !ActorId.IsPlayer(Event.SourceId),
        Aim.AnyPlayer => ActorId.IsPlayer(Event.SourceId),
        Aim.OtherPlayer => !SourceIsMe && ActorId.IsPlayer(Event.SourceId),
        _ => true,
    };

    // Whether this event is the case the line was written for.
    public bool Aimed(Aim aim) => aim switch
    {
        Aim.Anyone => true,
        Aim.Me => TargetIsMe,
        Aim.NotMe => HasRealTarget && !TargetIsMe,
        Aim.Enemy => !HasRealTarget || !ActorId.IsPlayer(Event.TargetId),
        Aim.AnyPlayer => HasRealTarget && ActorId.IsPlayer(Event.TargetId),
        Aim.OtherPlayer => HasRealTarget && !TargetIsMe && ActorId.IsPlayer(Event.TargetId),
        Aim.Untargeted => !HasRealTarget,
        _ => true,
    };


    public bool TargetIsMe => Event.TargetId == Player.MyId;
    public bool SourceIsMe => Event.SourceId == Player.MyId;

    public string TargetSlot => Party.SlotOf(Event.TargetId);

    public string MySlot => Player.MySlot;

    public bool ForMe(string audience) => Audience.Includes(audience, Player.MySlot);

    public string NameTarget() => Describe(Event.TargetId);

    public string Describe(uint actorId)
    {
        var slot = Party.SlotOf(actorId);
        var name = Actors.ShortName(actorId);

        return Player.Naming switch
        {
            CallNaming.SlotOnly => Pick(slot, name),
            CallNaming.NameOnly => Pick(name, slot),
            _ => name.Length > 0 && slot.Length > 0 ? $"{name} ({slot})" : Pick(name, slot),
        };

        // Falling back to the other one beats saying "someone" when the preferred
        // form is simply not known here.
        static string Pick(string first, string second) =>
            first.Length > 0 ? first : second.Length > 0 ? second : "someone";
    }
}

public sealed record Trigger
{
    public required string Id { get; init; }
    public required EventKind On { get; init; }

    public uint MatchId { get; init; }

    // Only fire when the event targets this player.
    public bool OnlyMe { get; init; }

    public Aim Aim { get; init; } = Aim.Anyone;

    // Fire only on this numbered occurrence of the id, or 0 for any.
    public int Occurrence { get; init; }

    // A trigger that exists only to claim an event so the pack does not answer it.
    // It says nothing, so listing it puts a row on the fight page with no call in
    // it and the row's own id where the words should be.
    public bool Claims { get; init; }

    // Which phase of the fight this belongs to, for grouping it on the fight page.
    // Zero means it was never given one, which reads as "everything else" and is
    // what a hand written call used to fall into whether it belonged there or not.
    public int Phase { get; init; }

    public string For { get; init; } = "";

    public bool OncePerBurst { get; init; } = true;

    public string[] Owns { get; init; } = [];

    // Which actor firing it this line is about, as opposed to who it landed on.
    public Aim From { get; init; } = Aim.Anyone;

    public float Hush { get; init; }

    // Said once a pull, then not again.
    public bool Once { get; init; }

    public bool Enabled { get; set; } = true;

    public required Func<TriggerContext, Call?> Make { get; init; }

    public bool Matches(in TriggerContext ctx) =>
        Enabled
        && ctx.Event.Kind == On
        && (MatchId == 0 || ctx.Event.Id == MatchId)
        && (!OnlyMe || ctx.TargetIsMe)
        && ctx.Aimed(Aim)
        && ctx.FiredBy(From)
        && (Occurrence == 0 || ctx.Nth == Occurrence)
        && (!OncePerBurst || OnlyMe || Aim == Aim.Me || ctx.FirstOfBurst)
        && ctx.ForMe(For);
}
