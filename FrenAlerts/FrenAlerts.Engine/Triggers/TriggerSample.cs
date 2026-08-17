namespace FrenAlerts.Engine;

// What a trigger sounds like before a pull, for the page that lists calls.
//
// The page cannot run a fight, so it runs each trigger once against a made-up
// event and shows what comes back. One run only ever hears one answer, and a
// call that names who it landed on has two: "Tank Cleave" and "Tank Cleave on
// YOU" are the same trigger, and the page showed the first while the second is
// the one somebody is scanning the list for.
public static class TriggerSample
{
    // Also what a hand written Says uses to separate its answers, so a row built
    // from two runs and a row written by hand read the same way.
    public const string Separator = " / ";

    // The two answers as one line, yours first.
    //
    // Yours first because that is the half being looked for: a player reading the
    // list wants to know what they will hear when it is on them. Identical answers
    // collapse to one, which is most calls, so the line stays short everywhere the
    // distinction does not exist.
    public static string Join(string? onYou, string onSomeoneElse)
    {
        if (string.IsNullOrWhiteSpace(onYou)) return onSomeoneElse;
        if (string.IsNullOrWhiteSpace(onSomeoneElse)) return onYou;
        return onYou == onSomeoneElse ? onSomeoneElse : onYou + Separator + onSomeoneElse;
    }
}
