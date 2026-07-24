namespace FrenMits;

// The narrow slice of the plugin that the versioned config migrations actually
// touch. Plugin implements it for the real thing; the test suite implements it
// with a stand-in so the whole v2..v23 chain can be replayed against a saved
// config with no game running - a bad migration is unrecoverable by the time a
// user notices, so it is the one path worth proving offline.
public interface IMigrationHost
{
    Configuration Config { get; }

    // v5: a full rebake of every built-in fight from the current sheet data.
    int ResetAllBuiltins();

    // v16..v19: stash a restorable copy of a fight before a rebake touches it.
    void SnapshotFight(FightProfile fight, string reason);
}
