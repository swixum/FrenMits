using System.Runtime.CompilerServices;

namespace FrenMits.Tests;

internal static class Fx
{
    // Configuration.Save() goes through Dalamud, which isn't there in a test host.
    // The plugin already has the switch for this (it uses it when a config won't
    // load), so flip it once for the whole assembly.
    [ModuleInitializer]
    internal static void Init() => Configuration.SuppressSave = true;

    public static MitLine Line(float time, string mechanic, string action, params string[] jobs)
        => new() { Time = time, Mechanic = mechanic, Action = action, Jobs = new List<string>(jobs) };

    // A fight carrying one built-in slot's current bake.
    public static FightProfile Builtin(uint territory, string slot)
    {
        var f = new FightProfile { TerritoryId = territory, Name = FrenMits.Builtin.Name(territory) };
        FrenMits.Builtin.ApplySlot(f, slot);
        return f;
    }

    // A Dancing Mad profile shaped the way a pre-v8 config really was: lines from
    // the FROZEN previous bake (DmuLegacy, which the repo keeps for exactly this),
    // under the old MT/OT/D1 slot names, plus whatever custom lines the caller
    // wants to prove survive the chain.
    public static FightProfile LegacyDmu(string legacySlot = "MT", params MitLine[] customs)
    {
        var lines = DmuLegacy.BuildLines(legacySlot);
        foreach (var c in customs) { c.Custom = true; lines.Add(c); }
        lines = lines.OrderBy(l => l.Time).ToList();
        var fight = new FightProfile
        {
            TerritoryId = FrenMits.Builtin.DmuTerritory,
            Name = "Dancing Mad (Ultimate)",   // the pre-v-rename name
            Category = "Ultimate",
            Slot = legacySlot,
            Lines = lines,
        };
        fight.SavedSlots[legacySlot] = lines;  // the alias invariant, as saved
        return fight;
    }

    public static Configuration ConfigAt(int version, params FightProfile[] fights)
    {
        var c = new Configuration { Version = version };
        c.Fights.AddRange(fights);
        return c;
    }
}

// Stands in for the plugin while the migration chain runs: same config, same
// rebake behaviour, but snapshots are recorded instead of written to disk.
internal sealed class FakeMigrationHost : IMigrationHost
{
    public Configuration Config { get; }
    public List<(string Fight, string Reason)> Snapshots { get; } = new();
    public int ResetAllCalls { get; private set; }

    public FakeMigrationHost(Configuration config) => Config = config;

    // Mirrors Plugin.ResetAllBuiltins minus the disk work.
    public int ResetAllBuiltins()
    {
        ResetAllCalls++;
        var n = 0;
        foreach (var f in Config.Fights)
        {
            if (!Builtin.Has(f.TerritoryId)) continue;
            f.SavedSlots.Clear();
            f.DeletedCalls.Clear();
            if (!string.IsNullOrEmpty(f.Slot)) Builtin.ResetSlot(f, f.Slot);
            else { f.Lines.Clear(); f.AutoLoaded = false; }
            n++;
        }
        return n;
    }

    public void SnapshotFight(FightProfile fight, string reason) => Snapshots.Add((fight.Name, reason));
}
