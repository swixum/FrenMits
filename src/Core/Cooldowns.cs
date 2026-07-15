using System;
using System.Collections.Generic;
using Lumina.Excel.Sheets;

namespace FrenMits;

// Looks up how long until one of your mits is off cooldown, so a call can warn
// when the mit won't be ready in time. Action ids are resolved from the game data
// by NAME (no hard-coded ids), and the recast is read through ActionManager.
// Everything is guarded — if any of it isn't available the call simply returns
// null and the overlay behaves exactly as before.
public static class Cooldowns
{
    // Canonical mit action names (must match the Action sheet). Matched as
    // substrings against a line's action text, so "Rampart + 90s" finds "Rampart".
    private static readonly string[] Names =
    {
        "Reprisal", "Rampart", "Feint", "Addle", "Bloodbath", "Second Wind", "Arm's Length",
        "Holmgang", "Vengeance", "Damnation", "Thrill of Battle", "Shake It Off", "Bloodwhetting",
        "Nascent Flash", "Raw Intuition", "Equilibrium",
        "Sentinel", "Guardian", "Hallowed Ground", "Bulwark", "Sheltron", "Holy Sheltron",
        "Intervention", "Divine Veil", "Passage of Arms", "Rampart",
        "Shadow Wall", "Dark Mind", "Living Dead", "The Blackest Night", "Oblation", "Dark Missionary",
        "Camouflage", "Nebula", "Superbolide", "Heart of Light", "Heart of Stone", "Heart of Corundum", "Aurora",
        "Sacred Soil", "Expedient", "Fey Illumination", "Seraph", "Recitation", "Whispering Dawn",
        "Temperance", "Plenary Indulgence", "Asylum", "Liturgy of the Bell", "Divine Caress",
        "Collective Unconscious", "Neutral Sect", "Macrocosmos", "Exaltation", "Sun Sign",
        "Kerachole", "Holos", "Panhaima", "Physis II", "Krasis", "Zoe", "Philosophia",
        "Magick Barrier", "Addle", "Tactician", "Troubadour", "Shield Samba", "Improvisation",
    };

    private static Dictionary<string, uint>? _byName;

    private static void EnsureMap()
    {
        if (_byName != null) return;
        var map = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var want = new HashSet<string>(Names, StringComparer.OrdinalIgnoreCase);
            var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            if (sheet != null)
                foreach (var row in sheet)
                {
                    var n = row.Name.ExtractText();
                    if (!string.IsNullOrEmpty(n) && want.Contains(n) && !map.ContainsKey(n))
                        map[n] = row.RowId;
                }
        }
        catch { }
        _byName = map;
    }

    // Seconds until the mit referenced by `actionText` is ready, or null if it
    // isn't a tracked mit / can't be read. 0 = ready now.
    public static float? Remaining(string? actionText)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(actionText)) return null;
            EnsureMap();
            if (_byName == null || _byName.Count == 0) return null;

            uint id = 0;
            foreach (var kv in _byName)
                if (actionText!.Contains(kv.Key, StringComparison.OrdinalIgnoreCase)) { id = kv.Value; break; }
            if (id == 0) return null;

            return RecastRemaining(id);
        }
        catch { return null; }
    }

    // ---- static planning data (from the game sheets, no combat needed) ----

    public readonly record struct PlanMit(string Name, float Recast, int Charges);

    private static Dictionary<string, PlanMit>? _planByName;

    private static void EnsurePlanMap()
    {
        if (_planByName != null) return;
        EnsureMap();
        var map = new Dictionary<string, PlanMit>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            if (sheet != null && _byName != null)
                foreach (var kv in _byName)
                {
                    var row = sheet.GetRowOrDefault(kv.Value);
                    if (row == null) continue;
                    var recast = row.Value.Recast100ms / 10f;
                    if (recast <= 5f) continue; // GCD-ish rows aren't worth validating
                    map[kv.Key] = new PlanMit(kv.Key, recast, Math.Max(1, (int)row.Value.MaxCharges));
                }
        }
        catch { }
        _planByName = map;
    }

    // Every tracked mit referenced in an action text ("Sacred Soil + Spreadlo"
    // yields Sacred Soil), with its full recast and charge count from the game
    // sheets. Word-boundary matched, so "Seraphism" is not read as "Seraph".
    public static IEnumerable<PlanMit> PlanMits(string? actionText)
    {
        if (string.IsNullOrWhiteSpace(actionText)) yield break;
        EnsurePlanMap();
        if (_planByName == null) yield break;
        foreach (var pm in _planByName.Values)
        {
            // Check every occurrence: "Seraphism + Seraph" must still find the
            // standalone Seraph even though the first occurrence fails the
            // boundary test inside "Seraphism".
            var idx = actionText!.IndexOf(pm.Name, StringComparison.OrdinalIgnoreCase);
            while (idx >= 0)
            {
                var before = idx == 0 ? ' ' : actionText[idx - 1];
                var end = idx + pm.Name.Length;
                var after = end >= actionText.Length ? ' ' : actionText[end];
                if (!char.IsLetter(before) && !char.IsLetter(after))
                {
                    yield return pm;
                    break;
                }
                idx = actionText.IndexOf(pm.Name, idx + 1, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static unsafe float? RecastRemaining(uint id)
    {
        var am = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
        if (am == null) return null;
        var adjusted = am->GetAdjustedActionId(id);
        var total = am->GetRecastTime(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, adjusted);
        if (total <= 0f) return 0f; // no recast group / not on your current job
        var elapsed = am->GetRecastTimeElapsed(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, adjusted);

        // Charge actions (Aurora, Oblation): the recast timer spans ALL charges,
        // so total - elapsed reads "on cooldown" even while a charge sits ready.
        // A charge is available once elapsed covers one per-charge span.
        var maxCharges = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.GetMaxCharges(adjusted, 0);
        if (maxCharges > 1)
        {
            var perCharge = total / maxCharges;
            if (perCharge > 0f && elapsed >= perCharge) return 0f;      // a charge is up
            return MathF.Max(0f, perCharge - elapsed);                  // time to first charge
        }

        return MathF.Max(0f, total - elapsed);
    }
}
