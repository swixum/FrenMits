using System;
using System.Collections.Generic;

namespace FrenAlerts.Ui;

// A job id to the three letters the game prints for it.
//
// Read out of the game's own sheet rather than a table written here, the same rule the
// call icons follow: a list typed in by hand is wrong the day a job is added, and wrong
// silently, because a stale row still hands back three plausible letters.
internal static class JobNames
{
    // Every job in the game and room to spare. Cleared rather than evicted one at a
    // time: the whole set costs a few hundred bytes and refills in one frame.
    private const int MaxCached = 64;

    private static readonly Dictionary<uint, string> Abbrevs = new();

    public static string Abbrev(uint jobId)
    {
        if (jobId == 0) return "";
        if (Abbrevs.TryGetValue(jobId, out var hit)) return hit;

        UiServices.Ensure();
        var name = "";
        if (UiServices.Ready)
        {
            try
            {
                if (UiServices.Data.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>()
                        ?.GetRowOrDefault(jobId) is { } row)
                    name = row.Abbreviation.ExtractText();
            }
            catch (Exception ex) { Service.Log.Warning(ex, $"job {jobId} has no abbreviation"); }
        }

        // A miss is remembered too, so a job the sheet cannot answer for is not looked
        // up again on every frame the list is open.
        if (Abbrevs.Count >= MaxCached) Abbrevs.Clear();
        return Abbrevs[jobId] = name;
    }

    // Dropped when the game is left, so a sheet read on the character screen cannot
    // outlive the language or the client it was read from.
    public static void Forget() => Abbrevs.Clear();
}
