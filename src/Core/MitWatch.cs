using System;
using System.Collections.Generic;
using Lumina.Excel.Sheets;

namespace FrenMits;

// Reads your active mit buffs by status name, for the indicator.
public static class MitWatch
{
    public readonly record struct Active(uint IconId, string Name, float Remaining, MitTypes.Kind Kind);

    // What a status id turned out to be; null means it is not a mit.
    private readonly record struct Known(uint IconId, string Name, MitTypes.Kind Kind);

    private static readonly Dictionary<uint, Known?> Resolved = new();

    // Fills the caller's list, since the bar asks for this every frame.
    public static void Fill(List<Active> into)
    {
        into.Clear();
        try
        {
            var me = Plugin.LocalPlayer;
            if (me == null) return;

            foreach (var st in me.StatusList)
            {
                if (st is null || st.StatusId == 0) continue;
                if (Resolve(st.StatusId) is not { } known) continue;
                into.Add(new Active(known.IconId, known.Name, MathF.Abs(st.RemainingTime), known.Kind));
            }
        }
        catch (Exception ex)
        {
            // Leave a trail, since no mits up looks the same as a failed read.
            Swallowed.Report("active mit read", ex);
        }
    }

    // A status id means the same thing all session, so name it once.
    private static Known? Resolve(uint statusId)
    {
        if (Resolved.TryGetValue(statusId, out var cached)) return cached;

        // English, so the keyword tables can classify the status.
        var sheet = GameSheets.English<Status>();
        // No sheet yet is a bad moment to ask, not an answer worth keeping.
        if (sheet == null) return null;

        Known? known = null;
        if (sheet.GetRowOrDefault(statusId) is { } row)
        {
            var name = row.Name.ExtractText();
            var kind = string.IsNullOrWhiteSpace(name) ? MitTypes.Kind.Other : MitTypes.Classify(name);
            // Only recognized mits belong on the bar.
            if (kind != MitTypes.Kind.Other) known = new Known((uint)row.Icon, name, kind);
        }
        Resolved[statusId] = known;
        return known;
    }
}
