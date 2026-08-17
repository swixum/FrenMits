using Jint;
using Jint.Native;
using Jint.Native.Object;

namespace FrenAlerts.Engine.Scripts;

// Their fights, run as they are written.
//
// The whole trigger set is carried over: base.js is the shared line library and
// output resolver, kit.js is the authoring layer, and each fight
// file registers itself through defineFight. Nothing here edits any of it.
//
// This is the loading half only: their files read in their own order, which fight
// covers which zone, and their per-pull state rebuilt from their own initData.
// Compiling and firing the triggers is ScriptTriggerRunner, which takes the engine
// this owns.
//
// Loaded rather than retyped because a fight is 162 triggers and every one retyped
// is a chance to get a word or a condition subtly wrong, silently, in a way only a
// real pull would find.
public sealed class ScriptFights : IDisposable
{
    // Their harness, in the order their own host loads it. Named once, in
    // ScriptLoading, because the scoping rule that goes with it lives there too.
    public static string[] Harness => ScriptLoading.Harness;

    // A script that misbehaves stops rather than taking the frame with it. Their
    // triggers are small, so this is far above anything real and only ever catches
    // a runaway.
    private const int StatementLimit = 200_000;

    private Jint.Engine? _js;

    // Which zone each registered fight belongs to, so an event only ever reaches the
    // fight it is about.
    //
    // A list per zone, not one fight: two of their files register against the same
    // zone, because M12S is written as two modules for the two halves of the same
    // encounter. Keyed one to one, the second file quietly replaced the first and
    // half the fight had no triggers at all.
    private readonly Dictionary<ushort, List<int>> _byZone = new();

    // Which of their files each registered fight came from, which is the only link
    // between a fight and the timeline of the same name sitting beside it.
    private readonly Dictionary<int, string> _fileOf = new();

    public IEnumerable<ushort> Zones => _byZone.Keys;

    public string? Problem { get; private set; }

    // Their fights, counted as registered rather than as zones: two in one zone is
    // two fights, and a count that said one would hide the file that failed to load.
    public int FightsLoaded => _fileOf.Count;

    // Every fight registered for a zone, in the order their files were read.
    public IReadOnlyList<int> SetsFor(ushort zone) =>
        _byZone.TryGetValue(zone, out var sets) ? sets : [];

    // The timeline files this zone's fights are named after. Theirs pair by name and
    // by nothing else: dancingmad.js beside dancingmad.txt, lindwurm_a.js beside lindwurm_a.txt.
    public IEnumerable<string> TimelineKeysFor(ushort zone)
    {
        foreach (var set in SetsFor(zone))
            if (_fileOf.TryGetValue(set, out var key)) yield return key;
    }

    // Loads their harness and every fight file in the folder.
    //
    // One engine holds all of them, because their files are independent and a fight
    // is chosen by zone rather than by which engine it lives in. A single file that
    // fails to parse is reported and skipped rather than taking the rest with it: a
    // fight nobody is standing in should not cost the one they are.
    public void Load(string folder)
    {
        try
        {
            _js?.Dispose();
            _js = new Jint.Engine(o => o.LimitRecursion(64).Strict(false)
                                   .MaxStatements(StatementLimit));
            _byZone.Clear();
            _fileOf.Clear();
            Problem = null;

            foreach (var name in Harness)
                _js.Execute(File.ReadAllText(Path.Combine(folder, name)));

            var broken = new List<string>();
            foreach (var file in Directory.EnumerateFiles(folder, "*.js").OrderBy(f => f))
            {
                if (ScriptLoading.IsHarness(Path.GetFileName(file))) continue;

                // Counted either side of the file, because a file is free to register
                // more than one fight and nothing in what it registers says which
                // file it came from.
                var before = Registered;
                try { _js.Execute(ScriptLoading.Wrap(File.ReadAllText(file))); }
                catch (Exception ex) { broken.Add($"{Path.GetFileName(file)}: {ex.Message}"); continue; }
                Index(Path.GetFileNameWithoutExtension(file), before);
            }

            // Anything sitting in the patches folder, applied on top of their files
            // the way their own host does. Nothing ships in there; it is where a fix
            // for one of their fights goes without editing the file it patches.
            foreach (var trouble in ScriptLoading.LoadPatches(
                         _js, Path.Combine(folder, ScriptLoading.PatchFolder)))
                broken.Add(trouble);

            if (broken.Count > 0) Problem = string.Join("; ", broken);
        }
        catch (Exception ex)
        {
            _js = null;
            _byZone.Clear();
            _fileOf.Clear();
            Problem = ex.Message;
        }
    }

