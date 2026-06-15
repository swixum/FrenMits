using System;
using System.Collections.Generic;
using Lumina.Excel.Sheets;

namespace FrenMits;

// Reads the local player's currently-active mitigation buffs (by status name, so
// no hard-coded ids) for the active-mits indicator. Read-only and fully guarded;
// if anything about the game state isn't available it simply returns nothing.
public static class MitWatch
{
    public readonly record struct Active(uint IconId, string Name, float Remaining, MitTypes.Kind Kind);

    public static List<Active> Current()
    {
        var list = new List<Active>();
        try
        {
            var me = Plugin.LocalPlayer;
            if (me == null) return list;
            var sheet = Service.DataManager.GetExcelSheet<Status>();
            if (sheet == null) return list;

            foreach (var st in me.StatusList)
            {
                if (st is null || st.StatusId == 0) continue;
                if (sheet.GetRowOrDefault(st.StatusId) is not { } row) continue;
                var name = row.Name.ExtractText();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var kind = MitTypes.Classify(name);
                if (kind == MitTypes.Kind.Other) continue; // only show recognised mits
                list.Add(new Active((uint)row.Icon, name, MathF.Abs(st.RemainingTime), kind));
            }
        }
        catch
        {
            // Never let a game-state read disturb the draw loop.
        }
        return list;
    }
}
