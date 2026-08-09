using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;

namespace FrenMits.Planning;

// Sheet View: turning a real pull into a sheet.
public partial class SheetViewWindow
{
    // ---- build from pull ----

    private bool _bpRows = true;
    private bool _bpAnchors = true;
    // Log import extras: keep the casts that mattered.
    private bool _flMeaningful = true;
    private bool _flDowntime = true;

    private static string ActionName(uint id)
    {
        try
        {
            var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            var row = sheet?.GetRowOrDefault(id);
            return row?.Name.ExtractText() ?? "";
        }
        catch { return ""; }
    }

    private void DrawBuildFromPullPopup()
    {
        // Modal, so a stray click outside cannot dismiss the form.
        var stay = true;
        if (!ImGui.BeginPopupModal("##buildpull", ref stay,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings)) return;

        PopupHeader("Build from last pull", 400f);

        // Only offer a capture from this duty, or the anchors go wrong.
        var casts = _fight != null && _plugin.Sync.LastPullTerritory == _fight.TerritoryId
            ? _plugin.Sync.LastPull.Where(cp => !cp.IsBoss).ToList()
            : new List<SyncEngine.Capture>();
        if (casts.Count == 0)
        {
            ImGui.TextDisabled("Nothing captured from this duty yet. Do a pull (even a");
            ImGui.TextDisabled("short wipe); the boss's casts are recorded automatically.");
            ImGui.EndPopup();
            return;
        }

        ImGui.TextUnformatted($"{casts.Count} casts captured from the last pull.");
        ImGui.Checkbox("Add mechanic rows", ref _bpRows);
        ImGui.Checkbox("Set resync anchors", ref _bpAnchors);
        if (_bpAnchors)
            ImGui.TextDisabled("Replaces this fight's existing cast anchors.");

        ImGui.BeginDisabled(!_bpRows && !_bpAnchors);
        if (ImGui.Button("Build", new Vector2(110, 0)))
        {
            BuildFromPull(casts, _bpRows, _bpAnchors);
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndDisabled();
        ImGui.EndPopup();
    }

    private readonly record struct BuildEvent(uint Id, float Time, string Name, bool Anchorable);

    // Two bars for an imported log's silences.
    private const float ImportWindowGap = 20f; // seeds an untargetable window
    private const float ImportSeamGap = 35f;   // also re-bases the clock (phase seam)

    private static readonly HashSet<string> InvulnNames = new(StringComparer.OrdinalIgnoreCase)
        { "Holmgang", "Hallowed Ground", "Living Dead", "Superbolide" };

    private static readonly HashSet<string> TankPersonalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Rampart", "Bloodwhetting", "Nascent Flash", "The Blackest Night", "Oblation",
        "Holy Sheltron", "Intervention", "Heart of Corundum", "Aurora",
        "Vengeance", "Damnation", "Sentinel", "Guardian", "Camouflage", "Nebula",
        "Thrill of Battle", "Dark Mind", "Bulwark",
    };

    // Grade a hit against the fight's hardest raidwide.
    private static int HurtLevel(long dmg, long max)
        => dmg <= 0 || max <= 0 ? 0
         : dmg >= max * 0.75 ? 3
         : dmg >= max * 0.40 ? 2
         : 1;

    private void BuildFromPull(List<SyncEngine.Capture> casts, bool rows, bool anchors)
    {
        // Live captures come from cast bars, so every one is anchorable.
        var events = SiftEvents(casts.OrderBy(cp => cp.Time).Select(cp => (cp.Id, cp.Time, Anchorable: true)));
        ApplyBuild(events, rows, anchors, "the last pull");
    }

    // Resolve names, then drop autos and back-to-back repeats.
    private static List<BuildEvent> SiftEvents(IEnumerable<(uint Id, float Time, bool Anchorable)> raw,
        IReadOnlyDictionary<uint, string>? names = null)
    {
        var events = new List<BuildEvent>();
        foreach (var (id, time, anchorable) in raw)
        {
            var name = names != null && names.TryGetValue(id, out var n) && n.Length > 0 ? n : ActionName(id);
            if (name.Length == 0) continue;
            if (string.Equals(name, "attack", StringComparison.OrdinalIgnoreCase)) continue;
            // An unknown ability may have no name in the game sheet either.
            if (IsUnnamedAbility(name) && ActionName(id).Length == 0) continue;
            if (events.Count > 0 && events[^1].Id == id && time - events[^1].Time < 3f) continue;
            events.Add(new BuildEvent(id, time, name, anchorable));
        }
        return events;
    }

