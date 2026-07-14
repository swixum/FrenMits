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

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    private int _selectedFight;

    // In-progress m:ss edit for the line table (one row at a time).
    private MitLine? _editTimeLine;
    private string _editTimeBuf = "";

    // In-memory line clipboard for the right-click copy / paste / duplicate menu.
    private MitLine? _copiedLine;

    private int _tankComp;

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
    private enum NavKind { Home, Fights, Timer, Display, Audio, Anchors, PartyRecap, Practice, CombatTimer, SheetView }
    private NavKind _nav = NavKind.Home;
    private int _anchorFight = -1; // target fight for anchor building
    private string _recName = "";  // name for saving the current capture
    private int _replayPick;       // selected saved recording
    private string[] _recordings = System.Array.Empty<string>();
    private string _navCategory = "Ultimate";

    private static readonly string[] Categories = { "Ultimate", "Savage", "Extreme" };

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
        : base("Fren Mits##config")
    {
        _plugin = plugin;
        Size = new Vector2(740, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    // Open the config straight to the Party Mit Recap page (the on-screen button).
    public void OpenPartyRecap()
    {
        _nav = NavKind.PartyRecap;
        IsOpen = true;
    }

    private DateTime _savedAt = DateTime.MinValue;

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

    private void DrawFooter()
    {
        ImGui.Separator();
        var justSaved = (DateTime.Now - _savedAt).TotalSeconds < 2;

        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Good);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF5FC46A);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xFF3FA44A);
        if (ImGui.Button("Save changes", new Vector2(150, 0)))
        {
            C.Save();
            _savedAt = DateTime.Now;
        }
        ImGui.PopStyleColor(3);

        ImGui.SameLine();
        if (justSaved)
        {
            ImGui.TextColored(ImGuiColors.ParsedGreen, "● saved");
        }
        else
        {
            ImGui.TextColored(ImGuiColors.DalamudYellow, "●");
            ImGui.SameLine(0, 5);
            ImGui.TextDisabled("autosaves as you edit");
        }
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
    private static void Tip(string text)
    {
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(text);
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
        ImGui.TextColored(on ? ImGuiColors.HealerGreen : ImGuiColors.DalamudGrey, "●");
        ImGui.SameLine(0, 4);
        ImGui.TextUnformatted(label);
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

            // Right-aligned quick actions.
            var right = ImGui.GetWindowWidth() - 150;
            if (right > 0) { ImGui.SameLine(); ImGui.SetCursorPosX(right); }
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Stopwatch)) _plugin.Timer.SyncNow();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Sync timer to now (/fm sync)");
            ImGui.SameLine();
            var test = C.TestMode;
            if (GreenCheckbox("Test", ref test)) { C.TestMode = test; C.Save(); }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Show a sample call so you can place / size the overlay");

            // Status dots on the second line.
            Dot(job != null, $"Job: {job ?? "?"}");
            ImGui.SameLine(0, 18);
            Dot(running, running ? $"Timer: {_plugin.Timer.Elapsed:0.0}s" : "Timer: idle");
            ImGui.SameLine(0, 18);
            Dot(C.AudioEnabled, "Audio");
            ImGui.SameLine(0, 18);
            Dot(C.EnableSync, "Resync");
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
        if (NavItem(FontAwesomeIcon.Home, "Home", null, _nav == NavKind.Home)) _nav = NavKind.Home;

        ImGui.Spacing();
        SidebarHeading("FIGHTS");
        foreach (var cat in Categories)
        {
            var count = C.Fights.Count(f => CategoryOf(f) == cat);
            if (NavItem(CategoryIcon(cat), cat, count, _nav == NavKind.Fights && _navCategory == cat))
            {
                _nav = NavKind.Fights;
                _navCategory = cat;
            }
        }

        ImGui.Spacing();
        SidebarHeading("SETTINGS");
        if (NavItem(FontAwesomeIcon.Stopwatch, "Timer", null, _nav == NavKind.Timer)) _nav = NavKind.Timer;
        if (NavItem(FontAwesomeIcon.Desktop, "Display", null, _nav == NavKind.Display)) _nav = NavKind.Display;
        if (NavItem(FontAwesomeIcon.VolumeUp, "Audio", null, _nav == NavKind.Audio)) _nav = NavKind.Audio;

        ImGui.Spacing();
        SidebarHeading("TOOLS");
        if (NavItem(FontAwesomeIcon.PlayCircle, "Practice", null, _nav == NavKind.Practice)) _nav = NavKind.Practice;
        if (NavItem(FontAwesomeIcon.Table, "Sheet View", null, _nav == NavKind.SheetView)) _nav = NavKind.SheetView;
        if (NavItem(FontAwesomeIcon.Clock, "Combat Timer", null, _nav == NavKind.CombatTimer)) _nav = NavKind.CombatTimer;
        if (NavItem(FontAwesomeIcon.Anchor, "Anchors", null, _nav == NavKind.Anchors)) _nav = NavKind.Anchors;
        if (NavItem(FontAwesomeIcon.ClipboardList, "Party Mit Recap", null, _nav == NavKind.PartyRecap)) _nav = NavKind.PartyRecap;

        DrawSidebarJob();
        DrawSidebarRole();
    }

    private static void SidebarHeading(string text)
    {
        ImGui.Spacing();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8);
        ImGui.TextColored(new Vector4(0.45f, 0.48f, 0.54f, 1f), text);
        ImGui.Spacing();
    }

    private bool NavItem(FontAwesomeIcon icon, string label, int? count, bool selected)
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

    private void DrawSidebarJob()
    {
        ImGui.Spacing();
        SidebarHeading("YOUR JOB");

        var options = new List<string> { "Auto (current job)" };
        options.AddRange(Jobs.Abbreviations);
        var idx = C.JobSelection == "Auto"
            ? 0
            : Math.Max(0, Array.IndexOf(Jobs.Abbreviations, C.JobSelection) + 1);

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 12);
        if (ImGui.Combo("##sbjob", ref idx, options.ToArray(), options.Count))
        {
            C.JobSelection = idx == 0 ? "Auto" : Jobs.Abbreviations[idx - 1];
            C.Save();
        }
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8);
        ImGui.TextDisabled($"active: {_plugin.ActiveJobAbbreviation() ?? "?"}");
    }

    // Global sheet-role pick: one choice applies to every built-in fight, mapping
    // to whatever slot that fight uses for the role (e.g. Melee 1 -> D1 in DMU, M1
    // in FRU). A green check shows when every built-in fight is on that role's slot.
    private void DrawSidebarRole()
    {
        ImGui.Spacing();
        SidebarHeading("YOUR ROLE");

        var roles = Builtin.Roles;
        var labels = new List<string> { "— pick a role —" };
        labels.AddRange(roles);
        var idx = string.IsNullOrEmpty(C.RoleSelection) ? 0 : Math.Max(0, Array.IndexOf(roles, C.RoleSelection) + 1);

        var active = !string.IsNullOrEmpty(C.RoleSelection) && RoleActiveEverywhere(C.RoleSelection);

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 12 - (active ? 24 : 0));
        if (ImGui.Combo("##sbrole", ref idx, labels.ToArray(), labels.Count))
        {
            if (idx == 0) { C.RoleSelection = ""; C.Save(); }
            else SelectRoleForAll(roles[idx - 1]);
        }
        if (active)
        {
            ImGui.SameLine();
            using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
                ImGui.TextColored(ImGuiColors.HealerGreen, FontAwesomeIcon.Check.ToIconString());
        }

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 8);
        ImGui.TextDisabled("applies to every fight");
    }

    // True if every built-in fight is currently on the slot this role maps to.
    private bool RoleActiveEverywhere(string role)
    {
        var fights = C.Fights.Where(f => Builtin.Has(f.TerritoryId)).ToList();
        return fights.Count > 0 && fights.All(f =>
            string.Equals(f.Slot, Builtin.RoleSlot(f.TerritoryId, role), StringComparison.OrdinalIgnoreCase));
    }

    // Apply the chosen role to every built-in fight, loading each one's matching
    // slot (keeping that slot's own edits).
    private void SelectRoleForAll(string role)
    {
        C.RoleSelection = role;
        FightProfile? last = null;
        foreach (var f in C.Fights)
        {
            if (!Builtin.Has(f.TerritoryId)) continue;
            var slot = Builtin.RoleSlot(f.TerritoryId, role);
            if (string.IsNullOrEmpty(slot)) continue;
            Builtin.ApplySlot(f, slot);
            last = f;
        }
        if (last != null) C.DmuSlot = last.Slot;
        C.Save();
        FlashBuiltin($"Set every fight to {role}.");
    }

    private void DrawSelectedPage()
    {
        switch (_nav)
        {
            case NavKind.Home: DrawHomePage(); break;
            case NavKind.Timer: DrawTimerTab(); break;
            case NavKind.Display: DrawDisplayTab(); break;
            case NavKind.Audio: DrawAudioTab(); break;
            case NavKind.Anchors: DrawAnchorsPage(); break;
            case NavKind.PartyRecap: DrawPartyRecapPage(); break;
            case NavKind.Practice: DrawPracticePage(); break;
            case NavKind.CombatTimer: DrawCombatTimerPage(); break;
            case NavKind.SheetView: DrawSheetViewPage(); break;
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

        // Action row: GitHub + Refresh side by side, centered together so they align.
        var ghW = IconBtnWidth(FontAwesomeIcon.ExternalLinkAlt, "GitHub");
        var rfW = IconBtnWidth(FontAwesomeIcon.Sync, "Refresh from sheet");
        Center(ghW + ImGui.GetStyle().ItemSpacing.X + rfW);
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ExternalLinkAlt, "GitHub"))
            Dalamud.Utility.Util.OpenLink("https://github.com/swixum/FrenMits");
        ImGui.SameLine();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, "Refresh from sheet"))
            ImGui.OpenPopup("##refreshall");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Rebake every built-in fight from the sheet, discarding line edits and any added potion / tank lines.");
        DrawRefreshConfirm();

        // Version + transient refresh result, centered below.
        ImGui.Dummy(new Vector2(0, 6));
        var ver = $"v{Version}";
        Center(ImGui.CalcTextSize(ver).X);
        ImGui.TextDisabled(ver);
        if ((DateTime.Now - _homeMsgAt).TotalSeconds < 5 && _homeMsg.Length > 0)
        {
            Center(ImGui.CalcTextSize(_homeMsg).X);
            ImGui.TextColored(ImGuiColors.ParsedGreen, _homeMsg);
        }

    }

    private void DrawRefreshConfirm()
    {
        var open = true;
        if (!ImGui.BeginPopupModal("##refreshall", ref open,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            return;

        ImGui.TextUnformatted("Rebake every built-in fight from the sheet?");
        ImGui.TextColored(ImGuiColors.DalamudYellow, "This discards your line edits and any added potion / tank lines.");
        ImGui.TextColored(ImGuiColors.DalamudRed, "This can't be undone.");
        ImGui.Spacing();

        // Cancel is leftmost and holds default focus, so a stray click or Enter
        // dismisses rather than wiping everything. Refresh is styled red (danger).
        if (ImGui.Button("Cancel", new Vector2(120, 0))) ImGui.CloseCurrentPopup();
        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF2222C8);        // red (ABGR)
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF3333DD);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xFF1A1AB0);
        if (ImGui.Button("Refresh", new Vector2(120, 0)))
        {
            var n = _plugin.ResetAllBuiltins();
            _homeMsg = $"Refreshed {n} fight(s) from the sheet.";
            _homeMsgAt = DateTime.Now;
            ImGui.CloseCurrentPopup();
        }
        ImGui.PopStyleColor(3);
        ImGui.EndPopup();
    }

    private string _homeMsg = "";
    private DateTime _homeMsgAt = DateTime.MinValue;

    // ---- Fights page ------------------------------------------------------

    private void DrawFightCategoryPage(string category)
    {
        var fights = C.Fights.Where(f => CategoryOf(f) == category).ToList();

        SeparatorText($"{category} — {fights.Count} fight{(fights.Count == 1 ? "" : "s")}");
        DrawCategoryToolbar(category);
        ImGui.Spacing();

        if (fights.Count == 0)
        {
            ImGui.TextDisabled("No fights here yet. Add one above, or load a preset.");
            return;
        }

        FightProfile? toDelete = null;
        foreach (var fight in fights)
        {
            ImGui.PushID(fight.Id);

            // Enable toggle + an expandable dropdown per fight.
            var enabled = fight.Enabled;
            if (GreenCheckbox("##en", ref enabled)) { fight.Enabled = enabled; C.Save(); }
            ImGui.SameLine();

            if (fight.Id == _expandFightId) { ImGui.SetNextItemOpen(true); _expandFightId = ""; }
            var open = ImGui.CollapsingHeader($"{fight.Name}   ({fight.Lines.Count})###fh-{fight.Id}");

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
                    DrawPotionsSection(fight);
                    DrawJobExtrasSection(fight);
                    DrawTankSection(fight);
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

    private void DrawCategoryToolbar(string category)
    {
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Plus, "Add fight"))
            AddFight(new FightProfile
            {
                Name = "New fight",
                TerritoryId = Service.ClientState.TerritoryType,
                Category = category,
            });
        ImGui.SameLine();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Paste, "Paste fight")) ImportFightFromClipboard();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Import a fight shared via clipboard into this category.");

        // Quick presets: any built-in for THIS category that isn't added yet.
        foreach (var (territory, name, cat) in Builtin.Fights)
        {
            if (cat != category) continue;
            if (C.Fights.Any(f => f.TerritoryId == territory)) continue;
            ImGui.SameLine();
            if (ImGui.Button($"+ {name}"))
                AddFight(new FightProfile { Name = name, TerritoryId = territory, Category = cat });
        }
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
    private static string SlotLabel(string code) => code switch
    {
        "D1" or "M1" => "Melee 1",
        "D2" or "M2" => "Melee 2",
        "D3" or "R" => "Phys Ranged",
        "D4" => "Caster",
        "MT" => "Main Tank",
        "OT" => "Off Tank",
        "T1" => "Tank 1",
        "T2" => "Tank 2",
        _ => code,
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

        if ((DateTime.Now - _builtinMsgAt).TotalSeconds < 4 && _builtinMsg.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudYellow, _builtinMsg);
        }

        DrawReplaceConfirm(fight, slot);
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
    private bool DrawFightEditor(FightProfile fight)
    {
        // Built-in fights (the ones shipped with the plugin) are locked: their name
        // can't be edited and they can't be deleted. Only user-added fights can.
        if (Builtin.Has(fight.TerritoryId))
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(fight.Name);
            ImGui.SameLine(0, 8);
            ImGui.TextDisabled("(built-in)");
            Tip("Line times are seconds from the pull — one continuous timeline across every phase; resets on a wipe.");
            return true;
        }

        var name = fight.Name;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.InputText("Name", ref name, 128)) { fight.Name = name; C.Save(); }
        Tip("Line times are seconds from the pull — one continuous timeline across every phase; resets on a wipe.");

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF2A2AB0);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF3A3AC8);
        var deleted = ImGuiComponents.IconButtonWithText(FontAwesomeIcon.TrashAlt, "Delete");
        ImGui.PopStyleColor(2);
        return !deleted;
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
        _tankComp = Math.Clamp(_tankComp, 0, comps.Length - 1);
        ImGui.SetNextItemWidth(140f);
        ImGui.Combo("Tank pairing", ref _tankComp, comps, comps.Length);

        var comp = comps[_tankComp];
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

    // Practice page (Tools): jump the overlay to a phase to preview/place its calls
    // without a real pull. One card per built-in fight that has phase data.
    private void DrawPracticePage()
    {
        SeparatorText("Practice");
        ImGui.TextWrapped("Jump the overlay to a phase to preview and place its calls — no pull needed. "
                          + "Picking a phase turns on Test Mode; press Stop when you're done.");
        ImGui.Spacing();

        var any = false;
        foreach (var fight in C.Fights)
        {
            var phases = Builtin.PhaseStarts(fight.TerritoryId);
            if (phases.Count == 0) continue;
            any = true;

            BeginCard(FontAwesomeIcon.PlayCircle, ImGuiColors.DalamudOrange, fight.Name);
            var previewing = Plugin.PreviewFight == fight && C.TestMode;
            for (var i = 0; i < phases.Count; i++)
            {
                if (i > 0) ImGui.SameLine();
                if (ImGui.Button($"{phases[i].Name}##prac{fight.Id}{i}"))
                    _plugin.PracticeJump(fight, phases[i].Time);
                Tip($"Preview from {(int)phases[i].Time / 60}:{(int)phases[i].Time % 60:00} (~6s before the first call).");
            }
            if (previewing)
            {
                ImGui.SameLine();
                if (ImGui.Button($"Stop##{fight.Id}")) _plugin.StopPractice();
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudYellow, "previewing");
            }
            EndCard();
        }

        if (!any)
            ImGui.TextDisabled("No fights with phase data. Practice works for Dancing Mad and the legacy ultimates.");
    }

    // Potions card: baked top-log potion windows for your job with a one-click add.
    private void DrawPotionsSection(FightProfile fight)
    {
        if (PotionTimings.BossSlug(fight.TerritoryId) == null) return;

        var job = _plugin.ActiveJobAbbreviation();
        var stat = PotionTimings.Stat(job);

        BeginCard(FontAwesomeIcon.Flask, ImGuiColors.DalamudViolet, "Potions", "top-log windows · raalm.com / Lorrgs");

        if (string.IsNullOrEmpty(job) || string.IsNullOrEmpty(stat))
        {
            ImGui.TextDisabled("Pick your job (top of the sidebar) to see its potion timings.");
            EndCard();
            return;
        }

        var times = PotionTimings.DefaultsFor(fight.TerritoryId, job);

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
        var extra = JobExtras.For(fight.TerritoryId, job);
        if (extra == null) return;

        BeginCard(FontAwesomeIcon.Shield, ImGuiColors.HealerGreen, "Job mitigation", "optional, from logs · raalm.com");

        ImGui.TextColored(new Vector4(0.62f, 0.66f, 0.72f, 1f), $"{job} · {extra.Action}");
        ImGui.SameLine(0, 10);
        ImGui.TextDisabled($"{extra.Lines.Length} casts, spaced to its {extra.Recast:0}s recast");

        ImGui.Spacing();
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

        var locked = Builtin.Has(fight.TerritoryId);
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
                    Enabled = fight.Enabled,
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
                });
            }
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
        if (ImGui.InputInt("Territory id", ref territory)) { fight.TerritoryId = (uint)Math.Max(0, territory); C.Save(); }
        ImGui.SameLine();
        if (ImGui.Button($"Use current zone ({Service.ClientState.TerritoryType})"))
        {
            fight.TerritoryId = Service.ClientState.TerritoryType;
            C.Save();
        }
        var zoneName = TerritoryName(fight.TerritoryId);
        if (!string.IsNullOrEmpty(zoneName)) { ImGui.SameLine(); ImGui.TextDisabled(zoneName); }
        ImGui.EndDisabled();

        var offset = fight.TimerOffset;
        ImGui.SetNextItemWidth(120f);
        if (ImGui.InputFloat("Timer offset (s)", ref offset, 0.1f, 1f, "%.1f"))
        {
            fight.TimerOffset = Math.Clamp(offset, -30f, 30f);
            C.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("+ fires every call earlier, - later. Survives resync.");
        HelpMarker("Shifts when this fight's calls fire: +10 makes every call come 10s sooner, "
                   + "even with resync on. Heads up: a big + shift can swallow calls timed inside "
                   + "the first seconds of a pull. The timer auto-starts on combat and resets on a "
                   + "wipe / when the duty ends; /fm sync zeroes the live timer.");

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
        if (!ImGui.BeginTable("##lines", 7, flags, new Vector2(0, tableH)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("On", ImGuiTableColumnFlags.WidthFixed, 28);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 70);
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
            if (GreenCheckbox("##on", ref on)) { line.Enabled = on; C.Save(); }

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
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Type m:ss (e.g. 2:30) or seconds — right-click to reset");
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
            if (ImGui.SmallButton("…")) ImGui.OpenPopup("lineopt");
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
        target.Tts = src.Tts;
        target.Sound = src.Sound;
        target.Color = src.Color;
        target.IconId = src.IconId;
    }

    private void DrawJobsCell(MitLine line)
    {
        var label = line.Jobs.Count == 0 ? "All" : string.Join(",", line.Jobs);
        if (label.Length > 14) label = label[..12] + "…";
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

    // ---- Timer / Display tabs -------------------------------------------

    private void DrawTimerTab()
    {
        var fight = _plugin.ActiveFight();

        if (Section("Timing", true))
        {
            var warn = C.WarningSeconds;
            ImGui.SetNextItemWidth(200f);
            if (ImGui.SliderFloat("Show ahead by (s)", ref warn, 1f, 12f, "%.1f")) { C.WarningSeconds = warn; C.Save(); }
            Tip("How early a call appears before its mit time. Per-line leads override this.");
            var hold = C.HoldSeconds;
            ImGui.SetNextItemWidth(200f);
            if (ImGui.SliderFloat("Hold on screen (s)", ref hold, 0f, 6f, "%.1f")) { C.HoldSeconds = hold; C.Save(); }
            Tip("How long a call stays up after its time passes.");
            C.OnlyInTargetTerritory = CfgCheck("Only run in the fight's territory", C.OnlyInTargetTerritory);
        }

        if (Section("Clock", true))
        {
            ImGui.TextUnformatted($"Elapsed: {(_plugin.Timer.Running ? _plugin.Timer.Elapsed.ToString("0.0") + "s" : "not running")}");
            if (ImGui.Button("Sync now")) _plugin.Timer.SyncNow();
            Tip("Zero the clock to the current moment (also /fm sync). Auto-starts on combat otherwise.");
            ImGui.SameLine();
            if (ImGui.Button("Reset")) _plugin.Timer.Reset();
        }

        if (Section("Resync", true))
        {
            C.EnableSync = CfgCheck("Resync the clock on boss casts", C.EnableSync);
            Tip("When a known boss ability casts, the clock snaps so it resolves on its scripted time — correcting phase drift from kill speed.");
            C.Diagnostics = CfgCheck("Write per-pull diagnostics file", C.Diagnostics);
            Tip("Saves a resync + cue log per pull to the plugin's diagnostics/ folder. Local only — nothing is sent anywhere. Use it to check resync accuracy.");
            if (ImGui.TreeNode("Advanced windows"))
            {
                var win = C.SyncWindowSeconds;
                ImGui.SetNextItemWidth(200f);
                if (ImGui.SliderFloat("Mechanic window (s)", ref win, 2f, 20f, "%.0f")) { C.SyncWindowSeconds = win; C.Save(); }
                Tip("Tight window for fine drift correction on a normal mechanic.");
                var pwin = C.SyncPhaseWindowSeconds;
                ImGui.SetNextItemWidth(200f);
                if (ImGui.SliderFloat("Phase window (s)", ref pwin, 15f, 120f, "%.0f")) { C.SyncPhaseWindowSeconds = pwin; C.Save(); }
                Tip("Wider window so a phase that starts well off the sheet's nominal time still locks on.");
                ImGui.TreePop();
            }

            ImGui.Spacing();
            ImGui.TextDisabled($"Last sync: {(_plugin.Sync.LastSync.Length > 0 ? _plugin.Sync.LastSync : "-")}");
            if (fight is { SyncPoints.Count: > 0 })
            {
                var phases = fight.SyncPoints.Count(s => s.IsPhase);
                ImGui.TextDisabled($"This fight: {fight.SyncPoints.Count} anchors ({phases} phase). Build more in the Anchors tab.");
            }
        }
    }

    // ---- Anchors tool -----------------------------------------------------

    private void DrawAnchorsPage()
    {
        SeparatorText("Resync anchors");
        ImGui.TextWrapped("Record a clean pull, then promote boss casts to anchors so the timeline keeps re-syncing "
                          + "through every phase (great for phases public timelines don't cover, like DMU P4-P5). "
                          + "Anchors are saved per fight.");

        // Target fight: defaults to the one you're in, but you can build for any.
        var fights = C.Fights;
        if (fights.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Add a fight first (Fights tab).");
            return;
        }
        var active = _plugin.ActiveFight();
        if (_anchorFight < 0 || _anchorFight >= fights.Count)
            _anchorFight = active != null ? Math.Max(0, fights.IndexOf(active)) : 0;

        var names = fights.Select(f => active == f ? $"{f.Name}  (you're here)" : f.Name).ToArray();
        ImGui.SetNextItemWidth(280f);
        ImGui.Combo("Target fight", ref _anchorFight, names, names.Length);
        var target = fights[_anchorFight];

        // --- Capture ---
        if (Section("Capture a pull", true))
        {
            var rec = _plugin.Sync.Recording;
            ImGui.PushStyleColor(ImGuiCol.Text, rec ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudGrey);
            if (GreenCheckbox("Recording boss casts", ref rec)) _plugin.Sync.Recording = rec;
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.TextDisabled($"{_plugin.Sync.Captured.Count} captured");
            ImGui.SameLine();
            if (ImGui.SmallButton("Clear")) { _plugin.Sync.Captured.Clear(); _plugin.Sync.CutsceneMarks.Clear(); }
            ImGui.SameLine();
            if (ImGui.SmallButton("Export"))
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("# FrenMits capture  (time_s\tability\tcaster\tkind)");
                foreach (var cc in _plugin.Sync.Captured)
                    sb.AppendLine($"{cc.Time:0.0}\t{(cc.IsBoss ? $"boss:{cc.Id}" : $"0x{cc.Id:X}")}\t{cc.Caster}\t{(cc.IsBoss ? "appear" : "cast")}");
                ImGui.SetClipboardText(sb.ToString());
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Copy all captures (time + ids) to paste/share for baking.");

            ImGui.TextDisabled("Tick on, do a clean pull (or replay), then add casts below as anchors.");

            if (_plugin.Sync.Captured.Count > 0 &&
                ImGui.BeginTable("##caps", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
                    new Vector2(0, 220)))
            {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableSetupColumn("Ability", ImGuiTableColumnFlags.WidthFixed, 90);
                ImGui.TableSetupColumn("Caster", ImGuiTableColumnFlags.WidthStretch, 1);
                ImGui.TableSetupColumn("Add anchor", ImGuiTableColumnFlags.WidthFixed, 130);
                ImGui.TableHeadersRow();

                for (var i = _plugin.Sync.Captured.Count - 1; i >= 0; i--)
                {
                    var cap = _plugin.Sync.Captured[i];
                    ImGui.TableNextRow();
                    ImGui.PushID(i);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{(int)cap.Time / 60}:{(int)cap.Time % 60:00}");
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(cap.IsBoss ? $"boss {cap.Id}" : $"0x{cap.Id:X}");
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(cap.Caster);
                    ImGui.TableNextColumn();
                    if (cap.IsBoss)
                    {
                        if (ImGui.SmallButton("+ boss")) AddBossAnchor(target, cap);
                    }
                    else
                    {
                        if (ImGui.SmallButton("+ phase")) AddAnchor(target, cap, true);
                        ImGui.SameLine();
                        if (ImGui.SmallButton("+ mech")) AddAnchor(target, cap, false);
                    }
                    ImGui.PopID();
                }
                ImGui.EndTable();
            }
        }

        // --- Save / replay a pull (desk testing) ---
        if (Section("Record & replay (desk test)", false))
        {
            ImGui.TextWrapped("Tick \"Recording\" above, do a pull (casts and cutscenes are captured), then save it here. "
                              + "Replay it any time to watch the overlay, cues and cutscene handling line up — no instance needed.");

            ImGui.SetNextItemWidth(200f);
            ImGui.InputTextWithHint("##recname", "recording name", ref _recName, 64);
            ImGui.SameLine();
            var canSave = _plugin.Sync.Captured.Count > 0 || _plugin.Sync.CutsceneMarks.Count > 0;
            if (!canSave) ImGui.BeginDisabled();
            if (ImGui.Button("Save capture"))
            {
                var rec = PullRecording.FromCapture(
                    string.IsNullOrWhiteSpace(_recName) ? $"{target.Name} pull" : _recName,
                    target.TerritoryId, target.Name,
                    _plugin.Sync.Captured, _plugin.Sync.CutsceneMarks);
                rec.Save();
                _recordings = PullRecording.List().ToArray();
                FlashBuiltin($"Saved recording with {rec.Events.Count} events.");
            }
            if (!canSave) ImGui.EndDisabled();

            ImGui.Spacing();
            if (_recordings.Length == 0) _recordings = PullRecording.List().ToArray();
            if (ImGui.SmallButton("Refresh")) _recordings = PullRecording.List().ToArray();
            ImGui.SameLine();
            if (_recordings.Length == 0)
            {
                ImGui.TextDisabled("No saved recordings yet.");
            }
            else
            {
                _replayPick = Math.Clamp(_replayPick, 0, _recordings.Length - 1);
                ImGui.SetNextItemWidth(200f);
                ImGui.Combo("##replaypick", ref _replayPick, _recordings, _recordings.Length);

                if (_plugin.Replay.Playing)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, 0xFF2A2AB0);
                    if (ImGui.Button("Stop replay")) _plugin.Replay.Stop();
                    ImGui.PopStyleColor();
                }
                else if (ImGui.Button("Play"))
                {
                    var rec = PullRecording.Load(_recordings[_replayPick]);
                    if (rec != null) _plugin.Replay.Start(rec);
                }
                ImGui.SameLine();
                if (ImGui.SmallButton("Delete"))
                {
                    PullRecording.Delete(_recordings[_replayPick]);
                    _recordings = PullRecording.List().ToArray();
                    _replayPick = 0;
                }
            }

            if (_plugin.Replay.Status.Length > 0)
                ImGui.TextDisabled(_plugin.Replay.Status);
        }

        // --- Last pull: mits you used ---
        if (Section("Last pull — mits you used", false))
        {
            var review = _plugin.Review.Last;
            if (review.Count == 0)
            {
                ImGui.TextDisabled("Do a pull — the mits you use (and when) are logged here to compare against the plan.");
            }
            else if (ImGui.BeginTable("##review", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
                         new Vector2(0, 200)))
            {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableSetupColumn("Mit", ImGuiTableColumnFlags.WidthStretch, 1);
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableHeadersRow();
                foreach (var u in review)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn(); ImGui.TextUnformatted($"{(int)u.Time / 60}:{(int)u.Time % 60:00}");
                    ImGui.TableNextColumn();
                    var col = MitTypes.Color(u.Kind, C);
                    if (col != 0) ImGui.PushStyleColor(ImGuiCol.Text, col);
                    ImGui.TextUnformatted(u.Name);
                    if (col != 0) ImGui.PopStyleColor();
                    ImGui.TableNextColumn(); ImGui.TextDisabled(u.Kind.ToString());
                }
                ImGui.EndTable();
            }
        }

        // --- Current anchors on the target fight ---
        if (Section($"Current anchors on {target.Name}", true))
        {
            var total = target.SyncPoints.Count + target.BossAnchors.Count;
            if (total == 0)
            {
                ImGui.TextDisabled("None yet. Add some from a capture above.");
            }
            else
            {
                var phases = target.SyncPoints.Count(s => s.IsPhase);
                ImGui.TextDisabled($"{target.SyncPoints.Count} cast ({phases} phase) + {target.BossAnchors.Count} boss.");
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, 0xFF2A2AB0);
                if (ImGui.SmallButton("Clear all")) { target.SyncPoints.Clear(); target.BossAnchors.Clear(); C.Save(); }
                ImGui.PopStyleColor();

                if (ImGui.BeginTable("##anchors", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
                        new Vector2(0, 200)))
                {
                    ImGui.TableSetupScrollFreeze(0, 1);
                    ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 60);
                    ImGui.TableSetupColumn("Trigger", ImGuiTableColumnFlags.WidthFixed, 90);
                    ImGui.TableSetupColumn("Kind / label", ImGuiTableColumnFlags.WidthStretch, 1);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 30);
                    ImGui.TableHeadersRow();

                    SyncPoint? rmSp = null;
                    BossAnchor? rmBa = null;
                    var n = 0;
                    foreach (var sp in target.SyncPoints.OrderBy(s => s.Time))
                    {
                        ImGui.TableNextRow(); ImGui.PushID(n++);
                        ImGui.TableNextColumn(); ImGui.TextUnformatted($"{(int)sp.Time / 60}:{(int)sp.Time % 60:00}");
                        ImGui.TableNextColumn(); ImGui.TextUnformatted($"0x{sp.Ability:X}");
                        ImGui.TableNextColumn(); ImGui.TextUnformatted((sp.IsPhase ? "phase  " : "mech  ") + sp.Label);
                        ImGui.TableNextColumn(); if (ImGui.SmallButton("X")) rmSp = sp;
                        ImGui.PopID();
                    }
                    foreach (var ba in target.BossAnchors.OrderBy(b => b.Time))
                    {
                        ImGui.TableNextRow(); ImGui.PushID(n++);
                        ImGui.TableNextColumn(); ImGui.TextUnformatted($"{(int)ba.Time / 60}:{(int)ba.Time % 60:00}");
                        ImGui.TableNextColumn(); ImGui.TextUnformatted($"boss {ba.NameId}");
                        ImGui.TableNextColumn(); ImGui.TextUnformatted("boss  " + ba.Label);
                        ImGui.TableNextColumn(); if (ImGui.SmallButton("X")) rmBa = ba;
                        ImGui.PopID();
                    }
                    ImGui.EndTable();

                    if (rmSp != null) { target.SyncPoints.Remove(rmSp); C.Save(); }
                    if (rmBa != null) { target.BossAnchors.Remove(rmBa); C.Save(); }
                }
            }
            ImGui.TextDisabled("Last sync: " + (_plugin.Sync.LastSync.Length > 0 ? _plugin.Sync.LastSync : "-"));

            // Self-tuning readout: how well the baked timeline matches your pace.
            if (_plugin.Sync.DriftSamples >= 3)
            {
                var drift = _plugin.Sync.AvgDrift;
                // drift + = the clock reads past the sheet time when a mechanic
                // actually resolves, i.e. mechanics land late vs the sheet, i.e.
                // the group runs behind it. Calls between anchors then fire early,
                // so the corrective shift is -drift (calls later), folded into the
                // offset the cue clock reads.
                var dir = drift > 0 ? "behind" : "ahead of";
                ImGui.TextDisabled($"Timeline fit: your group runs {Math.Abs(drift):0.0}s {dir} the sheet (avg of {_plugin.Sync.DriftSamples} corrections).");
                if (Math.Abs(drift) >= 1.5f)
                {
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"Shift {target.Name} by {-drift:+0.0;-0.0}s"))
                    {
                        target.TimerOffset = Math.Clamp(target.TimerOffset - drift, -30f, 30f);
                        C.Save();
                        FlashBuiltin($"Nudged timer offset by {-drift:+0.0;-0.0}s to match your pace.");
                    }
                }
            }
        }
    }

    private void AddAnchor(FightProfile fight, SyncEngine.Capture cap, bool isPhase)
    {
        fight.SyncPoints.RemoveAll(s => s.Ability == cap.Id && MathF.Abs(s.Time - cap.Time) < 4f);
        fight.SyncPoints.Add(new SyncPoint
        {
            Ability = cap.Id,
            Time = cap.Time,
            IsPhase = isPhase,
            Label = $"{cap.Caster} (captured)"
        });
        C.Save();
    }

    private void AddBossAnchor(FightProfile fight, SyncEngine.Capture cap)
    {
        fight.BossAnchors.RemoveAll(b => b.NameId == cap.Id);
        fight.BossAnchors.Add(new BossAnchor { NameId = cap.Id, Time = cap.Time, Label = $"{cap.Caster} (captured)" });
        C.Save();
    }

    private void ResetDisplayDefaults()
    {
        C.OverlayFontSizePx = 40f; C.UpcomingFontSizePx = 20f; C.IconScale = 0.8f;
        C.OverlayColorImminent = 0xFF55FFFF; C.OverlayColorActive = 0xFF55FF55;
        C.OverlayColorMechanic = 0xC0FFFFFF; C.OverlayColorUpcoming = 0xB0FFFFFF;
        C.HeadlineFormat = "{action} ({remaining})"; C.ActiveSuffix = "  NOW";
        C.ShowCountdownNumber = false; C.ShowMechanicLine = true; C.ShowAbilityIcon = true;
        C.TextShadow = true; C.ShowProgressBar = true; C.ProgressBarHeight = 6f;
        C.PulseWhenImminent = true; C.ShowBackground = false; C.BackgroundColor = 0xB0000000;
        C.WarningSeconds = 3f; C.HoldSeconds = 2f;
        C.OverlayPosition = new Vector2(0.5f, 0.35f);
        C.Save();
        _plugin.OverlayWindow.RequestReposition();
    }

    // ---- Party Mit Recap --------------------------------------------------

    private void DrawPartyRecapPage()
    {
        SeparatorText("Party Mit Recap");
        ImGui.TextWrapped("After a wipe, a full recap of the pull's mitigation in its own window: the damage-downs "
                          + "on the boss (Reprisal / Feint / Addle / Dismantle) plus the party's defensive cooldowns "
                          + "(Rampart, Sacred Soil, Kerachole, ...), who used them and when, and which standard raid "
                          + "mits never landed.");
        ImGui.Spacing();

        C.RecapAutoCapture = CfgCheck("Auto-capture the recap every pull", C.RecapAutoCapture);
        Tip("On by default — the recap is captured automatically as you fight, so you never have to trigger it. Untick to turn the whole tool off.");

        C.ShowRecapButton = CfgCheck("Show the recap popup after every wipe", C.ShowRecapButton);
        Tip("When on, a small \"Mit Recap\" popup appears after every pull ends so you can open the recap. Off = it never appears.");

        if (C.ShowRecapButton)
        {
            var locked = C.RecapPopupLocked;
            if (GreenCheckbox("Lock popup position", ref locked)) { C.RecapPopupLocked = locked; _plugin.RecapButtonWindow.RequestReposition(); C.Save(); }
            ImGui.SameLine();
            ImGui.TextDisabled("(unlock, then drag the popup to place it)");
        }

        ImGui.Spacing();
        if (ImGui.Button("Open recap window")) _plugin.RecapWindow.IsOpen = true;
        Tip("Opens the movable recap window with the last pull's data.");
        ImGui.SameLine();
        if (ImGui.Button("Sample recap"))
        {
            _plugin.Recap.LoadSample();             // fill with a fake randomised pull
            _plugin.RecapWindow.IsOpen = true;
        }
        Tip("Fills the recap with a randomised fake pull so you can see exactly how it looks in-game — icons, colors, missing mits — without a real pull.");
        ImGui.SameLine();
        if (ImGui.Button("Test placement"))
        {
            _plugin.Recap.ShowTestPopup();          // make the popup appear so you can drag it
            _plugin.RecapWindow.IsOpen = true;      // and open the window to place it too
        }
        Tip("Pops up the popup + window now (no wipe needed) so you can drag both into place.");
        ImGui.SameLine();
        ImGui.TextDisabled(_plugin.Recap.CapturedAt == default
            ? "no capture yet"
            : $"last captured {(int)(DateTime.UtcNow - _plugin.Recap.CapturedAt).TotalSeconds}s ago");
    }

    private void DrawSheetViewPage()
    {
        SeparatorText("Sheet View");
        ImGui.TextWrapped("The whole raid plan as one sheet, like the Google sheet everyone plans from: "
                          + "rows are the fight's mechanics, columns are all the slots. Your slot is starred; "
                          + "its column is the live plan your overlay calls.");
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Accent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentHover);
        if (ImGui.Button("Open Sheet View", new Vector2(180, 0)))
        {
            var fight = _plugin.ActiveFight();
            _plugin.SheetViewWindow.Open(fight != null && Builtin.Has(fight.TerritoryId) ? fight : null);
        }
        ImGui.PopStyleColor(2);
        ImGui.SameLine();
        ImGui.TextDisabled("or /fm sheet");

        ImGui.Spacing();
        if (Section("How it works", true))
        {
            ImGui.Bullet(); ImGui.TextWrapped("Click a TIME to re-time that mechanic for every slot at once - "
                + "the \"shift all instances of this one thing\" edit.");
            ImGui.Bullet(); ImGui.TextWrapped("Click a CELL to change one slot's mit only. Clear the text to remove it.");
            ImGui.Bullet(); ImGui.TextWrapped("Anything you touch turns orange: it's yours now, and sheet updates "
                + "won't revert it. The row's ⟲ puts a mechanic back on the sheet.");
            ImGui.Bullet(); ImGui.TextWrapped("Edits to OTHER slots live in your saved copy until you press "
                + "\"Share plan\" and friends import the code - then their own slot updates in place.");
        }
    }

    private void DrawCombatTimerPage()
    {
        SeparatorText("Combat Timer");
        ImGui.TextWrapped("A plain stopwatch of the current pull's combat time (mm:ss), shown as its own "
                          + "overlay. Use the \"Test\" toggle in the header to preview while you place and style it.");
        ImGui.Spacing();

        C.ShowCombatTimer = CfgCheck("Show the combat timer", C.ShowCombatTimer);
        if (!C.ShowCombatTimer) return;

        if (Section("Placement", true))
        {
            C.CombatTimerLocked = CfgCheck("Lock position (click-through)", C.CombatTimerLocked);
            ImGui.SameLine();
            ImGui.TextDisabled(C.CombatTimerLocked ? "locked, unlock to drag" : "drag it, or use the sliders");
            ImGui.TextDisabled("Auto-locks in combat — move it out of combat or with Test preview.");

            var pos = C.CombatTimerPosition;
            ImGui.SetNextItemWidth(200f);
            if (ImGui.SliderFloat("Horizontal", ref pos.X, 0f, 1f, "%.2f"))
            { C.CombatTimerPosition = pos; C.Save(); _plugin.CombatTimerWindow.RequestReposition(); }
            ImGui.SetNextItemWidth(200f);
            if (ImGui.SliderFloat("Vertical", ref pos.Y, 0f, 1f, "%.2f"))
            { C.CombatTimerPosition = pos; C.Save(); _plugin.CombatTimerWindow.RequestReposition(); }
            if (ImGui.Button("Center top"))
            {
                C.CombatTimerPosition = new Vector2(0.5f, 0.08f);
                C.Save();
                _plugin.CombatTimerWindow.RequestReposition();
            }
        }

        if (Section("Font & size", true))
        {
            var fonts = FontManager.FamilyNames;
            var fIdx = Math.Max(0, Array.IndexOf(fonts, C.CombatTimerFontFamily));
            ImGui.SetNextItemWidth(220f);
            if (ImGui.Combo("Font", ref fIdx, fonts, fonts.Length)) { C.CombatTimerFontFamily = fonts[fIdx]; C.Save(); }
            var bold = C.CombatTimerFontBold;
            if (GreenCheckbox("Bold", ref bold)) { C.CombatTimerFontBold = bold; C.Save(); }
            ImGui.SameLine();
            var italic = C.CombatTimerFontItalic;
            if (GreenCheckbox("Italic", ref italic)) { C.CombatTimerFontItalic = italic; C.Save(); }
            if (C.CombatTimerFontFamily == "Default" && (C.CombatTimerFontBold || C.CombatTimerFontItalic))
            {
                ImGui.SameLine();
                ImGui.TextDisabled("(pick a font for bold/italic)");
            }
            var px = C.CombatTimerFontSizePx;
            ImGui.SetNextItemWidth(220f);
            if (ImGui.SliderFloat("Text size", ref px, 12f, 120f, "%.0f px")) { C.CombatTimerFontSizePx = px; C.Save(); }
        }

        if (Section("Colors", true))
        {
            var col = ColorToVec4(C.CombatTimerColor);
            if (ImGui.ColorEdit4("Text color", ref col)) { C.CombatTimerColor = Vec4ToColor(col); C.Save(); }

            C.CombatTimerShowBackground = CfgCheck("Draw a background box", C.CombatTimerShowBackground);
            if (C.CombatTimerShowBackground)
            {
                var bg = ColorToVec4(C.CombatTimerBackgroundColor);
                if (ImGui.ColorEdit4("Background color", ref bg)) { C.CombatTimerBackgroundColor = Vec4ToColor(bg); C.Save(); }
                ImGui.TextDisabled("Drag the alpha channel down for a translucent box.");
            }
        }
    }

    private void DrawDisplayTab()
    {
        // One-click reset of everything on this tab. To preview while you adjust,
        // use the "Test" toggle in the header (always visible).
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Undo, "Reset display")) ResetDisplayDefaults();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Reset every setting on this tab to defaults.");

        if (Section("Placement", true))
        {
            C.OverlayLocked = CfgCheck("Lock overlay (click-through)", C.OverlayLocked);
            ImGui.SameLine();
            ImGui.TextDisabled(C.OverlayLocked ? "locked, unlock to drag" : "drag it, or use the sliders");
            ImGui.TextDisabled("Auto-locks in combat — use Live preview or the sliders to move it during a pull.");

            var pos = C.OverlayPosition;
            ImGui.SetNextItemWidth(200f);
            if (ImGui.SliderFloat("Horizontal", ref pos.X, 0f, 1f, "%.2f"))
            { C.OverlayPosition = pos; C.Save(); _plugin.OverlayWindow.RequestReposition(); }
            ImGui.SetNextItemWidth(200f);
            if (ImGui.SliderFloat("Vertical", ref pos.Y, 0f, 1f, "%.2f"))
            { C.OverlayPosition = pos; C.Save(); _plugin.OverlayWindow.RequestReposition(); }
            if (ImGui.Button("Center"))
            {
                C.OverlayPosition = new Vector2(0.5f, 0.35f);
                C.Save();
                _plugin.OverlayWindow.RequestReposition();
            }
        }

        if (Section("Font & size", true))
        {
            var fonts = FontManager.FamilyNames;
            var fIdx = Math.Max(0, Array.IndexOf(fonts, C.OverlayFontFamily));
            ImGui.SetNextItemWidth(220f);
            if (ImGui.Combo("Font", ref fIdx, fonts, fonts.Length)) { C.OverlayFontFamily = fonts[fIdx]; C.Save(); }
            var bold = C.OverlayFontBold;
            if (GreenCheckbox("Bold", ref bold)) { C.OverlayFontBold = bold; C.Save(); }
            ImGui.SameLine();
            var italic = C.OverlayFontItalic;
            if (GreenCheckbox("Italic", ref italic)) { C.OverlayFontItalic = italic; C.Save(); }
            if (C.OverlayFontFamily == "Default" && (C.OverlayFontBold || C.OverlayFontItalic))
            {
                ImGui.SameLine();
                ImGui.TextDisabled("(pick a font for bold/italic)");
            }
            var align = C.OverlayTextAlign;
            ImGui.SetNextItemWidth(140f);
            if (ImGui.Combo("Alignment", ref align, new[] { "Left", "Center", "Right" }, 3))
            { C.OverlayTextAlign = align; C.Save(); }

            var callPx = C.OverlayFontSizePx;
            ImGui.SetNextItemWidth(220f);
            if (ImGui.SliderFloat("Call text size", ref callPx, 12f, 120f, "%.0f px")) { C.OverlayFontSizePx = callPx; C.Save(); }
            // The timeline's own text size + color live with the timeline toggle in
            // the "Next-mits timeline" section below.
            if (C.ShowAbilityIcon)
            {
                var iconScale = C.IconScale;
                ImGui.SetNextItemWidth(220f);
                if (ImGui.SliderFloat("Icon size", ref iconScale, 0.4f, 1.5f, "%.2fx")) { C.IconScale = iconScale; C.Save(); }
            }
        }

        if (Section("Text & content"))
        {
            if (ImGui.TreeNode("Advanced format"))
            {
                var fmt = C.HeadlineFormat;
                ImGui.SetNextItemWidth(280f);
                if (ImGui.InputText("Call format", ref fmt, 128)) { C.HeadlineFormat = fmt; C.Save(); }
                ImGui.TextDisabled("Placeholders: {action} {mechanic} {time} {count} {remaining}");
                var suffix = C.ActiveSuffix;
                ImGui.SetNextItemWidth(280f);
                if (ImGui.InputText("\"NOW\" suffix", ref suffix, 64)) { C.ActiveSuffix = suffix; C.Save(); }
                ImGui.TreePop();
            }

            ImGui.Spacing();
            if (ImGui.BeginTable("##texttoggles", 2, ImGuiTableFlags.SizingStretchSame))
            {
                C.ShowAbilityIcon = GridCheck("Ability icon", C.ShowAbilityIcon,
                    "Matched from the action name; pin one per line with the \"…\" button.");
                C.ShowRadialRing = GridCheck("Radial ring", C.ShowRadialRing,
                    "A depleting countdown ring around the call icon.");
                C.ShowMechanicLine = GridCheck("Mechanic 2nd line", C.ShowMechanicLine);
                C.ShowCountdownNumber = GridCheck("Countdown number", C.ShowCountdownNumber);
                C.TextShadow = GridCheck("Drop shadow", C.TextShadow,
                    "Improves readability over busy backgrounds.");
                C.CooldownAwareCalls = GridCheck("Cooldown warnings", C.CooldownAwareCalls,
                    "Reddens the main call ([CD Ns]) and dims it in the upcoming list when your mit is still on cooldown past the call time. Your job's mits only.");
                C.ShowDtrBar = GridCheck("Server-bar next mit", C.ShowDtrBar,
                    "Shows the next mit on the server-info bar.");
                C.ShowMitBar = GridCheck("Active-mits bar", C.ShowMitBar,
                    "A row of your active defensive buffs with seconds remaining, tinted by mit type.");
                ImGui.EndTable();
            }
            if (C.ShowMitBar)
            {
                var locked = C.MitBarLocked;
                if (GreenCheckbox("Lock active-mits position", ref locked)) { C.MitBarLocked = locked; _plugin.MitBarWindow.RequestReposition(); C.Save(); }
                ImGui.TextDisabled("Auto-locks in combat — move it out of combat or with Live preview.");
            }
        }

        if (Section("Colors"))
        {
            var imminent = ColorToVec4(C.OverlayColorImminent);
            if (ImGui.ColorEdit4("Counting down", ref imminent)) { C.OverlayColorImminent = Vec4ToColor(imminent); C.Save(); }
            var active = ColorToVec4(C.OverlayColorActive);
            if (ImGui.ColorEdit4("Active (NOW)", ref active)) { C.OverlayColorActive = Vec4ToColor(active); C.Save(); }
            var mechCol = ColorToVec4(C.OverlayColorMechanic);
            if (ImGui.ColorEdit4("Mechanic line", ref mechCol)) { C.OverlayColorMechanic = Vec4ToColor(mechCol); C.Save(); }
            // The timeline list color lives with the timeline toggle in the
            // "Next-mits timeline" section below.
            if (ImGui.SmallButton("Reset colors"))
            {
                C.OverlayColorImminent = 0xFF55FFFF; C.OverlayColorActive = 0xFF55FF55;
                C.OverlayColorMechanic = 0xC0FFFFFF; C.OverlayColorUpcoming = 0xB0FFFFFF;
                C.Save();
            }

            ImGui.Spacing();
            C.ColorByMitType = CfgCheck("Color the call by mit type", C.ColorByMitType);
            HelpMarker("Tints calls by what kind of mit they are. Lines with their own color override are left alone.");
            if (C.ColorByMitType)
            {
                var party = ColorToVec4(C.MitColorParty);
                if (ImGui.ColorEdit4("Party mit", ref party)) { C.MitColorParty = Vec4ToColor(party); C.Save(); }
                var tank = ColorToVec4(C.MitColorTank);
                if (ImGui.ColorEdit4("Tank cooldown", ref tank)) { C.MitColorTank = Vec4ToColor(tank); C.Save(); }
                var personal = ColorToVec4(C.MitColorPersonal);
                if (ImGui.ColorEdit4("Personal", ref personal)) { C.MitColorPersonal = Vec4ToColor(personal); C.Save(); }
            }
        }

        if (Section("Countdown bar"))
        {
            C.ShowProgressBar = CfgCheck("Show countdown bar under the call", C.ShowProgressBar);
            if (C.ShowProgressBar)
            {
                var barH = C.ProgressBarHeight;
                ImGui.SetNextItemWidth(200f);
                if (ImGui.SliderFloat("Bar height", ref barH, 2f, 24f, "%.0f px")) { C.ProgressBarHeight = barH; C.Save(); }
            }
            C.PulseWhenImminent = CfgCheck("Pulse the text in the last second", C.PulseWhenImminent);
        }

        if (Section("Background"))
        {
            C.ShowBackground = CfgCheck("Draw a background box", C.ShowBackground);
            if (C.ShowBackground)
            {
                var bg = ColorToVec4(C.BackgroundColor);
                if (ImGui.ColorEdit4("Background color", ref bg)) { C.BackgroundColor = Vec4ToColor(bg); C.Save(); }
                ImGui.TextDisabled("Drag the alpha channel down for a translucent box.");
            }
        }

        if (Section("Next-mits timeline (separate window)"))
        {
            C.ShowUpcoming = CfgCheck("Show the next-mits timeline window", C.ShowUpcoming);
            ImGui.TextDisabled("The main call only shows the imminent mit; everything still coming up lists here.");
            if (C.ShowUpcoming)
            {
                C.TimelineLocked = CfgCheck("Lock timeline (click-through)", C.TimelineLocked);
                ImGui.SameLine();
                if (ImGui.Button("Reset position"))
                {
                    C.TimelinePosition = new Vector2(0.5f, 0.62f);
                    C.Save();
                    _plugin.TimelineWindow.RequestReposition();
                }
                ImGui.SameLine();
                ImGui.TextDisabled(C.TimelineLocked ? "(unlock to drag)" : "(drag it to move)");
                ImGui.TextDisabled("Auto-locks in combat — move it out of combat or with Live preview.");

                var count = C.UpcomingCount;
                ImGui.SetNextItemWidth(120f);
                if (ImGui.SliderInt("Timeline lines", ref count, 1, 8)) { C.UpcomingCount = count; C.Save(); }
                var look = C.UpcomingLookaheadSeconds;
                ImGui.SetNextItemWidth(160f);
                if (ImGui.SliderFloat("Look-ahead (s)", ref look, 5f, 90f, "%.0f")) { C.UpcomingLookaheadSeconds = look; C.Save(); }

                var upPx = C.UpcomingFontSizePx;
                ImGui.SetNextItemWidth(220f);
                if (ImGui.SliderFloat("Timeline text size", ref upPx, 10f, 60f, "%.0f px")) { C.UpcomingFontSizePx = upPx; C.Save(); }
                var upCol = ColorToVec4(C.OverlayColorUpcoming);
                if (ImGui.ColorEdit4("Timeline text color", ref upCol)) { C.OverlayColorUpcoming = Vec4ToColor(upCol); C.Save(); }
            }
        }
    }

    private void DrawAudioTab()
    {
        C.AudioEnabled = CfgCheck("Enable audio cues", C.AudioEnabled);
        ImGui.TextDisabled("Plays when a call enters its warning window, once per pull, even if the overlay is hidden.");

        if (Section("Voice", true))
        {
            C.TtsEnabled = CfgCheck("Speak the action", C.TtsEnabled);

            // Engine: online neural (Edge) for the nice custom voices, or offline Windows.
            var online = C.TtsUseEdge;
            if (ImGui.RadioButton("Online neural voices", online)) { C.TtsUseEdge = true; C.Save(); }
            ImGui.SameLine();
            if (ImGui.RadioButton("Windows voices (offline)", !online)) { C.TtsUseEdge = false; C.Save(); }
            HelpMarker("Online uses Microsoft Edge's free Read-Aloud voices (Aria, Guy, Jenny, ...). No key, "
                       + "no install, needs internet; falls back to a Windows voice if offline. Windows uses the "
                       + "voices installed on your PC.");

            if (C.TtsUseEdge)
            {
                // Snap any unknown/old saved voice (e.g. the removed child voice) to a valid one.
                var cur = Array.Find(Audio.EdgeVoices, v => v.Id == C.TtsEdgeVoice);
                if (cur.Id == null) { cur = Audio.EdgeVoices[0]; C.TtsEdgeVoice = cur.Id; C.Save(); }
                var female = cur.Female;

                if (ImGui.RadioButton("Female", female) && !female)
                { C.TtsEdgeVoice = Audio.EdgeVoices.First(v => v.Female).Id; C.Save(); female = true; }
                ImGui.SameLine();
                if (ImGui.RadioButton("Male", !female) && female)
                { C.TtsEdgeVoice = Audio.EdgeVoices.First(v => !v.Female).Id; C.Save(); female = false; }

                var list = Audio.EdgeVoices.Where(v => v.Female == female).ToArray();
                var names = list.Select(v => v.Name).ToArray();
                var idx = Math.Max(0, Array.FindIndex(list, v => v.Id == C.TtsEdgeVoice));
                ImGui.SetNextItemWidth(220f);
                if (ImGui.Combo("Voice##edge", ref idx, names, names.Length))
                {
                    C.TtsEdgeVoice = list[idx].Id;
                    C.Save();
                }
            }
            else
            {
                // Every installed SAPI voice; female voices (Zira, Hazel) appear if installed.
                var voices = new List<string> { "System default" };
                voices.AddRange(_plugin.Audio.VoiceNames());
                var voiceIndex = string.IsNullOrEmpty(C.TtsVoice) ? 0 : Math.Max(0, voices.IndexOf(C.TtsVoice));
                ImGui.SetNextItemWidth(280f);
                if (ImGui.Combo("Voice##sapi", ref voiceIndex, voices.ToArray(), voices.Count))
                {
                    C.TtsVoice = voiceIndex == 0 ? "" : voices[voiceIndex];
                    C.Save();
                }
                if (voices.Count <= 1)
                    ImGui.TextDisabled("No extra voices found. Add more in Windows, Time & language, Speech.");
            }

            // Advanced: paste any Edge voice id to use one outside the list.
            if (C.TtsUseEdge && ImGui.TreeNode("More voices (advanced)"))
            {
                var custom = C.TtsCustomVoice;
                ImGui.SetNextItemWidth(280f);
                if (ImGui.InputTextWithHint("##customvoice", "e.g. en-US-AvaMultilingualNeural", ref custom, 64))
                { C.TtsCustomVoice = custom; C.Save(); }
                ImGui.TextDisabled("Overrides the picker above. Full list: the Edge / Azure neural voice catalog.");
                if (!string.IsNullOrWhiteSpace(C.TtsCustomVoice) && ImGui.SmallButton("Use the picker instead"))
                { C.TtsCustomVoice = ""; C.Save(); }
                ImGui.TreePop();
            }

            var rate = C.TtsRate;
            ImGui.SetNextItemWidth(220f);
            if (ImGui.SliderInt("Speed", ref rate, -10, 10)) { C.TtsRate = rate; C.Save(); }
            var vol = C.TtsVolume;
            ImGui.SetNextItemWidth(220f);
            if (ImGui.SliderInt("Volume", ref vol, 0, 100)) { C.TtsVolume = vol; C.Save(); }

            ImGui.Spacing();
            var mech = C.TtsSpeakMechanic;
            if (ImGui.RadioButton("Speak the mit", !mech)) { C.TtsSpeakMechanic = false; C.Save(); }
            Tip("Reads the action you press, e.g. \"Reprisal\". Override the exact words per line with the \"…\" button.");
            ImGui.SameLine();
            if (ImGui.RadioButton("Speak the mechanic", mech)) { C.TtsSpeakMechanic = true; C.Save(); }

            if (ImGui.TreeNode("Advanced"))
            {
                var gap = C.TtsMinGapSeconds;
                ImGui.SetNextItemWidth(220f);
                if (ImGui.SliderFloat("Min gap between cues (s)", ref gap, 0f, 5f, "%.1f")) { C.TtsMinGapSeconds = gap; C.Save(); }
                Tip("Skips a cue if one was spoken within this many seconds. 0 = never skip.");
                ImGui.TreePop();
            }
        }

        if (Section("Test", true))
        {
            ImGui.SetNextItemWidth(220f);
            ImGui.InputTextWithHint("##testtext", "text to test…", ref _ttsTestText, 128);
            ImGui.SameLine();
            if (ImGui.Button("Speak"))
            {
                var t = string.IsNullOrWhiteSpace(_ttsTestText) ? "Reprisal" : _ttsTestText;
                var voice = C.TtsUseEdge
                    ? (string.IsNullOrWhiteSpace(C.TtsCustomVoice) ? C.TtsEdgeVoice : C.TtsCustomVoice)
                    : C.TtsVoice;
                _plugin.Audio.Speak(t, C.TtsRate, C.TtsVolume, C.TtsUseEdge, voice);
            }
            if (C.TtsUseEdge)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("(first use of a voice fetches it, then it's instant)");
            }

            var status = _plugin.Audio.LastTtsStatus;
            if (!string.IsNullOrEmpty(status))
            {
                var ok = status.StartsWith("Online OK") || status == "Windows voice";
                ImGui.TextColored(ok ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudYellow, "Status: " + status);
            }
            ImGui.TextDisabled("Per line you can override the spoken text or mute the cue (the \"…\" button).");
        }
    }

    private string _ttsTestText = "";

    // ---- per-line overrides popup ---------------------------------------

    private string _iconSearch = "";
    private int _iconBrowseStart = 405; // action icons start around here
    private const int IconPage = 64;

    private void DrawLineOptionsPopup(MitLine line)
    {
        if (!ImGui.BeginPopup("lineopt")) return;

        var fight = (_selectedFight >= 0 && _selectedFight < C.Fights.Count) ? C.Fights[_selectedFight] : null;
        if (fight != null)
        {
            var idx = fight.Lines.IndexOf(line);
            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowUp) && idx > 0)
            {
                (fight.Lines[idx - 1], fight.Lines[idx]) = (fight.Lines[idx], fight.Lines[idx - 1]);
                C.Save();
            }
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowDown) && idx >= 0 && idx < fight.Lines.Count - 1)
            {
                (fight.Lines[idx + 1], fight.Lines[idx]) = (fight.Lines[idx], fight.Lines[idx + 1]);
                C.Save();
            }
            ImGui.SameLine();
            ImGui.TextDisabled("reorder");
            ImGui.Separator();
        }

        SeparatorText("Icon");
        var resolved = Icons.For(line, _plugin.ActiveJobAbbreviation());
        Icons.Draw(resolved, new Vector2(40, 40));
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextUnformatted(line.IconId != 0 ? $"pinned (#{line.IconId})"
            : (resolved != 0 ? "auto (action / status / keyword)" : "none"));
        if (ImGui.SmallButton("Use auto")) { line.IconId = 0; C.Save(); }
        ImGui.SameLine();
        if (ImGui.SmallButton("Potion")) { line.IconId = Icons.PotionIconFor(line); C.Save(); }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Pin the potion (Gemdraught) icon to this line.");
        ImGui.SameLine();
        var iconId = (int)line.IconId;
        ImGui.SetNextItemWidth(110f);
        if (ImGui.InputInt("##iconid", ref iconId)) { line.IconId = (uint)Math.Max(0, iconId); C.Save(); }
        ImGui.SameLine();
        ImGui.TextDisabled("id");
        ImGui.EndGroup();

        // Search actions + statuses by name -> clickable icon grid.
        ImGui.SetNextItemWidth(240f);
        ImGui.InputTextWithHint("##iconsearch", "search actions & statuses…", ref _iconSearch, 64);
        if (!string.IsNullOrWhiteSpace(_iconSearch))
        {
            var n = 0;
            foreach (var (name, ic) in Icons.Search(_iconSearch, 40))
            {
                if (Icons.Button(ic, new Vector2(32, 32), $"##s{ic}_{n}")) { line.IconId = ic; C.Save(); }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{name}  (#{ic})");
                if (++n % 8 != 0) ImGui.SameLine();
            }
            ImGui.NewLine();
        }

        // Quick palette: the keyword "bucket" (Bait, Stun, Bind, Heal, Knockback …).
        // Click one to pin it. Typing the same word on a line auto-fills it too.
        if (ImGui.TreeNode("Common mechanic icons"))
        {
            var n = 0;
            foreach (var (label, ic) in Icons.Common())
            {
                if (Icons.Button(ic, new Vector2(32, 32), $"##c{ic}_{n}")) { line.IconId = ic; C.Save(); }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{label}  (#{ic})");
                if (++n % 8 != 0) ImGui.SameLine();
            }
            ImGui.NewLine();
            ImGui.TreePop();
        }

        // Browse any icon by id (paged grid) — pick literally anything.
        if (ImGui.TreeNode("Browse all icons"))
        {
            ImGui.SetNextItemWidth(120f);
            ImGui.InputInt("Start id", ref _iconBrowseStart, 8, 64);
            _iconBrowseStart = Math.Clamp(_iconBrowseStart, 0, 250000);
            ImGui.SameLine();
            if (ImGui.ArrowButton("##icoprev", ImGuiDir.Left)) _iconBrowseStart = Math.Max(0, _iconBrowseStart - IconPage);
            ImGui.SameLine();
            if (ImGui.ArrowButton("##iconext", ImGuiDir.Right)) _iconBrowseStart += IconPage;
            ImGui.SameLine();
            ImGui.TextDisabled($"{_iconBrowseStart}–{_iconBrowseStart + IconPage - 1}");

            if (ImGui.BeginChild("##iconbrowse", new Vector2(0, 220), true))
            {
                for (var k = 0; k < IconPage; k++)
                {
                    var id = (uint)(_iconBrowseStart + k);
                    if (Icons.Button(id, new Vector2(32, 32), $"##b{id}")) { line.IconId = id; C.Save(); }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip($"#{id}");
                    if ((k + 1) % 8 != 0) ImGui.SameLine();
                }
            }
            ImGui.EndChild();
            ImGui.TreePop();
        }

        SeparatorText("Overrides (0 / empty = global)");

        var lead = line.LeadOverride;
        ImGui.SetNextItemWidth(120f);
        if (ImGui.InputFloat("Warning lead (s)", ref lead, 0.5f, 1f, "%.1f"))
        {
            line.LeadOverride = MathF.Max(0f, lead);
            C.Save();
        }

        var tts = line.Tts;
        ImGui.SetNextItemWidth(220f);
        if (ImGui.InputText("Speak instead", ref tts, 128)) { line.Tts = tts; C.Save(); }
        ImGui.TextDisabled("Empty = speak the action.");

        var sound = line.Sound;
        if (GreenCheckbox("Play audio cue for this line", ref sound)) { line.Sound = sound; C.Save(); }

        var useColor = line.Color != 0;
        if (GreenCheckbox("Custom text color", ref useColor))
        {
            line.Color = useColor ? 0xFF55FFFF : 0u;
            C.Save();
        }
        if (line.Color != 0)
        {
            var col = ColorToVec4(line.Color);
            if (ImGui.ColorEdit4("Color", ref col)) { line.Color = Vec4ToColor(col); C.Save(); }
        }

        ImGui.EndPopup();
    }

    // ---- share via clipboard --------------------------------------------

    private void ExportFight(FightProfile fight)
    {
        try
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(fight);
            // FRENMITS2 = gzip-compressed, so a full raid plan is a much shorter,
            // paste-friendly code to share. (FRENMITS1 plain base64 still imports.)
            var raw = System.Text.Encoding.UTF8.GetBytes(json);
            using var ms = new System.IO.MemoryStream();
            using (var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal))
                gz.Write(raw, 0, raw.Length);
            ImGui.SetClipboardText("FRENMITS2:" + Convert.ToBase64String(ms.ToArray()));
            FlashBuiltin("Plan code copied to clipboard.");
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: export failed");
        }
    }

    private void ImportFightFromClipboard()
    {
        try
        {
            var text = (ImGui.GetClipboardText() ?? "").Trim();
            string json;
            if (text.StartsWith("FRENMITS2:"))
            {
                var data = Convert.FromBase64String(text["FRENMITS2:".Length..]);
                using var ms = new System.IO.MemoryStream(data);
                using var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress);
                using var outMs = new System.IO.MemoryStream();
                gz.CopyTo(outMs);
                json = System.Text.Encoding.UTF8.GetString(outMs.ToArray());
            }
            else if (text.StartsWith("FRENMITS1:"))
            {
                json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(text["FRENMITS1:".Length..]));
            }
            else
            {
                FlashBuiltin("No FrenMits plan code on the clipboard.");
                return;
            }

            var fight = Newtonsoft.Json.JsonConvert.DeserializeObject<FightProfile>(json);
            if (fight == null) return;

            // A same-territory import UPDATES the existing profile instead of
            // adding a duplicate: a second profile for one territory never fires
            // (ActiveFight takes the first match), and a duplicate of a built-in
            // renders locked, with no way to delete it.
            var existing = fight.TerritoryId != 0
                ? _plugin.Config.Fights.FirstOrDefault(f => f.TerritoryId == fight.TerritoryId)
                : null;
            if (existing != null)
            {
                // Slot-scoped update: the import replaces the sender's ACTIVE slot
                // only. Wholesale-replacing SavedSlots/DeletedCalls would silently
                // wipe YOUR saved edits for every other slot in the fight.
                existing.Lines = fight.Lines;
                existing.TimerOffset = fight.TimerOffset;
                if (!string.IsNullOrEmpty(fight.Slot))
                {
                    existing.Slot = fight.Slot;
                    existing.SavedSlots[fight.Slot] = fight.Lines;
                    existing.DeletedCalls.RemoveAll(d =>
                        string.Equals(d.Slot, fight.Slot, StringComparison.OrdinalIgnoreCase));
                    existing.DeletedCalls.AddRange(fight.DeletedCalls.Where(d =>
                        string.Equals(d.Slot, fight.Slot, StringComparison.OrdinalIgnoreCase)));
                }
                if (!Builtin.Has(existing.TerritoryId))
                {
                    // Custom fights carry their hand-built anchors; built-ins keep
                    // the canonical baked ones (ApplySlot refreshes those anyway).
                    existing.Name = fight.Name;
                    existing.SyncPoints = fight.SyncPoints;
                    existing.BossAnchors = fight.BossAnchors;
                }
                C.Save();
                FlashBuiltin(string.IsNullOrEmpty(fight.Slot)
                    ? $"Imported \"{fight.Name}\" into your existing \"{existing.Name}\"."
                    : $"Imported \"{fight.Name}\" into your existing \"{existing.Name}\" ({SlotLabel(fight.Slot)} slot; your other slots kept).");
                return;
            }

            fight.Id = Guid.NewGuid().ToString("N");
            // Drop it into the category you're currently viewing and expand it.
            if (_nav == NavKind.Fights) fight.Category = _navCategory;
            AddFight(fight);
            FlashBuiltin($"Imported \"{fight.Name}\".");
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: import failed");
        }
    }

    // ---- helpers ---------------------------------------------------------

    // The best-matching baked built-in line, for the right-click "reset to default"
    // options on time / mechanic / action. Scored by time proximity with a strong
    // bonus for a matching mechanic and/or action — so whichever field you typo'd,
    // the OTHER fields (and the time) still pin the right baked line. Null when
    // there's no baked default (custom / tank / potion lines, non-built-in fights).
    private MitLine? DefaultLineFor(FightProfile fight, MitLine line)
    {
        if (!Builtin.Has(fight.TerritoryId)) return null;
        var baked = Builtin.BuildLines(fight.TerritoryId, fight.Slot);
        if (baked.Count == 0) return null;

        var mech = line.Mechanic.Trim();
        var act = line.Action.Trim();

        MitLine? best = null;
        var bestScore = float.MaxValue;
        var bestHasMatch = false;
        foreach (var b in baked)
        {
            var mMatch = mech.Length > 0 && string.Equals(b.Mechanic.Trim(), mech, StringComparison.OrdinalIgnoreCase);
            var aMatch = act.Length > 0 && string.Equals(b.Action.Trim(), act, StringComparison.OrdinalIgnoreCase);
            var hasMatch = mMatch || aMatch;
            var score = MathF.Abs(b.Time - line.Time) - (mMatch ? 1000f : 0f) - (aMatch ? 1000f : 0f);
            // Prefer any line that shares a field; among those, the lowest score.
            if (best == null || (hasMatch && !bestHasMatch) || (hasMatch == bestHasMatch && score < bestScore))
            {
                best = b; bestScore = score; bestHasMatch = hasMatch;
            }
        }
        // Only offer a default when a baked line actually corresponds to this one
        // (shares its mechanic or action) — not just the nearest in time.
        return bestHasMatch ? best : null;
    }

    private static string Ellipsis(string s, int max) => s.Length > max ? s[..max] + "…" : s;

    private static MitLine CloneLine(MitLine l) => new()
    {
        Time = l.Time, Mechanic = l.Mechanic, Action = l.Action,
        Jobs = new List<string>(l.Jobs), Enabled = l.Enabled,
        LeadOverride = l.LeadOverride, Tts = l.Tts, Sound = l.Sound, Color = l.Color, IconId = l.IconId
    };

    private static string Get(string[] row, int i) => i >= 0 && i < row.Length ? row[i] : "";
    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    private static string TerritoryName(uint id)
    {
        if (id == 0) return "";
        try
        {
            var sheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>();
            var row = sheet?.GetRowOrDefault(id);
            var name = row?.PlaceName.ValueNullable?.Name.ExtractText();
            return string.IsNullOrWhiteSpace(name) ? "" : name!;
        }
        catch
        {
            return "";
        }
    }

    private static Vector4 ColorToVec4(uint abgr) => new(
        (abgr & 0xFF) / 255f,
        ((abgr >> 8) & 0xFF) / 255f,
        ((abgr >> 16) & 0xFF) / 255f,
        ((abgr >> 24) & 0xFF) / 255f);

    private static uint Vec4ToColor(Vector4 v) =>
        ((uint)(Math.Clamp(v.W, 0, 1) * 255) << 24) |
        ((uint)(Math.Clamp(v.Z, 0, 1) * 255) << 16) |
        ((uint)(Math.Clamp(v.Y, 0, 1) * 255) << 8) |
        (uint)(Math.Clamp(v.X, 0, 1) * 255);
}
