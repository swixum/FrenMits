namespace FrenAlerts.Engine;

public sealed class SequenceTrigger
{
    public required string Id { get; init; }

    // What opens the sequence.
    public required EventKind StartOn { get; init; }
    public uint StartId { get; init; }

    public required EventKind ThenOn { get; init; }
    public uint ThenId { get; init; }
    public double Within { get; init; } = 10.0;

    // Only complete when the follow-up landed on this player.
    public bool ThenOnMe { get; init; }

    // Only arm when the opening event landed on this player. Without it a sequence
    // waiting on two debuffs arms on whoever in the party got the first one, and
    // then pairs it with yours: eight players make eight chances to mismatch.
    public bool StartOnMe { get; init; }

    // Which phase this belongs to, so it groups on the fight page like the rest.
    public int Phase { get; init; }

    public required Func<TriggerContext, Call?> Make { get; init; }

    private bool _armed;
    private double _armedAt;

    public Call? Step(in TriggerContext ctx)
    {
        var e = ctx.Event;

        if (_armed && e.Time - _armedAt > Within) _armed = false;

        if (!_armed)
        {
            if (e.Kind == StartOn && (StartId == 0 || e.Id == StartId)
                && (!StartOnMe || ctx.TargetIsMe))
            {
                _armed = true;
                _armedAt = e.Time;
            }
            return null;
        }

        if (e.Kind != ThenOn || (ThenId != 0 && e.Id != ThenId)) return null;
        if (ThenOnMe && !ctx.TargetIsMe) return null;

        _armed = false;
        return Make(ctx);
    }

    public void Reset()
    {
        _armed = false;
        _armedAt = 0;
    }
}