    // The log's placeholder for an ability it has no name for.
    internal static bool IsUnnamedAbility(string? name)
        => name != null && System.Text.RegularExpressions.Regex.IsMatch(
            name.Trim(), @"^unknown[_ ]?[0-9a-fA-F]+$");

    private void ApplyBuild(List<BuildEvent> events, bool rows, bool anchors, string source,
        Dictionary<uint, FFLogsClient.AbilityDamage>? damage = null,
        List<FFLogsClient.MitPress>? mitPresses = null,
        bool meaningfulOnly = false, bool deriveDowntime = false)
    {
        // Custom sheets only, or a built-in would lose its anchors.
        if (_fight == null || !_isCustom || AbortIfStale()) return;
        if (events.Count == 0)
        {
            Flash("No usable casts found (only auto-attacks).");
            return;
        }

        PushUndo($"build from {source}");
        _plugin.Snapshots.Save(_fight, $"before build from {source}");

        // The fight's timer, if this pull ran into it.
        bool IsEnrage(BuildEvent e)
            => Enrages.Is(_fight.TerritoryId, e.Id)
               || (damage != null && damage.TryGetValue(e.Id, out var d)
                   && Enrages.LooksLikeOne(d.Worst, d.Targets));

        // Raidwides and busters grade on separate scales.
        var maxDmg = 0L;   // hardest raidwide
        var maxTb = 0L;    // hardest buster
        if (damage is { Count: > 0 })
            foreach (var e in events)
                if (!IsEnrage(e) && damage.TryGetValue(e.Id, out var d))
                {
                    if (d.Targets > 3) { if (d.Worst > maxDmg) maxDmg = d.Worst; }
                    else if (d.Worst > maxTb) maxTb = d.Worst;
                }

        // With the log's damage we can tell real mechanics from filler.
        bool Meaningful(BuildEvent e)
            => e.Anchorable || (damage != null && damage.ContainsKey(e.Id));
        // Only filter when we have the damage data to judge with.
        var rowEvents = meaningfulOnly && damage is { Count: > 0 }
            ? events.Where(Meaningful).ToList() : events;
        var filteredOut = events.Count - rowEvents.Count;

        var addedRows = 0;
        var graded = 0;
        if (rows)
            foreach (var e in rowEvents)
            {
                var hurt = 0;
                var buster = false;
                var enrage = IsEnrage(e);
                if (!enrage && damage != null && damage.TryGetValue(e.Id, out var d))
                {
                    buster = d.Targets > 0 && d.Targets <= 3;
                    hurt = HurtLevel(d.Worst, buster ? maxTb : maxDmg);
                }
                // A row that already exists still learns its grade from the log.
                var existing = _fight.CustomRows.FirstOrDefault(cr =>
                    MechEquals(cr.Mechanic, e.Name) && MathF.Abs(cr.Time - e.Time) < 2f);
                if (existing != null)
                {
                    if (enrage) existing.Enrage = true;
                    if (existing.Hurt == 0 && hurt > 0) { existing.Hurt = hurt; existing.Buster = buster; graded++; }
                    continue;
                }
                if (_rows.Any(r => !r.Ghost && MechEquals(r.Mechanic, e.Name) && MathF.Abs(r.Time - e.Time) < 2f))
                    continue;
                _fight.CustomRows.Add(new CustomRow
                    { Time = MathF.Round(e.Time), Mechanic = e.Name, Hurt = hurt, Buster = buster, Enrage = enrage });
                if (hurt > 0) graded++;
                addedRows++;
            }

        // Second signal: where the log's PLAYERS pressed their mits.
        if (rows && mitPresses is { Count: > 0 })
        {
            var allRows = _fight.CustomRows.OrderBy(r => r.Time).ToList();
            if (allRows.Count > 0)
            {
                var party = new Dictionary<CustomRow, int>();
                var tank = new Dictionary<CustomRow, int>();
                var invuln = new Dictionary<CustomRow, int>();
                foreach (var press in mitPresses)
                {
                    var name = ActionName(press.AbilityId);
                    if (name.Length == 0) continue;
                    // The nearest row this press could be for.
                    CustomRow? target = null;
                    foreach (var r in allRows)
                    {
                        if (r.Time < press.Time - 1f) continue;
                        if (r.Time > press.Time + 20f) break;
                        target = r;
                        break;
                    }
                    if (target == null) continue;
                    if (InvulnNames.Contains(name)) invuln[target] = invuln.GetValueOrDefault(target) + 1;
                    else if (TankPersonalNames.Contains(name)) tank[target] = tank.GetValueOrDefault(target) + 1;
                    else party[target] = party.GetValueOrDefault(target) + 1;
                }
                foreach (var r in allRows)
                {
                    var pv = party.GetValueOrDefault(r);
                    var tv = tank.GetValueOrDefault(r);
                    var iv = invuln.GetValueOrDefault(r);
                    var before = (r.Hurt, r.Buster);
                    if (pv >= 9) r.Hurt = Math.Max(r.Hurt, 3);
                    else if (pv >= 6) r.Hurt = Math.Max(r.Hurt, 2);
                    else if (pv >= 3) r.Hurt = Math.Max(r.Hurt, 1);
                    if (pv <= 2 && (iv >= 1 || tv >= 2))
                    {
                        r.Buster = true;
                        r.Hurt = Math.Max(r.Hurt, iv >= 1 ? 3 : 2);
                    }
                    if (before != (r.Hurt, r.Buster)) graded++;
                }
            }
        }

        var anchorCount = 0;
        var noAnchorable = anchors && !events.Any(e => e.Anchorable);
        if (anchors && noAnchorable)
        {
            // Nothing here had a cast bar, so leave the anchors alone.
            anchors = false;
        }
        if (anchors)
        {
            // A captured cast is an anchor: its id and resolve time.
            var points = new List<SyncPoint>();
            var prev = 0f;
            var pendingPhase = false;
            var lastById = new Dictionary<uint, float>();
            foreach (var e in events)
            {
                // The gap detector runs over every event.
                if (e.Time - prev > (deriveDowntime ? ImportSeamGap : 90f)) pendingPhase = true;
                prev = e.Time;
                if (!e.Anchorable) continue;
                // Same ability again within two match windows: skip the anchor.
                if (lastById.TryGetValue(e.Id, out var lt) && e.Time - lt < 18f) continue;
                lastById[e.Id] = e.Time;
                points.Add(new SyncPoint { Ability = e.Id, Time = e.Time, IsPhase = pendingPhase, Label = e.Name });
                pendingPhase = false;
            }
            // Keep learned anchors past this pull, so a wipe can't truncate.
            var end = events[^1].Time;
            points.AddRange(_fight.SyncPoints.Where(sp => sp.Time > end + 10f));
            _fight.SyncPoints = points;
            anchorCount = points.Count;
        }

        // Downtime windows from the log's silences.
        var downtimeCount = 0;
        if (deriveDowntime && events.Count > 1)
        {
            var windows = new List<DowntimeWindow>();
            for (var i = 1; i < events.Count; i++)
            {
                var gap = events[i].Time - events[i - 1].Time;
                if (gap < ImportWindowGap) continue; // shorter lulls are just mechanic spacing
                // Trim a little off each end of the raw silence.
                var start = MathF.Round(events[i - 1].Time + 3f);
                var dur = MathF.Round(gap - 5f);
                // A silence long enough to re-base is long enough to be a cutscene.
                windows.Add(new DowntimeWindow
                    { Start = start, Duration = dur, TargetHp = -1f, Cutscene = gap >= ImportSeamGap });
            }
            if (windows.Count > 0)
            {
                _fight.CustomDowntimes = windows;
                downtimeCount = windows.Count;
            }
        }

        if (addedRows == 0 && anchorCount == 0 && graded == 0 && downtimeCount == 0)
        {
            PopUndo();
            Flash("Nothing new there (rows already covered, anchors unticked).");
            return;
        }

        C.Save();
        _dirty = true;
        var gradeNote = graded > 0
            ? $" {graded} row(s) graded from the damage and where its players pressed their mits."
            : "";
        var trimNote = filteredOut > 0 ? $" {filteredOut} filler cast(s) left off." : "";
        var lullNote = downtimeCount > 0 ? $" {downtimeCount} untargetable window(s)." : "";
        Flash(noAnchorable
            ? $"Built from {source}: {addedRows} new row(s).{gradeNote}{lullNote}{trimNote} No cast-bar casts found, so existing anchors were left untouched."
            : $"Built from {source}: {addedRows} new row(s), {anchorCount} anchor(s).{gradeNote}{lullNote}{trimNote} "
              + "Build again any time; anchors past this build's end are kept.");
    }

