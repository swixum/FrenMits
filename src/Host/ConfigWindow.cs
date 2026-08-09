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
    private static readonly Vector4 GoldStar = Theme.V(Theme.Gold);
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
    internal enum NavKind { Home, Fights, Display, NextMits, Audio, PartyRecap, CombatTimer, PrepCheck, Meter, Appearance }
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
                if (Searching) DrawSearchResults();
                else DrawSelectedPage();
                ImGui.Unindent(Theme.S(4f));
            }
            ImGui.EndChild();
        }
        ImGui.EndChild();

        DrawFooter();
        ImGui.PopStyleVar(4);
        Theme.PopWidgets();

        Widgets.RollLabelCols();

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
            StatusDot(Theme.V(Theme.Warn));
            ImGui.SameLine(0, Theme.S(6f));
            ImGui.TextColored(Theme.V(Theme.Warn),
                "Saving is OFF this session (your config file failed to load and was backed up).");
            return;
        }

        // A drag holds its write until it stops, so say so rather than look stale.
        if (C.SavePending)
        {
            StatusDot(Theme.V(Theme.Warn));
            ImGui.SameLine(0, Theme.S(6f));
            ImGui.TextDisabled("Saving your changes...");
            return;
        }

        var last = Configuration.LastSavedAt;
        var recent = last != DateTime.MinValue && (DateTime.Now - last).TotalSeconds < 3;
        StatusDot(Theme.V(recent ? Theme.GoodBright : Theme.Good));
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

    // Section header with an accent bar and uppercase label.
    private static void SeparatorText(string text)
    {
        ImGui.Spacing();
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var h = ImGui.GetTextLineHeight();
        dl.AddRectFilled(p + new Vector2(0, 1), p + new Vector2(Theme.S(3f), h), Theme.Accent, 2f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 10);
        ImGui.TextColored(Theme.V(Theme.Heading), text.ToUpperInvariant());
        ImGui.Spacing();
    }

    // Collapsible section, true when expanded.
    // ---- shared label column ----
    // Labels are right-aligned into one column so every row's control starts at
    // the same x. Width is measured as the rows draw and applied next frame,
    // the same one-frame settle the sidebar uses.

    // The column itself lives in Widgets, so the shared slider helper can use it.
    private static float _labelCol => Widgets.LabelColWidth;
    private static void RowLabel(string text) => Widgets.RowLabel(text);

    // A labelled value control: the label leads, ImGui's trailing one is hidden.
    private static void LabelledWidth(string label, float width)
    {
        Widgets.RowLabel(label);
        ImGui.SetNextItemWidth(Theme.S(width));
    }

    // An icon in place of a word, right-aligned into the same column.
    private void RowLabelIcon(FontAwesomeIcon icon, uint color)
    {
        float w;
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            w = ImGui.CalcTextSize(icon.ToIconString()).X;
        var col = MathF.Max(_labelCol, w);
        ImGui.AlignTextToFramePadding();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + col - w);
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            ImGui.TextColored(Theme.V(color), icon.ToIconString());
        ImGui.SameLine(0, Theme.S(8f));
    }

    // A row that starts in the content column with no label of its own.
    private void RowIndent()
    {
        ImGui.Dummy(new Vector2(MathF.Max(_labelCol, 0f), 1f));
        ImGui.SameLine(0, Theme.S(8f));
    }

    private static bool Section(string text, bool open = false)
    {
        ImGui.Spacing();
        return ImGui.CollapsingHeader(text.ToUpperInvariant(),
            open ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
    }

    private static void Dot(bool on, string label)
    {
        StatusDot(Theme.V(on ? Theme.Good : Theme.Muted));
        ImGui.SameLine(0, Theme.S(4f));
        ImGui.TextUnformatted(label);
    }

    private static void WarnDot(string label)
    {
        StatusDot(Theme.V(Theme.Warn));
        ImGui.SameLine(0, Theme.S(4f));
        ImGui.TextColored(Theme.V(Theme.Warn), label);
    }

    // A filled dot via the draw list, since the font has no circle. Frame-align
    // it wherever the text beside it is frame-aligned, or the two sit apart.
    private static void StatusDot(Vector4 color, bool frameAligned = false)
    {
        var size = ImGui.GetTextLineHeight();
        var h = frameAligned ? ImGui.GetFrameHeight() : size;
        var pos = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddCircleFilled(
            new Vector2(pos.X + size * 0.5f, pos.Y + (frameAligned ? h * 0.5f : size * 0.55f)),
            size * 0.22f, ImGui.ColorConvertFloat4ToU32(color));
        ImGui.Dummy(new Vector2(size, h));
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
                var slotText = string.IsNullOrEmpty(fight.Slot) ? "No slot picked"
                    : job != null ? $"{fight.Slot} as {job}" : fight.Slot;
                var slotW = ImGui.CalcTextSize(slotText).X + Theme.S(10f);
                ImGui.TextUnformatted(Widgets.Elide(fight.Name, right - slotW - ImGui.GetCursorPosX()));
                ImGui.SameLine(0, Theme.S(10f));
                ImGui.TextColored(
                    Theme.V(string.IsNullOrEmpty(fight.Slot) ? Theme.Warn : Theme.Accent), slotText);
            }
            else
            {
                ImGui.TextColored(Theme.V(Theme.Heading),
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
                WarnDot($"Internal errors ({_plugin.FrameErrorCount}): check /xllog");
            }
            // Anything that failed quietly, like a sheet moving on patch day.
            if (Swallowed.Any)
            {
                ImGui.SameLine(0, Theme.S(18f));
                var worst = Swallowed.Worst();
                WarnDot($"Degraded: {worst.Site} (x{worst.Count})");
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
        DrawSidebarSearch();
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
        // Sheet View is a window, so the nav item opens it.
        if (NavItem(FontAwesomeIcon.Table, "Sheet View", false))
        {
            var fight = _plugin.ActiveFight();
            _plugin.SheetViewWindow.Open(
                fight != null && (Builtin.Has(fight.TerritoryId) || fight.CustomSlots.Count > 0) ? fight : null);
        }

        // Grouped by where the thing shows up, not by what kind of thing it is.
        ImGui.Spacing();
        SidebarHeading("ON SCREEN");
        if (NavItem(FontAwesomeIcon.Desktop, "Call Display", _nav == NavKind.Display)) _nav = NavKind.Display;
        if (NavItem(FontAwesomeIcon.ShieldAlt, "Next Mits", _nav == NavKind.NextMits)) _nav = NavKind.NextMits;
        if (NavItem(FontAwesomeIcon.ChartBar, "Fren Meter", _nav == NavKind.Meter,
            dot: !C.MeterEnabled ? 0u : _plugin.Meter.Connected ? Theme.Good : Theme.Warn)) _nav = NavKind.Meter;
        if (NavItem(FontAwesomeIcon.Clock, "Combat Timer", _nav == NavKind.CombatTimer)) _nav = NavKind.CombatTimer;
        if (NavItem(FontAwesomeIcon.Utensils, "Food & Pot", _nav == NavKind.PrepCheck)) _nav = NavKind.PrepCheck;
        if (NavItem(FontAwesomeIcon.ClipboardList, "Mit Recap", _nav == NavKind.PartyRecap)) _nav = NavKind.PartyRecap;

        ImGui.Spacing();
        SidebarHeading("SETTINGS");
        if (NavItem(FontAwesomeIcon.VolumeUp, "Audio", _nav == NavKind.Audio,
            dot: C.AudioEnabled ? 0u : Theme.Warn)) _nav = NavKind.Audio;
        if (NavItem(FontAwesomeIcon.Palette, "Appearance", _nav == NavKind.Appearance)) _nav = NavKind.Appearance;

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
        ImGui.TextColored(Theme.V(Theme.Heading), text.ToUpperInvariant());
        ImGui.Spacing();
    }

    private bool NavItem(FontAwesomeIcon icon, string label, bool selected, int? count = null, uint dot = 0)
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
        var navMin = ImGui.GetItemRectMin();
        var navMax = ImGui.GetItemRectMax();
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
        var col = selected ? new Vector4(1f, 1f, 1f, 1f) : Theme.V(Theme.NavText);

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

        // A small status dot on the right, where a count would sit.
        if (dot != 0)
            ImGui.GetWindowDrawList().AddCircleFilled(
                new Vector2(navMax.X - Theme.S(12f), (navMin.Y + navMax.Y) * 0.5f), Theme.S(3f), dot);

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
        ImGui.TextColored(Theme.V(liveJob != null ? Theme.Accent : Theme.Muted), jobStr);
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
        ImGui.TextColored(Theme.V(roleStr != "[---]" ? Theme.Accent : Theme.Muted), roleStr);
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
        // A dot on the tab says something inside it is off its default. The ###
        // keeps the id fixed, so the dot appearing does not reselect the tab.
        var changed = SettingsIndex.ChangedIn(C, _nav, label);
        // A middle dot, since the icon font is not in play on a tab label.
        var shown = changed.Count > 0 ? $"{label} ·###{label}" : $"{label}###{label}";
        var open = ImGui.BeginTabItem(shown, flags);
        if (flags != ImGuiTabItemFlags.None) _jumpTab = "";
        if (changed.Count > 0 && Widgets.HoveredDelayed())
            ImGui.SetTooltip($"{changed.Count} setting{(changed.Count == 1 ? "" : "s")} changed here");
        // Each tab sizes its own label column.
        if (open) { Widgets.LabelScope($"{_nav}/{label}"); DrawTabResetBar(label, changed); }
        return open;
    }

    // One short line, and only on a tab you have actually changed.
    private void DrawTabResetBar(string tab, List<SettingsIndex.Entry> changed)
    {
        if (changed.Count == 0) { ImGui.Spacing(); return; }

        ImGui.Spacing();
        var w = ImGui.CalcTextSize("Reset").X + ImGui.GetStyle().FramePadding.X * 2f;
        ImGui.SameLine(MathF.Max(0f, ImGui.GetContentRegionMax().X - w));
        if (ImGui.SmallButton($"Reset##rst{tab}"))
        {
            foreach (var e in changed) e.Reset(C);
            C.Save();
            RefreshAfterReset();
        }
        if (Widgets.HoveredDelayed())
            ImGui.SetTooltip("Put this tab back to its defaults:\n"
                             + string.Join("\n", changed.Select(e => "  " + e.Label)));
        ImGui.Spacing();
    }

    // Everything on one page back to how it ships.
    private void ResetPage(NavKind nav)
    {
        SettingsIndex.ResetPage(C, nav);
        C.Save();
        RefreshAfterReset();
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

    // One fixed place for the box, so no page shifts down a line to make room.
    private void DrawSidebarSearch()
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - Theme.S(2f));
        _searchEntered = ImGui.InputTextWithHint("##settingsearch", "Search", ref _search, 64,
            ImGuiInputTextFlags.EnterReturnsTrue);
        // A new query starts back at the top of the list.
        if (_search != _searchPrev) { _searchPrev = _search; _searchSel = 0; }
        ImGui.Spacing();
    }

    // True while a query is up, so the page gives way to the results.
    private bool Searching => _search.Trim().Length >= 2;

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
        // A setting that lives on a tab only exists once the page shows everything.
        if (_jumpTab.Length > 0) _pageAll[_nav] = true;
        _search = "";
        _searchSel = 0;
    }

    private void DrawSelectedPage()
    {
        Widgets.LabelScope(_nav.ToString());
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
            case NavKind.Appearance: DrawAppearancePage(); break;
            default: DrawFightCategoryPage(_navCategory); break;
        }
    }

    // ---- page header ----
    // Every page opens with the same row, so the master switch, the reset and
    // the Basic / All choice are always in the same three pixels.

    // Which pages are showing everything. Not saved: a page opens simple.
    private readonly Dictionary<NavKind, bool> _pageAll = new();
    private bool AllMode => _pageAll.TryGetValue(_nav, out var v) && v;
    private void SetAllMode(bool on) => _pageAll[_nav] = on;

    // Returns the master switch's value; pass hasMaster false where there is none.
    // The one page title: an accent bar, then the name a size up and in the
    // accent, so a page reads as a page and not as one more row. Returns how
    // tall the row came out and where the name ends, so whatever shares the
    // line can centre on it and never land on top of it.
    private (float RowH, float EndX) PageTitle(string name, FontAwesomeIcon icon = FontAwesomeIcon.None)
    {
        var frameH = ImGui.GetFrameHeight();
        // A real font at the bigger size. Scaling the window font would blur it.
        var px = MathF.Round(ImGui.GetFontSize() * 1.35f);
        var font = _plugin.Fonts.Get(px, "Default", false, false);
        var big = font is { Available: true };

        var start = ImGui.GetCursorPos();
        var scr = ImGui.GetCursorScreenPos();

        Vector2 sz = default;
        if (big) using (font!.Push()) sz = ImGui.CalcTextSize(name);
        else sz = ImGui.CalcTextSize(name);

        var rowH = MathF.Max(sz.Y, frameH);
        var top = (rowH - sz.Y) * 0.5f;

        ImGui.GetWindowDrawList().AddRectFilled(
            new Vector2(scr.X, scr.Y + top + Theme.S(2f)),
            new Vector2(scr.X + Theme.S(3f), scr.Y + top + sz.Y - Theme.S(2f)),
            Theme.Accent, 2f);

        // The page's emblem, the same one its nav row carries.
        var textX = start.X + Theme.S(11f);
        if (icon != FontAwesomeIcon.None)
        {
            using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            {
                var ic = icon.ToIconString();
                var isz = ImGui.CalcTextSize(ic);
                ImGui.SetCursorPos(new Vector2(textX, start.Y + (rowH - isz.Y) * 0.5f));
                ImGui.TextColored(Theme.V(Theme.Accent), ic);
                textX += isz.X + Theme.S(8f);
            }
        }

        ImGui.SetCursorPos(new Vector2(textX, start.Y + top));
        if (big) using (font!.Push()) ImGui.TextColored(Theme.V(Theme.Accent), name);
        else ImGui.TextColored(Theme.V(Theme.Accent), name);

        ImGui.SetCursorPos(start);
        return (rowH, textX + sz.X);
    }

    private bool PageHead(string name, string note, bool master,
        bool hasMaster = true, bool hasModes = false, Action? reset = null,
        FontAwesomeIcon icon = FontAwesomeIcon.None, uint noteCol = 0)
    {
        var frameH = ImGui.GetFrameHeight();
        var start = ImGui.GetCursorPos();
        var (rowH, endX) = PageTitle(name, icon);

        var used = endX;
        if (note.Length > 0)
        {
            var lh = ImGui.GetTextLineHeight();
            ImGui.SetCursorPos(new Vector2(endX + Theme.S(12f), start.Y + (rowH - lh) * 0.5f));
            ImGui.TextColored(Theme.V(noteCol == 0 ? Theme.Muted : noteCol), note);
            used = endX + Theme.S(12f) + ImGui.CalcTextSize(note).X;
        }

        var segW = hasModes ? Widgets.ButtonWidth("Basic", "All") + Theme.S(4f) : 0f;
        var right = segW + (reset != null ? frameH + Theme.S(8f) : 0f) + (hasMaster ? frameH + Theme.S(8f) : 0f);
        // Never left of the name, whatever it turned out to be.
        ImGui.SetCursorPos(new Vector2(
            MathF.Max(used + Theme.S(12f), ImGui.GetContentRegionMax().X - right),
            start.Y + (rowH - frameH) * 0.5f));

        if (hasModes)
        {
            // Frame height, so it sits on the same center line as the reset
            // button and the switch beside it. A small button is shorter than
            // both and gets clipped by the line they set.
            var all = AllMode;
            Widgets.SegmentBegin();
            if (Widgets.SegmentTall("Basic##pm", !all)) SetAllMode(false);
            ImGui.SameLine();
            if (Widgets.SegmentTall("All##pm", all)) SetAllMode(true);
            Widgets.SegmentEnd();
            ImGui.SameLine(0, Theme.S(8f));
        }
        if (reset != null)
        {
            if (ImGuiComponents.IconButton("##pgreset", FontAwesomeIcon.Undo)) reset();
            if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Put this page back to the defaults.");
            ImGui.SameLine(0, Theme.S(8f));
        }
        if (hasMaster)
        {
            var v = master;
            if (GreenCheckbox("##pgmaster", ref v)) { master = v; _toggleDirty = true; }
            if (Widgets.HoveredDelayed()) ImGui.SetTooltip(master ? "On. Untick to turn this off." : "Off.");
        }
        ImGui.SetCursorPos(new Vector2(start.X, start.Y + rowH));
        ImGui.Spacing();
        return master;
    }

    private string Version => typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "2.0.0.0";

    // Approximate width of an icon button, for centering.
    private float IconBtnWidth(FontAwesomeIcon icon, string text)
    {
        float iw;
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
            iw = ImGui.CalcTextSize(icon.ToIconString()).X;
        var st = ImGui.GetStyle();
        return iw + st.ItemInnerSpacing.X + ImGui.CalcTextSize(text).X + st.FramePadding.X * 2f;
    }

    // Home answers the four things you would otherwise go looking for: the
    // fight you are in, whether the meter is up, what is drawn on screen, and
    // what is wrong. Then the handful of things you would do about it.
    private void DrawHomePage()
    {
        var muted = Theme.V(Theme.Muted);

        // Title row, the same shape every other page opens with, with the
        // plugin's own icon ahead of it since this is the page that greets you.
        var frameH = ImGui.GetFrameHeight();
        var headStart = ImGui.GetCursorPos();
        var logo = IconWrap();
        if (logo != null)
        {
            var lsz = ImGui.GetFrameHeight();
            ImGui.Image(logo.Handle, new Vector2(lsz, lsz));
            ImGui.SameLine(0, Theme.S(9f));
        }
        var (headH, headEnd) = PageTitle("Fren Mits");

        ImGui.SetCursorPos(new Vector2(headEnd + Theme.S(12f),
            headStart.Y + (headH - ImGui.GetTextLineHeight()) * 0.5f));
        ImGui.TextColored(muted, $"v{Version}");
        var used = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;

        var ghW = IconBtnWidth(FontAwesomeIcon.ExternalLinkAlt, "GitHub");
        ImGui.SetCursorPos(new Vector2(
            MathF.Max(used + Theme.S(12f), ImGui.GetContentRegionMax().X - ghW),
            headStart.Y + (headH - frameH) * 0.5f));
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ExternalLinkAlt, "GitHub"))
            Dalamud.Utility.Util.OpenLink("https://github.com/swixum/FrenMits");
        ImGui.SetCursorPos(new Vector2(headStart.X, headStart.Y + headH));

        ImGui.Spacing();
        DrawHomeTiles();
        ImGui.Spacing();

        var noSlots = !C.Fights.Any(f => !string.IsNullOrEmpty(f.Slot));
        if (noSlots)
        {
            Widgets.ListBegin();
            if (Widgets.RowDoor("1. Pick Your Slot", "That column of the sheet becomes yours"))
            { _nav = NavKind.Fights; _navCategory = "Ultimate"; }
            Widgets.RowNote("2. Tick Test, then drag the call where you want it");
            Widgets.RowNote("3. Pull. It runs itself from there");
            Widgets.ListEnd();
            ImGui.Spacing();
        }

        Widgets.ListBegin();
        Widgets.RowBegin("Open Sheet View", "Your plan for this zone",
            Widgets.SmallWidth("Open"), ctlHeight: Widgets.SmallHeight, clickable: true,
            icon: FontAwesomeIcon.Table, iconCol: Theme.Accent);
        var openSheet = ImGui.SmallButton("Open##sv") || Widgets.RowClicked;
        Widgets.RowEnd();
        if (openSheet)
        {
            var f = _plugin.ActiveFight();
            _plugin.SheetViewWindow.Open(
                f != null && (Builtin.Has(f.TerritoryId) || f.CustomSlots.Count > 0) ? f : null);
        }

        var test = C.TestMode;
        if (Widgets.RowCheckClick("Place the Overlays", "Shows a sample so you can drag them", ref test,
            FontAwesomeIcon.ArrowsAlt, Theme.Accent))
        { C.TestMode = test; C.Save(); }

        if (!C.AudioEnabled)
        {
            var au = C.AudioEnabled;
            if (Widgets.RowCheckClick("Turn Audio Back On", "", ref au,
                FontAwesomeIcon.VolumeUp, Theme.Warn))
            { C.AudioEnabled = au; C.SaveSettings(); }
        }

        DrawSelectRoleRow();
        Widgets.ListEnd();
    }

    // Four tiles, two up. Each is a label, a line that matters, and a detail.
    private void DrawHomeTiles()
    {
        var fight = _plugin.ActiveFight();
        var job = _plugin.ActiveJobAbbreviation();
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var w = (ImGui.GetContentRegionAvail().X - gap) * 0.5f;
        var h = ImGui.GetTextLineHeightWithSpacing() * 3f + Theme.S(9f) * 2f;

        var zoneLine = fight?.Name ?? "No Sheet in This Zone";
        var zoneSub = fight == null ? "Nothing is called here"
            : string.IsNullOrEmpty(fight.Slot) ? "No slot picked yet"
            : $"{fight.Slot} as {job ?? "?"} - {fight.Lines.Count} lines";
        var zoneCol = fight != null && string.IsNullOrEmpty(fight.Slot)
            ? Theme.V(Theme.Warn) : Theme.V(Theme.TextBright);

        var on = new List<string>();
        var off = new List<string>();
        (C.ShowUpcoming ? on : off).Add("Next Mits");
        (C.MeterEnabled ? on : off).Add("Meter");
        (C.ShowCombatTimer ? on : off).Add("Timer");
        (C.PrepCheckEnabled ? on : off).Add("Food");

        var problems = new List<string>();
        if (!C.AudioEnabled) problems.Add("Audio is off");
        if (!C.EnableSync) problems.Add("Resync is off");
        var noSlot = C.Fights.Count(f => (Builtin.Has(f.TerritoryId) || f.CustomSlots.Count > 0)
                                         && string.IsNullOrEmpty(f.Slot));
        if (noSlot > 0) problems.Add($"{noSlot} fight{(noSlot == 1 ? "" : "s")} with no slot");

        if (HomeTile("##t1", w, h, FontAwesomeIcon.MapMarkerAlt, Theme.V(Theme.Accent),
            "This Zone", zoneLine, zoneCol, zoneSub) && fight != null)
            OpenFightPage(fight);
        Tip(fight == null ? "No sheet here." : "Open this fight.");
        ImGui.SameLine();
        if (HomeTile("##t2", w, h, FontAwesomeIcon.ChartBar,
            Theme.V(!C.MeterEnabled ? Theme.Muted : _plugin.Meter.Connected ? Theme.Good : Theme.Warn),
            "Fren Meter",
            !C.MeterEnabled ? "Off" : _plugin.Meter.Connected ? "Connected" : "Not Connected",
            Theme.V(!C.MeterEnabled ? Theme.Muted : _plugin.Meter.Connected ? Theme.Good : Theme.Warn),
            C.MeterEnabled ? _plugin.Meter.StatusText : "Turn it on to see damage")) _nav = NavKind.Meter;
        Tip("Open the meter's settings.");

        if (HomeTile("##t3", w, h, FontAwesomeIcon.Desktop, Theme.V(Theme.Accent),
            "On Screen",
            on.Count == 0 ? "Just the Call" : string.Join(", ", on),
            Theme.V(Theme.TextBright),
            off.Count == 0 ? "Everything is on" : "Off: " + string.Join(", ", off))) _nav = NavKind.Display;
        Tip("Open the call display.");
        ImGui.SameLine();
        if (HomeTile("##t4", w, h,
            problems.Count == 0 ? FontAwesomeIcon.CheckCircle : FontAwesomeIcon.ExclamationTriangle,
            Theme.V(problems.Count == 0 ? Theme.Good : Theme.Warn),
            "Needs a Look",
            problems.Count == 0 ? "All Good" : problems[0],
            Theme.V(problems.Count == 0 ? Theme.Good : Theme.Warn),
            problems.Count > 1 ? string.Join(", ", problems.Skip(1)) : ""))
            _nav = !C.AudioEnabled ? NavKind.Audio : NavKind.Fights;
        Tip(problems.Count == 0 ? "Nothing needs attention." : "Go and fix the first one.");
    }

    // Drawn by hand rather than as a child window, so the whole tile is one
    // click target and can show a hover state.
    private static bool HomeTile(string id, float w, float h, FontAwesomeIcon icon, Vector4 iconCol,
        string label, string line, Vector4 lineCol, string sub)
    {
        var p = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, new Vector2(w, h));
        var hot = ImGui.IsItemHovered();
        if (hot) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(p, p + new Vector2(w, h), hot ? 0xFF1E1A16 : Theme.PanelBg, Theme.S(8f));
        dl.AddRect(p, p + new Vector2(w, h), hot ? Theme.Accent : Widgets.CardBorder, Theme.S(8f));

        var pad = Theme.S(9f);
        var lineH = ImGui.GetTextLineHeightWithSpacing();
        var room = w - pad * 2f;

        // Status-tinted icon in the corner, echoing the sidebar's.
        var iconW = 0f;
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var ic = icon.ToIconString();
            iconW = ImGui.CalcTextSize(ic).X;
            dl.AddText(p + new Vector2(w - pad - iconW, pad), Widgets.ToColor(iconCol), ic);
        }

        dl.AddText(p + new Vector2(pad, pad), Theme.Muted,
            Widgets.Elide(label, room - iconW - Theme.S(6f)));
        dl.AddText(p + new Vector2(pad, pad + lineH), Widgets.ToColor(lineCol), Widgets.Elide(line, room));
        if (sub.Length > 0)
            dl.AddText(p + new Vector2(pad, pad + lineH * 2f), Theme.Muted, Widgets.Elide(sub, room));
        return clicked;
    }

    // What seat each role takes. The picks themselves live under Your Setup.
    private void DrawSelectRoleRow()
    {
        var picks = new[] { JobRole.Tank, JobRole.Healer, JobRole.Melee, JobRole.PhysicalRanged }
            .Select(r => C.GlobalRolePreferences.TryGetValue(r, out var p) ? p : "-")
            .ToArray();
        var text = string.Join("  ", picks);
        Widgets.RowBegin("Select Role", "Change these under Your Setup in the sidebar",
            ImGui.CalcTextSize(text).X, icon: FontAwesomeIcon.Users, iconCol: Theme.Accent);
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.V(Theme.Accent), text);
        Widgets.RowEnd();
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
