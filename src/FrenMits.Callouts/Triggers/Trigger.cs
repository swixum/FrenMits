using System;

namespace FrenMits.Callouts;

// One thing to watch for, and what to say when it happens.
//
// Everything past the plain fields is for a fight written as code rather than
// as a table. Half of a hard fight's calls depend on something no table can
// hold: which way the boss turned, who took the marker first, which strat the
// group runs. Those get a condition and a voice of their own. A trigger read
// from a pack leaves them empty and behaves exactly as it did before.
public sealed record Trigger
{
    // Stable name, used for config, for once-per-pull and for voice pack clips.
    public string Key { get; init; } = "";

    // What this says, in a sentence, for a settings page to show. A trigger
    // whose words are worked out per pull has no fixed text to display, and
    // "Real Short Accel / Fake Long + Shriek / ..." is what there is to say
    // about it instead.
    public string About { get; init; } = "";

    public TriggerMatch On { get; init; } = new();

    // Banner text. Tokens {source} {target} {ability} {me} {n} {nth} are filled in.
    public string Text { get; init; } = "";

    // Spoken text; empty speaks the banner instead.
    public string Tts { get; init; } = "";

    public CallSeverity Severity { get; init; } = CallSeverity.Info;

    // Seconds to hold the call back, for a mechanic that resolves later.
    public float Delay { get; init; }

    public float Duration { get; init; } = 4f;

    // For a mechanic that only ever happens once, whatever the log repeats.
    public bool OncePerPull { get; init; }

    public string Where { get; init; } = "";

    public bool Enabled { get; init; } = true;

    // Only while the fight is in this phase; empty means any.
    public string Phase { get; init; } = "";

    // Who needs to hear it: "tank", "healer", "dps", or several separated by a
    // comma. Empty means everyone. A healer does not need the tank swap.
    public string Roles { get; init; } = "";

    // Narrower than a role: the jobs that can act on it, separated by a comma.
    // Empty means every job. "Stun it" is only worth hearing if you brought a
    // stun, and that is a job question, not a role one.
    public string Jobs { get; init; } = "";

    // Announcing this also moves the fight into that phase.
    public string SetsPhase { get; init; } = "";

    // Say it this long before the debuff runs out, rather than when it lands.
    // The engine reads the duration off the event, so one trigger covers every
    // length the fight uses.
    public float BeforeExpiry { get; init; }

    // Stay quiet for this long after speaking. A mechanic that lands on eight
    // players at once fires eight times, and without this the party hears it
    // eight times.
    public float Suppress { get; init; }

    // A second opinion on whether this event is really the one, for a mechanic
    // the match alone cannot pick out.
    public Func<TriggerContext, bool>? When { get; init; }

    // Works out what to say from the state of the pull. Returning nothing means
    // say nothing, which is the right answer for a mechanic that is somebody
    // else's problem this time.
    public Func<TriggerContext, Say?>? Says { get; init; }

    // Watch this and remember it, without saying anything. This is how a fight
    // knows which tower was blue by the time it has to call one.
    public Action<TriggerContext>? Note { get; init; }

    // A trigger that only remembers has nothing to announce.
    public bool Silent => Text.Length == 0 && Tts.Length == 0 && Says is null;
}
