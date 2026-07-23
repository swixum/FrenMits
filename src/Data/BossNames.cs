using System;
using System.Collections.Generic;

namespace FrenMits;

// Resolves boss display names to their BNpcName row id (the NameId an actor
// reports), so boss-presence anchors can be baked by name without hardcoding
// ids.
public static class BossNames
{
    private static readonly Dictionary<string, uint> _cache = new(StringComparer.OrdinalIgnoreCase);

    public static uint Resolve(string singular)
    {
        if (string.IsNullOrWhiteSpace(singular)) return 0;
        if (_cache.TryGetValue(singular, out var cached)) return cached;

        uint id = 0;
        try
        {
            var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.BNpcName>();
            if (sheet != null)
                foreach (var row in sheet)
                    if (string.Equals(row.Singular.ExtractText(), singular, StringComparison.OrdinalIgnoreCase))
                    {
                        id = row.RowId;
                        break;
                    }
        }
        catch (Exception ex)
        {
            // Data not ready yet: return without caching so a later call retries,
            // instead of pinning this name to 0 for the session.
            Service.Log.Warning(ex, "FrenMits: BNpcName resolve failed");
            return 0;
        }

        _cache[singular] = id;
        return id;
    }

    public static void Add(List<BossAnchor> list, string singular, float time, string label)
    {
        var id = Resolve(singular);
        if (id != 0)
            list.Add(new BossAnchor { NameId = id, Time = time, Label = label });
    }
}
