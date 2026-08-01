namespace FrenMits;

// The slice of the plugin the config migrations touch.
public interface IMigrationHost
{
    Configuration Config { get; }

    // v5: rebake every built-in from the current sheet data.
    int ResetAllBuiltins();

    // v16..v19: stash a restorable copy before a rebake.
    void SnapshotFight(FightProfile fight, string reason);
}
