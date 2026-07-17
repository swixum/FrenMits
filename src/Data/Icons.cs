using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;

namespace FrenMits;

// Resolves action names to game icon ids (built once from the Action sheet) and
// draws those icons. Lines may pin an explicit icon; otherwise the icon is
// inferred from the action text.
public static class Icons
{
    private static Dictionary<string, uint>? _exact;
    private static List<(string Name, uint Icon)>? _byLength;
    private static Dictionary<string, uint>? _statusExact;
    private static List<(string Name, uint Icon)>? _statusByLength;
    private static List<(string Kw, uint Icon)>? _keywords;
    private static readonly Dictionary<string, uint> _textCache = new(StringComparer.OrdinalIgnoreCase);

    // A "bucket" of friendly mechanic shorthand -> a game action/status name we
    // resolve to an icon at runtime (no hard-coded ids, so it survives patches).
    // Typing e.g. "Bait" on a line auto-fills the matching icon; the picker also
    // lists these as a quick palette. Extend freely.
    private static readonly Dictionary<string, string> KeywordNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Debuffs (Status sheet)
        ["bind"] = "Bind", ["stun"] = "Stun", ["heavy"] = "Heavy", ["slow"] = "Slow",
        ["sleep"] = "Sleep", ["silence"] = "Silence", ["doom"] = "Doom", ["poison"] = "Poison",
        ["paralysis"] = "Paralysis", ["paralyze"] = "Paralysis", ["blind"] = "Blind",
        ["bleed"] = "Bleeding", ["burn"] = "Burns", ["burns"] = "Burns",
        ["vuln"] = "Vulnerability Up", ["vulnerability"] = "Vulnerability Up", ["vulnerable"] = "Vulnerability Up",
        // Actions (Action sheet)
        ["heal"] = "Cure", ["esuna"] = "Esuna", ["cleanse"] = "Esuna",
        ["raise"] = "Raise", ["rez"] = "Raise", ["sprint"] = "Sprint",
        ["provoke"] = "Provoke", ["shirk"] = "Shirk", ["rescue"] = "Rescue",
        ["interrupt"] = "Interject", ["interject"] = "Interject",
        ["reprisal"] = "Reprisal", ["feint"] = "Feint", ["addle"] = "Addle",
        ["knockback"] = "Arm's Length", ["kb"] = "Arm's Length", ["arms length"] = "Arm's Length",
        ["bait"] = "Cast", // fisher's rod, a stand-in for baiting
    };

    private static void EnsureBuilt()
    {
        if (_exact != null) return;
        _exact = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        var list = new List<(string, uint)>();
        try
        {
            var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            if (sheet != null)
            {
                foreach (var row in sheet)
                {
                    if (!row.IsPlayerAction) continue;
                    var icon = (uint)row.Icon;
                    if (icon == 0) continue;
                    var name = row.Name.ExtractText();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    _exact.TryAdd(name, icon);
                    list.Add((name, icon));
                }
            }
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: action index build failed");
        }

        list.Sort((a, b) => b.Item1.Length - a.Item1.Length);
        _byLength = list;

        // Status effects too: their names (Bind, Stun, Doom, Vulnerability Up, …)
        // and icons make great auto-matches and picker results for mechanic text.
        var sExact = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        var sList = new List<(string, uint)>();
        try
        {
            var statuses = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>();
            if (statuses != null)
                foreach (var row in statuses)
                {
                    var icon = (uint)row.Icon;
                    if (icon == 0) continue;
                    var name = row.Name.ExtractText();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (sExact.TryAdd(name, icon)) sList.Add((name, icon));
                }
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: status index build failed");
        }

        sList.Sort((a, b) => b.Item1.Length - a.Item1.Length);
        _statusExact = sExact;
        _statusByLength = sList;
    }

    // Resolve the keyword bucket once: map each shorthand to an icon via the
    // action/status indices, dropping any that don't resolve on this client.
    private static void EnsureKeywords()
    {
        if (_keywords != null) return;
        EnsureBuilt();
        var list = new List<(string, uint)>();
        foreach (var (kw, name) in KeywordNames)
        {
            var ic = NameIcon(name);
            if (ic != 0) list.Add((kw.ToLowerInvariant(), ic));
        }
        list.Sort((a, b) => b.Item1.Length - a.Item1.Length); // longest keyword wins
        _keywords = list;
    }

    // Icon for a known game name: exact action, exact status, then substring of each.
    private static uint NameIcon(string name)
    {
        if (_exact!.TryGetValue(name, out var a)) return a;
        if (_statusExact!.TryGetValue(name, out var s)) return s;
        var sub = Substr(_byLength!, name);
        return sub != 0 ? sub : Substr(_statusByLength!, name);
    }

    // Longest-first substring match over a name->icon index (names >= 4 chars).
    private static uint Substr(List<(string Name, uint Icon)> index, string text)
    {
        foreach (var (name, ic) in index)
            if (name.Length >= 4 && text.Contains(name, StringComparison.OrdinalIgnoreCase))
                return ic;
        return 0;
    }

    // A keyword-bucket match: a whole word equal to a single-word keyword, or the
    // text containing a multi-word keyword. Word-level so short keys ("kb") don't
    // false-match inside longer words.
    private static uint KeywordIcon(string text)
    {
        EnsureKeywords();
        if (_keywords!.Count == 0) return 0;
        var lower = text.ToLowerInvariant();
        var tokens = Tokenize(lower);
        foreach (var (kw, ic) in _keywords!)
        {
            if (kw.IndexOf(' ') >= 0) { if (lower.Contains(kw)) return ic; }
            else if (tokens.Contains(kw)) return ic;
        }
        return 0;
    }

    private static HashSet<string> Tokenize(string lower)
    {
        var set = new HashSet<string>();
        var sb = new System.Text.StringBuilder();
        foreach (var ch in lower)
        {
            if (char.IsLetter(ch)) sb.Append(ch);
            else if (sb.Length > 0) { set.Add(sb.ToString()); sb.Clear(); }
        }
        if (sb.Length > 0) set.Add(sb.ToString());
        return set;
    }

    // The resolved keyword bucket as (label, icon) for a quick palette in the picker.
    public static IEnumerable<(string Label, uint Icon)> Common()
    {
        EnsureKeywords();
        var seen = new HashSet<uint>();
        foreach (var (kw, ic) in _keywords!)
            if (seen.Add(ic))
                yield return (char.ToUpper(kw[0]).ToString() + kw.Substring(1), ic);
    }

    // The icon a line should display: its pinned icon, else the potion icon for a
    // potion line, else the active job's matching ability for a generic mit term
    // ("Party Mit" -> Troubadour on BRD, Shake It Off on WAR, ...), else inferred
    // from the action text.
    public static uint For(MitLine line, string? job = null)
    {
        if (line.IconId != 0) return line.IconId;
        if (IsPotion(line)) return PotionIcon(PotionStat(line));
        // Only your segments of a combined call: on a WAR, "Reprisal + Party Mit
        // (GNB/DRK)" should icon as Reprisal, matching the filtered call text.
        var action = line.ActionFor(job);
        var jm = JobMitIcon(action, job);
        if (jm != 0) return jm;
        return ResolveFromText(action);
    }

    // Generic mit terms -> the per-job ability whose icon to show. Lets a single
    // "Party Mit" line render the right party-mitigation icon for whoever's looking.
    private static readonly Dictionary<string, Dictionary<string, string>> JobMits =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Party Mit"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["WAR"] = "Shake It Off", ["PLD"] = "Divine Veil",
                ["DRK"] = "Dark Missionary", ["GNB"] = "Heart of Light",
                ["BRD"] = "Troubadour", ["MCH"] = "Tactician", ["DNC"] = "Shield Samba",
                ["RDM"] = "Magick Barrier",
            },
            // The sheet's "use your single-target mit on your co-tank" call.
            ["Buddy Mit"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["WAR"] = "Nascent Flash", ["PLD"] = "Intervention",
                ["DRK"] = "The Blackest Night", ["GNB"] = "Heart of Corundum",
            },
        };

    // The active job's ability for a generic mit term in the action, honoring an
    // optional job qualifier like "Party Mit (WAR/PLD)": resolves only when the job
    // has no qualifier or is named in it. Null if it doesn't apply.
    private static string? ResolveMitAbility(string? action, string? job)
    {
        if (string.IsNullOrWhiteSpace(action) || string.IsNullOrEmpty(job)) return null;
        foreach (var (term, map) in JobMits)
        {
            if (!map.TryGetValue(job!, out var ability)) continue;
            var m = Regex.Match(action!, Regex.Escape(term) + @"(?:\s*\(([^)]*)\))?", RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            var quals = m.Groups[1].Value;
            if (quals.Length == 0 || quals.IndexOf(job!, StringComparison.OrdinalIgnoreCase) >= 0)
                return ability;
        }
        return null;
    }

    // Icon for the active job's version of a generic mit term, or 0 if not applicable.
    public static uint JobMitIcon(string? action, string? job)
        => ResolveMitAbility(action, job) is { } a ? ResolveFromText(a) : 0u;

    // Replace a generic mit term (and its job qualifier) in the action text with the
    // active job's real ability name, so a call reads "Troubadour" not "Party Mit".
    public static string DisplayAction(string action, string? job)
    {
        if (string.IsNullOrWhiteSpace(action) || string.IsNullOrEmpty(job)) return action;
        foreach (var (term, map) in JobMits)
        {
            if (!map.TryGetValue(job!, out var ability)) continue;
            action = Regex.Replace(action, Regex.Escape(term) + @"(?:\s*\(([^)]*)\))?", m =>
            {
                var quals = m.Groups[1].Value;
                return quals.Length == 0 || quals.IndexOf(job!, StringComparison.OrdinalIgnoreCase) >= 0
                    ? ability : m.Value;
            }, RegexOptions.IgnoreCase);
        }
        return action;
    }

    // A potion line (from the Potions section): action "Potion", or a "Potion (…)"
    // mechanic. These have no player-action icon, so they get the item icon instead.
    public static bool IsPotion(MitLine line)
        => line.Action.Trim().Equals("Potion", StringComparison.OrdinalIgnoreCase)
           || line.Mechanic.StartsWith("Potion", StringComparison.OrdinalIgnoreCase);

    // The stat baked into a potion line's mechanic, e.g. "Potion (Strength)" -> Strength.
    private static string PotionStat(MitLine line)
    {
        var m = line.Mechanic;
        int i = m.IndexOf('('), j = m.IndexOf(')');
        return i >= 0 && j > i ? m.Substring(i + 1, j - i - 1).Trim() : "";
    }

    // The stat-coloured Gemdraught icon for a line (Strength/Dexterity/Intelligence/
    // Mind). Public so the icon picker can pin the right one.
    public static uint PotionIconFor(MitLine line) => PotionIcon(PotionStat(line));

    // Icon for a stat's Gemdraught, resolved from the Item sheet and cached per stat.
    // Falls back to any Gemdraught, then 0 (e.g. a non-English client) — caller draws none.
    private static readonly Dictionary<string, uint> _potionIconByStat = new(StringComparer.OrdinalIgnoreCase);
    public static uint PotionIcon(string? stat = null)
    {
        stat = (stat ?? "").Trim();
        var key = stat.Length == 0 ? "*" : stat;
        if (_potionIconByStat.TryGetValue(key, out var cached)) return cached;

        uint icon = 0, anyGem = 0;
        try
        {
            var items = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
            if (items != null)
                foreach (var row in items)
                {
                    var name = row.Name.ExtractText();
                    if (!name.Contains("Gemdraught", StringComparison.OrdinalIgnoreCase)) continue;
                    if (anyGem == 0) anyGem = row.Icon;
                    if (stat.Length > 0 && name.Contains(stat, StringComparison.OrdinalIgnoreCase))
                    {
                        icon = row.Icon;
                        break;
                    }
                }
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: potion icon lookup failed");
        }

        if (icon == 0) icon = anyGem; // unknown/blank stat -> any Gemdraught
        _potionIconByStat[key] = icon;
        return icon;
    }

    public static uint ResolveFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        if (_textCache.TryGetValue(text, out var cached)) return cached;
        EnsureBuilt();

        // Priority: exact action > exact status > keyword bucket > action substring
        // > status substring. Exact matches first keeps every existing line's icon;
        // keywords/statuses only fill in where nothing matched before.
        var t = text.Trim();
        uint icon = 0;
        if (_exact!.TryGetValue(t, out var ax)) icon = ax;
        if (icon == 0 && _statusExact!.TryGetValue(t, out var sx)) icon = sx;
        if (icon == 0) icon = KeywordIcon(t);
        if (icon == 0) icon = Substr(_byLength!, t);
        if (icon == 0) icon = Substr(_statusByLength!, t);

        _textCache[text] = icon;
        return icon;
    }

    public static IEnumerable<(string Name, uint Icon)> Search(string query, int max)
    {
        EnsureBuilt();
        if (string.IsNullOrWhiteSpace(query)) yield break;
        var n = 0;
        var seen = new HashSet<uint>();
        // Actions first, then statuses; dedupe by icon so the grid stays tidy.
        foreach (var (name, ic) in _byLength!)
            if (name.Contains(query, StringComparison.OrdinalIgnoreCase) && seen.Add(ic))
            {
                yield return (name, ic);
                if (++n >= max) yield break;
            }
        foreach (var (name, ic) in _statusByLength!)
            if (name.Contains(query, StringComparison.OrdinalIgnoreCase) && seen.Add(ic))
            {
                yield return (name, ic);
                if (++n >= max) yield break;
            }
    }

    public static void Draw(uint iconId, Vector2 size)
    {
        if (iconId == 0) { ImGui.Dummy(size); return; }
        try
        {
            var tex = Service.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
            ImGui.Image(tex.Handle, size);
        }
        catch
        {
            ImGui.Dummy(size);
        }
    }

    // A clickable icon (image button). Returns true when clicked. Falls back to an
    // empty same-size button for ids with no icon. PushID keeps each unique even
    // when the texture (and thus ImGui's derived id) repeats.
    public static bool Button(uint iconId, Vector2 size, string id)
    {
        ImGui.PushID(id);
        try
        {
            var tex = Service.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
            return ImGui.ImageButton(tex.Handle, size);
        }
        catch
        {
            return ImGui.Button("##empty", size + new Vector2(8, 8));
        }
        finally
        {
            ImGui.PopID();
        }
    }
}
