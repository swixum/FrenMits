using FrenAlerts.Engine;

namespace FrenAlerts.Game;

// Builds the engine for a fight: which triggers it gets, in which order, and which
// of them are switched on.
//
// Order is the whole job. A fight's own module goes in first because it is measured,
// then the marker calls skip whatever it already covers, then the pack skips both,
// then the group's plan goes in last so it is never skipped as covered by the call it
// hangs on.
public sealed class FightLoader
{
    private readonly List<CallSpec> _pack;

    private List<CallSpec> _plan = [];

    public FightLoader() => _pack = LoadPack();

    // Which fight is loaded, for the status command.
    public string Fight { get; private set; } = "none";

    public int PlanCalls => _plan.Count;

    // Asked once per call as a fight loads: true to switch it on, false off, null to
    // leave it as the pack shipped it.
    public Func<string, bool?>? Switched { get; set; }

    // Which strat the group runs, asked per fight and setting.
    public Func<ushort, string, string>? Strat { get; set; }

    // A fresh engine per fight, rather than clearing one: every bound and every reset
    // lives inside it, and a new one cannot inherit the last fight's state.
    public TriggerEngine Build(uint territory, bool theirs = false)
    {
        var engine = new TriggerEngine();

        // Their fight owns this zone, so ours is not built: not the module, not the
        // marker calls, not the pack and not the plan. An engine with no triggers,
        // because everything downstream still reads the party, the actors and the
        // phase off it.
        //
        // All of it or none of it. Half of ours left in beside all of theirs is two
        // calls for one mechanic, which is louder and later than either on its own.
        if (theirs)
        {
            Fight = "theirs";
            return engine;
        }

        Fight = AddFightModule(engine, territory);

        engine.AddRange(MarkerCalls.Triggers((ushort)territory, engine.Triggers));
        var built = engine.Triggers.Count;

        // A fight that names its own phase changes gets those and nothing else; the
        // pack's guesses only fill in for fights with no module.
        if (territory == DancingMad.Territory)
            engine.State.LearnPhases(DancingMad.PhaseChanges());
        else engine.State.LearnPhases(_pack
            .Where(s => s.Territory == territory && s.Phase > 0)
            .Select(s => (s.On, s.MatchId, s.Phase)));
        engine.AddRange(TriggerPack.Build(_pack, (ushort)territory, engine.Triggers));

        engine.AddRange(TriggerPack.Build(_plan, (ushort)territory, []));

        if (Fight == "none" && engine.Triggers.Count > 0) Fight = $"territory {territory}";

        // Whatever was switched by hand wins over how the call shipped, applied here
        // so a change takes effect on the next zone rather than a reload.
        // The group's answers, read fresh each load so a change takes effect on the
        // next zone rather than the next restart.
        if (Strat is { } strat)
            engine.Player.Strat = key => strat((ushort)territory, key);

        if (Switched is { } ask)
            foreach (var t in engine.Triggers)
                if (ask(t.Id) is { } wanted) t.Enabled = wanted;

        Service.Log.Debug(
            $"Fren Alerts: territory {territory}, {Fight}, {built} built-in + " +
            $"{engine.Triggers.Count - built} from the pack, " +
            $"{engine.Triggers.Count(t => t.Enabled)} of {engine.Triggers.Count} switched on.");

        return engine;
    }

    // Which boss lines this fight reads, so the listener watches those and no
    // others. Empty for every fight but one: a yell is only a mechanic where the
    // fight has nothing else to announce it with.
    public static IReadOnlySet<uint> YellsFor(uint territory) =>
        territory == UnendingCoil.Territory ? UnendingCoil.QuoteIds : new HashSet<uint>();

    private static string AddFightModule(TriggerEngine engine, uint territory)
    {
        if (territory == DancingMad.Territory)
        {
            engine.AddRange(DancingMad.Triggers());
            engine.AddRange(DancingMad.AllSequences());
            return "Dancing Mad";
        }
        if (territory == Lindwurm.Territory)
        {
            engine.AddRange(Lindwurm.Triggers());
            return "M12S Lindwurm";
        }
        if (territory == RedHotDeepBlue.Territory)
        {
            engine.AddRange(RedHotDeepBlue.Triggers());
            return "M10S Red Hot & Deep Blue";
        }
        if (territory == TyrantComet.Territory)
        {
            engine.AddRange(TyrantComet.Triggers());
            return "M11S The Tyrant & Comet";
        }
        if (territory == VampFatale.Territory)
        {
            engine.AddRange(VampFatale.Triggers());
            return "M9S Vamp Fatale";
        }
        if (territory == WeaponsRefrain.Territory)
        {
            engine.AddRange(WeaponsRefrain.Triggers());
            return "The Weapon's Refrain";
        }
        if (territory == UnendingCoil.Territory)
        {
            engine.AddRange(UnendingCoil.Triggers());
            return "The Unending Coil of Bahamut";
        }
        return "none";
    }

    // Reads plan.txt from the config folder and returns what it did, because a plan
    // that silently matched nothing looks exactly like a plan that worked.
    public string ReadPlan(ushort territory)
    {
        try
        {
            var path = Path.Combine(Service.PluginInterface.ConfigDirectory.FullName, "plan.txt");
            if (!File.Exists(path))
            {
                _plan = [];
                return $"No plan file. Write one at {path} and run this again.";
            }

            var entries = RaidPlan.Read(File.ReadLines(path));
            var missed = new List<string>();
            _plan = RaidPlan.Apply(_pack, entries, territory, missed).ToList();

            var note = missed.Count == 0
                ? ""
                : $" {missed.Count} matched no call in this fight: {string.Join(", ", missed.Take(5))}.";
            return $"Plan: {entries.Count} mechanics, {_plan.Count} calls for {Fight}.{note}";
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Fren Alerts: could not read the plan.");
            return "The plan could not be read; the log has why.";
        }
    }

    // Read once at startup, because a raid night is one process and re-reading a file
    // on every zone change is disk work for no gain.
    private static List<CallSpec> LoadPack()
    {
        try
        {
            var dir = Service.PluginInterface.AssemblyLocation.Directory?.FullName;
            if (dir is null) return [];

            var path = Path.Combine(dir, "calls.facall");
            if (!File.Exists(path))
            {
                Service.Log.Warning("Fren Alerts: calls.facall missing, only the built-in fights will speak.");
                return [];
            }
            return CallPack.ReadAll(File.ReadLines(path)).ToList();
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Fren Alerts: could not read the call pack.");
            return [];
        }
    }
}
