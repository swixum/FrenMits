using System.Text.Json;

namespace FrenAlerts.Engine.UserTriggers;

// What a zone has been seen to do, ported from theirs.
//
// Somebody writing their own trigger for a fight needs to pick a cast out of a list
// rather than go hunting for its id, and the only list that is ever complete is the
// one the plugin built while standing in the fight. So every hostile cast, every
// debuff a boss applied, every marker and every tether gets remembered against the
// zone it happened in.
//
// Their filters carry across because they are what keeps the list usable: a cast is
// only recorded from an enemy, and a status only when whatever applied it was
// hostile. Without those the list fills up with the party's own buffs within one
// pull and nobody can find anything in it.
public sealed class LearnedCatalog
{
    // Bumped when the stored shape changes, so an older file is dropped rather than
    // read wrong.
    public const int StoreVersion = 2;

    private readonly Dictionary<ushort, Dictionary<long, CatalogEntry>> _byZone = [];

    public int Zones => _byZone.Count;

    public bool Dirty { get; private set; }

    private static long Key(CatalogKind kind, uint id) => ((long)kind << 32) | id;

    // One event, in the zone it happened in. Told whether the source was hostile,
    // because the engine has no view of the object table.
    public bool Record(ushort territory, TriggerEvent e, bool fromHostile)
    {
        if (territory == 0) return false;

        CatalogKind kind;
        switch (e.Kind)
        {
            case TriggerEventKind.CastStart:
                if (e.SourceSide != ActorSide.Enemy) return false;
                kind = CatalogKind.Cast;
                break;

            case TriggerEventKind.StatusGain:
                if (!fromHostile) return false;
                kind = CatalogKind.Status;
                break;

            case TriggerEventKind.Headmarker:
                kind = CatalogKind.Headmarker;
                break;

            case TriggerEventKind.Tether:
                kind = CatalogKind.Tether;
                break;

            default:
                return false;
        }

        // Something with neither an id nor a name is nothing anybody could pick out
        // of a list later.
        if (e.DataId == 0 && string.IsNullOrEmpty(e.Name)) return false;

        if (!_byZone.TryGetValue(territory, out var zone)) _byZone[territory] = zone = [];

        var key = Key(kind, e.DataId);
        if (zone.ContainsKey(key)) return false;

        zone[key] = new CatalogEntry(kind, e.DataId, e.Name);
        Dirty = true;
        return true;
    }

    // Everything learned for a zone, casts first and each kind in the order it was
    // first seen, which is roughly the order the fight does them in.
    public IReadOnlyList<CatalogEntry> For(ushort territory) =>
        _byZone.TryGetValue(territory, out var zone) ? [.. zone.Values] : [];

    public IReadOnlyList<CatalogEntry> For(ushort territory, CatalogKind kind)
    {
        var found = new List<CatalogEntry>();
        foreach (var entry in For(territory))
            if (entry.Kind == kind) found.Add(entry);
        return found;
    }

    // Seeded rather than learned: their shipped name file fills the same list for a
    // fight nobody has stood in yet.
    public int Seed(ushort territory, IEnumerable<CatalogEntry> entries)
    {
        if (!_byZone.TryGetValue(territory, out var zone)) _byZone[territory] = zone = [];

        var added = 0;
        foreach (var entry in entries)
        {
            if (entry.Id == 0 && string.IsNullOrEmpty(entry.Name)) continue;
            if (zone.TryAdd(Key(entry.Kind, entry.Id), entry)) added++;
        }

        if (added > 0) Dirty = true;
        return added;
    }

    public void Forget(ushort territory)
    {
        if (_byZone.Remove(territory)) Dirty = true;
    }

    public void Clear()
    {
        _byZone.Clear();
        Dirty = true;
    }

    // ---- storage -------------------------------------------------------------

    private sealed class Store
    {
        public int Version { get; set; }
        public List<ZoneStore> Zones { get; set; } = [];
    }

    private sealed class ZoneStore
    {
        public ushort Territory { get; set; }
        public List<CatalogEntry> Entries { get; set; } = [];
    }

    public string Write()
    {
        var store = new Store { Version = StoreVersion };
        foreach (var (territory, zone) in _byZone)
            store.Zones.Add(new ZoneStore { Territory = territory, Entries = [.. zone.Values] });

        return JsonSerializer.Serialize(store);
    }

    // A file from an older version is dropped rather than read wrong: what it holds
    // is a convenience that rebuilds itself the next time somebody pulls the fight.
    public bool Read(string json)
    {
        try
        {
            var store = JsonSerializer.Deserialize<Store>(json);
            if (store is null || store.Version != StoreVersion) return false;

            _byZone.Clear();
            foreach (var zone in store.Zones)
            {
                var entries = new Dictionary<long, CatalogEntry>();
                foreach (var entry in zone.Entries) entries[Key(entry.Kind, entry.Id)] = entry;
                _byZone[zone.Territory] = entries;
            }

            Dirty = false;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public void Save(string path)
    {
        File.WriteAllText(path, Write());
        Dirty = false;
    }

    public bool Load(string path) => File.Exists(path) && Read(File.ReadAllText(path));
}
