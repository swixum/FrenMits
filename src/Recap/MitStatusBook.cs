using System;
using System.Collections.Generic;
using Lumina.Excel.Sheets;

namespace FrenMits.Recap;

// Which game statuses are mitigation, and which button granted them.
//
// The recap used to decide this by running a status name past the word lists
// the sheets are classified with, so anything the game names differently from
// its button - Holy Sheltron's follow-ups, Heart of Corundum, Expedient - was
// invisible. Here a status is keyed by id: seeded from the Status sheet where
// the names already agree, then learned from the action packet, which says
// outright which statuses an action applied. Nothing is spelled from memory.
public static class MitStatusBook
{
    // A status, named by the button it came from.
    public readonly record struct Entry(string Mit, MitTypes.Kind Kind, uint Icon);

    private static readonly Dictionary<uint, Entry> ById = new();
    private static bool _seeded;
    // Sheets can be cold at login, and this is called off the packet thread, so
    // a failed seed waits rather than walking the whole sheet on every status.
    private static DateTime _nextSeed = DateTime.MinValue;

    // A mit the recap can follow.
    private static bool Counts(string mit) => KindOf(mit) != MitTypes.Kind.Other;

    // An upgrade sharing a cooldown family with a classified mit is the same
    // kind of call. The sheet word lists know Shadow Wall but not Shadowed
    // Vigil, which left a tank's whole wall invisible to the recap.
    public static MitTypes.Kind KindOf(string mit)
    {
        var kind = MitTypes.Classify(mit);
        if (kind != MitTypes.Kind.Other) return kind;
        if (!AbilityBook.SharedFamily.TryGetValue(mit, out var family)) return kind;
        foreach (var (sibling, f) in AbilityBook.SharedFamily)
        {
            if (f != family || string.Equals(sibling, mit, StringComparison.OrdinalIgnoreCase)) continue;
            var theirs = MitTypes.Classify(sibling);
            if (theirs != MitTypes.Kind.Other) return theirs;
        }
        return kind;
    }

    // The tracked mits, as a set the action packet can be tested against.
    private static readonly HashSet<string> TrackedMits = BuildTracked();

    private static HashSet<string> BuildTracked()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in AbilityBook.Tracked)
            if (Counts(name)) set.Add(name);
        return set;
    }

    // Whether an action is one the recap follows.
    public static bool IsTrackedAction(string action) => TrackedMits.Contains(action);

    // Statuses the game names exactly like their button need no learning.
    private static void Seed()
    {
        if (_seeded || DateTime.UtcNow < _nextSeed) return;
        _nextSeed = DateTime.UtcNow.AddSeconds(5);
        try
        {
            // English, since every tracked name is written in it.
            var sheet = GameData.English<Status>();
            if (sheet == null) return; // sheets not ready: seed on a later call
            foreach (var row in sheet)
            {
                var name = row.Name.ExtractText();
                if (string.IsNullOrWhiteSpace(name) || !TrackedMits.Contains(name)) continue;
                // A name can carry several rows, and each id is as real as the others.
                ById[row.RowId] = new Entry(name, KindOf(name), (uint)row.Icon);
            }
            _seeded = true;
        }
        catch (Exception ex) { Swallowed.Report("mit status seed", ex); }
    }

    // The mit this status belongs to, or null when it is not one.
    public static Entry? Resolve(uint statusId)
    {
        if (statusId == 0) return null;
        Seed();
        return ById.TryGetValue(statusId, out var e) ? e : null;
    }

    // The action packet naming a status it applied is the whole binding.
    public static void Learn(uint statusId, string mitAction)
    {
        if (statusId == 0 || string.IsNullOrEmpty(mitAction)) return;
        Seed();
        if (ById.ContainsKey(statusId)) return; // a seeded name outranks a press
        if (!TrackedMits.Contains(mitAction)) return;
        ById[statusId] = new Entry(mitAction, KindOf(mitAction), IconOf(statusId));
    }

    private static uint IconOf(uint statusId)
    {
        try
        {
            var sheet = GameData.English<Status>();
            return sheet?.GetRowOrDefault(statusId) is { } row ? (uint)row.Icon : 0u;
        }
        catch { return 0u; }
    }

    // ---- boss damage-downs ----

    // The bit a standard raid mit occupies in a hit's debuff mask.
    public static int BitOf(string mit)
    {
        for (var i = 0; i < MitRecap.StandardRaidMits.Length; i++)
            if (string.Equals(mit, MitRecap.StandardRaidMits[i], StringComparison.OrdinalIgnoreCase))
                return 1 << i;
        return 0;
    }

    public static bool IsBossMit(string mit) => BitOf(mit) != 0;
}
