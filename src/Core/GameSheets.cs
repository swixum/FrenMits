using System;
using Dalamud.Game;
using Lumina.Excel;

namespace FrenMits;

// Everything FrenMits ships is written in English: the baked sheets say
// "Reprisal", the mit-type keyword tables say "sacred soil", the boss anchors say
// "Usurper of Frost". Those strings get MATCHED against the game's own rows to
// find an action id, an icon or a status - and on a French, German or Japanese
// client those rows come back localized, so every match silently fails. The
// result was a client where cooldown-aware calls, ability icons and the mit recap
// all quietly did nothing, with no error to go on.
//
// So any lookup that matches one of OUR names reads the English sheet, while row
// ids, recasts and icon ids (which have no language) stay exactly as they were.
// Names shown TO the user - duty names, zone names - deliberately keep using the
// client's own language and must not come through here.
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
            // A client with no English data installed still gets the local sheet:
            // matching will mostly miss, but nothing crashes and an English client
            // (the common case) is unaffected.
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
