using System;
using System.Collections.Generic;
using Lumina.Excel.Sheets;

namespace FrenMits.Game;

// Time until a tracked mit is off cooldown, read from the live client.
public static class CooldownTracker
{
    private static Dictionary<string, uint>? _byName;

    private static void EnsureMap()
    {
        if (_byName != null) return;
        var map = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var want = new HashSet<string>(AbilityBook.Names, StringComparer.OrdinalIgnoreCase);
            // Names are English, so read the English sheet.
            var sheet = GameData.English<Lumina.Excel.Sheets.Action>();
            var recastOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (sheet != null)
                foreach (var row in sheet)
                {
                    var n = row.Name.ExtractText();
                    // Legacy and PvP rows share names with the real action.
                    if (row.ClassJobLevel == 0 || row.IsPvP) continue;
                    if (string.IsNullOrEmpty(n) || !want.Contains(n)) continue;
                    // The fairy's copies share the name but have no recast.
                    if (map.ContainsKey(n) && recastOf[n] >= row.Recast100ms) continue;
                    map[n] = row.RowId;
                    recastOf[n] = row.Recast100ms;
                }
        }
        catch (Exception ex) { Swallowed.Report("cooldown action map", ex); }
        _byName = map;
    }

    // Action text to every tracked name it mentions, memoized for per-frame callers.
    private static readonly Dictionary<string, List<string>> _namesByText = new(StringComparer.Ordinal);

    // Seconds until the soonest mit the text names is ready, or null.
    public static float? Remaining(string? actionText)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(actionText)) return null;
            EnsureMap();
            if (_byName == null || _byName.Count == 0) return null;

            if (!_namesByText.TryGetValue(actionText!, out var names))
            {
                // Same matching the planner uses.
                names = new List<string>();
                foreach (var kv in _byName)
                {
                    if (AbilityBook.MentionAt(actionText!, kv.Key) >= 0)
                    {
                        names.Add(kv.Key);
                    }
                }
                _namesByText[actionText!] = names;
            }
            if (names.Count == 0) return null;

            // A cell naming several reads as ready when any one of them is.
            float? min = null;
            foreach (var name in names)
            {
                var game = RecastRemaining(_byName[name]);
                var own = TrackedRemaining(name);
                // The game read can blank out right after the server confirms a press, so our own log backs it up.
                float? r = game.HasValue || own > 0f ? MathF.Max(game ?? 0f, own) : null;
                if (r.HasValue && (min == null || r.Value < min.Value)) min = r.Value;
            }
            return min;
        }
        catch (Exception ex) { Swallowed.Report("cooldown recast read", ex); return null; }
    }

    // ---- our own press log ----

    // Press times per mit, stamped by the action-effect hook.
    private static readonly Dictionary<string, List<double>> _presses = new(StringComparer.OrdinalIgnoreCase);

    private static double NowSec => Environment.TickCount64 / 1000.0;

    // Called for every action the local player (or their pet) lands.
    public static void NotePress(string actionName)
    {
        try
        {
            if (string.IsNullOrEmpty(actionName) || PlanInfo(actionName) is not { } info) return;
            Stamp(info.Name, info);
            // A shared-cooldown sibling is spent by the same press.
            if (info.Family.Length > 0)
                foreach (var kv in AbilityBook.SharedFamily)
                    if (kv.Value == info.Family && !kv.Key.Equals(info.Name, StringComparison.OrdinalIgnoreCase)
                        && PlanInfo(kv.Key) is { } sib)
                        Stamp(sib.Name, sib);
        }
        catch (Exception ex) { Swallowed.Report("cooldown press note", ex); }
    }

    private static void Stamp(string name, AbilityBook.PlanMit info)
    {
        if (!_presses.TryGetValue(name, out var list)) _presses[name] = list = new List<double>();
        var now = NowSec;
        list.Add(now);
        while (list.Count > 0 && now - list[0] > info.Recast) list.RemoveAt(0);
        while (list.Count > Math.Max(1, info.Charges)) list.RemoveAt(0);
    }

    // What the press log says remains, 0 while a charge is still free.
    private static float TrackedRemaining(string name)
    {
        if (!_presses.TryGetValue(name, out var list) || list.Count == 0) return 0f;
        if (PlanInfo(name) is not { } info) return 0f;
        var now = NowSec;
        while (list.Count > 0 && now - list[0] > info.Recast) list.RemoveAt(0);
        if (list.Count < Math.Max(1, info.Charges)) return 0f;
        return (float)Math.Max(0.0, info.Recast - (now - list[0]));
    }

    // A wipe resets every cooldown, so a new pull starts the log over.
    public static void ClearPresses() => _presses.Clear();

    // ---- plan data, backed by the Action sheet ----

    private static Dictionary<string, AbilityBook.PlanMit>? _planByName;

    private static void EnsurePlanMap()
    {
        if (_planByName != null) return;
        EnsureMap();
        var map = new Dictionary<string, AbilityBook.PlanMit>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var sheet = GameData.English<Lumina.Excel.Sheets.Action>();
            if (sheet != null && _byName != null)
                foreach (var kv in _byName)
                {
                    var row = sheet.GetRowOrDefault(kv.Value);
                    if (row == null) continue;
                    var recast = AbilityBook.RecastOverrides.GetValueOrDefault(kv.Key, row.Value.Recast100ms / 10f);
                    if (recast <= 5f) continue; // GCD-ish rows aren't worth validating
                    map[kv.Key] = new AbilityBook.PlanMit(kv.Key, recast,
                        Math.Max(1, (int)row.Value.MaxCharges),
                        AbilityBook.SharedFamily.GetValueOrDefault(kv.Key, ""),
                        row.Value.ClassJobLevel,
                        AbilityBook.Durations.GetValueOrDefault(kv.Key));
                }
        }
        catch (Exception ex) { Swallowed.Report("cooldown plan map", ex); }
        _planByName = map;
    }

    // Plan data for one mit by exact name.
    public static AbilityBook.PlanMit? PlanInfo(string name)
    {
        EnsurePlanMap();
        return _planByName != null && _planByName.TryGetValue(name, out var pm) ? pm : null;
    }

    // The level a duty syncs to, 0 when unknown.
    public static int DutySyncLevel(uint territory)
    {
        try
        {
            var t = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()?.GetRowOrDefault(territory);
            var cfc = t?.ContentFinderCondition.ValueNullable;
            return cfc?.ClassJobLevelSync ?? 0;
        }
        catch (Exception ex) { Swallowed.Report("duty sync level", ex); return 0; }
    }

    // PlanMits results per action text, memoized.
    private static readonly Dictionary<string, List<AbilityBook.PlanMit>> _planMitsByText = new(StringComparer.Ordinal);
    private static readonly AbilityBook.PlanMit[] _noPlanMits = Array.Empty<AbilityBook.PlanMit>();

    // Cached PlanMits for per-frame callers.
    public static IReadOnlyList<AbilityBook.PlanMit> PlanMitsCached(string? actionText)
    {
        if (string.IsNullOrWhiteSpace(actionText)) return _noPlanMits;
        if (_planMitsByText.TryGetValue(actionText!, out var hit)) return hit;
        var list = new List<AbilityBook.PlanMit>(PlanMits(actionText));
        // Never memoize a miss from an unbuilt map.
        if (_planByName is { Count: > 0 }) _planMitsByText[actionText!] = list;
        return list;
    }

    // Every tracked mit named in an action text.
    public static IEnumerable<AbilityBook.PlanMit> PlanMits(string? actionText)
    {
        if (string.IsNullOrWhiteSpace(actionText)) yield break;
        EnsurePlanMap();
        if (_planByName == null) yield break;
        foreach (var pm in _planByName.Values)
            if (AbilityBook.Mentions(actionText!, pm.Name)) yield return pm;
    }

    private static unsafe float? RecastRemaining(uint id)
    {
        var am = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
        if (am == null) return null;
        var adjusted = am->GetAdjustedActionId(id);
        var total = am->GetRecastTime(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, adjusted);
        // An active mit can adjust into a follow-up with no recast (Earthly Star), so the base id keeps the timer.
        if (total <= 0f && adjusted != id)
        {
            adjusted = id;
            total = am->GetRecastTime(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, adjusted);
        }
        if (total <= 0f) return null; // no recast group / not on your current job
        var elapsed = am->GetRecastTimeElapsed(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, adjusted);

        // Charge actions: the recast spans all charges.
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
