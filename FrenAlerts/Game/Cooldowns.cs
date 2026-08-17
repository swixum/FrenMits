using FFXIVClientStructs.FFXIV.Client.Game;
using FrenAlerts.Engine;
using FrenAlerts.Engine.UserTriggers;

namespace FrenAlerts.Game;

// The cooldown tracker, connected to this game.
//
// The board is the engine's and holds no client at all: what is running, how long is
// left, how far through. This is the half that reads the two things it cannot know.
// An action's recast comes off the client's own action manager, and a status comes
// off the events the plugin already reads.
//
// Polled rather than hooked. A recast is a number the client keeps and updates
// itself, so there is nothing to catch: reading it a few times a second is both
// simpler and impossible to get out of step with.
public sealed class Cooldowns
{
    // Twice a second. A sweep drawn from a number that is a fifth of a second stale
    // is indistinguishable from a live one, and this walks every tracked action.
    private const double Pace = 0.5;

    private readonly CooldownBoard _board = new();
    private double _lastPoll = -99;

    public CooldownBoard Board => _board;

    public List<CooldownEntry> Entries => _board.Entries;

    // Which job the player is on, since half of what is tracked only shows on one.
    public string Job { get; private set; } = "";

    // Everything somebody set up, held as the one list the page edits and the board
    // reads.
    public void Use(IEnumerable<CooldownEntry> saved)
    {
        _board.Entries.Clear();
        _board.Entries.AddRange(saved);
    }

    // What the client says about every tracked action. Statuses are left alone here:
    // they arrive as events and are already written down.
    public void Poll(double now, ushort territory)
    {
        if (!Paced.Due(now, _lastPoll, Pace)) return;
        _lastPoll = now;

        Job = PartySlots.Me?.ClassJob.ValueNullable?.Abbreviation.ExtractText() ?? "";

        foreach (var entry in _board.Entries)
        {
            if (!entry.Enabled || entry.IsStatus || entry.Id == 0) continue;
            if (Recast(entry.Id) is not { Total: > 0f } recast) continue;

            _board.Note(entry.Id, now + recast.Left, recast.Total);
        }
    }

    // A status somebody is tracking, off the same events every call is made from.
    // Only the ones on this player: a tracker is what your own cooldowns are doing,
    // and the same debuff on eight people would overwrite itself eight times.
    public void Feed(in GameEvent e, uint you)
    {
        if (e.TargetId != you || e.TargetId == 0) return;

        if (e.Kind == EventKind.StatusGain && Tracks(e.Id))
            _board.Note(e.Id, e.Time + e.Duration, e.Duration);
        else if (e.Kind == EventKind.StatusLose && Tracks(e.Id))
            _board.Forget(e.Id);
    }

    private bool Tracks(uint statusId)
    {
        foreach (var entry in _board.Entries)
            if (entry.IsStatus && entry.Id == statusId) return true;

        return false;
    }

    // A pull ending or a zone changing: what was running belonged to the fight that
    // is over, and a bar left mid-sweep describes nothing.
    public void Reset()
    {
        _board.Reset();
        _lastPoll = -99;
    }

    // How long this action has left and how long it takes, read off the client.
    //
    // Asked of the action itself rather than of its recast group, because that is
    // what somebody typed in: the group is an implementation detail and two actions
    // sharing one would otherwise show the same number under two names.
    private static unsafe (float Left, float Total)? Recast(uint actionId)
    {
        try
        {
            var manager = ActionManager.Instance();
            if (manager is null) return null;

            var total = manager->GetRecastTime(ActionType.Action, actionId);
            if (total <= 0f) return null;

            var gone = manager->GetRecastTimeElapsed(ActionType.Action, actionId);
            return (MathF.Max(0f, total - gone), total);
        }
        catch (Exception ex)
        {
            Service.Log.Debug($"Fren Alerts: no recast for {actionId}, {ex.Message}");
            return null;
        }
    }
}
