using System;
using System.Collections.Generic;

namespace FrenMits;

// Action id -> its display name, memoized. Used when turning a captured pull into
// timeline rows, where the same handful of boss abilities get looked up over and
// over across a fight.
public static class ActionNames
{
    // Concurrent for the same reason as BossNames: cheap insurance against a
    // torn write poisoning the table for a whole session.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, string> _cache = new();

    public static string Of(uint id)
    {
        if (id == 0) return "";
        if (_cache.TryGetValue(id, out var hit)) return hit;
        var name = "";
        try
        {
            // The client's own language here: these names are shown to the user as
            // mechanic labels, not matched against our English tables.
            var row = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRowOrDefault(id);
            name = row?.Name.ExtractText() ?? "";
        }
        catch (Exception ex) { Swallowed.Report("action name lookup", ex); }
        // Never memoize a miss caused by the sheet not being ready yet.
        if (name.Length > 0) _cache[id] = name;
        return name;
    }
}