    // ---- log import ----

    private string _flUrl = "";
    private string _flStatus = "";
    private volatile bool _flBusy;
    private List<FFLogsClient.FightInfo>? _flFights;
    private int _flPick;
    private List<FFLogsClient.LogCast>? _flCasts;
    private Dictionary<uint, FFLogsClient.AbilityDamage>? _flDamage;
    private List<FFLogsClient.MitPress>? _flMits;
    private Dictionary<uint, string>? _flNames; // report's own ability names (#2)
    private int _flCastsForFight = -1;
    private int _flAutoCastsFor = -1; // fight id we've already auto-kicked a cast load for
    private string _flFightName = ""; // "Build from FFLogs" by encounter name (#8)
    private string _flIdBuf = "";
    private string _flSecretBuf = "";
    private FightProfile? _flForFight; // whose sheet the cached report state belongs to

    private void DrawFFLogsPopup()
    {
        // Modal, so a stray click outside cannot dismiss the form.
        var stay = true;
        if (!ImGui.BeginPopupModal("##fflogs", ref stay,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings)) return;

        PopupHeader("Build from FFLogs", 460f);

        // Cached report state is per fight, so duties can't cross.
        if (ImGui.IsWindowAppearing() && _flForFight != _fight)
        {
            _flFights = null;
            _flCasts = null;
            _flDamage = null;
            _flMits = null;
            _flNames = null;
            _flCastsForFight = -1;
            _flAutoCastsFor = -1;
            _flFightName = "";
            _flStatus = "";
            _flForFight = _fight;
        }

        // One-time credentials, made on the logs site.
        if (C.FflogsClientId.Length == 0 || C.FflogsClientSecret.Length == 0)
        {
            ImGui.TextDisabled("One-time setup (about two minutes)");
            ImGui.TextWrapped("FFLogs' API needs a personal client. Create one (name it \"FrenMits\", no "
                + "redirect URL needed), then paste its id and secret here. They stay on this PC, "
                + "and the secret is saved encrypted (it only unlocks on your Windows account).");
            if (ImGui.SmallButton("Open fflogs.com/api/clients"))
                Dalamud.Utility.Util.OpenLink("https://www.fflogs.com/api/clients");
            ImGui.SetNextItemWidth(300f);
            ImGui.InputTextWithHint("##flid", "client id", ref _flIdBuf, 128);
            ImGui.SetNextItemWidth(300f);
            ImGui.InputTextWithHint("##flsecret", "client secret", ref _flSecretBuf, 128, ImGuiInputTextFlags.Password);
            ImGui.BeginDisabled(_flIdBuf.Trim().Length == 0 || _flSecretBuf.Trim().Length == 0);
            if (ImGui.Button("Save credentials", new Vector2(160, 0)))
            {
                C.FflogsClientId = _flIdBuf.Trim();
                C.FflogsClientSecret = _flSecretBuf.Trim();
                C.Save();
            }
            ImGui.EndDisabled();
            ImGui.EndPopup();
            return;
        }

        // Fastest path to a skeleton: type the fight name.
        ImGui.SetNextItemWidth(320f);
        ImGui.InputTextWithHint("##flfightname", "fight name (e.g. Futures Rewritten) - pulls the top kill", ref _flFightName, 128);
        ImGui.SameLine();
        ImGui.BeginDisabled(_flBusy || _flFightName.Trim().Length == 0);
        if (ImGui.SmallButton("Find top kill")) SearchEncounter();
        ImGui.EndDisabled();
        if (Widgets.HoveredDelayed())
            ImGui.SetTooltip("Loads the current #1 speed kill, ready to import.");

        ImGui.TextDisabled("or paste a specific log:");
        ImGui.SetNextItemWidth(320f);
        ImGui.InputTextWithHint("##flurl", "FFLogs report link (or code)", ref _flUrl, 256);
        ImGui.SameLine();
        ImGui.BeginDisabled(_flBusy || FFLogsClient.ParseReportCode(_flUrl) == null);
        if (ImGui.SmallButton("Fetch")) FetchFights();
        ImGui.EndDisabled();
        ImGui.SameLine();
        // Typo'd credentials must be fixable without file surgery.
        if (ImGui.SmallButton("Credentials..."))
        {
            _flIdBuf = C.FflogsClientId;
            _flSecretBuf = "";
            C.FflogsClientId = "";
            C.FflogsClientSecret = "";
            C.Save();
        }
        if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Re-enter your FFLogs API client id and secret.");

        if (_flStatus.Length > 0) ImGui.TextDisabled(_flStatus);

        if (_flFights is { Count: > 0 } fights)
        {
            var labels = fights.Select(f =>
                $"#{f.Id}  {f.Name}  {(f.Kill ? "KILL" : "wipe")}  {(int)f.DurationSec / 60}:{(int)f.DurationSec % 60:00}").ToArray();
            _flPick = Math.Clamp(_flPick, 0, fights.Count - 1);
            ImGui.SetNextItemWidth(320f);
            if (ImGui.Combo("##flfight", ref _flPick, labels, labels.Length))
            {
                _flCasts = null; // picked a different fight: refetch its casts
                _flDamage = null;
                _flMits = null;
                _flNames = null;
                _flCastsForFight = -1;
                _flAutoCastsFor = -1; // let the new pick auto-load
            }

            var picked = fights[_flPick];
            if (_flCasts == null || _flCastsForFight != picked.Id)
            {
                // Load the picked fight's casts once, so the flow stays short.
                if (!_flBusy && _flAutoCastsFor != picked.Id)
                {
                    _flAutoCastsFor = picked.Id;
                    FetchCasts(picked);
                }
                if (_flBusy)
                    ImGui.TextDisabled("Loading casts...");
                else if (ImGui.Button("Reload casts", new Vector2(120, 0)))
                    FetchCasts(picked);
            }
            else
            {
                ImGui.TextUnformatted($"{_flCasts.Count} enemy casts loaded.");
                ImGui.Checkbox("Add mechanic rows", ref _bpRows);
                ImGui.Checkbox("Set resync anchors", ref _bpAnchors);
                ImGui.BeginDisabled(!_bpRows);
                ImGui.Checkbox("Only meaningful mechanics", ref _flMeaningful);
                if (Widgets.HoveredDelayed())
                    ImGui.SetTooltip("Keep only casts that hit or had a cast bar.");
                ImGui.EndDisabled();
                // Untargetable windows come from the silences, not the rows.
                ImGui.Checkbox("Add untargetable windows", ref _flDowntime);
                if (Widgets.HoveredDelayed())
                    ImGui.SetTooltip("Turn the log's downtime gaps into untargetable rows.");
                ImGui.TextDisabled("Their kill's timings become this sheet's skeleton; anchors");
                ImGui.TextDisabled("snap it to YOUR pulls live. Make sure the log is this duty.");
                ImGui.BeginDisabled(!_bpRows && !_bpAnchors && !_flDowntime);
                if (ImGui.Button("Import", new Vector2(120, 0)))
                {
                    var events = SiftEvents(_flCasts.OrderBy(c => c.Time)
                        .Select(c => (c.AbilityId, c.Time, Anchorable: c.HasCastBar)), _flNames);
                    ApplyBuild(events, _bpRows, _bpAnchors, "the log", _flDamage, _flMits,
                        _bpRows && _flMeaningful, _flDowntime);
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndDisabled();
            }
        }

        // Attribution: credit the data source, no endorsement implied.
        ImGui.Separator();
        ImGui.TextDisabled("Data from the FFLogs API. FrenMits is not affiliated with or endorsed by FFLogs.");

        ImGui.EndPopup();
    }

    // Resolve a fight name to its top kill, then run the same flow.
    private void SearchEncounter()
    {
        var name = _flFightName.Trim();
        if (name.Length == 0) return;
        _flBusy = true;
        _flStatus = $"Finding the top kill for \"{name}\"...";
        _flFights = null;
        _flCasts = null;
        _flDamage = null;
        _flMits = null;
        _flNames = null;
        _flCastsForFight = -1;
        _flAutoCastsFor = -1;
        _flForFight = _fight;
        var forFight = _fight;
        var (id, secret) = (C.FflogsClientId, C.FflogsClientSecret);
        System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var enc = await _plugin.FFLogs.FindEncounterAsync(id, secret, name);
                if (enc == null) { _flStatus = $"No encounter matches \"{name}\"."; return; }
                var top = await _plugin.FFLogs.GetTopKillAsync(id, secret, enc.Id, "speed");
                if (top == null) { _flStatus = $"Found {enc.Name}, but it has no ranked kills yet."; return; }
                var fights = await _plugin.FFLogs.GetFightsAsync(id, secret, top.Value.Code);
                // They switched sheets mid-fetch, so drop this result.
                if (_flForFight != forFight) return;
                var idx = fights.FindIndex(f => f.Id == top.Value.FightId);
                // Pre-select before publishing, so the right casts auto-load.
                _flPick = idx >= 0 ? idx : 0;
                _flUrl = top.Value.Code;
                _flAutoCastsFor = -1;
                _flFights = fights;
                _flStatus = $"{enc.Name}: loaded the current top-speed kill ({fights.Count} fight(s) in that log).";
            }
            catch (Exception ex)
            {
                _flStatus = ex.Message;
                Service.Log.Warning(ex, "FrenMits: FFLogs encounter search failed");
            }
            finally { _flBusy = false; }
        });
    }

    private void FetchFights()
    {
        var code = FFLogsClient.ParseReportCode(_flUrl);
        if (code == null) return;
        _flBusy = true;
        _flStatus = "Fetching report...";
        _flFights = null;
        _flCasts = null;
        _flDamage = null;
        _flMits = null;
        _flNames = null;
        _flCastsForFight = -1;
        _flAutoCastsFor = -1;     // a fresh report should auto-load its first fight
        _flPick = 0;              // reset on the draw thread: it also clamps this
        _flForFight = _fight;
        var forFight = _fight;
        var (id, secret) = (C.FflogsClientId, C.FflogsClientSecret);
        System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var fights = await _plugin.FFLogs.GetFightsAsync(id, secret, code);
                if (_flForFight != forFight) return; // sheet switched mid-fetch
                _flFights = fights;
                _flStatus = fights.Count == 0 ? "No boss fights in that report." : $"{fights.Count} fight(s); kills listed first.";
            }
            catch (Exception ex)
            {
                _flStatus = ex.Message;
                Service.Log.Warning(ex, "FrenMits: FFLogs fights fetch failed");
            }
            finally { _flBusy = false; }
        });
    }

    private void FetchCasts(FFLogsClient.FightInfo fight)
    {
        var code = FFLogsClient.ParseReportCode(_flUrl);
        if (code == null) return;
        _flBusy = true;
        _flStatus = $"Loading {fight.Name}'s casts...";
        var forFight = _fight;
        var (id, secret) = (C.FflogsClientId, C.FflogsClientSecret);
        System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var casts = await _plugin.FFLogs.GetCastsAsync(id, secret, code, fight);
                // Grades and mit presses are bonuses, never blockers.
                Dictionary<uint, FFLogsClient.AbilityDamage>? dmg = null;
                try { dmg = await _plugin.FFLogs.GetDamageAsync(id, secret, code, fight); }
                catch (Exception dex) { Service.Log.Warning(dex, "FrenMits: FFLogs damage fetch failed"); }
                List<FFLogsClient.MitPress>? mits = null;
                try { mits = await _plugin.FFLogs.GetMitCastsAsync(id, secret, code, fight); }
                catch (Exception mex) { Service.Log.Warning(mex, "FrenMits: FFLogs mit-press fetch failed"); }
                // The report's own ability names, so rows match the logs.
                Dictionary<uint, string>? names = null;
                try { names = await _plugin.FFLogs.GetAbilityNamesAsync(id, secret, code); }
                catch (Exception nex) { Service.Log.Warning(nex, "FrenMits: FFLogs ability-name fetch failed"); }
                if (_flForFight != forFight) return; // sheet switched mid-fetch
                _flCasts = casts;
                _flDamage = dmg;
                _flMits = mits;
                _flNames = names;
                _flCastsForFight = fight.Id;
                _flStatus = "";
            }
            catch (Exception ex)
            {
                _flStatus = ex.Message;
                Service.Log.Warning(ex, "FrenMits: FFLogs casts fetch failed");
            }
            finally { _flBusy = false; }
        });
    }
}
