using System.Globalization;

namespace FrenAlerts.Engine.UserTriggers;

// What a fight's casts and statuses are called, ported from theirs.
//
// The client hides the names of a new fight's abilities behind placeholder strings
// until the patch that opens it, so everything the engine sees during a world first
// is an id with no words on it. Their answer was to ship the mapping as a file, and
// this reads the same file in the same shape: a placeholder, a pipe, and the name.
//
// Worth having long after the names go public, because this is also the list a
// trigger editor offers somebody who wants to pick a cast rather than type its id.
public sealed class NameCatalog
{
    // Which of the two tables a placeholder belongs to. Their own hashes, and the
    // only thing that tells a cast from a status in the file.
    private const string CastHash = "SE2DC5B04";
    private const string StatusHash = "S74CFC3B0";

    private readonly Dictionary<(CatalogKind Kind, uint Id), string> _names = [];

    public int Count => _names.Count;

    public string? Of(CatalogKind kind, uint id) => _names.GetValueOrDefault((kind, id));

    public string CastName(uint id) => Of(CatalogKind.Cast, id) ?? "";

    public string StatusName(uint id) => Of(CatalogKind.Status, id) ?? "";

    public IEnumerable<CatalogEntry> All()
    {
        foreach (var ((kind, id), name) in _names)
            yield return new CatalogEntry(kind, id, name);
    }

    // One of their files. Read rather than merged blindly: the same id can appear
    // twice with two names, and the first one wins, as it does for them.
    public int Read(string text)
    {
        var added = 0;

        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            var bar = line.IndexOf('|');
            if (bar <= 0) continue;

            var placeholder = line[..bar];
            var name = line[(bar + 1)..].Trim();
            if (name.Length == 0 || !placeholder.StartsWith("_rsv_", StringComparison.Ordinal)) continue;

            // _rsv_<id>_<unknown>_<unknown>_<sub>_<unknown>_<table hash>_<end hash>
            var parts = placeholder.Split('_');
            if (parts.Length < 8) continue;
            if (!uint.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) continue;

            CatalogKind kind;
            if (parts[7] == CastHash) kind = CatalogKind.Cast;
            // A status placeholder carries its name in one row and its description in
            // another; only the first is the name.
            else if (parts[7] == StatusHash && parts[5] == "0") kind = CatalogKind.Status;
            else continue;

            if (_names.TryAdd((kind, id), name)) added++;
        }

        return added;
    }

    // Every file in a folder, which is how a second fight's names arrive later.
    public int Load(string folder)
    {
        if (!Directory.Exists(folder)) return 0;

        var added = 0;
        foreach (var path in Directory.GetFiles(folder, "*.txt"))
            added += Read(File.ReadAllText(path));

        return added;
    }
}

public readonly record struct CatalogEntry(CatalogKind Kind, uint Id, string Name);

public enum CatalogKind : byte
{
    Cast,
    Status,
    Headmarker,
    Tether,
}

// Which timeline file belongs to which fight, and what that fight is called.
//
// Theirs, verbatim, including the two halves of the last savage fight staying two
// entries. The script id is the one each fight file registers itself under, so this
// is the join between the three folders: a script, a timeline and a name.
public static class FightFiles
{
    public readonly record struct Entry(string TimelineFile, string Name, string ScriptId);

    public static readonly Entry[] All =
    [
        new("vampfatale.txt", "M9S - Vamp Fatale", "AacHeavyweightM1Savage"),
        new("redhotdeepblue.txt", "M10S - Red Hot & Deep Blue", "AacHeavyweightM2Savage"),
        new("tyrantcomet.txt", "M11S - The Tyrant & Comet", "AacHeavyweightM3Savage"),
        new("lindwurm_a.txt", "M12S PT1 - Lindwurm", "AacHeavyweightM4SavageP1"),
        new("lindwurm_b.txt", "M12S PT2 - Lindwurm", "AacHeavyweightM4SavageP2"),
        new("dancingmad.txt", "Dancing Mad", "DancingMadUltimate"),
        new("coil.txt", "The Unending Coil", "TheUnendingCoilOfBahamutUltimate"),
        new("refrain.txt", "UWU - The Weapon's Refrain", "TheWeaponsRefrainUltimate"),
        new("unmaking.txt", "The Unmaking (Extreme)", "TheUnmakingExtreme"),
    ];

    public static Entry? ForScript(string scriptId)
    {
        foreach (var entry in All)
            if (string.Equals(entry.ScriptId, scriptId, StringComparison.Ordinal)) return entry;
        return null;
    }

    public static Entry? ForTimeline(string file)
    {
        foreach (var entry in All)
            if (string.Equals(entry.TimelineFile, file, StringComparison.OrdinalIgnoreCase)) return entry;
        return null;
    }
}
