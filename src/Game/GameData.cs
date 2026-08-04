using System;
using Dalamud.Game;
using Lumina.Excel;

namespace FrenMits.Game;

// Everything FrenMits ships is written in English.
public static class GameData
{
    private static bool _warned;

    public static ExcelSheet<T>? English<T>() where T : struct, IExcelRow<T>
    {
        try
        {
            return Service.DataManager.GetExcelSheet<T>(ClientLanguage.English);
        }
        catch (Exception ex)
        {
            // A client with no English data falls back to local.
            if (!_warned)
            {
                _warned = true;
                Service.Log?.Warning(ex, "FrenMits: English game data unavailable; falling back to the client language");
            }
            try { return Service.DataManager.GetExcelSheet<T>(); }
            catch { return null; }
        }
    }

    // A duty's display name, or the generic label when it can't be read.
    public static string DutyName(uint territory)
    {
        try
        {
            var t = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()?.GetRowOrDefault(territory);
            var name = t?.ContentFinderCondition.ValueNullable?.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name))
                return char.ToUpperInvariant(name![0]) + name[1..];
        }
        catch (Exception ex) { Swallowed.Report("duty name lookup", ex); }
        return "Duty timeline";
    }
}
