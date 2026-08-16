namespace FrenAlerts.Engine;

// Pack rows that never load, named one at a time.
//
// A row gets dropped here for one reason only: with the mechanic's name taken out
// of it there is nothing left to say. Its wording was the name, and the action it
// stands for needs where things spawned that pull, which the engine cannot answer
// yet. Naming a mechanic at the moment it happens is not a call, it is narration.
//
// Dropping by row rather than by id matters: several ids carry a bare row and a
// real one, and Future's End also carries "tank limit break", which is the single
// most important line in the fight.
//
// These come back the moment the direction behind them can be worked out. Nothing
// here is dropped for being wrong, only for being empty.
public static class Trimmed
{
    public static bool Drops(ushort territory, string id) =>
        territory == DancingMad.Territory && DancingMad_.Contains(id);

    public static int Count(ushort territory) =>
        territory == DancingMad.Territory ? DancingMad_.Count : 0;

    private static readonly HashSet<string> DancingMad_ =
    [
        // Element pairs: which way to go depends on which two elements landed.
        "dmu-p1-mystery-magic-fire-and-thunder",
        "dmu-p1-mystery-magic-ice-and-fire",
        "dmu-p1-and-p4-mystery-magic-ice-and-thunder",

        // A side to stand on, decided by where the wing is.
        "dmu-p2-single-wing-of-destruction-1",
        "dmu-p2-single-wing-of-destruction-2",
        "dmu-p2-wings-of-destruction",

        // The bare half of these two ids; the tank limit break row on the same
        // ids stays, which is why this list is by row and not by id.
        "dmu-p2-future-s-end-past-s-end-early-1",
        "dmu-p2-future-s-end-past-s-end-early-2",

        // Puddles or donuts first, read off a debuff order nobody is tracking yet.
        "dmu-p4-second-and-fourth-debuffs-early-1",
        "dmu-p4-second-and-fourth-debuffs-early-2",

        // A colour and a direction, both computed at the time.
        "dmu-p4-flood-of-naught-1",
        "dmu-p4-flood-of-naught-2",
        "dmu-p4-flood-of-naught-3",
        "dmu-p4-flood-of-naught-4",
        "dmu-p5-flood",

        // Says a different thing every time it is cast.
        "dmu-p4-kefka-says",

        // Two debuffs combined into one line by their own collector.
        "dmu-p4-short-debuffs-1",
        "dmu-p4-short-debuffs-2",
        "dmu-p4-stray-flames-and-long-debuffs",
        "dmu-p4-fifth-debuffs-1",
        "dmu-p4-fifth-debuffs-2",
        "dmu-p4-fifth-debuffs-3",
        "dmu-p4-fifth-debuffs-4",
        "dmu-p4-fifth-debuffs-5",
        "dmu-p4-fifth-debuffs-6",
        "dmu-p4-fifth-debuffs-7",
        "dmu-p4-tsunami-inferno-and-first-debuffs-early-2",
        "dmu-p4-tsunami-inferno-and-first-debuffs-early-3",
        "dmu-p4-tsunami-inferno-and-third-debuffs-early-2",
        "dmu-p4-tsunami-inferno-and-third-debuffs-early-3",

        // A portent is a direction, and the direction is the whole call.
        "dmu-p1-tele-portents-1",
        "dmu-p1-tele-portents-2",
        "dmu-p1-tele-portents-3",
        "dmu-p1-tele-portents-4",
        "dmu-p1-tele-portents-5",
        "dmu-p1-tele-portents-6",
        "dmu-p1-tele-portents-7",
        "dmu-p1-tele-portents-8",

        // The spread and the stack that follow are already called off the cast.
        "dmu-p5-maddening-orchestra-flare",
        "dmu-p5-maddening-orchestra-holy",

        // Wind direction, with no direction attached to it.
        "dmu-p3-bowels-of-agony-debuffs-and-short-element-1",
        "dmu-p3-bowels-of-agony-debuffs-and-short-element-2",

        // The catch-all tether call already says one is on you, and says it once.
        "dmu-p1-gravitas-and-vitrophyre-tethers-2",
        "dmu-p1-indulgent-will-and-idyllic-will-tethers-early",
        "dmu-p1-pulse-wave-tethers",
        "dmu-p3-black-hole-2-nothingness-2",
        "dmu-p3-black-hole-6-nothingness-10",
    ];
}
