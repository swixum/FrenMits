namespace FrenAlerts.Engine;

// Whose statuses are worth reading off the frame, and which of them are news.
//
// The poll used to walk the party and only the party, so a debuff on the boss did
// not exist. A parser covers that when one is running, and in a replay one never is:
// a parser reads live network traffic and a recording makes none. Most of a fight's
// hardest calls read something the boss is wearing, so in a replay they simply never
// fired, which reads as the fight missing half its calls.
//
// Reading enemies as well brings those back. The cost is that every damage-over-time
// and every debuff the party applies is also a status on an enemy, and there are
// thousands of those in a pull: eight players reapplying for twenty minutes. None of
// them is ever a mechanic, and they are told apart by who put them there.
public static class StatusWatch
{
    // Enough for a boss and the adds an arena holds at once. Past it the read is
    // refused rather than allowed to grow: this runs ten times a second, and an
    // unbounded walk of every actor in a zone is frame time spent on nothing.
    public const int MaxEnemies = 16;

    // Whether a status that just appeared is worth raising.
    //
    // On a player, everything: a debuff is the mechanic, whoever applied it.
    //
    // On an enemy, only what the fight put there. A status on an enemy sourced from
    // somebody in the party is that player's own damage-over-time or debuff, which
    // no call has ever read and which would otherwise be the entire feed.
    public static bool Wanted(bool onAPlayer, bool fromThePartysOwnAction) =>
        onAPlayer || !fromThePartysOwnAction;

    // A status duration as the fight applies it, rather than as the poll finds it.
    //
    // The game applies whole seconds. A parser reads that number off the line and
    // gets it exactly; without one it comes off a poll running ten times a second,
    // which always finds the status already ticking, so a 49 second debuff arrives
    // as 48.8 and a 68 second one as 67.9. Both seen in a recorded pull.
    //
    // That fraction is not harmless. Half of what this fight hides is read out of a
    // duration: which of Neo Exdeath's debuffs are the real ones is decided by
    // whether it is over 46 seconds and whether it is over 83, and a threshold
    // compared against a number that is always slightly short is a call that names
    // the wrong half. The trap is worse still, because it says the number out loud:
    // "trap 48.8s", when the whole point is that 5, 49 and 68 are three jobs.
    //
    // Negative is left alone. The game marks a status with no timer that way, and
    // rounding it would turn "forever" into a number.
    public static float WholeSeconds(float remaining) =>
        remaining <= 0f ? remaining : MathF.Round(remaining);
}
