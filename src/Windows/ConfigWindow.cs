using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;

namespace FrenMits.Windows;

public partial class ConfigWindow : Window, IDisposable
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    // Official-sheet star color (drawn with the icon font).
    private static readonly Vector4 GoldStar = new(0.98f, 0.82f, 0.35f, 1f);
    // User-created fight marker color.
    private static readonly Vector4 UserBlue = new(0.55f, 0.75f, 0.98f, 1f);

    private int _selectedFight;

    // In-progress m:ss edit for the line table (one row at a time).
    private MitLine? _editTimeLine;
    private string _editTimeBuf = "";
    private MitLine? _editOffLine;      // per-line offset (±s column) inline edit
    private string _editOffBuf = "";
    private string _editOffSeed = "";
    private bool _offFocusPending;

    // Land a half-typed ±s edit before switching cells: clicking an EARLIER row
    // draws before the active editor, which would otherwise drop the text.
    private void CommitPendingOffset()
    {
        if (_editOffLine != null && _editOffBuf != _editOffSeed
            && float.TryParse(_editOffBuf, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v))
        {
            _editOffLine.OffsetSeconds = Math.Clamp(v, -30f, 30f);
            C.Save();
            _plugin.SheetViewWindow.MarkPlanDirty();
        }
        _editOffLine = null;
    }

    // In-memory line clipboard for the right-click copy / paste / duplicate menu.
    private MitLine? _copiedLine;

    // Plugin icon (group-hug logo), loaded once from the file shipped next to the
    // DLL. Null until found; the Home page falls back to a glyph if it's missing.
    private Dalamud.Interface.Textures.ISharedImmediateTexture? _iconShared;
    private bool _iconLookedUp;
    private Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap? IconWrap()
    {
        if (!_iconLookedUp)
        {
            _iconLookedUp = true;
            try
            {
                var dir = Service.PluginInterface.AssemblyLocation.Directory?.FullName;
                var path = dir == null ? null : System.IO.Path.Combine(dir, "icon.png");
                if (path != null && System.IO.File.Exists(path))
                    _iconShared = Service.TextureProvider.GetFromFile(path);
            }
            catch { /* fall back to the glyph emblem */ }
        }
        return _iconShared?.GetWrapOrDefault();
    }

    // Left-sidebar navigation.
    private enum NavKind { Home, Fights, Display, NextMits, Audio, PartyRecap, CombatTimer }
    private NavKind _nav = NavKind.Home;
    private string _navCategory = "Ultimate";

    private static readonly string[] Categories = { "Ultimate", "Savage", "Extreme" };

    // Every group a fight can file under (the sidebar's order). Legacy
    // "Raids"/"Other" categories display and file as Extreme (CategoryOf).
    private static readonly string[] FightTypes = { "Ultimate", "Savage", "Extreme" };

    // The sidebar group a fight belongs to. Built-ins use their baked category;
    // any fight whose stored category is no longer a tab (e.g. the removed Raids /
    // Other) falls back to Extreme so it isn't orphaned.
    private static string CategoryOf(FightProfile f)
    {
        if (!string.IsNullOrEmpty(f.Category) && Array.IndexOf(Categories, f.Category) >= 0)
            return f.Category;
        return Builtin.Has(f.TerritoryId) ? Builtin.Category(f.TerritoryId) : "Extreme";
    }

    // Import state.
    private string _importBuffer = "";
    private List<string[]>? _importGrid;
    private char _importDelimiter = '\t';
    private int _timeCol, _mechCol = 1, _actionCol = 2;
    private bool _importHeader = true;
    private int _importJobMode; // 0 = all, 1 = current selection, 2 = pick
    private readonly HashSet<string> _importPickedJobs = new(StringComparer.OrdinalIgnoreCase);

    public ConfigWindow(Plugin plugin)
        : base("Fren Mits###config")
    {
        _plugin = plugin;
        Size = new Vector2(740, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    // Window-level theming (background, title, border) must be applied before the
    // window begins, so it lives in PreDraw/PostDraw.
    public override void PreDraw()
    {
        Theme.PushWindow();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 6f);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(4);
        Theme.PopWindow();
    }

    public override void Draw()
    {
        Theme.PushWidgets();
        // Fatter scrollbars (easier to grab) + softer rounded controls.
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 18f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 9f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 4f);

        DrawStatusHeader();
        ImGui.Separator();

        // Content sits above a pinned footer: a left nav sidebar + the active page.
        var footerH = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y + 4f;
        if (ImGui.BeginChild("##content", new Vector2(0, -footerH), false))
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.PanelBg);
            if (ImGui.BeginChild("##sidebar", new Vector2(186, 0), true))
                DrawSidebar();
            ImGui.EndChild();
            ImGui.PopStyleColor();

            ImGui.SameLine();
            if (ImGui.BeginChild("##page", new Vector2(0, 0), false))
            {
                ImGui.Spacing();
                ImGui.Indent(4f);
                DrawSelectedPage();
                ImGui.Unindent(4f);
            }
            ImGui.EndChild();
        }
        ImGui.EndChild();

        DrawFooter();
        ImGui.PopStyleVar(4);
        Theme.PopWidgets();

        // Toggle()/CfgCheck()/GridCheck() return the new value for the CALLER to
        // assign, so saving inside them would persist the pre-assignment state
        // (one edit stale on disk until some later save). They mark this flag
        // instead, and the save runs here, after every assignment this frame.
        if (_toggleDirty)
        {
            _toggleDirty = false;
            C.Save();
        }
    }

    private bool _toggleDirty;

    // Truthful save status: every edit writes to disk the moment it happens, so
    // there is never an unsaved state to warn about on exit. The old "Save
    // changes" button was ceremonial and implied otherwise.
    private void DrawFooter()
    {
        ImGui.Separator();

        if (Configuration.SuppressSave)
        {
            StatusDot(ImGuiColors.DalamudYellow);
            ImGui.SameLine(0, 6);
            ImGui.TextColored(ImGuiColors.DalamudYellow,
                "Saving is OFF this session (your config file failed to load and was backed up).");
            return;
        }

        var last = Configuration.LastSavedAt;
        var recent = last != DateTime.MinValue && (DateTime.Now - last).TotalSeconds < 3;
        StatusDot(recent ? ImGuiColors.ParsedGreen : ImGuiColors.HealerGreen);
        ImGui.SameLine(0, 6);
        ImGui.TextDisabled(last == DateTime.MinValue
            ? "All changes save instantly; nothing to lose on exit."
            : recent
                ? "All changes saved just now."
                : $"All changes saved; every edit writes instantly (last {Ago(last)}).");
    }

    private static string Ago(DateTime t)
    {
        var s = (DateTime.Now - t).TotalSeconds;
        return s < 90 ? $"{(int)s}s ago" : s < 5400 ? $"{(int)(s / 60)}m ago" : $"{(int)(s / 3600)}h ago";
    }

    // Config-bound checkbox: edits a local copy, saves on change, returns the new value.
    private bool CfgCheck(string label, bool value) => Toggle(label, value);

    // A checkbox + label. Fills green with a white tick when on. Saves on change
    // (deferred to the end of Draw, AFTER the caller assigns the returned value).
    private bool Toggle(string label, bool value)
    {
        var v = value;
        if (GreenCheckbox($"##tg_{label}", ref v)) _toggleDirty = true;
        ImGui.SameLine(0, 8);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        return v;
    }

    // The one checkbox style used across the whole config: fills green with a white
    // tick when checked (replaces the old hard-to-read pill toggle). Mirrors
    // ImGui.Checkbox's signature/return so it's a drop-in everywhere.
    private static bool GreenCheckbox(string label, ref bool v)
    {
        var on = v; // style by the current state; push and pop must use the same flag
        if (on)
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, 0xFF5AC832);        // green (ABGR)
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, 0xFF6FD647);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, 0xFF5AC832);
            ImGui.PushStyleColor(ImGuiCol.CheckMark, 0xFFFFFFFF);      // white tick
        }
        var changed = ImGui.Checkbox(label, ref v);
        if (on) ImGui.PopStyleColor(4);
        return changed;
    }

    // Tooltip on the previous item — keeps help off the page (no inline "(?)") so
    // toggle grids stay clean.
    // A short hover delay before informational tooltips, so sweeping the mouse
    // across a page doesn't flash one on every control it crosses. (These
    // bindings predate ImGui's ForTooltip flag, hence the manual timer.)
    private static Vector2 _tipPos;
    private static double _tipSince;

    private static int _tipFrame;

    private static void Tip(string text)
    {
        if (!ImGui.IsItemHovered()) return;
        // The item rect is a fine identity for "did the hovered thing change";
        // a frame gap means the mouse left and the delay starts over.
        var pos = ImGui.GetItemRectMin();
        var now = ImGui.GetTime();
        var frame = ImGui.GetFrameCount();
        if (pos != _tipPos || frame - _tipFrame > 2) { _tipPos = pos; _tipSince = now; }
        _tipFrame = frame;
        if (now - _tipSince >= 0.35) ImGui.SetTooltip(text);
    }

    // A checkbox in the next cell of a 2-column toggle grid. Returns the value so
    // the caller can assign it straight back to its setting.
    private bool GridCheck(string label, bool value, string? tip = null)
    {
        ImGui.TableNextColumn();
        value = Toggle(label, value);
        if (tip != null) Tip(tip);
        return value;
    }

    // Section header with a blue accent bar + uppercase label, matching the
    // panel-based look of the reference UI.
    private static void SeparatorText(string text)
    {
        ImGui.Spacing();
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var h = ImGui.GetTextLineHeight();
        dl.AddRectFilled(p + new Vector2(0, 1), p + new Vector2(3, h), Theme.Accent, 2f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 10);
        ImGui.TextColored(new Vector4(0.62f, 0.66f, 0.72f, 1f), text.ToUpperInvariant());
        ImGui.Spacing();
    }

    // Collapsible section. Returns true when expanded; wrap the body in the if.
    private static bool Section(string text, bool open = false)
    {
        ImGui.Spacing();
        return ImGui.CollapsingHeader(text.ToUpperInvariant(),
            open ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
    }

    private static void Dot(bool on, string label)
    {
        StatusDot(on ? ImGuiColors.HealerGreen : ImGuiColors.DalamudGrey);
        ImGui.SameLine(0, 4);
        ImGui.TextUnformatted(label);
    }

    private static void WarnDot(string label)
    {
        StatusDot(ImGuiColors.DalamudYellow);
        ImGui.SameLine(0, 4);
        ImGui.TextColored(ImGuiColors.DalamudYellow, label);
    }

    // Small filled status dot via the draw list: the text font has no circle
    // glyph, so a "●" literal renders as an empty box.
    private static void StatusDot(Vector4 color)
    {
        var size = ImGui.GetTextLineHeight();
        var pos = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddCircleFilled(
            new Vector2(pos.X + size * 0.5f, pos.Y + size * 0.55f), size * 0.22f,
            ImGui.ColorConvertFloat4ToU32(color));
        ImGui.Dummy(new Vector2(size, size));
    }

    private static void HelpMarker(string text)
    {
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 28f);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    private void DrawStatusHeader()
    {
        var fight = _plugin.ActiveFight();
        var job = _plugin.ActiveJobAbbreviation();
        var running = _plugin.Timer.Running;

        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.PanelBg);
        var height = ImGui.GetTextLineHeightWithSpacing() * 2 + 16;
        if (ImGui.BeginChild("##status", new Vector2(0, height), true, ImGuiWindowFlags.NoScrollbar))
        {
            // Accent bar down the left edge of the panel.
            var dl = ImGui.GetWindowDrawList();
            var wp = ImGui.GetWindowPos();
            dl.AddRectFilled(wp, wp + new Vector2(3, ImGui.GetWindowHeight()), Theme.Accent);

            // Title + the zone's fight (or a hint when there isn't one).
            ImGui.TextUnformatted("Fren Mits");
            ImGui.SameLine(0, 10);
            ImGui.TextColored(new Vector4(0.55f, 0.59f, 0.66f, 1f),
                fight != null ? fight.Name : "no supported fight in this zone");

            // Right-aligned quick action (measured, not hardcoded, so font
            // scaling can't push it off the edge).
            var right = ImGui.GetWindowWidth()
                - (ImGui.CalcTextSize("Test").X + ImGui.GetFrameHeight()
                   + ImGui.GetStyle().ItemInnerSpacing.X + ImGui.GetStyle().WindowPadding.X + 12f);
            if (right > 0) { ImGui.SameLine(); ImGui.SetCursorPosX(right); }
            var test = C.TestMode;
            if (GreenCheckbox("Test", ref test)) { C.TestMode = test; C.Save(); }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Show a sample call so you can place / size the overlay.\nTurns itself off automatically when a real pull starts.");

            // Status dots on the second line.
            Dot(job != null, $"Job: {job ?? "?"}");
            ImGui.SameLine(0, 18);
            Dot(running, running ? $"Timer: {_plugin.Timer.Elapsed:0.0}s" : "Timer: idle");
            // Audio / Resync only appear when they need attention: a healthy
            // plugin shows a quiet header.
            if (!C.AudioEnabled) { ImGui.SameLine(0, 18); WarnDot("Audio off"); }
            if (!C.EnableSync) { ImGui.SameLine(0, 18); WarnDot("Resync off"); }
            if (_plugin.FrameErrorCount > 0 && (DateTime.Now - _plugin.LastFrameErrorAt.ToLocalTime()).TotalMinutes < 5)
            {
                ImGui.SameLine(0, 18);
                WarnDot($"internal errors ({_plugin.FrameErrorCount}): check /xllog");
            }
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    // ---- Left sidebar nav -------------------------------------------------

    private string _expandFightId = "";

    private static FontAwesomeIcon CategoryIcon(string cat) => cat switch
    {
        "Ultimate" => FontAwesomeIcon.Crown,
        "Savage" => FontAwesomeIcon.Skull,
        "Extreme" => FontAwesomeIcon.Fire,
        _ => FontAwesomeIcon.LayerGroup,
    };

    private void DrawSidebar()
    {
        if (NavItem(FontAwesomeIcon.Home, "Home", _nav == NavKind.Home)) _nav = NavKind.Home;

        ImGui.Spacing();
        SidebarHeading("FIGHTS");
        foreach (var cat in Categories)
        {
            var count = C.Fights.Count(f => CategoryOf(f) == cat);
            if (NavItem(CategoryIcon(cat), cat, _nav == NavKind.Fights && _navCategory == cat, count))
            {
                _nav = NavKind.Fights;
                _navCategory = cat;
            }
        }

        ImGui.Spacing();
        SidebarHeading("TOOLS");
        // Sheet View is a window, not a page: the nav item opens it directly.
        if (NavItem(FontAwesomeIcon.Table, "Sheet View", false))
        {
            var fight = _plugin.ActiveFight();
            _plugin.SheetViewWindow.Open(
                fight != null && (Builtin.Has(fight.TerritoryId) || fight.CustomSlots.Count > 0) ? fight : null);
        }
        if (NavItem(FontAwesomeIcon.ShieldAlt, "Next Mits & Timeline", _nav == NavKind.NextMits)) _nav = NavKind.NextMits;
        if (NavItem(FontAwesomeIcon.Clock, "Combat Timer", _nav == NavKind.CombatTimer)) _nav = NavKind.CombatTimer;
        if (NavItem(FontAwesomeIcon.ClipboardList, "Party Mit Recap", _nav == NavKind.PartyRecap)) _nav = NavKind.PartyRecap;

        ImGui.Spacing();
        SidebarHeading("SETTINGS");
        if (NavItem(FontAwesomeIcon.Desktop, "Display", _nav == NavKind.Display)) _nav = NavKind.Display;
        if (NavItem(FontAwesomeIcon.VolumeUp, "Audio", _nav == NavKind.Audio)) _nav = NavKind.Audio;

        DrawSidebarSetup();
    }

    private static void SidebarHeading(string text)
    {
        ImGui.Spacing();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8);
        // Soft sky blue (same family as the user-created badge): cool headings
        // against the warm orange selection reads cleaner than orange-on-orange.
        ImGui.TextColored(new Vector4(0.55f, 0.75f, 0.98f, 1f), text);
        ImGui.Spacing();
    }

    private bool NavItem(FontAwesomeIcon icon, string label, bool selected, int? count = null)
    {
        var startX = ImGui.GetCursorPosX();
        var startY = ImGui.GetCursorPosY();

        if (selected)
        {
            ImGui.PushStyleColor(ImGuiCol.Header, 0x66F6823B);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0x88F6823B);
        }
        var clicked = ImGui.Selectable($"##nav-{label}", selected, ImGuiSelectableFlags.None, new Vector2(0, 27));
        if (selected) ImGui.PopStyleColor(2);

        var endX = ImGui.GetCursorPosX();
        var endY = ImGui.GetCursorPosY();
        var col = selected ? new Vector4(1f, 1f, 1f, 1f) : new Vector4(0.74f, 0.77f, 0.82f, 1f);

        // Icon (icon font) + label drawn over the selectable row.
        ImGui.SameLine();
        ImGui.SetCursorPos(new Vector2(startX + 10, startY + 6));
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            ImGui.TextColored(col, icon.ToIconString());
        ImGui.SameLine();
        ImGui.SetCursorPos(new Vector2(startX + 36, startY + 6));
        ImGui.TextColored(col, label);

        if (count is { } n)
        {
            var txt = n.ToString();
            ImGui.SameLine();
            ImGui.SetCursorPos(new Vector2(ImGui.GetContentRegionMax().X - ImGui.CalcTextSize(txt).X - 10, startY + 6));
            ImGui.TextDisabled(txt);
        }

        ImGui.SetCursorPos(new Vector2(endX, endY)); // resume normal flow below the row
        return clicked;
    }

    // Job + role in one compact block. The role pick applies to every built-in
    // fight, mapping to whatever slot that fight uses for the role (e.g. Melee 1
    // -> D1 in DMU, M1 in FRU). A green check shows when every built-in fight is
    // on that role's slot.
    private void DrawSidebarSetup()
    {
        ImGui.Spacing();
        SidebarHeading("YOUR SETUP");

        // Job row.
        var options = new List<string> { "Auto (current job)" };
        options.AddRange(Jobs.Abbreviations);
        var jobIdx = C.JobSelection == "Auto"
            ? 0
            : Math.Max(0, Array.IndexOf(Jobs.Abbreviations, C.JobSelection) + 1);

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Job");
        ImGui.SameLine(48f);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 12);
        if (ImGui.Combo("##sbjob", ref jobIdx, options.ToArray(), options.Count))
        {
            C.JobSelection = jobIdx == 0 ? "Auto" : Jobs.Abbreviations[jobIdx - 1];
            C.Save();
        }
        Tip($"The job your mits and calls are read for. Active now: {_plugin.ActiveJobAbbreviation() ?? "?"}.");

        // One click to pin whatever you're playing right now - no list-diving.
        // Hidden on Auto: Auto already follows the live job, so pinning from
        // there would only stop it following the NEXT job change.
        var live = Plugin.LocalPlayer?.ClassJob.RowId is { } rid ? Jobs.ByRowId(rid)?.Abbreviation : null;
        if (live != null
            && !string.Equals(C.JobSelection, "Auto", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(C.JobSelection, live, StringComparison.OrdinalIgnoreCase))
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8f);
            if (ImGui.SmallButton($"Use current ({live})"))
            {
                C.JobSelection = live;
                C.Save();
            }
            Tip("Pin your job with one click instead of picking it from the list.");
        }

        // Role row.
        var roles = Builtin.Roles;
        var labels = new List<string> { "(pick a role)" };
        labels.AddRange(roles);
        var roleIdx = string.IsNullOrEmpty(C.RoleSelection) ? 0 : Math.Max(0, Array.IndexOf(roles, C.RoleSelection) + 1);

        var active = !string.IsNullOrEmpty(C.RoleSelection) && RoleActiveEverywhere(C.RoleSelection);

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Role");
        ImGui.SameLine(48f);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 12 - (active ? 24 : 0));
        if (ImGui.Combo("##sbrole", ref roleIdx, labels.ToArray(), labels.Count))
        {
            if (roleIdx == 0) { C.RoleSelection = ""; C.Save(); }
            else SelectRoleForAll(roles[roleIdx - 1]);
        }
        Tip("One pick sets your slot in every fight that has a sheet.");
        if (active)
        {
            ImGui.SameLine();
            using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                ImGui.TextColored(ImGuiColors.HealerGreen, FontAwesomeIcon.Check.ToIconString());
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Every fight is on this role's slot.");
        }

        // One click to match the role to the job you're playing right now, same
        // idea as the job picker's "Use current". Hidden when already on it, and
        // also when the pick is the other seat of the same pair (Off Tank while
        // tanking, Melee 2 on a melee): that pick is deliberate, don't nag.
        var liveRole = RoleForJob(_plugin.ActiveJobAbbreviation());
        if (liveRole != null
            && !string.Equals(C.RoleSelection, liveRole, StringComparison.OrdinalIgnoreCase)
            && !SameSeatGroup(C.RoleSelection, liveRole))
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8f);
            if (ImGui.SmallButton($"Use current ({liveRole})"))
                SelectRoleForAll(liveRole);
            Tip("Set the role from your current job with one click. Tanks and melee "
                + "land on the first seat (Main Tank / Melee 1); pick Off Tank or "
                + "Melee 2 from the list if that's your spot.");
        }

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8);
        var ask = C.ShowSlotPopupOnEntry;
        if (GreenCheckbox("Ask on duty entry", ref ask)) { C.ShowSlotPopupOnEntry = ask; C.Save(); }
        Tip("When you enter a fight that has a sheet, a tiny popup shows which slot is yours\nand lets you change it. Shows once per entry; hides when combat starts. Off by default.");
    }

    // Both roles are seats of the same pair (MT/OT, or Melee 1/2), so the
    // current pick already matches the live job's role.
    private static bool SameSeatGroup(string selection, string liveRole)
        => (selection is "Main Tank" or "Off Tank" && liveRole is "Main Tank" or "Off Tank")
        || (selection is "Melee 1" or "Melee 2" && liveRole is "Melee 1" or "Melee 2");

    // The canonical role for a job: healers map to their own column, everyone
    // else by role bucket, preferring the first seat (mirrors DefaultSlotForJob).
    private static string? RoleForJob(string? jobAbbr)
    {
        if (Jobs.ByAbbreviation(jobAbbr) is not { } job) return null;
        if (Builtin.Roles.Contains(job.Abbreviation)) return job.Abbreviation;
        return job.Role switch
        {
            JobRole.Tank => "Main Tank",
            JobRole.Melee => "Melee 1",
            JobRole.PhysicalRanged => "Phys Ranged",
            JobRole.Caster => "Caster",
            _ => null,
        };
    }

    // True if every fight with a sheet is currently on the slot this role maps
    // to. A sheet with no column for the role can't disagree, so it passes.
    private bool RoleActiveEverywhere(string role)
    {
        var fights = C.Fights.Where(f => Builtin.Has(f.TerritoryId) || f.CustomSlots.Count > 0).ToList();
        return fights.Count > 0 && fights.All(f =>
        {
            var want = Builtin.Has(f.TerritoryId)
                ? Builtin.RoleSlot(f.TerritoryId, role)
                : Builtin.RoleSlotIn(f.CustomSlots, role);
            return want == null || string.Equals(f.Slot, want, StringComparison.OrdinalIgnoreCase);
        });
    }

    // Apply the chosen role to every built-in fight, loading each one's matching
    // slot (keeping that slot's own edits).
    private void SelectRoleForAll(string role)
    {
        _plugin.SetRoleForAll(role);
        var last = C.Fights.LastOrDefault(f => Builtin.Has(f.TerritoryId));
        if (last != null) C.DmuSlot = last.Slot;
        FlashBuiltin($"Set every fight to {role}.");
    }

    private void DrawSelectedPage()
    {
        switch (_nav)
        {
            case NavKind.Home: DrawHomePage(); break;
            case NavKind.Display: DrawDisplayTab(); break;
            case NavKind.NextMits: DrawNextMitsPage(); break;
            case NavKind.Audio: DrawAudioTab(); break;
            case NavKind.PartyRecap: DrawPartyRecapPage(); break;
            case NavKind.CombatTimer: DrawCombatTimerPage(); break;
            default: DrawFightCategoryPage(_navCategory); break;
        }
    }

    private string Version => typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    // Approximate width of an ImGuiComponents.IconButtonWithText, for centering it.
    private float IconBtnWidth(FontAwesomeIcon icon, string text)
    {
        float iw;
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            iw = ImGui.CalcTextSize(icon.ToIconString()).X;
        var st = ImGui.GetStyle();
        return iw + st.ItemInnerSpacing.X + ImGui.CalcTextSize(text).X + st.FramePadding.X * 2f;
    }

    private void DrawHomePage()
    {
        void Center(float w)
        {
            var x = (ImGui.GetContentRegionAvail().X - w) * 0.5f;
            if (x > 0) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + x);
        }

        var accent = new Vector4(0.23f, 0.51f, 0.96f, 1f);
        var grey = new Vector4(0.55f, 0.59f, 0.66f, 1f);

        ImGui.Dummy(new Vector2(0, 10));

        // Emblem: the group-hug icon, or a glyph shield if it didn't load.
        var icon = IconWrap();
        if (icon != null)
        {
            const float sz = 112f;
            Center(sz);
            ImGui.Image(icon.Handle, new Vector2(sz, sz));
        }
        else
            using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            {
                ImGui.SetWindowFontScale(2.6f);
                var s = FontAwesomeIcon.Shield.ToIconString();
                Center(ImGui.CalcTextSize(s).X);
                ImGui.TextColored(accent, s);
                ImGui.SetWindowFontScale(1f);
            }

        // Title (big crisp font) + tagline.
        var titleFont = _plugin.Fonts.Get(34f, "Default", false, false);
        if (titleFont is { Available: true })
            using (titleFont.Push())
            {
                Center(ImGui.CalcTextSize("Fren Mits").X);
                ImGui.TextUnformatted("Fren Mits");
            }
        else { Center(ImGui.CalcTextSize("Fren Mits").X); ImGui.TextUnformatted("Fren Mits"); }

        Center(ImGui.CalcTextSize("It's mits with frens.").X);
        ImGui.TextColored(grey, "It's mits with frens.");

        // Accent divider.
        ImGui.Dummy(new Vector2(0, 8));
        var dl = ImGui.GetWindowDrawList();
        var cy = ImGui.GetCursorScreenPos().Y;
        var cx = ImGui.GetWindowPos().X + ImGui.GetWindowWidth() * 0.5f;
        dl.AddRectFilled(new Vector2(cx - 60, cy), new Vector2(cx + 60, cy + 2), 0xFFF6823B, 1f);
        ImGui.Dummy(new Vector2(0, 14));

        // First-run: three steps until the plugin is actually calling mits.
        // Disappears forever once any fight has a slot picked.
        if (!C.Fights.Any(f => !string.IsNullOrEmpty(f.Slot)))
        {
            var cardW = MathF.Max(220f, MathF.Min(430f, ImGui.GetContentRegionAvail().X - 20f));
            Center(cardW);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.PanelBg);
            if (ImGui.BeginChild("##firstrun",
                    new Vector2(cardW, ImGui.GetTextLineHeightWithSpacing() * 9f + 24f), true))
            {
                ImGui.TextColored(new Vector4(0.42f, 0.66f, 0.96f, 1f), "Get started");
                ImGui.TextWrapped("1. Pick your job in the sidebar (or leave it on Auto).");
                ImGui.TextWrapped("2. Open your fight and choose \"Your slot\": that column of the mit sheet becomes yours.");
                ImGui.TextWrapped("3. Tick Test (top right) and drag the call display where you want it. It switches off by itself when you pull.");
                ImGui.Spacing();
                if (ImGui.SmallButton("Take me to the fights"))
                {
                    _nav = NavKind.Fights;
                    _navCategory = "Ultimate";
                }
            }
            ImGui.EndChild();
            ImGui.PopStyleColor();
            ImGui.Dummy(new Vector2(0, 10));
        }

        // Action row: just GitHub.
        var ghW = IconBtnWidth(FontAwesomeIcon.ExternalLinkAlt, "GitHub");
        Center(ghW);
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ExternalLinkAlt, "GitHub"))
            Dalamud.Utility.Util.OpenLink("https://github.com/swixum/FrenMits");

        // Version, centered below.
        ImGui.Dummy(new Vector2(0, 6));
        var ver = $"v{Version}";
        Center(ImGui.CalcTextSize(ver).X);
        ImGui.TextDisabled(ver);
    }

    // ---- Fights page ------------------------------------------------------

    // Jump from Sheet View straight to a fight's page (per-line options and
    // import tools live there).
    public void OpenFightPage(FightProfile fight)
    {
        IsOpen = true;
        BringToFront();
        _nav = NavKind.Fights;
        _navCategory = CategoryOf(fight);
        _expandFightId = fight.Id;
    }

    // The expansion a fight's zone belongs to, straight from the game data
    // (TerritoryType.ExVersion; 0 = ARR through 5 = Dawntrail). Correct for
    // anything a user adds too, no table to maintain. uint.MaxValue = unknown.
    // Cached per territory - this runs inside the per-frame sort.
    private static readonly Dictionary<uint, uint> ExCache = new();

    private static uint ExpansionOf(FightProfile f)
    {
        if (ExCache.TryGetValue(f.TerritoryId, out var hit)) return hit;
        uint ex;
        try
        {
            var t = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()?.GetRowOrDefault(f.TerritoryId);
            ex = t?.ExVersion.RowId ?? uint.MaxValue;
        }
        catch { ex = uint.MaxValue; }
        ExCache[f.TerritoryId] = ex;
        return ex;
    }

    private static string ExpansionName(uint ex)
    {
        try
        {
            var name = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.ExVersion>()?.GetRowOrDefault(ex)?.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name)) return name!;
        }
        catch { /* fall through */ }
        return "Other";
    }

    private void DrawFightCategoryPage(string category)
    {
        var fights = C.Fights.Where(f => CategoryOf(f) == category).ToList();

        SeparatorText($"{category}: {fights.Count} fight{(fights.Count == 1 ? "" : "s")}");
        DrawCategoryToolbar(category);
        ImGui.Spacing();

        if (fights.Count == 0)
        {
            ImGui.TextDisabled("No fights here yet. Add one above, or load a preset.");
            return;
        }

        // Group by expansion, newest first (unknown zones sink to the bottom).
        fights = fights
            .OrderByDescending(f => ExpansionOf(f) == uint.MaxValue ? -1L : ExpansionOf(f))
            .ToList();
        var lastEx = uint.MaxValue - 1; // sentinel that matches no real value

        FightProfile? toDelete = null;
        for (int i = 0; i < fights.Count; i++)
        {
            var fight = fights[i];
            var ex = ExpansionOf(fight);
            if (ex != lastEx)
            {
                lastEx = ex;
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(0.55f, 0.75f, 0.98f, 1f),
                    ex == uint.MaxValue ? "Other" : ExpansionName(ex));
                ImGui.Spacing();
            }

            ImGui.PushID(fight.Id);

            // Drag handle: reorder fights within their expansion group. The list
            // is drawn from a stable sort of C.Fights, so swapping two same-group
            // fights in C.Fights is all it takes - the display and save follow.
            DrawReorderGrip(fights, i);
            ImGui.SameLine();

            // Enable toggle + an expandable dropdown per fight.
            var enabled = fight.Enabled;
            if (GreenCheckbox("##en", ref enabled)) { fight.Enabled = enabled; C.Save(); }
            ImGui.SameLine();

            if (fight.Id == _expandFightId) { ImGui.SetNextItemOpen(true); _expandFightId = ""; }
            // Gold star after the name = official: ships with the plugin, baked
            // from the community sheet. Icon font, since the text font has no star.
            var official = Builtin.Has(fight.TerritoryId);
            var headerStartX = ImGui.GetCursorPosX();
            var headerLabel = fight.Name;
            var open = ImGui.CollapsingHeader($"{headerLabel}###fh-{fight.Id}");
            // The star tooltip and the sheet button are drawn ON TOP of this
            // header row; without allow-overlap the header claims the mouse
            // first and they can never be hovered or clicked.
            ImGui.SetItemAllowOverlap();
            // A framed tree node indents its label one extra FramePadding.X
            // beyond GetTreeNodeToLabelSpacing().
            ImGui.SameLine(headerStartX + ImGui.GetTreeNodeToLabelSpacing()
                + ImGui.GetStyle().FramePadding.X + ImGui.CalcTextSize(headerLabel).X + 8f);
            ImGui.AlignTextToFramePadding();
            using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            {
                if (!official) ImGui.SetWindowFontScale(0.8f);
                ImGui.TextColored(official ? GoldStar : UserBlue,
                    (official ? FontAwesomeIcon.Star : FontAwesomeIcon.User).ToIconString());
                if (!official) ImGui.SetWindowFontScale(1f);
            }
            // The tooltip lives on the symbol, not the whole header: sweeping
            // the fight list stays silent, hovering the symbol explains it.
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(official ? "Official sheet." : "User created.");
            // Quick jump into Sheet View for any fight that has a sheet.
            if (Builtin.Has(fight.TerritoryId) || fight.CustomSlots.Count > 0)
            {
                ImGui.SameLine(ImGui.GetContentRegionMax().X - 28f);
                using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                {
                    if (ImGui.SmallButton(FontAwesomeIcon.Table.ToIconString() + "##opensheet"))
                        _plugin.SheetViewWindow.Open(fight);
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open in Sheet View");
            }

            if (open)
            {
                ImGui.Indent(10f);
                _selectedFight = C.Fights.IndexOf(fight); // drives the per-line options popup
                if (!DrawFightEditor(fight))
                {
                    toDelete = fight;
                }
                else
                {
                    if (Builtin.Has(fight.TerritoryId)) DrawBuiltinLoad(fight);
                    else if (fight.CustomSlots.Count > 0) DrawCustomColumnRow(fight);
                    DrawFightOffsetRow(fight);
                    DrawPracticeRow(fight);
                    // Optional add-ons live behind one fold, so an expanded fight
                    // reads as offset + line table by default.
                    var job = _plugin.ActiveJobAbbreviation();
                    var hasExtras = PotionTimings.BossSlug(fight.TerritoryId) != null
                        || (fight.CustomSlots.Count > 0 && fight.CustomRows.Count > 0)
                        || (!string.IsNullOrEmpty(job) && JobExtras.AllFor(fight, job).Count > 0)
                        || (TankMits.Has(fight.TerritoryId) && IsTankSlot(fight.Slot));
                    if (hasExtras && Section("Extras: potions, job mits, tank busters", false))
                    {
                        DrawPotionsSection(fight);
                        DrawJobExtrasSection(fight);
                        DrawTankSection(fight);
                    }
                    ImGui.Separator();
                    DrawLineTable(fight);
                    ImGui.Spacing();
                    DrawImportSection(fight);
                    DrawAdvancedFightSettings(fight);
                }
                ImGui.Unindent(10f);
            }

            ImGui.PopID();
        }

        if (toDelete != null) { C.Fights.Remove(toDelete); C.Save(); }
    }

    // A small grip you drag up/down to reorder a fight within its expansion
    // group. Only same-group neighbours swap: crossing a group line does
    // nothing, since the group header sort would just snap it back.
    private void DrawReorderGrip(List<FightProfile> shown, int i)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, 0u);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0x22FFFFFFu);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0x33FFFFFFu);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Muted);
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            ImGui.Button(FontAwesomeIcon.GripVertical.ToIconString() + "##grip",
                new Vector2(18f, ImGui.GetFrameHeight()));
        ImGui.PopStyleColor(4);

        var held = ImGui.IsItemActive();
        if (held || ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNs);
        if (ImGui.IsItemHovered() && !held) ImGui.SetTooltip("Drag to reorder");

        if (!held) return;
        var dy = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left).Y;
        // Wait for a real drag past half a row before swapping, so a click or
        // tiny wobble on the grip never nudges the order.
        if (MathF.Abs(dy) < ImGui.GetFrameHeightWithSpacing() * 0.5f) return;

        var j = i + (dy < 0 ? -1 : 1);
        if (j < 0 || j >= shown.Count) return;
        if (ExpansionOf(shown[j]) != ExpansionOf(shown[i])) return;

        var a = C.Fights.IndexOf(shown[i]);
        var b = C.Fights.IndexOf(shown[j]);
        if (a < 0 || b < 0) return;
        (C.Fights[a], C.Fights[b]) = (C.Fights[b], C.Fights[a]);
        (shown[i], shown[j]) = (shown[j], shown[i]); // keep this frame's list in step
        ImGui.ResetMouseDragDelta();
        C.Save();
    }

    // One menu instead of a button row that grows every tier: blank fight,
    // paste a code, and any not-yet-added official sheets for this category.
    private void DrawCategoryToolbar(string category)
    {
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, "Add fight"))
            ImGui.OpenPopup("##addfight");
        if (!ImGui.BeginPopup("##addfight")) return;

        // A blank fight in an official-sheet zone would be a locked, never-firing
        // duplicate of the built-in (ActiveFight takes the first match), so the
        // item goes disabled there.
        var zone = Service.ClientState.TerritoryType;
        if (Builtin.Has(zone))
            ImGui.MenuItem("New blank fight (this zone has an official sheet)", false);
        else if (ImGui.MenuItem("New blank fight (this zone)"))
            AddFight(new FightProfile
            {
                Name = "New fight",
                TerritoryId = zone,
                Category = category,
            });
        if (ImGui.MenuItem("Paste fight code from clipboard")) ImportFightFromClipboard();

        var presets = Builtin.Fights
            .Where(f => f.Category == category && C.Fights.All(x => x.TerritoryId != f.Territory))
            .ToList();
        if (presets.Count > 0)
        {
            ImGui.Separator();
            ImGui.TextDisabled("Official sheets");
            foreach (var (territory, name, cat) in presets)
                if (ImGui.MenuItem(name))
                    AddFight(new FightProfile { Name = name, TerritoryId = territory, Category = cat });
        }
        ImGui.EndPopup();
    }

    // Adds a fight and auto-expands its dropdown.
    private void AddFight(FightProfile fight)
    {
        C.Fights.Add(fight);
        _selectedFight = C.Fights.Count - 1;
        _expandFightId = fight.Id;
        C.Save();
    }

    private int _builtinSlot;

    // "PhysicalRanged" -> "Phys Ranged" for the role headers.
    private static string RoleLabel(JobRole role) => role switch
    {
        JobRole.PhysicalRanged => "Phys Ranged",
        _ => role.ToString(),
    };

    // Friendly names for the raw sheet-slot codes shown in the slot picker.
    private static string SlotLabel(string code) => SlotNames.Canon(code) switch
    {
        "M1" => "Melee 1",
        "M2" => "Melee 2",
        "R1" => "Phys Ranged",
        "R2" => "Caster",
        "T1" => "Main Tank",
        "T2" => "Off Tank",
        var c => c,
    };

    private string _builtinMsg = "";
    private DateTime _builtinMsgAt = DateTime.MinValue;

    // True if your current lines differ from a fresh bake of this slot (added,
    // removed, or a changed action) — i.e. a Replace would throw away your work.
    private bool HasBuiltinEdits(FightProfile fight, string slot)
    {
        if (fight.Lines.Count == 0) return false;
        var baked = Builtin.BuildLines(fight.TerritoryId, slot);
        if (fight.Lines.Count != baked.Count) return true;
        foreach (var b in baked)
        {
            var m = fight.Lines.FirstOrDefault(l => Builtin.SameCall(l, b));
            if (m == null) return true;
            if (!string.Equals((m.Action ?? "").Trim(), (b.Action ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // Switch the active slot and load only that slot's mits (keeping its own edits).
    private void SelectBuiltinSlot(FightProfile fight, string slot)
    {
        Builtin.ApplySlot(fight, slot);
        C.DmuSlot = fight.Slot;
        C.Save();
        FlashBuiltin($"Loaded {SlotLabel(fight.Slot)} mits.");
    }

    private void ResetBuiltinSlot(FightProfile fight, string slot)
    {
        Builtin.ResetSlot(fight, slot);
        C.DmuSlot = fight.Slot;
        C.Save();
        FlashBuiltin($"Reset {SlotLabel(slot)} to the baked sheet.");
    }

    private void FlashBuiltin(string msg) { _builtinMsg = msg; _builtinMsgAt = DateTime.Now; }

    private void DrawBuiltinLoad(FightProfile fight)
    {
        var slots = Builtin.Slots(fight.TerritoryId);

        // Reflect the fight's active slot in the picker. When this fight has no
        // valid slot yet (fresh profile / removed legacy slot), fall back to the
        // first slot rather than whatever index the LAST fight's picker used;
        // otherwise "Reset to sheet" would bake this fight onto that stale slot.
        var savedIdx = Array.IndexOf(slots, fight.Slot);
        _builtinSlot = savedIdx >= 0 ? savedIdx : 0;
        _builtinSlot = Math.Clamp(_builtinSlot, 0, slots.Length - 1);

        var slotLabels = slots.Select(SlotLabel).ToArray();
        ImGui.SetNextItemWidth(170f);
        if (ImGui.Combo("Your slot", ref _builtinSlot, slotLabels, slotLabels.Length))
            SelectBuiltinSlot(fight, slots[_builtinSlot]);  // load that slot now
        Tip("Pick your slot and its mits load automatically (and again when you enter the zone). Each slot keeps its own edits; tanks pick a tank slot, healers their job, DPS their role slot.");
        var slot = slots[_builtinSlot];

        ImGui.SameLine();
        if (ImGui.SmallButton("Reset to sheet"))
        {
            if (HasBuiltinEdits(fight, slot)) ImGui.OpenPopup("##confirm-replace");
            else ResetBuiltinSlot(fight, slot);
        }
        Tip("Reloads this slot from the baked sheet, discarding only this slot's edits.");

        ImGui.SameLine();
        if (ImGui.SmallButton("Reset all columns")) ImGui.OpenPopup("##confirm-resetall");
        Tip("Reloads EVERY column from the baked sheet: all slots' edits and deletions go, "
            + "including added potion, job and tank lines. A snapshot is saved first, so "
            + "Sheet View > Plan > History can restore the old plan.");

        if ((DateTime.Now - _builtinMsgAt).TotalSeconds < 4 && _builtinMsg.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudYellow, _builtinMsg);
        }

        DrawReplaceConfirm(fight, slot);
        DrawResetAllConfirm(fight, slot);
    }

    // Full reset across every column, for when single-slot resets aren't enough
    // (stale edits living in OTHER slots' preview columns). Snapshot-first and
    // confirmed, so it's safe to reach for.
    private void DrawResetAllConfirm(FightProfile fight, string slot)
    {
        var open = true;
        if (!ImGui.BeginPopupModal("##confirm-resetall", ref open,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            return;

        ImGui.TextUnformatted("Reset every column to the baked sheet?");
        ImGui.TextColored(ImGuiColors.DalamudYellow, "All slots' edits and deletions go, including added potion, job and tank lines.");
        ImGui.TextDisabled("A snapshot is saved first; Sheet View > Plan > History restores it.");
        ImGui.Spacing();

        if (ImGui.Button("Cancel", new Vector2(120, 0))) ImGui.CloseCurrentPopup();
        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF1E40C0);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF2046D0);
        if (ImGui.Button("Reset every column", new Vector2(180, 0)))
        {
            _plugin.SnapshotPlan(fight, "before Reset all columns");
            fight.SavedSlots.Clear();
            fight.DeletedCalls.Clear();
            Builtin.ResetSlot(fight, slot);
            C.DmuSlot = fight.Slot;
            C.Save();
            FlashBuiltin("Every column reset to the baked sheet. History restores the old plan.");
            ImGui.CloseCurrentPopup();
        }
        ImGui.PopStyleColor(2);
        ImGui.EndPopup();
    }

    private void DrawReplaceConfirm(FightProfile fight, string slot)
    {
        var open = true;
        if (!ImGui.BeginPopupModal("##confirm-replace", ref open,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            return;

        ImGui.TextUnformatted($"You've customized the {SlotLabel(slot)} slot.");
        ImGui.TextDisabled("Resetting will discard this slot's changes and load the baked sheet fresh.");
        ImGui.Separator();

        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF1E40C0);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF2046D0);
        if (ImGui.Button("Reset and lose my edits", new Vector2(220, 0)))
        {
            ResetBuiltinSlot(fight, slot);
            ImGui.CloseCurrentPopup();
        }
        ImGui.PopStyleColor(2);
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(100, 0)))
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    // The expanded editor for one fight. Returns false if it was deleted this
    // frame (caller removes it and stops drawing). Category lives in the sidebar
    // now; the rare zone/timing fields are tucked into an Advanced sub-section.
    // The fight-wide offset, up top where it's findable: shifts EVERY call. The
    // per-line ±s column below handles individual calls.
    private void DrawFightOffsetRow(FightProfile fight)
    {
        var offset = fight.TimerOffset;
        ImGui.SetNextItemWidth(110f);
        if (ImGui.InputFloat("Timer offset (s)", ref offset, 0.1f, 1f, "%.1f"))
        {
            fight.TimerOffset = Math.Clamp(offset, -30f, 30f);
            C.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("+ fires every call earlier, - later. Survives resync.");
        HelpMarker("Shifts when this fight's calls fire: +10 makes every call come 10s sooner, "
                   + "even with resync on. For one call only, use the ±s column in the line table. "
                   + "Heads up: a big + shift can swallow calls timed inside the first seconds of a "
                   + "pull. The timer auto-starts on combat and resets on a wipe / duty end.");
    }

    // Set when a zone edit is refused (official-sheet zone); shows a warning
    // line under the territory controls for a few seconds.
    private double _zoneRejectUntil;

    // The canonical profile for a built-in zone: first in the list, like
    // ActiveFight resolves. A stray DUPLICATE on a built-in zone (old configs
    // could produce one) stays a normal editable fight so it can be deleted.
    private bool IsOfficial(FightProfile f)
        => Builtin.Has(f.TerritoryId)
           && ReferenceEquals(C.Fights.FirstOrDefault(x => x.TerritoryId == f.TerritoryId), f);

    private bool DrawFightEditor(FightProfile fight)
    {
        // Built-in fights (the ones shipped with the plugin) are locked: their name
        // can't be edited and they can't be deleted. Only user-added fights can.
        if (IsOfficial(fight))
        {
            ImGui.AlignTextToFramePadding();
            using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                ImGui.TextColored(GoldStar, FontAwesomeIcon.Star.ToIconString());
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Official sheet.");
            ImGui.SameLine(0, 5);
            ImGui.TextUnformatted(fight.Name);
            ImGui.SameLine(0, 8);
            ImGui.TextDisabled("(official sheet)");
            Tip("Line times are seconds from the pull, one continuous timeline across every phase; resets on a wipe.");
            return true;
        }

        var name = fight.Name;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.InputText("Name", ref name, 128)) { fight.Name = name; C.Save(); }
        Tip("Line times are seconds from the pull, one continuous timeline across every phase; resets on a wipe.");

        var ci = Array.IndexOf(FightTypes, fight.Category);
        if (ci < 0) ci = FightTypes.Length - 1;
        ImGui.SetNextItemWidth(120f);
        if (ImGui.Combo("Type", ref ci, FightTypes, FightTypes.Length))
        {
            fight.Category = FightTypes[ci];
            C.Save();
        }
        Tip("Ultimate / Savage / Extreme: which sidebar group this fight files under.");

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF2A2AB0);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF3A3AC8);
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.TrashAlt, "Delete"))
            ImGui.OpenPopup("##delfight");
        ImGui.PopStyleColor(2);
        return !DrawDeleteFightConfirm(fight);
    }

    // Deleting a fight is the most destructive click in the plugin (a custom
    // sheet can be hours of work), so it confirms and snapshots first.
    private bool DrawDeleteFightConfirm(FightProfile fight)
    {
        var open = true;
        if (!ImGui.BeginPopupModal("##delfight", ref open,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            return false;

        ImGui.TextUnformatted($"Delete \"{fight.Name}\"?");
        ImGui.TextColored(ImGuiColors.DalamudYellow, "Every slot's plan, notes and anchors go with it.");
        ImGui.TextDisabled("A snapshot is saved first. To recover later: recreate a sheet in the");
        ImGui.TextDisabled("same duty, then History > Find this duty's older snapshots.");
        ImGui.Spacing();

        var confirmed = false;
        if (ImGui.Button("Cancel", new Vector2(120, 0))) ImGui.CloseCurrentPopup();
        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF2222C8);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF3333DD);
        if (ImGui.Button("Delete", new Vector2(120, 0)))
        {
            _plugin.SnapshotPlan(fight, "before delete");
            confirmed = true;
            ImGui.CloseCurrentPopup();
        }
        ImGui.PopStyleColor(2);
        ImGui.EndPopup();
        return confirmed;
    }

    // Fetch potion timings for the current job from top logs, then (only if you
    // click Add) drop them in as lines for that job. Never automatic.
    // Tank slots across every fight's slot list (MT/OT, or FRU's T1/T2).
    private static readonly string[] TankSlots = { "MT", "OT", "T1", "T2" };
    private static bool IsTankSlot(string? slot)
        => slot != null && TankSlots.Contains(slot, StringComparer.OrdinalIgnoreCase);

    // Tank-buster mit plan from the Ikuya sheet: pick your pairing, add your job's
    // lines. Shown only for fights that have tank-combo data AND when you're set to
    // a tank slot (MT/OT/T1/T2) — it's irrelevant on any other role.
    private void DrawTankSection(FightProfile fight)
    {
        if (!TankMits.Has(fight.TerritoryId)) return;
        if (!IsTankSlot(fight.Slot)) return;
        // Check BEFORE BeginCard: returning between Begin/EndCard would leak the
        // draw-list channel split + indent and corrupt the next card this frame.
        var comps = TankMits.Comps(fight.TerritoryId);
        if (comps.Length == 0) return;

        BeginCard(FontAwesomeIcon.ShieldAlt, ImGuiColors.TankBlue, "Tank busters", "from Ikuya");
        ImGui.TextDisabled("Pick your tank pairing, then add your job's tank-buster mit plan. Re-adding replaces it.");
        // The pick is stored on the fight profile so it's remembered per fight
        // across sessions (and per character config).
        var tankComp = Array.IndexOf(comps, fight.TankPairing);
        if (tankComp < 0) tankComp = 0;
        ImGui.SetNextItemWidth(140f);
        if (ImGui.Combo("Tank pairing", ref tankComp, comps, comps.Length))
        {
            fight.TankPairing = comps[tankComp];
            C.Save();
        }

        var comp = comps[tankComp];
        var myJob = _plugin.ActiveJobAbbreviation();
        foreach (var j in TankMits.Jobs(comp))
        {
            var entries = TankMits.For(fight.TerritoryId, comp, j);
            ImGui.SameLine();
            var label = j == myJob ? $"Add {j} (yours)" : $"Add {j}";
            if (ImGui.Button($"{label}##tank{j}"))
            {
                var merged = new List<MitLine>(fight.Lines);
                // Replace any existing tank lines for this job, then add fresh.
                merged.RemoveAll(l => l.Mechanic.StartsWith("Tank:", StringComparison.Ordinal)
                                      && l.Jobs.Contains(j, StringComparer.OrdinalIgnoreCase));
                foreach (var e in entries)
                    merged.Add(new MitLine
                    {
                        Time = e.Time,
                        Mechanic = $"Tank: {e.Mechanic}",
                        Action = e.Action,
                        Jobs = new List<string> { j },
                        Enabled = true,
                        Custom = true,
                    });
                SetFightLines(fight, merged.OrderBy(l => l.Time).ToList());
                FlashBuiltin($"Added {entries.Length} {j} tank-buster line(s).");
            }
        }
        ImGui.TextDisabled("Lines are tagged to the job, so they only show when you're on it.");
        EndCard();
    }

    private Vector2 _cardTopLeft;
    private float _cardWidth;

    // Begin an auto-height styled card: a panel background + left accent bar + an
    // icon title, drawn behind the content via draw-list channels so the panel fits
    // whatever's inside. Every BeginCard must be paired with EndCard.
    private void BeginCard(FontAwesomeIcon icon, Vector4 iconColor, string title, string subtitle = "")
    {
        ImGui.Spacing();
        _cardTopLeft = ImGui.GetCursorScreenPos();
        _cardWidth = ImGui.GetContentRegionAvail().X;

        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1); // content on the foreground channel

        ImGui.Indent(12f);
        ImGui.Dummy(new Vector2(0, 6));
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            ImGui.TextColored(iconColor, icon.ToIconString());
        ImGui.SameLine(0, 8);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(title);
        if (!string.IsNullOrEmpty(subtitle))
        {
            ImGui.SameLine(0, 10);
            ImGui.TextColored(new Vector4(0.55f, 0.59f, 0.66f, 1f), subtitle);
        }
        ImGui.Spacing();
    }

    private void EndCard()
    {
        ImGui.Dummy(new Vector2(0, 8));
        ImGui.Unindent(12f);

        var dl = ImGui.GetWindowDrawList();
        var min = _cardTopLeft;
        var max = new Vector2(_cardTopLeft.X + _cardWidth, ImGui.GetCursorScreenPos().Y);
        dl.ChannelsSetCurrent(0); // background channel
        dl.AddRectFilled(min, max, Theme.PanelBg, 8f);
        dl.AddRectFilled(min + new Vector2(0, 8), new Vector2(min.X + 3, max.Y - 8), Theme.Accent);
        dl.ChannelsMerge();
        ImGui.Spacing();
    }

    // A rounded "pill" showing one potion window (mm:ss). Returns its right edge X
    // so the caller can flow the next one after it.
    private static void TimePill(string text)
    {
        var dl = ImGui.GetWindowDrawList();
        var pad = new Vector2(8, 3);
        var sz = ImGui.CalcTextSize(text);
        var p = ImGui.GetCursorScreenPos();
        var box = sz + pad * 2;
        dl.AddRectFilled(p, p + box, 0xFF2A2017, 6f);
        dl.AddRect(p, p + box, Theme.Accent, 6f);
        dl.AddText(p + pad, 0xFFECE8E6, text);
        ImGui.Dummy(box);
    }

    private static string Mmss(float t) => $"{(int)t / 60}:{(int)t % 60:00}";

    // Custom sheets: the same "Your slot" row the built-in fights get, so a
    // custom fight reads exactly like an official one on its page.
    private void DrawCustomColumnRow(FightProfile fight)
    {
        var slots = fight.CustomSlots.ToArray();
        if (slots.Length == 0) return;
        var idx = Array.FindIndex(slots, s => string.Equals(s, fight.Slot, StringComparison.OrdinalIgnoreCase));

        ImGui.SetNextItemWidth(170f);
        // idx -1 (no column picked yet) shows an empty preview until they pick.
        if (ImGui.Combo("Your slot##customslot", ref idx, slots, slots.Length)
            && idx >= 0 && !string.Equals(slots[idx], fight.Slot, StringComparison.OrdinalIgnoreCase))
        {
            // SetSlot parks the old column's lines and gives a never-picked
            // column a FRESH list; assigning fight.Lines here instead would
            // alias two columns to one list.
            _plugin.SetSlot(fight, slots[idx]);
            _plugin.SheetViewWindow.MarkPlanDirty();
        }
        Tip("Which column of this sheet is YOURS; that column's lines are what the overlay calls.");
        var slot = idx >= 0 ? slots[idx] : slots[0];

        ImGui.SameLine();
        if (ImGui.SmallButton("Reset this column")) ImGui.OpenPopup("##confirm-customreset");
        Tip("Empties this column's mits. The rows, grades and notes stay; a snapshot is saved first.");

        ImGui.SameLine();
        if (ImGui.SmallButton("Reset all columns")) ImGui.OpenPopup("##confirm-customresetall");
        Tip("Empties EVERY column's mits; rows, grades and notes stay. A snapshot is saved first, "
            + "so Sheet View > Plan > History can restore the old plan.");

        if ((DateTime.Now - _builtinMsgAt).TotalSeconds < 4 && _builtinMsg.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudYellow, _builtinMsg);
        }

        DrawCustomResetConfirm(fight, slot);
        DrawCustomResetAllConfirm(fight);
    }

    private void ClearCustomColumn(FightProfile fight, string slot)
    {
        // Clear IN PLACE: Sheet View and SavedSlots share these list objects.
        if (string.Equals(slot, fight.Slot, StringComparison.OrdinalIgnoreCase)) fight.Lines.Clear();
        if (fight.SavedSlots.TryGetValue(slot, out var saved)) saved.Clear();
    }

    private void DrawCustomResetConfirm(FightProfile fight, string slot)
    {
        var open = true;
        if (!ImGui.BeginPopupModal("##confirm-customreset", ref open,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            return;

        ImGui.TextUnformatted($"Empty the {slot} column?");
        ImGui.TextColored(ImGuiColors.DalamudYellow, "Its mits go; the sheet's rows, grades and notes stay.");
        ImGui.TextDisabled("A snapshot is saved first; Sheet View > Plan > History restores it.");
        ImGui.Spacing();

        if (ImGui.Button("Cancel", new Vector2(120, 0))) ImGui.CloseCurrentPopup();
        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF1E40C0);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF2046D0);
        if (ImGui.Button("Empty this column", new Vector2(160, 0)))
        {
            _plugin.SnapshotPlan(fight, $"before reset {slot}");
            ClearCustomColumn(fight, slot);
            C.Save();
            _plugin.SheetViewWindow.MarkPlanDirty();
            FlashBuiltin($"{slot} emptied. History restores the old plan.");
            ImGui.CloseCurrentPopup();
        }
        ImGui.PopStyleColor(2);
        ImGui.EndPopup();
    }

    private void DrawCustomResetAllConfirm(FightProfile fight)
    {
        var open = true;
        if (!ImGui.BeginPopupModal("##confirm-customresetall", ref open,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            return;

        ImGui.TextUnformatted("Empty every column of this sheet?");
        ImGui.TextColored(ImGuiColors.DalamudYellow, "All columns' mits go; the rows, grades and notes stay.");
        ImGui.TextDisabled("A snapshot is saved first; Sheet View > Plan > History restores it.");
        ImGui.Spacing();

        if (ImGui.Button("Cancel", new Vector2(120, 0))) ImGui.CloseCurrentPopup();
        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF1E40C0);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF2046D0);
        if (ImGui.Button("Empty every column", new Vector2(170, 0)))
        {
            _plugin.SnapshotPlan(fight, "before reset all columns");
            fight.Lines.Clear();
            foreach (var saved in fight.SavedSlots.Values) saved.Clear();
            C.Save();
            _plugin.SheetViewWindow.MarkPlanDirty();
            FlashBuiltin("Every column emptied. History restores the old plan.");
            ImGui.CloseCurrentPopup();
        }
        ImGui.PopStyleColor(2);
        ImGui.EndPopup();
    }

    private int _pracRowIdx;

    // Practice, contextual: one row of phase-jump buttons inside the fight it
    // belongs to (the old Practice page, dissolved). Custom sheets have no baked
    // phases, so they practice from any of their own rows instead.
    private void DrawPracticeRow(FightProfile fight)
    {
        var phases = Builtin.PhaseStarts(fight.TerritoryId);
        if (phases.Count == 0)
        {
            var rows = fight.CustomRows.OrderBy(r => r.Time).ToList();
            if (rows.Count == 0) return;
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled("Practice:");
            Tip("Jump the overlay to a row to preview and place its calls; no pull needed.\nPicking a row turns on Test Mode; Stop (or a real pull) ends it.");
            _pracRowIdx = Math.Clamp(_pracRowIdx, 0, rows.Count - 1);
            var labels = rows.Select(r => $"{Mmss(r.Time)}  {r.Mechanic}").ToArray();
            ImGui.SameLine(0, 6);
            ImGui.SetNextItemWidth(240f);
            ImGui.Combo("##pracrow", ref _pracRowIdx, labels, labels.Length);
            ImGui.SameLine(0, 4);
            if (ImGui.SmallButton("Go##pracrow")) _plugin.PracticeJump(fight, rows[_pracRowIdx].Time);
            if (Plugin.PreviewFight == fight && C.TestMode)
            {
                ImGui.SameLine(0, 8);
                if (ImGui.SmallButton("Stop##pracrow")) _plugin.StopPractice();
            }
            return;
        }

        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Practice:");
        Tip("Jump the overlay to a phase to preview and place its calls; no pull needed.\nPicking a phase turns on Test Mode; Stop (or a real pull) ends it.");
        for (var i = 0; i < phases.Count; i++)
        {
            ImGui.SameLine(0, 4);
            if (ImGui.SmallButton($"{phases[i].Name}##prac{i}"))
                _plugin.PracticeJump(fight, phases[i].Time);
            Tip($"Preview from {(int)phases[i].Time / 60}:{(int)phases[i].Time % 60:00} (~6s before the first call).");
        }
        if (Plugin.PreviewFight == fight && C.TestMode)
        {
            ImGui.SameLine(0, 8);
            if (ImGui.SmallButton("Stop##prac")) _plugin.StopPractice();
            ImGui.SameLine(0, 6);
            ImGui.TextColored(ImGuiColors.DalamudYellow, "previewing");
        }
    }

    // Potions card: baked top-log potion windows for your job with a one-click
    // add. Custom sheets get the standard 2-minute burst meta instead: pot the
    // opener, re-pot each 6:00 burst that fits the fight.
    private void DrawPotionsSection(FightProfile fight)
    {
        var customPots = PotionTimings.BossSlug(fight.TerritoryId) == null
            && fight.CustomSlots.Count > 0 && fight.CustomRows.Count > 0;
        if (PotionTimings.BossSlug(fight.TerritoryId) == null && !customPots) return;

        var job = _plugin.ActiveJobAbbreviation();
        var stat = PotionTimings.Stat(job);

        BeginCard(FontAwesomeIcon.Flask, ImGuiColors.DalamudViolet, "Potions",
            customPots ? "2-minute burst meta" : "top-log windows");

        if (string.IsNullOrEmpty(job) || string.IsNullOrEmpty(stat))
        {
            ImGui.TextDisabled("Pick your job (top of the sidebar) to see its potion timings.");
            EndCard();
            return;
        }

        var times = customPots
            ? PotionTimings.GenericWindows(fight.CustomRows.Max(r => r.Time))
            : PotionTimings.DefaultsFor(fight.TerritoryId, job);

        // Window pills.
        ImGui.TextColored(new Vector4(0.62f, 0.66f, 0.72f, 1f), $"{job} · {stat}");
        if (times.Count == 0) { ImGui.SameLine(0, 10); ImGui.TextDisabled("no windows"); }
        foreach (var t in times)
        {
            ImGui.SameLine(0, 6);
            TimePill(Mmss(t));
        }

        // Add to the timeline.
        ImGui.Spacing();
        ImGui.BeginDisabled(times.Count == 0);
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Accent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentHover);
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, $"Add {times.Count} potion line(s)"))
        {
            var lines = new List<MitLine>(fight.Lines);
            lines.RemoveAll(l => l.Mechanic.StartsWith("Potion", StringComparison.Ordinal)
                                 && l.Jobs.Contains(job, StringComparer.OrdinalIgnoreCase));
            foreach (var t in times)
                lines.Add(new MitLine
                {
                    Time = t,
                    Mechanic = $"Potion ({stat})",
                    Action = "Potion",
                    Jobs = new List<string> { job },
                    Enabled = true,
                    Custom = true,
                });
            SetFightLines(fight, lines.OrderBy(l => l.Time).ToList());
            FlashBuiltin($"Added {times.Count} {job} potion line(s).");
        }
        ImGui.PopStyleColor(2);
        ImGui.EndDisabled();
        Tip("Adds these as job-tagged lines (replacing any existing potion lines for this job), so they only show when you're on it.");

        if ((DateTime.Now - _builtinMsgAt).TotalSeconds < 4 && _builtinMsg.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.ParsedGreen, _builtinMsg);
        }
        EndCard();
    }

    // Job-mitigation card: optional job-specific mit timers from logs (Asylum-style)
    // — e.g. BRD Nature's Minne, MNK Mantra, PLD Passage of Arms. Shows only when
    // you're on a job that has one for this fight. One-click add, job-tagged + Custom.
    private void DrawJobExtrasSection(FightProfile fight)
    {
        var job = _plugin.ActiveJobAbbreviation();
        if (string.IsNullOrEmpty(job)) return; // also lets the compiler see job is non-null below
        // Baked schedule(s) for built-ins; for a custom sheet, computed from its
        // own rows (hardest-graded hits first). A job may offer several (e.g.
        // DNC's Curing Waltz + Improvisation). Optional either way, exactly like
        // the Ikuya sheets' Extras column.
        var extras = JobExtras.AllFor(fight, job);
        if (extras.Count == 0) return;
        var custom = JobExtras.For(fight.TerritoryId, job) == null; // no baked zone schedule -> from the sheet

        BeginCard(FontAwesomeIcon.Shield, ImGuiColors.HealerGreen, "Job mitigation", "optional");
        if (custom)
            ImGui.TextDisabled("Spots picked from this sheet's rows, hardest-graded hits first.");

        foreach (var extra in extras)
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.62f, 0.66f, 0.72f, 1f), $"{job} · {extra.Action}");
            ImGui.SameLine(0, 10);
            ImGui.TextDisabled($"{extra.Lines.Length} casts, spaced to its {extra.Recast:0}s recast");

            ImGui.PushStyleColor(ImGuiCol.Button, Theme.Accent);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentHover);
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, $"Add {extra.Lines.Length} {extra.Action} line(s)"))
            {
                var lines = new List<MitLine>(fight.Lines);
                lines.RemoveAll(l => string.Equals(l.Action, extra.Action, StringComparison.OrdinalIgnoreCase)
                                     && l.Jobs.Contains(job, StringComparer.OrdinalIgnoreCase));
                foreach (var (time, mech) in extra.Lines)
                    lines.Add(new MitLine
                    {
                        Time = time,
                        Mechanic = mech,
                        Action = extra.Action,
                        Jobs = new List<string> { job },
                        Enabled = true,
                        Custom = true,
                    });
                SetFightLines(fight, lines.OrderBy(l => l.Time).ToList());
                FlashBuiltin($"Added {extra.Lines.Length} {job} {extra.Action} line(s).");
            }
            ImGui.PopStyleColor(2);
            Tip($"Adds {extra.Action} as {job}-tagged lines (replacing any existing ones), so they only show on {job}.");
        }

        if ((DateTime.Now - _builtinMsgAt).TotalSeconds < 4 && _builtinMsg.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.ParsedGreen, _builtinMsg);
        }
        EndCard();
    }

    // Rarely-touched share / duplicate actions + zone & timing knobs, behind a
    // collapsing header so the editor opens lean.
    private void DrawAdvancedFightSettings(FightProfile fight)
    {
        if (!Section("Manage & advanced")) return;
        ImGui.Indent(10f);

        var locked = IsOfficial(fight);
        if (!locked)  // duplicating a built-in would make a same-zone copy that's then locked
        {
            if (ImGui.Button("Duplicate"))
            {
                AddFight(new FightProfile
                {
                    Name = fight.Name + " copy",
                    TerritoryId = fight.TerritoryId,
                    Category = fight.Category,
                    TimerOffset = fight.TimerOffset,
                    // The copy starts disabled: with both live, the original
                    // would keep winning the zone and edits to the copy would
                    // silently never fire. Enable whichever should be live.
                    Enabled = false,
                    Slot = fight.Slot,
                    Lines = fight.Lines.Select(CloneLine).ToList(),
                    // Deep-copy the rest too: for a custom fight the hand-built
                    // anchors are its most laborious data, and sharing object
                    // references between two profiles would make edits bleed over.
                    SyncPoints = fight.SyncPoints.Select(s => new SyncPoint
                    { Ability = s.Ability, Time = s.Time, IsPhase = s.IsPhase, Label = s.Label }).ToList(),
                    BossAnchors = fight.BossAnchors.Select(b => new BossAnchor
                    { NameId = b.NameId, Time = b.Time, Label = b.Label }).ToList(),
                    DeletedCalls = fight.DeletedCalls.Select(d => new DeletedCall
                    { Slot = d.Slot, Time = d.Time, Mechanic = d.Mechanic, Action = d.Action }).ToList(),
                    SavedSlots = fight.SavedSlots.ToDictionary(
                        kv => kv.Key, kv => kv.Value.Select(CloneLine).ToList()),
                    CustomSlots = fight.CustomSlots.ToList(),
                    CustomRows = fight.CustomRows.Select(cr => new CustomRow
                    { Time = cr.Time, Mechanic = cr.Mechanic }).ToList(),
                });
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The copy starts disabled - enable whichever version should be live.");
            ImGui.SameLine();
        }
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Upload, "Export to clipboard")) ExportFight(fight);
        ImGui.SameLine();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Download, "Import from clipboard")) ImportFightFromClipboard();
        Tip("Share a whole fight (lines included) with a friend via a clipboard code.");

        ImGui.Spacing();
        ImGui.BeginDisabled(locked); // a built-in's zone is fixed
        var territory = (int)fight.TerritoryId;
        ImGui.SetNextItemWidth(120f);
        // Official-sheet zones are refused here: pointing a custom fight at one
        // creates a duplicate that never fires (the built-in wins the zone).
        if (ImGui.InputInt("Territory id", ref territory))
        {
            var target = (uint)Math.Max(0, territory);
            if (Builtin.Has(target) && target != fight.TerritoryId) _zoneRejectUntil = ImGui.GetTime() + 4;
            else { fight.TerritoryId = target; C.Save(); }
        }
        ImGui.SameLine();
        if (ImGui.Button($"Use current zone ({Service.ClientState.TerritoryType})"))
        {
            var target = Service.ClientState.TerritoryType;
            if (Builtin.Has(target) && target != fight.TerritoryId) _zoneRejectUntil = ImGui.GetTime() + 4;
            else { fight.TerritoryId = target; C.Save(); }
        }
        var zoneName = TerritoryName(fight.TerritoryId);
        if (!string.IsNullOrEmpty(zoneName)) { ImGui.SameLine(); ImGui.TextDisabled(zoneName); }
        if (ImGui.GetTime() < _zoneRejectUntil)
            ImGui.TextColored(new Vector4(0.95f, 0.75f, 0.35f, 1f), "That zone already has an official sheet - it can't be assigned to a custom fight.");
        ImGui.EndDisabled();

        ImGui.TextDisabled("Timer offset now lives at the top of this fight, above the mit sections.");

        ImGui.Unindent(10f);
    }

    // Reassign a fight's lines while keeping the active slot's saved copy in sync,
    // so per-slot storage never goes stale after a sort / import.
    private void SetFightLines(FightProfile fight, List<MitLine> lines)
    {
        fight.Lines = lines;
        if (!string.IsNullOrEmpty(fight.Slot))
            fight.SavedSlots[fight.Slot] = lines;
        C.Save();
    }

    private void DrawLineTable(FightProfile fight)
    {
        ImGui.TextUnformatted($"Lines ({fight.Lines.Count})");
        ImGui.SameLine();
        if (ImGui.SmallButton("Add line")) { fight.Lines.Add(new MitLine { Custom = true }); C.Save(); }
        ImGui.SameLine();
        if (ImGui.SmallButton("Sort by time")) SetFightLines(fight, fight.Lines.OrderBy(l => l.Time).ToList());
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Right-click a line's time / mechanic / action cell to copy, paste, duplicate, reorder, or delete it.");

        // Deleted sheet calls are remembered (so updates can't re-add them); show
        // that hidden state and offer the way back.
        var dead = fight.DeletedCalls.Count(d => string.Equals(d.Slot, fight.Slot, StringComparison.OrdinalIgnoreCase));
        if (dead > 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"· {dead} deleted sheet call{(dead == 1 ? "" : "s")}");
            ImGui.SameLine();
            if (ImGui.SmallButton("Restore"))
            {
                fight.DeletedCalls.RemoveAll(d => string.Equals(d.Slot, fight.Slot, StringComparison.OrdinalIgnoreCase));
                var back = Builtin.ApplySlot(fight, fight.Slot);
                C.Save();
                FlashBuiltin($"Restored {back} deleted sheet call{(back == 1 ? "" : "s")}.");
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Bring every deleted sheet call for this slot back from the sheet.");
        }

        // Grow the table to fill what's left, leaving room for the import header
        // underneath, so a freshly loaded sheet isn't cut off.
        var avail = ImGui.GetContentRegionAvail().Y;
        var tableH = MathF.Max(200f, avail - ImGui.GetFrameHeightWithSpacing() - 8f);

        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY;
        if (!ImGui.BeginTable("##lines", 8, flags, new Vector2(0, tableH)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("On", ImGuiTableColumnFlags.WidthFixed, 28);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 70);
        ImGui.TableSetupColumn("±s", ImGuiTableColumnFlags.WidthFixed, 44);
        ImGui.TableSetupColumn("Mechanic", ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableSetupColumn("Jobs", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableSetupColumn("##opt", ImGuiTableColumnFlags.WidthFixed, 28);
        ImGui.TableSetupColumn("##del", ImGuiTableColumnFlags.WidthFixed, 28);
        ImGui.TableHeadersRow();

        MitLine? toDelete = null;
        // Right-click line ops (paste / duplicate / move) mutate the list, so we
        // capture them here and run them after the table loop, never mid-iteration.
        Action? deferred = null;
        for (var i = 0; i < fight.Lines.Count; i++)
        {
            var line = fight.Lines[i];
            ImGui.TableNextRow();
            ImGui.PushID(i);

            ImGui.TableNextColumn();
            // Mit-type colour chip: faint tint on the left cell (party / tank / personal).
            var chip = MitTypes.Color(MitTypes.Classify(line.Action, line.Mechanic), C);
            if (chip != 0)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, (chip & 0x00FFFFFFu) | 0x55000000u, 0);
            var on = line.Enabled;
            if (GreenCheckbox("##on", ref on)) { line.Enabled = on; C.Save(); _plugin.SheetViewWindow.MarkPlanDirty(); }

            ImGui.TableNextColumn();
            // Edit time as m:ss. Use a per-edit buffer so partial typing isn't lost.
            var timeBuf = _editTimeLine == line ? _editTimeBuf : line.TimeText;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##time", ref timeBuf, 12)) _editTimeBuf = timeBuf;
            if (ImGui.IsItemActivated()) { _editTimeLine = line; _editTimeBuf = line.TimeText; }
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                // Commit ONLY if the shared buffer still belongs to this line.
                // Clicking straight into an earlier row's time cell activates that
                // cell first in the frame (overwriting the buffer), so committing
                // unconditionally here would write the OTHER row's time into this
                // line.
                if (_editTimeLine == line && SheetImport.TryParseTime(_editTimeBuf, out var sec)
                    && MathF.Abs(sec - line.Time) > 0.001f)
                {
                    PreserveBakedEdit(fight, line);
                    line.Time = sec;
                    C.Save();
                }
                if (_editTimeLine == line) _editTimeLine = null;
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Type m:ss (e.g. 2:30) or seconds; right-click to reset");
            if (ImGui.BeginPopupContextItem("##timectx"))
            {
                if (DefaultLineFor(fight, line) is { } def)
                {
                    if (ImGui.MenuItem($"Reset time to default ({(int)def.Time / 60}:{(int)def.Time % 60:00})"))
                    {
                        line.Time = def.Time;
                        if (_editTimeLine == line) _editTimeBuf = line.TimeText;
                        C.Save();
                    }
                }
                else
                {
                    ImGui.TextDisabled("No baked default for this line.");
                }
                ImGui.Separator();
                LineContextItems(fight, line, i, ref deferred, ref toDelete);
                ImGui.EndPopup();
            }

            // Per-line offset: + fires just this call earlier. Blank = none.
            ImGui.TableNextColumn();
            if (_editOffLine == line)
            {
                ImGui.SetNextItemWidth(-1);
                if (_offFocusPending) { ImGui.SetKeyboardFocusHere(); _offFocusPending = false; }
                ImGui.InputText("##off", ref _editOffBuf, 8);
                if (ImGui.IsItemDeactivated())
                {
                    if (_editOffLine == line && ImGui.IsItemDeactivatedAfterEdit()
                        && float.TryParse(_editOffBuf, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var ov))
                    {
                        line.OffsetSeconds = Math.Clamp(ov, -30f, 30f);
                        C.Save();
                        _plugin.SheetViewWindow.MarkPlanDirty();
                    }
                    if (_editOffLine == line) _editOffLine = null;
                }
            }
            else
            {
                var offLabel = line.OffsetSeconds == 0f ? " " : line.OffsetSeconds.ToString("+0.#;-0.#");
                if (line.OffsetSeconds != 0f) ImGui.PushStyleColor(ImGuiCol.Text, 0xFF5C9EF5); // orange (ABGR)
                if (ImGui.Selectable(offLabel + "##off", false))
                {
                    CommitPendingOffset();
                    _editOffLine = line;
                    _editOffBuf = _editOffSeed = line.OffsetSeconds == 0f ? ""
                        : line.OffsetSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                    _offFocusPending = true;
                }
                if (line.OffsetSeconds != 0f) ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Offset just this call: + = earlier, - = later. Click to edit."
                        + (line.OffsetSeconds != 0f ? $"\nCurrently {line.OffsetSeconds:+0.#;-0.#}s." : ""));
            }

            ImGui.TableNextColumn();
            var mech = line.Mechanic;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##mech", ref mech, 256))
            {
                PreserveBakedEdit(fight, line); // before the first keystroke lands
                line.Mechanic = mech;
                C.Save();
            }
            if (ImGui.BeginPopupContextItem("##mechctx"))
            {
                var def = DefaultLineFor(fight, line);
                if (def != null && !string.Equals(def.Mechanic.Trim(), line.Mechanic.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    if (ImGui.MenuItem($"Reset mechanic to \"{Ellipsis(def.Mechanic, 40)}\"")) { line.Mechanic = def.Mechanic; C.Save(); }
                }
                else ImGui.TextDisabled(def == null ? "No baked default for this line." : "Already the default.");
                ImGui.Separator();
                LineContextItems(fight, line, i, ref deferred, ref toDelete);
                ImGui.EndPopup();
            }

            ImGui.TableNextColumn();
            var icon = Icons.For(line, _plugin.ActiveJobAbbreviation());
            if (icon != 0)
            {
                var h = ImGui.GetFrameHeight();
                Icons.Draw(icon, new Vector2(h, h));
                ImGui.SameLine(0, 4);
            }
            var action = line.Action;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##action", ref action, 256))
            {
                // Tombstone here too, not just on time/mechanic edits: otherwise
                // editing the ACTION first would leave later tombstones recording
                // the mutated action, which no longer matches the baked original.
                PreserveBakedEdit(fight, line);
                line.Action = action;
                C.Save();
            }
            if (ImGui.BeginPopupContextItem("##actionctx"))
            {
                var def = DefaultLineFor(fight, line);
                if (def != null && !string.Equals(def.Action.Trim(), line.Action.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    if (ImGui.MenuItem($"Reset action to \"{Ellipsis(def.Action, 40)}\"")) { line.Action = def.Action; C.Save(); }
                }
                else ImGui.TextDisabled(def == null ? "No baked default for this line." : "Already the default.");
                ImGui.Separator();
                LineContextItems(fight, line, i, ref deferred, ref toDelete);
                ImGui.EndPopup();
            }

            ImGui.TableNextColumn();
            DrawJobsCell(line);

            ImGui.TableNextColumn();
            if (ImGui.SmallButton("...")) ImGui.OpenPopup("lineopt");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Per-line lead / speech / color / mute");
            DrawLineOptionsPopup(line);

            ImGui.TableNextColumn();
            if (ImGui.SmallButton("X")) toDelete = line;

            ImGui.PopID();
        }

        ImGui.EndTable();

        deferred?.Invoke();
        if (toDelete != null)
        {
            // Sheet-baked lines get a tombstone so the zone-in top-up / slot
            // switches / sheet re-bakes can't resurrect them. Custom lines exist
            // only in the saved lists, so removal alone is final for those.
            if (!toDelete.Custom && Builtin.Has(fight.TerritoryId) && !string.IsNullOrEmpty(fight.Slot))
            {
                fight.DeletedCalls.Add(new DeletedCall
                {
                    Slot = fight.Slot,
                    Time = toDelete.Time,
                    Mechanic = toDelete.Mechanic,
                    Action = toDelete.Action,
                });
                FlashBuiltin("Line deleted. It stays deleted; Restore (above the table) brings it back.");
            }
            fight.Lines.Remove(toDelete);
            // Keep the slot's saved copy in step even right after a config reload,
            // when Lines and SavedSlots hold separate list objects.
            if (!string.IsNullOrEmpty(fight.Slot))
                fight.SavedSlots[fight.Slot] = fight.Lines;
            C.Save();
        }
    }

    // Right-click line menu shared by the time / mechanic / action cells: copy a
    // line to the in-memory clipboard, paste a copy above / below / over this one,
    // duplicate, reorder, or delete. List-mutating actions are deferred so the
    // caller can run them once the row loop finishes.
    private void LineContextItems(FightProfile fight, MitLine line, int index, ref Action? deferred, ref MitLine? toDelete)
    {
        if (ImGui.MenuItem("Copy line")) _copiedLine = CloneLine(line);

        var hasCopy = _copiedLine != null;
        if (ImGui.MenuItem("Paste above", string.Empty, false, hasCopy) && _copiedLine != null)
        {
            var clip = CloneLine(_copiedLine);
            var at = index;
            deferred = () => { fight.Lines.Insert(Math.Clamp(at, 0, fight.Lines.Count), clip); C.Save(); };
        }
        if (ImGui.MenuItem("Paste below", string.Empty, false, hasCopy) && _copiedLine != null)
        {
            var clip = CloneLine(_copiedLine);
            var at = index + 1;
            deferred = () => { fight.Lines.Insert(Math.Clamp(at, 0, fight.Lines.Count), clip); C.Save(); };
        }
        if (ImGui.MenuItem("Paste over this line", string.Empty, false, hasCopy) && _copiedLine != null)
        {
            PreserveBakedEdit(fight, line); // pasting over rewrites time/mechanic
            OverwriteLine(line, _copiedLine);
            _plugin.SheetViewWindow.MarkPlanDirty();
            C.Save();
        }

        ImGui.Separator();
        if (ImGui.MenuItem("Duplicate line"))
        {
            var dup = CloneLine(line);
            var at = index + 1;
            deferred = () => { fight.Lines.Insert(Math.Clamp(at, 0, fight.Lines.Count), dup); C.Save(); };
        }
        if (ImGui.MenuItem("Move up", string.Empty, false, index > 0))
        {
            var at = index;
            deferred = () => { (fight.Lines[at - 1], fight.Lines[at]) = (fight.Lines[at], fight.Lines[at - 1]); C.Save(); };
        }
        if (ImGui.MenuItem("Move down", string.Empty, false, index < fight.Lines.Count - 1))
        {
            var at = index;
            deferred = () => { (fight.Lines[at + 1], fight.Lines[at]) = (fight.Lines[at], fight.Lines[at + 1]); C.Save(); };
        }

        ImGui.Separator();
        if (ImGui.MenuItem("Delete line")) toDelete = line;
    }

    // Editing the time or mechanic of a sheet-baked line breaks its identity with
    // the bake (SameCall keys on time + mechanic), so the next zone-in would
    // re-add the original and the de-overlap would sweep the edited copy,
    // silently reverting the edit. Preserve it the same way delete does: record
    // a tombstone at the ORIGINAL coordinates (call BEFORE mutating the line)
    // and flag the line Custom so it's the user's from here on.
    private static void PreserveBakedEdit(FightProfile fight, MitLine line)
        => Builtin.PreserveEdit(fight, fight.Slot, line);

    // Copy every field of src onto target in place (used by "Paste over").
    private static void OverwriteLine(MitLine target, MitLine src)
    {
        target.Time = src.Time;
        target.Mechanic = src.Mechanic;
        target.Action = src.Action;
        target.Jobs = new List<string>(src.Jobs);
        target.Enabled = src.Enabled;
        target.LeadOverride = src.LeadOverride;
        target.OffsetSeconds = src.OffsetSeconds;
        target.Tts = src.Tts;
        target.Sound = src.Sound;
        target.Color = src.Color;
        target.IconId = src.IconId;
    }

    private void DrawJobsCell(MitLine line)
    {
        var label = line.Jobs.Count == 0 ? "All" : string.Join(",", line.Jobs);
        if (label.Length > 14) label = label[..12] + "...";
        if (ImGui.Button(label + "##jobs", new Vector2(-1, 0)))
            ImGui.OpenPopup("jobspopup");

        if (ImGui.BeginPopup("jobspopup"))
        {
            if (ImGui.Button("All jobs")) { line.Jobs.Clear(); C.Save(); }

            foreach (var role in Enum.GetValues<JobRole>())
            {
                SeparatorText(RoleLabel(role));
                var first = true;
                foreach (var abbr in Jobs.AbbreviationsForRole(role))
                {
                    if (!first) ImGui.SameLine();
                    first = false;
                    var has = line.Jobs.Contains(abbr, StringComparer.OrdinalIgnoreCase);
                    if (GreenCheckbox(abbr, ref has))
                    {
                        if (has && !line.Jobs.Contains(abbr)) line.Jobs.Add(abbr);
                        else line.Jobs.RemoveAll(j => string.Equals(j, abbr, StringComparison.OrdinalIgnoreCase));
                        C.Save();
                    }
                }
                ImGui.SameLine();
                if (ImGui.SmallButton($"+all##{role}"))
                {
                    foreach (var abbr in Jobs.AbbreviationsForRole(role))
                        if (!line.Jobs.Contains(abbr)) line.Jobs.Add(abbr);
                    C.Save();
                }
            }
            ImGui.EndPopup();
        }
    }

    // ---- Import ----------------------------------------------------------

    private void DrawImportSection(FightProfile fight)
    {
        if (!ImGui.CollapsingHeader("Import from a sheet (paste rows)"))
            return;

        ImGui.TextWrapped("Copy rows straight out of Google Sheets / Excel and paste below. "
                          + "Pick which columns hold the time, mechanic, and the action you press. "
                          + "Rows without a readable time (headers, blanks) are skipped.");

        ImGui.InputTextMultiline("##importbuf", ref _importBuffer, 65536, new Vector2(-1, 120));

        if (ImGui.Button("Parse")) _importGrid = SheetImport.ParseGrid(_importBuffer, out _importDelimiter);
        ImGui.SameLine();
        if (ImGui.Button("Clear")) { _importBuffer = ""; _importGrid = null; }

        if (_importGrid == null || _importGrid.Count == 0) return;

        var cols = _importGrid.Max(r => r.Length);
        ImGui.TextDisabled($"Detected {_importGrid.Count} rows, {cols} columns, delimiter = "
                           + (_importDelimiter == '\t' ? "Tab" : "Comma"));

        var colNames = Enumerable.Range(0, cols).Select(i => $"Col {i}{HeaderHint(i)}").ToArray();
        _timeCol = Math.Clamp(_timeCol, 0, cols - 1);
        _mechCol = Math.Clamp(_mechCol, 0, cols - 1);
        _actionCol = Math.Clamp(_actionCol, 0, cols - 1);

        ImGui.SetNextItemWidth(220f);
        ImGui.Combo("Time column", ref _timeCol, colNames, colNames.Length);
        ImGui.SetNextItemWidth(220f);
        ImGui.Combo("Mechanic column", ref _mechCol, colNames, colNames.Length);
        ImGui.SetNextItemWidth(220f);
        ImGui.Combo("Action column (your mit)", ref _actionCol, colNames, colNames.Length);

        var header = _importHeader;
        if (GreenCheckbox("First row is a header", ref header)) _importHeader = header;

        ImGui.TextUnformatted("Assign imported lines to:");
        ImGui.RadioButton("Everyone", ref _importJobMode, 0); ImGui.SameLine();
        ImGui.RadioButton("My selected job", ref _importJobMode, 1); ImGui.SameLine();
        ImGui.RadioButton("Pick below", ref _importJobMode, 2);

        var pickedJobs = new List<string>();
        if (_importJobMode == 2)
        {
            foreach (var role in Enum.GetValues<JobRole>())
            {
                ImGui.TextDisabled(RoleLabel(role) + ":");
                foreach (var abbr in Jobs.AbbreviationsForRole(role))
                {
                    ImGui.SameLine();
                    var on = _importPickedJobs.Contains(abbr);
                    if (GreenCheckbox(abbr + "##imp", ref on))
                    {
                        if (on) _importPickedJobs.Add(abbr); else _importPickedJobs.Remove(abbr);
                    }
                }
            }
            pickedJobs = _importPickedJobs.ToList();
        }
        else if (_importJobMode == 1)
        {
            var active = _plugin.ActiveJobAbbreviation();
            if (active != null) pickedJobs.Add(active);
        }

        var previewRow = _importGrid.Skip(_importHeader ? 1 : 0).FirstOrDefault();
        if (previewRow != null)
        {
            var okTime = SheetImport.TryParseTime(Get(previewRow, _timeCol), out var sec);
            ImGui.TextDisabled($"Preview: time={(okTime ? sec.ToString("0.#") + "s" : "??")}  "
                               + $"mech=\"{Get(previewRow, _mechCol)}\"  action=\"{Get(previewRow, _actionCol)}\"");
        }

        var opt = new SheetImport.Options
        {
            TimeColumn = _timeCol,
            MechanicColumn = _mechCol,
            ActionColumn = _actionCol,
            FirstRowIsHeader = _importHeader,
            Jobs = pickedJobs
        };

        if (ImGui.Button("Add to current mits"))
        {
            if (_importJobMode == 1 && pickedJobs.Count == 0)
            {
                // "My selected job" resolved to nothing (Job selection on Auto with
                // no player loaded). Empty Jobs means "everyone", so importing now
                // would silently give every job these lines - block it instead.
                FlashBuiltin("Couldn't resolve your job - pick jobs manually or set Job selection first.");
            }
            else
            {
                // Always additive: imported lines are appended onto whatever this slot
                // already has, then sorted. Nothing is replaced.
                var imported = SheetImport.BuildLines(_importGrid, opt);
                var merged = new List<MitLine>(fight.Lines);
                merged.AddRange(imported);
                SetFightLines(fight, merged.OrderBy(l => l.Time).ToList());
                FlashBuiltin($"Added {imported.Count} imported line(s).");
            }
        }
        ImGui.SameLine();
        ImGui.TextDisabled("Imported lines are added onto your current slot.");
    }

    private string HeaderHint(int col)
    {
        if (_importGrid == null || !_importHeader || _importGrid.Count == 0) return "";
        var header = Get(_importGrid[0], col);
        return string.IsNullOrWhiteSpace(header) ? "" : $" ({Trunc(header, 14)})";
    }

    // ---- Display tab ------------------------------------------------------

    private void ResetDisplayDefaults()
    {
        C.OverlayFontSizePx = 40f; C.IconScale = 0.8f;
        C.OverlayColorImminent = 0xFF55FFFF; C.OverlayColorActive = 0xFF55FF55;
        C.OverlayColorMechanic = 0xC0FFFFFF;
        C.HeadlineFormat = "{action} ({remaining})"; C.ActiveSuffix = "  NOW";
        C.ShowCountdownNumber = false; C.ShowMechanicLine = true; C.ShowAbilityIcon = true;
        C.TextShadow = true; C.ShowProgressBar = true; C.ProgressBarHeight = 6f;
        C.PulseWhenImminent = true; C.ShowBackground = false; C.BackgroundColor = 0xB0000000;
        C.WarningSeconds = 3f; C.HoldSeconds = 2f;
        // The next-mits window's settings live on the Next Mits page with
        // their own reset (ResetNextMitsDefaults) - not touched from here.
        C.OverlayPosition = new Vector2(0.5f, 0.35f);
        C.Save();
        _plugin.OverlayWindow.RequestReposition();
    }

}
