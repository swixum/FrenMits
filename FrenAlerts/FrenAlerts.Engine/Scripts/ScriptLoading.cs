using Jint;

namespace FrenAlerts.Engine.Scripts;

// How one of their fight files has to be handed to the engine.
//
// Every fight of theirs declares things at the top of its file under the same few
// names: `headMarkerData`, `center`, `centerX`. Loaded as written into one engine,
// the second file to use a name is a syntax error and the whole fight is dropped,
// so a plugin that loads all nine ends up knowing three of them. Nothing on screen
// says so: the zone has no calls and looks like a fight nobody wrote yet.
//
// Theirs wraps each file in a function before running it, which gives every file its
// own scope while `defineFight` and `triggerSets` stay shared. That one line is the
// difference between three fights and nine, so it lives here with a name on it
// rather than inline in a loader where the next person deletes it as noise.
public static class ScriptLoading
{
    // Their wrapper, newlines and all: the leading one keeps a file that opens with a
    // comment from swallowing its own first statement, and the trailing one does the
    // same for a file that ends without a newline.
    public static string Wrap(string source) => "(function(){\n" + source + "\n})();";

    // The two files that are not fights and must stay in the shared scope: everything
    // else reads their helpers by name. Named in load order, which is the order they
    // depend on each other in and not alphabetical.
    public static readonly string[] Harness = ["base.js", "kit.js"];

    public static bool IsHarness(string fileName) =>
        Array.Exists(Harness, h => string.Equals(h, fileName, StringComparison.OrdinalIgnoreCase));

    // Our own files, run after every fight of theirs has registered.
    //
    // A patch adds something their file does not have, by wrapping what it already
    // does rather than by editing it: the Dancing Mad black hole tethers called
    // relative to Kefka are the first. Kept in their own folder so replacing the
    // whole set of their fights with a newer copy cannot take one away.
    public const string PatchFolder = "patches";

    // Loads every patch beside a fight folder, wrapped the same way, and returns what
    // failed. A patch that throws is a patch skipped, never a fight lost.
    public static IReadOnlyList<string> LoadPatches(Jint.Engine js, string folder)
    {
        var broken = new List<string>();

        // Said rather than shrugged off. This returned quietly for a missing folder,
        // which reads exactly like every patch loading fine, and that is how the
        // folder being looked for in the wrong place went unnoticed: the modes our
        // patches add were simply never on any dropdown.
        if (!Directory.Exists(folder))
        {
            broken.Add($"no patch folder at {folder}, so none of ours loaded");
            return broken;
        }

        foreach (var path in Directory.GetFiles(folder, "*.js").OrderBy(f => f, StringComparer.Ordinal))
        {
            try { js.Execute(Wrap(File.ReadAllText(path))); }
            catch (Exception ex) { broken.Add($"{Path.GetFileName(path)}: {ex.Message}"); }
        }

        return broken;
    }
}
