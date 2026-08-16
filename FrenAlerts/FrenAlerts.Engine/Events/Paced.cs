namespace FrenAlerts.Engine;

// Whether something that runs on an interval is due.
//
// Written out because four separate polls asked it as "now minus last is under the
// interval", which is only the same question while the clock moves forward. In a
// replay it does not: scrubbing back three minutes left every one of those polls
// holding off for three minutes of replay time, which reads from the outside as the
// plugin having stopped.
public static class Paced
{
    // Due when the interval has passed, and due immediately when the clock has gone
    // backwards, because there is nothing sensible to wait for.
    public static bool Due(double now, double last, double interval) =>
        now < last || now - last >= interval;
}
