using System;
using Dalamud.Game;
using Lumina.Excel;

namespace FrenMits;

// Everything FrenMits ships is written in English.
public static class GameSheets
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
            // A client with no English data falls back to the local sheet.
            if (!_warned)
            {
                _warned = true;
                Service.Log?.Warning(ex, "FrenMits: English game data unavailable; falling back to the client language");
            }
            try { return Service.DataManager.GetExcelSheet<T>(); }
            catch { return null; }
        }
    }
}
