using System;
using System.Collections.Generic;

namespace FrenMits.Game;

// Action id to display name, memoized.
public static class ActionNames
{
    // Concurrent so a torn write can't poison the table.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, string> _cache = new();

    public static string Of(uint id)
    {
        if (id == 0) return "";
        if (_cache.TryGetValue(id, out var hit)) return hit;
        var name = "";
        try
        {
            // Client language here, since these are shown as labels.
            var row = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRowOrDefault(id);
            name = row?.Name.ExtractText() ?? "";
        }
        catch (Exception ex) { Swallowed.Report("action name lookup", ex); }
        // Never memoize a miss from an unready sheet.
        if (name.Length > 0) _cache[id] = name;
        return name;
    }
}