    private int Registered => _js is null ? 0 : (int)_js.Evaluate("triggerSets.length").AsNumber();

    // What one of their files just registered: which zone each fight covers, and the
    // name to look its timeline up under.
    private void Index(string file, int from)
    {
        if (_js is null) return;

        for (var i = from; i < Registered; i++)
        {
            _fileOf[i] = ScriptTimelines.KeyOf(file);

            var zone = _js.Evaluate($"triggerSets[{i}].zoneId || 0").AsNumber();
            if (zone is not (> 0 and < 65536)) continue;

            if (!_byZone.TryGetValue((ushort)zone, out var sets)) _byZone[(ushort)zone] = sets = [];
            sets.Add(i);
        }
    }

    public bool Knows(ushort zone) => _byZone.ContainsKey(zone);

    // What a registered fight calls itself, which is the key their own strategy
    // picks are held under.
    public string IdOf(int setIndex) =>
        _js is null ? "" : _js.Evaluate($"triggerSets[{setIndex}].id || ''").AsString();

    // How many of their triggers cover this zone, for the window to report. Every
    // fight in the zone, since all of them run.
    public int TriggerCount(ushort zone)
    {
        if (_js is null) return 0;

        var total = 0;
        foreach (var i in SetsFor(zone))
            total += (int)_js.Evaluate($"triggerSets[{i}].triggers.length").AsNumber();
        return total;
    }

    // What to call the fight on screen. The first of their names for the zone, which
    // for a fight written in two halves is the half that opens it.
    public string NameOf(ushort zone)
    {
        if (_js is null) return "";

        foreach (var i in SetsFor(zone))
            return _js.Evaluate($"triggerSets[{i}].name || ''").AsString();
        return "";
    }

    // Starts a pull for a zone: their own per-fight state, rebuilt from their own
    // initData so the second pull of the night does not carry the first one's
    // counters. Held in the script rather than here, because their triggers read and
    // write it directly as `data`.
    public void StartPull(ushort zone, string me, string role, string job)
    {
        if (_js is null || !_byZone.ContainsKey(zone)) return;

        try
        {
            // One state for the zone, with every fight in it folded in. Their two
            // halves of M12S are one encounter written twice and expect to read each
            // other's counters; a state each would give the second half a first half
            // that never happened.
            _js.Execute("var __data = {};");
            foreach (var i in SetsFor(zone))
                _js.Execute($"(function (s) {{ for (var k in s) __data[k] = s[k]; }})"
                            + $"(triggerSets[{i}].initData());");

            _js.Execute("__data.me = " + Quote(me) + ";");
            _js.Execute("__data.role = " + Quote(role) + ";");
            _js.Execute("__data.job = " + Quote(job) + ";");
            // Their party helper, which several of their lines call to name somebody.
            _js.Execute("__data.party = { member: function (n) { return n || 'someone'; } };");
            _js.Execute("__data.triggerSetConfig = __data.triggerSetConfig || {};");
        }
        catch (Exception ex)
        {
            Problem = ex.Message;
        }
    }

    // A single-quoted JavaScript string. Their names come from the game and can hold
    // an apostrophe, which unescaped would end the string and change the program.
    private static string Quote(string s) =>
        "'" + (s ?? "").Replace("\\", "\\\\").Replace("'", "\\'") + "'";

    public void Dispose()
    {
        _js?.Dispose();
        _js = null;
    }

    // The engine their fights are loaded into. Handed out because the two halves that
    // run them take it: their trigger runner is constructed on one, and their world
    // reads are bound into one. Nothing else should be reaching in here.
    public Jint.Engine? Js => _js;
}
