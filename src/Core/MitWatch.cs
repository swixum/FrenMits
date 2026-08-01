using System;
using System.Collections.Generic;
using Lumina.Excel.Sheets;

namespace FrenMits;

// Reads your active mit buffs by status name, for the indicator.
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
            // English, so the keyword tables can classify the status.
            var sheet = GameSheets.English<Status>();
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
        catch (Exception ex)
        {
            // Leave a trail, since no mits up looks the same as a failed read.
            Swallowed.Report("active mit read", ex);
        }
        return list;
    }
}
