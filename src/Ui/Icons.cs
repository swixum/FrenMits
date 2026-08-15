using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;

namespace FrenMits.Ui;

// Resolves action names to game icon ids and draws them.
public static class Icons
{
    private static Dictionary<string, uint>? _exact;
    private static List<(string Name, uint Icon)>? _byLength;
    private static Dictionary<string, uint>? _statusExact;
    private static List<(string Name, uint Icon)>? _statusByLength;
    private static List<(string Kw, uint Icon)>? _keywords;
    private static readonly Dictionary<string, uint> _textCache = new(StringComparer.OrdinalIgnoreCase);

    // Friendly shorthand to a game name we resolve at runtime.
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
        ["tbn"] = "The Blackest Night", // the sheet's tank tabs abbreviate it
        ["knockback"] = "Arm's Length", ["kb"] = "Arm's Length", ["arms length"] = "Arm's Length",
        ["bait"] = "Cast", // fisher's rod, a stand-in for baiting
        ["zoe"] = "Zoe",
    };

    private static void EnsureBuilt()
    {
        if (_exact != null) return;
        _exact = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        var list = new List<(string, uint)>();
        try
        {
            // English, since these are matched against our own sheets.
            var sheet = GameData.English<Lumina.Excel.Sheets.Action>();
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

        // Status effects too, whose names make good auto-matches.
        var sExact = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        var sList = new List<(string, uint)>();
        try
        {
            var statuses = GameData.English<Lumina.Excel.Sheets.Status>();
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

    // Resolve the keyword bucket once, dropping what won't.
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

    // Icon for a game name, exact first, then substring.
    private static uint NameIcon(string name)
    {
        if (_exact!.TryGetValue(name, out var a)) return a;
        if (_statusExact!.TryGetValue(name, out var s)) return s;
        var sub = Substr(_byLength!, name);
        return sub != 0 ? sub : Substr(_statusByLength!, name);
    }

    // Longest-first substring match over a name index.
    private static uint Substr(List<(string Name, uint Icon)> index, string text)
    {
        foreach (var (name, ic) in index)
            if (name.Length >= 4 && text.Contains(name, StringComparison.OrdinalIgnoreCase))
                return ic;
        return 0;
    }

    // A keyword match, at word level so short keys behave.
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

    // The keyword bucket as a quick palette for the picker.
    public static IEnumerable<(string Label, uint Icon)> Common()
    {
        EnsureKeywords();
        var seen = new HashSet<uint>();
        foreach (var (kw, ic) in _keywords!)
            if (seen.Add(ic))
                yield return (char.ToUpper(kw[0]).ToString() + kw.Substring(1), ic);
    }

    // The icon a line shows: pinned, potion, job, else inferred.
    public static uint For(MitLine line, string? job = null)
    {
        if (line.IconId != 0) return line.IconId;
        // Memoized, since the overlays ask every frame.
        var key = (line.Action, line.Mechanic, job ?? "");
        if (_forCache.TryGetValue(key, out var cached)) return cached;
        uint icon;
        if (IsPotion(line)) icon = PotionIcon(PotionStat(line));
        else
        {
            // Only your segments of a combined call.
            var action = line.ActionFor(job);
            var jm = JobMitIcon(action, job);
            icon = jm != 0 ? jm : ResolveFromText(action);
        }
        if (_forCache.Count >= CacheMax) _forCache.Clear();
        _forCache[key] = icon;
        return icon;
    }

    // Keys come from plan text, so growth is slow but has no end of its own.
    private const int CacheMax = 4096;

    private static readonly Dictionary<(string Action, string Mech, string Job), uint> _forCache = new();

    // Generic mit terms to the per-job ability to show.
    private static readonly Dictionary<string, Dictionary<string, string>> JobMits =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Party Mit"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["WAR"] = "Shake It Off", ["PLD"] = "Divine Veil",
                ["DRK"] = "Dark Missionary", ["GNB"] = "Heart of Light",
                ["BRD"] = "Troubadour", ["MCH"] = "Tactician", ["DNC"] = "Shield Samba",
                ["RDM"] = "Magick Barrier", ["PCT"] = "Tempera Grassa",
                ["WHM"] = "Temperance", ["AST"] = "Neutral Sect",
                ["SCH"] = "Sacred Soil", ["SGE"] = "Kerachole",
            },
            // The sheet's "use your single-target mit on your co-tank" call.
            ["Buddy Mit"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["WAR"] = "Nascent Flash", ["PLD"] = "Intervention",
                ["DRK"] = "The Blackest Night", ["GNB"] = "Heart of Corundum",
            },
            // Tank-buster generics the planner speaks.
            ["Short Mit"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["WAR"] = "Bloodwhetting", ["PLD"] = "Holy Sheltron",
                ["DRK"] = "The Blackest Night", ["GNB"] = "Heart of Corundum",
            },
            ["Invulnerability"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["WAR"] = "Holmgang", ["PLD"] = "Hallowed Ground",
                ["DRK"] = "Living Dead", ["GNB"] = "Superbolide",
            },
            // The tank's big self-mit (~40% reduction) and its long-recast (~90s)
            // partner, as generic magnitude/recast tags rather than a fixed
            // ability name. Max-level trait upgrades shown (Damnation over
            // Vengeance, Guardian over Sentinel, Shadowed Vigil over Shadow
            // Wall, Great Nebula over Nebula).
            ["40%"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["WAR"] = "Damnation", ["PLD"] = "Guardian",
                ["DRK"] = "Shadowed Vigil", ["GNB"] = "Great Nebula",
            },
            ["90s"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["WAR"] = "Thrill of Battle", ["PLD"] = "Bulwark",
                ["DRK"] = "Dark Mind", ["GNB"] = "Camouflage",
            },
        };

        // Whether a term's trailing "(...)" is a job-restriction list (e.g.
        // "Short Mit (PLD/DRK)") rather than decorative context (e.g.
        // "(First Hit)", "(Close)", "(Solo)") - true only when every token
        // inside is itself a real job abbreviation.
        private static bool IsJobList(string quals)
        {
            if (quals.Length == 0) return false;
            var tokens = quals.Split(new[] { '/', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return tokens.Length > 0 && tokens.All(t => Jobs.ByAbbreviation(t) != null);
        }

    // The icon a line should display: its pinned icon, else the potion icon for a
    // potion line, else the active job's matching ability for a generic mit term
    // ("Party Mit" -> Troubadour on BRD, Shake It Off on WAR, ...), else inferred
    // from the action text.
    public static uint ForMitPress(MitPress press, string? job = null)
    {
        if (press.SourceLine.IconId != 0) return press.SourceLine.IconId;
        var jm = JobMitIcon(press.MitName, job);
        return jm != 0 ? jm : ResolveFromText(press.MitName);
    }

    // The active job's ability for a generic mit term.
    private static string? ResolveMitAbility(string? action, string? job)
    {
        if (string.IsNullOrWhiteSpace(action) || string.IsNullOrEmpty(job)) return null;
        foreach (var (term, map) in JobMits)
        {
            if (!map.TryGetValue(job!, out var ability)) continue;
            var m = Regex.Match(action!, Regex.Escape(term) + @"(?:\s*\(([^)]*)\))?", RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            var quals = m.Groups[1].Value;
            if (quals.Length == 0 || !IsJobList(quals) || quals.IndexOf(job!, StringComparison.OrdinalIgnoreCase) >= 0)
                return ability;
        }
        return null;
    }

    // Icon for the job's version of a generic term, or 0.
    public static uint JobMitIcon(string? action, string? job)
        => ResolveMitAbility(action, job) is { } a ? ResolveFromText(a) : 0u;

    // What a person reads: the job's real ability name, numerals as digits.
    public static string DisplayAction(string action, string? job)
        => Fmt.Numerals(ResolveAction(action, job));

    // Swap a generic term for the job's real ability name. Spelled the way the
    // game spells it, so ability lookups off this text still hit.
    public static string ResolveAction(string action, string? job)
    {
        if (string.IsNullOrWhiteSpace(action) || string.IsNullOrEmpty(job)) return action;
        // Memoized, since the board calls this per row per frame.
        var key = (action, job!);
        if (_displayCache.TryGetValue(key, out var cached)) return cached;
        var resolved = DisplayActionUncached(action, job);
        if (_displayCache.Count >= CacheMax) _displayCache.Clear();
        _displayCache[key] = resolved;
        return resolved;
    }

    private static readonly Dictionary<(string Action, string Job), string> _displayCache = new();

    private static string DisplayActionUncached(string action, string? job)
    {
        foreach (var (term, map) in JobMits)
        {
            if (!map.TryGetValue(job!, out var ability)) continue;
            action = Regex.Replace(action, Regex.Escape(term) + @"(?:\s*\(([^)]*)\))?", m =>
            {
                var quals = m.Groups[1].Value;
                if (quals.Length == 0) return ability;
                if (!IsJobList(quals)) return $"{ability} ({quals})"; // decorative, e.g. "(First Hit)"
                return quals.IndexOf(job!, StringComparison.OrdinalIgnoreCase) >= 0 ? ability : m.Value;
            }, RegexOptions.IgnoreCase);
        }
        return action;
    }

    // A potion line, which gets the item icon instead.
    public static bool IsPotion(MitLine line)
        => line.Action.Trim().Equals("Potion", StringComparison.OrdinalIgnoreCase)
           || line.Mechanic.StartsWith("Potion", StringComparison.OrdinalIgnoreCase);

    // The stat baked into a potion line's mechanic.
    private static string PotionStat(MitLine line)
    {
        var m = line.Mechanic;
        int i = m.IndexOf('('), j = m.IndexOf(')');
        return i >= 0 && j > i ? m.Substring(i + 1, j - i - 1).Trim() : "";
    }

    // The stat-colored Gemdraught icon for a line.
    public static uint PotionIconFor(MitLine line) => PotionIcon(PotionStat(line));

    // A stat's Gemdraught icon, cached, falling back to 0.
    private static readonly Dictionary<string, uint> _potionIconByStat = new(StringComparer.OrdinalIgnoreCase);
    public static uint PotionIcon(string? stat = null)
    {
        stat = (stat ?? "").Trim();
        var key = stat.Length == 0 ? "*" : stat;
        if (_potionIconByStat.TryGetValue(key, out var cached)) return cached;

        uint icon = 0, anyGem = 0;
        try
        {
            var items = GameData.English<Lumina.Excel.Sheets.Item>();
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

    // Icons straight off the row id the combat log gave us.
    private static readonly Dictionary<uint, uint> _actionIcons = new();
    private static readonly Dictionary<uint, uint> _statusIcons = new();

    public static uint ByActionId(uint actionId)
    {
        if (actionId == 0) return 0;
        if (_actionIcons.TryGetValue(actionId, out var cached)) return cached;
        uint icon = 0;
        try
        {
            if (GameData.English<Lumina.Excel.Sheets.Action>()?.GetRowOrDefault(actionId) is { } row)
                icon = row.Icon;
        }
        catch { /* an id this client's sheet has never heard of */ }
        return _actionIcons[actionId] = icon;
    }

    public static uint ByStatusId(uint statusId)
    {
        if (statusId == 0) return 0;
        if (_statusIcons.TryGetValue(statusId, out var cached)) return cached;
        uint icon = 0;
        try
        {
            if (GameData.English<Lumina.Excel.Sheets.Status>()?.GetRowOrDefault(statusId) is { } row)
                icon = row.Icon;
        }
        catch { /* same */ }
        return _statusIcons[statusId] = icon;
    }

    public static uint ResolveFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        if (_textCache.TryGetValue(text, out var cached)) return cached;
        EnsureBuilt();

        // Priority: exact action, exact status, keyword, substring.
        var t = text.Trim();
        uint icon = 0;
        if (_exact!.TryGetValue(t, out var ax)) icon = ax;
        if (icon == 0 && _statusExact!.TryGetValue(t, out var sx)) icon = sx;
        if (icon == 0) icon = KeywordIcon(t);
        if (icon == 0) icon = Substr(_byLength!, t);
        if (icon == 0) icon = Substr(_statusByLength!, t);

        if (_textCache.Count >= CacheMax) _textCache.Clear();
        _textCache[text] = icon;
        return icon;
    }

    // The picker's own index, in the CLIENT's language.
    private static List<(string Name, uint Icon)>? _searchIndex;

    private static List<(string Name, uint Icon)> SearchIndex()
    {
        if (_searchIndex != null) return _searchIndex;
        var list = new List<(string Name, uint Icon)>();
        try
        {
            var actions = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            if (actions != null)
                foreach (var row in actions)
                {
                    if (!row.IsPlayerAction || row.Icon == 0) continue;
                    var name = row.Name.ExtractText();
                    if (!string.IsNullOrWhiteSpace(name)) list.Add((name, (uint)row.Icon));
                }
            var statuses = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>();
            if (statuses != null)
                foreach (var row in statuses)
                {
                    if (row.Icon == 0) continue;
                    var name = row.Name.ExtractText();
                    if (!string.IsNullOrWhiteSpace(name)) list.Add((name, (uint)row.Icon));
                }
        }
        catch (Exception ex) { Swallowed.Report("icon search index", ex); }
        list.Sort((a, b) => b.Name.Length - a.Name.Length);
        return _searchIndex = list;
    }

    public static IEnumerable<(string Name, uint Icon)> Search(string query, int max)
    {
        EnsureBuilt();
        if (string.IsNullOrWhiteSpace(query)) yield break;
        var n = 0;
        var seen = new HashSet<uint>();
        // Client language first, so you can type what you see.
        foreach (var (name, ic) in SearchIndex())
            if (name.Contains(query, StringComparison.OrdinalIgnoreCase) && seen.Add(ic))
            {
                yield return (name, ic);
                if (++n >= max) yield break;
            }
        // Then the English indices, so an English name always resolves too.
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
            if (tex != null) ImGui.Image(tex.Handle, size);
        }
        catch
        {
            ImGui.Dummy(size);
        }
    }

    // Draws an icon directly to a draw list, avoiding ImGui layout side effects.
    public static void DrawTo(ImDrawListPtr dl, uint iconId, Vector2 p0, Vector2 size)
    {
        if (iconId == 0) return;
        try
        {
            var tex = Service.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
            if (tex != null) dl.AddImage(tex.Handle, p0, p0 + size);
        }
        catch { }
    }

    // A clickable icon, falling back to an empty button.
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
