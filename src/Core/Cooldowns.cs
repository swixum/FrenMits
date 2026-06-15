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

    private static unsafe float? RecastRemaining(uint id)
    {
        var am = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
        if (am == null) return null;
        var adjusted = am->GetAdjustedActionId(id);
        var total = am->GetRecastTime(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, adjusted);
        if (total <= 0f) return 0f; // no recast group / not on your current job
        var elapsed = am->GetRecastTimeElapsed(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, adjusted);
        return MathF.Max(0f, total - elapsed);
    }
}
