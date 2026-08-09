using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;

namespace FrenMits.Host;

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
    private MitLine? _scrollToLine;
    private MitLine? _focusNewAction;
    private MitLine? _editOffLine;      // per-line offset (±s column) inline edit
    private string _editOffBuf = "";
    private string _editOffSeed = "";

    // Land a half-typed offset before switching cells.
    private void CommitPendingOffset()
    {
        if (_editOffLine != null && _editOffBuf != _editOffSeed
            && float.TryParse(_editOffBuf, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v))
        {
            _editOffLine.OffsetSeconds = Math.Clamp(v, -30f, 30f);
            _editOffLine.OffsetManual = true; // hand-set: the auto cooldown timer won't touch it
            C.Save();
            _plugin.SheetViewWindow.MarkPlanDirty();
        }
        _editOffLine = null;
    }

    // In-memory line clipboard for the right-click menu.
    private MitLine? _copiedLine;

    // Plugin icon, loaded once from beside the DLL.
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
    internal enum NavKind { Home, Fights, Display, NextMits, Audio, PartyRecap, CombatTimer, PrepCheck, Meter }
    private NavKind _nav = NavKind.Home;
    private string _navCategory = "Ultimate";

    // Every group a fight can file under, in sidebar order. The Type combo on
    // the fight page picks from the same list.
    private static readonly string[] Categories = { "Ultimate", "Savage", "Extreme" };

    // The sidebar group a fight belongs to.
    private static string CategoryOf(FightProfile f)
    {
        if (!string.IsNullOrEmpty(f.Category) && Array.IndexOf(Categories, f.Category) >= 0)
            return f.Category;
        return Builtin.Has(f.TerritoryId) ? Builtin.Category(f.TerritoryId) : "Extreme";
    }

    // Import scratch, per fight, since two headers can be open.
    private string _importBuffer = "";
    private List<string[]>? _importGrid;
    private readonly Dictionary<string, string> _importBufs = new();
    private readonly Dictionary<string, List<string[]>?> _importGrids = new();
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

    // Window theming has to be applied before the window begins.
    public override void PreDraw()
    {
        Theme.PushWindow();
        // Only the two this window wants tighter than the theme's defaults.
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 6f);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(2);
        Theme.PopWindow();
    }

    public override void Draw()
    {
        Theme.Accent = C.AccentColor;
        Theme.Scale = Math.Clamp(C.UiScale, 0.8f, 1.6f);
        Theme.PushWidgets();
        using var uiFont = Widgets.PushUiFont(_plugin.Fonts, Theme.Scale);
        // Fatter scrollbars (easier to grab) + softer rounded controls.
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 18f * Theme.Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 9f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 4f);

        DrawStatusHeader();
        ImGui.Separator();

        // Content sits above a pinned footer, beside the sidebar.
        var footerH = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y + 4f;
        if (ImGui.BeginChild("##content", new Vector2(0, -footerH), false))
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.PanelBg);
            if (ImGui.BeginChild("##sidebar", new Vector2(_sidebarW, 0), true))
                DrawSidebar();
            ImGui.EndChild();
            ImGui.PopStyleColor();

            ImGui.SameLine();
            if (ImGui.BeginChild("##page", new Vector2(0, 0), false))
            {
                ImGui.Spacing();
                ImGui.Indent(Theme.S(4f));
                var searching = DrawSettingsSearch();
                if (searching) DrawSearchResults();
                else DrawSelectedPage();
                ImGui.Unindent(Theme.S(4f));
            }
            ImGui.EndChild();
        }
        ImGui.EndChild();

        DrawFooter();
        ImGui.PopStyleVar(4);
        Theme.PopWidgets();

        // Toggle returns the new value, so the save runs here.
        if (_toggleDirty)
        {
            _toggleDirty = false;
            // Settings only: nothing here can have edited a plan.
            C.SaveSettings();
        }
    }

    private bool _toggleDirty;

    // Every edit is kept the moment it happens; the write follows a breath later.
    private void DrawFooter()
    {
        ImGui.Separator();

        if (Configuration.SuppressSave)
        {
            StatusDot(ImGuiColors.DalamudYellow);
            ImGui.SameLine(0, Theme.S(6f));
            ImGui.TextColored(ImGuiColors.DalamudYellow,
                "Saving is OFF this session (your config file failed to load and was backed up).");
            return;
        }

        // A drag holds its write until it stops, so say so rather than look stale.
        if (C.SavePending)
        {
            StatusDot(ImGuiColors.DalamudYellow);
            ImGui.SameLine(0, Theme.S(6f));
            ImGui.TextDisabled("Saving your changes...");
            return;
        }

        var last = Configuration.LastSavedAt;
        var recent = last != DateTime.MinValue && (DateTime.Now - last).TotalSeconds < 3;
        StatusDot(recent ? ImGuiColors.ParsedGreen : ImGuiColors.HealerGreen);
        ImGui.SameLine(0, Theme.S(6f));
        ImGui.TextDisabled(last == DateTime.MinValue
            ? "All changes are saved; nothing to lose on exit."
            : recent
                ? "All changes saved just now."
                : $"All changes saved; nothing to lose on exit (last {Ago(last)}).");
    }

    private static string Ago(DateTime t)
    {
        var s = (DateTime.Now - t).TotalSeconds;
        return s < 90 ? $"{(int)s}s ago" : s < 5400 ? $"{(int)(s / 60)}m ago" : $"{(int)(s / 3600)}h ago";
    }

    // The page is too narrow to read two columns of settings.
    private static bool Narrow => ImGui.GetContentRegionAvail().X < 420f * Theme.Scale;

    // Checkbox grids drop to one column rather than clip their labels.
    private static int GridCols() => Narrow ? 1 : 2;

    // Second column of a two-up row: half the page, never on top of a long first
    // label, and its own line once the page is narrow.
    private static void NextColumn()
    {
        if (Narrow) return;
        var half = ImGui.GetContentRegionMax().X * 0.5f;
        var after = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + ImGui.GetStyle().ItemSpacing.X * 2;
        ImGui.SameLine(MathF.Max(half, after));
    }

    // Config-bound checkbox that saves on change.
    private bool CfgCheck(string label, bool value) => Toggle(label, value);

    // A checkbox that saves on change, deferred to the end of Draw.
    private bool Toggle(string label, bool value)
    {
        var v = value;
        if (GreenCheckbox($"##tg_{label}", ref v)) _toggleDirty = true;
        ImGui.SameLine(0, Theme.S(8f));
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        return v;
    }

    // The one checkbox style used across the whole config.
    private static bool GreenCheckbox(string label, ref bool v) => Widgets.GreenCheckbox(label, ref v);

    // Tooltip with a hover delay, so sweeping a page stays quiet.
    private static void Tip(string text) => Widgets.Tooltip(text);

    // For a control inside BeginDisabled: hover is ignored by default, which
    // would hide the one hint that says why it is held.
    private static void TipHeld(string text) => Widgets.TooltipWhenHeld(text);

    // A checkbox in the next cell of a two-column grid.
    private bool GridCheck(string label, bool value, string? tip = null)
    {
        ImGui.TableNextColumn();
        value = Toggle(label, value);
        if (tip != null) Tip(tip);
        return value;
    }

    // Section header with an accent bar and uppercase label.
    private static void SeparatorText(string text)
    {
        ImGui.Spacing();
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var h = ImGui.GetTextLineHeight();
        dl.AddRectFilled(p + new Vector2(0, 1), p + new Vector2(Theme.S(3f), h), Theme.Accent, 2f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 10);
        ImGui.TextColored(new Vector4(0.62f, 0.66f, 0.72f, 1f), text.ToUpperInvariant());
        ImGui.Spacing();
    }

    // Collapsible section, true when expanded.
    private static bool Section(string text, bool open = false)
    {
        ImGui.Spacing();
        return ImGui.CollapsingHeader(text.ToUpperInvariant(),
            open ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
    }

    private static void Dot(bool on, string label)
    {
        StatusDot(on ? ImGuiColors.HealerGreen : ImGuiColors.DalamudGrey);
        ImGui.SameLine(0, Theme.S(4f));
        ImGui.TextUnformatted(label);
    }

    private static void WarnDot(string label)
    {
        StatusDot(ImGuiColors.DalamudYellow);
        ImGui.SameLine(0, Theme.S(4f));
        ImGui.TextColored(ImGuiColors.DalamudYellow, label);
    }

    // A filled dot via the draw list, since the font has no circle.
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
        if (Widgets.HoveredDelayed())
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
            dl.AddRectFilled(wp, wp + new Vector2(Theme.S(3f), ImGui.GetWindowHeight()), Theme.Accent);

            // The Test control's room, reserved before anything else is drawn:
            // measuring it first is what lets the name be cut to fit.
            var right = ImGui.GetWindowWidth()
                - (ImGui.CalcTextSize("Test").X + ImGui.GetFrameHeight()
                   + ImGui.GetStyle().ItemInnerSpacing.X + ImGui.GetStyle().WindowPadding.X + Theme.S(12f));

            // The zone's fight leads, since the title bar already says the name.
            if (fight != null)
            {
                var slotText = string.IsNullOrEmpty(fight.Slot) ? "no slot picked"
                    : job != null ? $"{fight.Slot} as {job}" : fight.Slot;
                var slotW = ImGui.CalcTextSize(slotText).X + Theme.S(10f);
                ImGui.TextUnformatted(Widgets.Elide(fight.Name, right - slotW - ImGui.GetCursorPosX()));
                ImGui.SameLine(0, Theme.S(10f));
                ImGui.TextColored(
                    Theme.V(string.IsNullOrEmpty(fight.Slot) ? Theme.Warn : Theme.Accent), slotText);
            }
            else
            {
                ImGui.TextColored(new Vector4(0.55f, 0.59f, 0.66f, 1f),
                    "No supported fight in this zone");
            }

            // Never left of what was just drawn, whatever the name turned out to be.
            var lineEnd = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;
            ImGui.SameLine(0, 0);
            ImGui.SetCursorPosX(MathF.Max(right, lineEnd + Theme.S(10f)));
            var test = C.TestMode;
            if (GreenCheckbox("Test", ref test)) { C.TestMode = test; C.Save(); }
            if (Widgets.HoveredDelayed())
                ImGui.SetTooltip("Show a sample call so you can place the overlay.");

            // Status dots on the second line.
            Dot(job != null, $"Job: {job ?? "?"}");
            ImGui.SameLine(0, Theme.S(18f));
            Dot(running, running ? $"Timer: {_plugin.Timer.Elapsed:0.0}s" : "Timer: idle");
            // These appear only when they need attention.
            if (!C.AudioEnabled) { ImGui.SameLine(0, Theme.S(18f)); WarnDot("Audio off"); }
            if (!C.EnableSync) { ImGui.SameLine(0, Theme.S(18f)); WarnDot("Resync off"); }
            if (_plugin.FrameErrorCount > 0 && (DateTime.Now - _plugin.LastFrameErrorAt.ToLocalTime()).TotalMinutes < 5)
            {
                ImGui.SameLine(0, Theme.S(18f));
                WarnDot($"internal errors ({_plugin.FrameErrorCount}): check /xllog");
            }
            // Anything that failed quietly, like a sheet moving on patch day.
            if (Swallowed.Any)
            {
                ImGui.SameLine(0, Theme.S(18f));
                var worst = Swallowed.Worst();
                WarnDot($"degraded: {worst.Site} (x{worst.Count})");
                if (Widgets.HoveredDelayed())
                {
                    var tip = new System.Text.StringBuilder(
                        "These failed and were skipped rather than crashing:\n");
                    foreach (var e in Swallowed.All())
                        tip.Append($"\n  {e.Site} - {e.Count}x, last: {e.Message}");
                    tip.Append("\n\nFull detail is in /xllog.");
                    ImGui.SetTooltip(tip.ToString());
                }
            }
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    // ---- left sidebar nav ----

    private string _expandFightId = "";

    // The sidebar fits its longest nav label, measured while it draws.
    private const float SidebarMinWidth = 186f;
    private float _sidebarW = SidebarMinWidth;
    private float _navNeed;

    private static FontAwesomeIcon CategoryIcon(string cat) => cat switch
    {
        "Ultimate" => FontAwesomeIcon.Crown,
        "Savage" => FontAwesomeIcon.Skull,
        "Extreme" => FontAwesomeIcon.Fire,
        _ => FontAwesomeIcon.LayerGroup,
    };

    private void DrawSidebar()
    {
        _navNeed = 0f;
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
        // Sheet View is a window, so the nav item opens it.
        if (NavItem(FontAwesomeIcon.Table, "Sheet View", false))
        {
            var fight = _plugin.ActiveFight();
            _plugin.SheetViewWindow.Open(
                fight != null && (Builtin.Has(fight.TerritoryId) || fight.CustomSlots.Count > 0) ? fight : null);
        }
        if (NavItem(FontAwesomeIcon.ShieldAlt, "Next Mits & Timeline", _nav == NavKind.NextMits)) _nav = NavKind.NextMits;
        if (NavItem(FontAwesomeIcon.Clock, "Combat Timer", _nav == NavKind.CombatTimer)) _nav = NavKind.CombatTimer;
        if (NavItem(FontAwesomeIcon.ClipboardList, "Party Mit Recap", _nav == NavKind.PartyRecap)) _nav = NavKind.PartyRecap;
        if (NavItem(FontAwesomeIcon.ChartBar, "Fren Meter", _nav == NavKind.Meter)) _nav = NavKind.Meter;
        if (NavItem(FontAwesomeIcon.Utensils, "Food & Pot", _nav == NavKind.PrepCheck)) _nav = NavKind.PrepCheck;

        ImGui.Spacing();
        SidebarHeading("SETTINGS");
        if (NavItem(FontAwesomeIcon.Desktop, "Display", _nav == NavKind.Display)) _nav = NavKind.Display;
        if (NavItem(FontAwesomeIcon.VolumeUp, "Audio", _nav == NavKind.Audio)) _nav = NavKind.Audio;

        DrawSidebarSetup();

        // Next frame's width, so no label is clipped once a scrollbar appears.
        var bar = ImGui.GetScrollMaxY() > 0f ? ImGui.GetStyle().ScrollbarSize : 0f;
        _sidebarW = MathF.Max(SidebarMinWidth * Theme.Scale, _navNeed + bar);
    }

    private static void SidebarHeading(string text)
    {
        ImGui.Spacing();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8);
        // Muted, so the accent belongs to the selected row alone.
        ImGui.TextColored(new Vector4(0.55f, 0.59f, 0.66f, 1f), text.ToUpperInvariant());
        ImGui.Spacing();
    }

    private bool NavItem(FontAwesomeIcon icon, string label, bool selected, int? count = null)
    {
        var startX = ImGui.GetCursorPosX();
        var startY = ImGui.GetCursorPosY();

        // A wash plus an edge bar, so the accent reads without shouting.
        var rgb = Theme.Accent & 0x00FFFFFFu;
        if (selected)
        {
            ImGui.PushStyleColor(ImGuiCol.Header, rgb | 0x2A000000u);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, rgb | 0x3C000000u);
        }
        var rowH = Theme.S(27f);
        var clicked = ImGui.Selectable($"##nav-{label}", selected, ImGuiSelectableFlags.None, new Vector2(0, rowH));
        if (selected) ImGui.PopStyleColor(2);
        if (selected)
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddRectFilled(
                new Vector2(min.X, min.Y + 2f), new Vector2(min.X + Theme.S(3f), max.Y - 2f), Theme.Accent, 2f);
        }

        var endX = ImGui.GetCursorPosX();
        var endY = ImGui.GetCursorPosY();
        var col = selected ? new Vector4(1f, 1f, 1f, 1f) : new Vector4(0.74f, 0.77f, 0.82f, 1f);

        // Icon (icon font) + label drawn over the selectable row.
        var textY = startY + (rowH - ImGui.GetTextLineHeight()) * 0.5f;
        var labelX = startX + Theme.S(38f);
        ImGui.SameLine();
        ImGui.SetCursorPos(new Vector2(startX + Theme.S(12f), textY));
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            ImGui.TextColored(col, icon.ToIconString());
        ImGui.SameLine();
        ImGui.SetCursorPos(new Vector2(labelX, textY));
        ImGui.TextColored(col, label);
        // The tail is the right padding plus any count badge.
        _navNeed = MathF.Max(_navNeed,
            labelX + ImGui.CalcTextSize(label).X + Theme.S(count is null ? 12f : 40f));

        if (count is { } n)
        {
            var txt = n.ToString();
            ImGui.SameLine();
            // Never left of the label, so a long one pushes the badge instead
            // of having the badge drawn across it.
            var badgeX = MathF.Max(labelX + ImGui.CalcTextSize(label).X + Theme.S(8f),
                ImGui.GetContentRegionMax().X - ImGui.CalcTextSize(txt).X - Theme.S(10f));
            ImGui.SetCursorPos(new Vector2(badgeX, textY));
            ImGui.TextDisabled(txt);
        }

        ImGui.SetCursorPos(new Vector2(endX, endY)); // resume normal flow below the row
        // Picking a page by hand ends any search that was up.
        if (clicked) { _search = ""; _jumpTab = ""; }
        return clicked;
    }

    // Job and role in one block, the role covering every built-in.
    private void DrawSidebarSetup()
    {
        ImGui.Spacing();
        SidebarHeading("YOUR SETUP");

        // Remove Job dropdown, show Job read-only
        var liveJob = Plugin.LocalPlayer?.ClassJob.RowId is { } rid ? Jobs.ByRowId(rid)?.Abbreviation : null;
        var jobStr = liveJob ?? "[---]";
        
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8);
        ImGui.TextDisabled("Job:");
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.DalamudYellow, jobStr);
        Tip("Your current job.");

        // The seat the plugin would pick, so a preference below shows up at once.
        var roleStr = "[---]";
        if (liveJob != null)
        {
            var seat = Builtin.DefaultSlotForJobIn(SlotNames.Standard, liveJob, C.SlotPrefs);
            if (seat.Length > 0) roleStr = seat;
        }

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8);
        ImGui.TextDisabled("Role:");
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.DalamudYellow, roleStr);
        Tip("The role the plugin assigns you based on your party/preferences.");

        ImGui.Spacing();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Role Preferences");
        Tip("Default roles chosen when you play each job type.");

        var roles = new (string Label, JobRole[] Types, string[] Opts)[]
        {
            ("Tank", new[] { JobRole.Tank }, new[] { "MT", "OT" }),
            ("Healer", new[] { JobRole.Healer }, new[] { "H1", "H2" }),
            ("Melee", new[] { JobRole.Melee }, new[] { "M1", "M2" }),
            ("Ranged", new[] { JobRole.PhysicalRanged, JobRole.Caster }, new[] { "R1", "R2" }),
        };

        // One column, measured: two would push the wider labels off the sidebar.
        var indent = ImGui.GetCursorPosX() + 8;
        var labelW = roles.Max(r => ImGui.CalcTextSize(r.Label).X) + ImGui.GetStyle().ItemSpacing.X * 2;
        var room = ImGui.GetContentRegionAvail().X - 8 - labelW - ImGui.GetStyle().ItemSpacing.X;
        var comboW = Math.Clamp(room, 44f, 64f);
        foreach (var (label, types, opts) in roles)
            DrawRolePrefCombo(label, types, opts, indent, labelW, comboW);

        ImGui.Spacing();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8);
        var ask = C.ShowSlotPopupOnEntry;
        if (GreenCheckbox("Ask on duty entry", ref ask)) { C.ShowSlotPopupOnEntry = ask; C.Save(); }
        Tip("A popup on entry showing which slot is yours.");
    }

    private void DrawRolePrefCombo(string label, JobRole[] roleTypes, string[] opts, float cursorX, float labelW, float comboW)
    {
        ImGui.SetCursorPosX(cursorX);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(label);
        ImGui.SameLine(cursorX + labelW);
        
        var firstRole = roleTypes[0];
        var current = C.GlobalRolePreferences.TryGetValue(firstRole, out var pref) ? pref : opts[0];
        var idx = Array.IndexOf(opts, current);
        if (idx < 0) idx = 0;

        ImGui.SetNextItemWidth(comboW);
        if (ImGui.Combo($"##rolepref{label}", ref idx, opts, opts.Length))
        {
            foreach (var r in roleTypes)
                C.GlobalRolePreferences[r] = opts[idx];
            C.Save();
        }
    }

    // Both roles are seats of one pair, so the pick already matches.
    private static bool SameSeatGroup(string selection, string liveRole)
        => (selection is "MT" or "OT" && liveRole is "MT" or "OT")
        || (selection is "M1" or "M2" && liveRole is "M1" or "M2");

    // True if every sheet is on the slot this role maps to.
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

    // Apply the role to every built-in, keeping each slot's edits.
    private void SelectRoleForAll(string role)
    {
        _plugin.SetRoleForAll(role);
        var last = C.Fights.LastOrDefault(f => Builtin.Has(f.TerritoryId));
        if (last != null) C.DmuSlot = last.Slot;
        FlashBuiltin($"Set every fight to {role}.");
    }

    // ---- settings search ----

    private string _search = "";
    // The tab a search result asked for, consumed by the next TabItem call.
    private string _jumpTab = "";

    // A tab that a search result can open directly, and that offers to put
    // itself back to defaults once anything on it has moved.
    private bool TabItem(string label)
    {
        var flags = _jumpTab == label ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        var open = ImGui.BeginTabItem(label, flags);
        if (flags != ImGuiTabItemFlags.None) _jumpTab = "";
        if (open) DrawTabResetBar(label);
        return open;
    }

    // Shown only when a tab holds non-default settings.
    private void DrawTabResetBar(string tab)
    {
        var changed = SettingsIndex.ChangedIn(C, _nav, tab);
        if (changed.Count == 0) return;

        ImGui.Spacing();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.V(Theme.Accent), $"{changed.Count} off default");
        ImGui.SameLine(0, Theme.S(10f));
        if (ImGui.SmallButton($"Reset tab##rst{tab}"))
        {
            foreach (var e in changed) e.Reset(C);
            C.Save();
            RefreshAfterReset();
        }
        if (Widgets.HoveredDelayed())
            ImGui.SetTooltip("Put every setting on this tab back to its default:\n"
                             + string.Join("\n", changed.Select(e => "  " + e.Label)));
        ImGui.Separator();
    }

    // Anything that caches a setting has to hear about a bulk reset.
    private void RefreshAfterReset()
    {
        Theme.Accent = C.AccentColor;
        Theme.Colorblind = C.ColorblindMode;
        Theme.Scale = Math.Clamp(C.UiScale, 0.8f, 1.6f);
        _plugin.OverlayWindow.RequestReposition();
        _plugin.TimelineWindow.RequestReposition();
        _plugin.InvalidateSolverCache();
    }

    // Which result the keyboard is on, and whether Enter was pressed in the box.
    private int _searchSel;
    private bool _searchEntered;
    private string _searchPrev = "";

    // True while a query is up, so the page gives way to the results.
    private bool DrawSettingsSearch()
    {
        // Home is a splash and Fights has its own filter; neither wants this bar.
        if (_nav is NavKind.Home or NavKind.Fights) return false;

        ImGui.SetNextItemWidth(MathF.Min(300f * Theme.Scale, ImGui.GetContentRegionAvail().X - 30f));
        _searchEntered = ImGui.InputTextWithHint("##settingsearch", "Search all settings...", ref _search, 64,
            ImGuiInputTextFlags.EnterReturnsTrue);
        if (_search.Length > 0)
        {
            ImGui.SameLine(0, Theme.S(4f));
            if (ImGui.SmallButton("x##clearsearch")) _search = "";
        }
        // A new query starts back at the top of the list.
        if (_search != _searchPrev) { _searchPrev = _search; _searchSel = 0; }
        ImGui.Spacing();
        return _search.Trim().Length >= 2;
    }

    private void DrawSearchResults()
    {
        var hits = SettingsIndex.Search(_search);
        if (hits.Count == 0)
        {
            ImGui.TextDisabled($"Nothing matches \"{_search.Trim()}\".");
            ImGui.TextDisabled("Try a word from the setting, or the page it lives on.");
            return;
        }

        // Arrows walk the list, Enter opens, Escape clears.
        var moved = false;
        if (ImGui.IsKeyPressed(ImGuiKey.DownArrow, true)) { _searchSel++; moved = true; }
        if (ImGui.IsKeyPressed(ImGuiKey.UpArrow, true)) { _searchSel--; moved = true; }
        _searchSel = Math.Clamp(_searchSel, 0, hits.Count - 1);
        if (ImGui.IsKeyPressed(ImGuiKey.Escape, false)) { _search = ""; _searchSel = 0; return; }

        var go = _searchEntered ? _searchSel : -1;

        ImGui.TextDisabled($"{hits.Count} setting{(hits.Count == 1 ? "" : "s")}   ·   up / down to move, enter to open");
        ImGui.Spacing();
        for (var i = 0; i < hits.Count; i++)
        {
            var e = hits[i];
            ImGui.PushID(e.Prop);
            if (ImGui.Selectable("##hit", i == _searchSel, ImGuiSelectableFlags.None,
                    new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 1.6f)))
                go = i;
            if (moved && i == _searchSel) ImGui.SetScrollHereY(0.5f);
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var dl = ImGui.GetWindowDrawList();
            // Two lines centered in the row, and each cut to the row's width so
            // a long label cannot run out past the selection.
            var textX = Theme.S(8f);
            var room = max.X - min.X - textX * 2f;
            var lineH = ImGui.GetTextLineHeight();
            var top = min.Y + (max.Y - min.Y - lineH * 2f) * 0.5f;
            dl.AddText(new Vector2(min.X + textX, top), Theme.TextBright, Widgets.Elide(e.Label, room));
            dl.AddText(new Vector2(min.X + textX, top + lineH), Theme.Muted,
                Widgets.Elide(SettingsIndex.Where(e), room));
            ImGui.PopID();
        }

        if (go < 0) return;
        _nav = hits[go].Nav;
        _jumpTab = hits[go].Tab;
        _search = "";
        _searchSel = 0;
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
            case NavKind.PrepCheck: DrawPrepCheckPage(); break;
            case NavKind.Meter: DrawMeterPage(); break;
            default: DrawFightCategoryPage(_navCategory); break;
        }
    }

    private string Version => typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    // Approximate width of an icon button, for centering.
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

        var accent = Theme.V(Theme.Accent);
        var grey = new Vector4(0.55f, 0.59f, 0.66f, 1f);

        ImGui.Dummy(new Vector2(0, Theme.S(10f)));

        // The logo, or a glyph shield if it didn't load.
        var icon = IconWrap();
        if (icon != null)
        {
            var sz = Theme.S(112f);
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
        var titleFont = _plugin.Fonts.Get(Theme.S(34f), "Default", false, false);
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
        ImGui.Dummy(new Vector2(0, Theme.S(8f)));
        var dl = ImGui.GetWindowDrawList();
        var cy = ImGui.GetCursorScreenPos().Y;
        // Centered on the content, like everything else on this page: the window
        // width ignores the page indent and would sit the rule slightly off.
        var cx = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X * 0.5f;
        var half = Theme.S(60f);
        dl.AddRectFilled(new Vector2(cx - half, cy), new Vector2(cx + half, cy + Theme.S(2f)), Theme.Accent, 1f);
        ImGui.Dummy(new Vector2(0, Theme.S(14f)));

        // First-run steps, gone once any fight has a slot picked.
        if (!C.Fights.Any(f => !string.IsNullOrEmpty(f.Slot)))
        {
            var cardW = MathF.Max(Theme.S(220f),
                MathF.Min(Theme.S(430f), ImGui.GetContentRegionAvail().X - Theme.S(20f)));
            // The panel is painted behind the text once its real height is
            // known, so however many lines the steps wrap to, none are cut off.
            Center(cardW);
            var cardPad = Theme.S(12f);
            var cardMin = ImGui.GetCursorScreenPos();
            var cardDl = ImGui.GetWindowDrawList();
            cardDl.ChannelsSplit(2);
            cardDl.ChannelsSetCurrent(1);

            ImGui.SetCursorScreenPos(cardMin + new Vector2(cardPad, cardPad));
            ImGui.BeginGroup();
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + cardW - cardPad * 2f);
            ImGui.TextColored(new Vector4(0.42f, 0.66f, 0.96f, 1f), "Get started");
            ImGui.TextWrapped("1. Pick your job in the sidebar (or leave it on Auto).");
            ImGui.TextWrapped("2. Open your fight and choose \"Your slot\": that column of the mit sheet becomes yours.");
            ImGui.TextWrapped("3. Tick Test (top right) and drag the call display where you want it. It switches off by itself when you pull.");
            ImGui.PopTextWrapPos();
            ImGui.Spacing();
            if (ImGui.SmallButton("Take me to the fights"))
            {
                _nav = NavKind.Fights;
                _navCategory = "Ultimate";
            }
            ImGui.EndGroup();

            var cardMax = new Vector2(cardMin.X + cardW, ImGui.GetItemRectMax().Y + cardPad);
            cardDl.ChannelsSetCurrent(0);
            cardDl.AddRectFilled(cardMin, cardMax, Theme.PanelBg, Theme.S(8f));
            cardDl.AddRect(cardMin, cardMax, Widgets.CardBorder, Theme.S(8f));
            cardDl.ChannelsMerge();

            // Put the cursor below the panel, since the group ended inside it.
            ImGui.SetCursorScreenPos(new Vector2(cardMin.X, cardMax.Y));
            ImGui.Dummy(new Vector2(0, Theme.S(10f)));
        }

        // Action row: just GitHub.
        var ghW = IconBtnWidth(FontAwesomeIcon.ExternalLinkAlt, "GitHub");
        Center(ghW);
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ExternalLinkAlt, "GitHub"))
            Dalamud.Utility.Util.OpenLink("https://github.com/swixum/FrenMits");

        // Version, centered below.
        ImGui.Dummy(new Vector2(0, Theme.S(6f)));
        var ver = $"v{Version}";
        Center(ImGui.CalcTextSize(ver).X);
        ImGui.TextDisabled(ver);
    }

    // ---- Display tab ----

    private void ResetDisplayDefaults()
    {
        C.OverlayFontSizePx = 40f; C.IconScale = 0.8f;
        C.OverlayColorImminent = 0xFF55FFFF; C.OverlayColorActive = 0xFF55FF55;
        C.OverlayColorMechanic = 0xC0FFFFFF;
        C.HeadlineFormat = "{action} ({remaining})"; C.ActiveSuffix = "  NOW";
        C.ShowCountdownNumber = false; C.ShowMechanicLine = true; C.ShowAbilityIcon = true;
        C.TextShadow = true; C.ShowProgressBar = true; C.ProgressBarHeight = 6f;
        C.PulseWhenImminent = true; C.ShowBackground = false; C.BackgroundColor = 0xB0000000;
        C.WarningSeconds = 3f; C.HoldSeconds = 2f; C.UseWindowLeadSeconds = 2f;
        // The next-mits window has its own reset, not this one.
        C.OverlayPosition = new Vector2(0.5f, 0.35f);
        C.Save();
        _plugin.OverlayWindow.RequestReposition();
    }

}
