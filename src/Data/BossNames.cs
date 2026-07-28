using System;
using System.Collections.Generic;

namespace FrenMits;

// Resolves boss display names to their BNpcName row id.
public static class BossNames
{
    // Concurrent: a torn write would corrupt the table for the rest of the session.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, uint> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static uint Resolve(string singular)
    {
        if (string.IsNullOrWhiteSpace(singular)) return 0;
        if (_cache.TryGetValue(singular, out var cached)) return cached;

        uint id = 0;
        try
        {
            // English: the anchors name their bosses in English (see GameSheets).
            var sheet = GameSheets.English<Lumina.Excel.Sheets.BNpcName>();
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
            // Data not ready yet: return without caching so a later call
            // retries, instead of pinning this name to 0 for the session.
            Service.Log?.Warning(ex, "FrenMits: BNpcName resolve failed");
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
