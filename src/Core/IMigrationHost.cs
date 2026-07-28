namespace FrenMits;

// The narrow slice of the plugin that the versioned config migrations actually
// touch.
public interface IMigrationHost
{
    Configuration Config { get; }

    // v5: a full rebake of every built-in fight from the current sheet data.
    int ResetAllBuiltins();

    // v16..v19: stash a restorable copy of a fight before a rebake touches it.
    void SnapshotFight(FightProfile fight, string reason);
}
