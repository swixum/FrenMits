using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FrenMits.Ui;

// The one call editor: the fight page opens it from an action pill, the sheet
// from a cell, so a call is edited the same way wherever you found it. The
// caller owns the popup; this draws its body.
internal static class MitLineEditor
{
    // What the host page does around an edit. The fight page tombstones a
    // rewritten sheet call; the sheet also takes an undo snapshot and adopts
    // the slot it is editing.
    internal sealed class Hooks
    {
        // True when the edit rewrites the action, not just its timing or look.
        public Action<MitLine, bool>? BeforeEdit;
        // Persist and mark the plan dirty.
        public Action? Save;
        public Action? Delete;
        // Null when this call has no sheet version to go back to.
        public Action? Reset;
        public MitLine? Default;
        // Active job, for icon resolution.
        public string? Job;
        // Where this call sits, e.g. "2:14 · Wroth Flames · H2".
        public string? Context;
        // True when the plan reloaded underneath us, so the edit is dropped.
        public Func<bool>? Stale;
    }

    // One left edge for every field, with the icon living in the label gutter.
    // Measured per frame: SameLine takes a window-local x, so the popup's own
    // padding counts, and a larger font would overrun anything hard-coded.
    private static float _fieldX = 54f;   // where every field starts
    private static float _fieldW = 252f;  // and how wide the field column is
    private static float _totalW = 306f;

    private static string _iconSearch = "";

    public static void Draw(MitLine line, Configuration c, Hooks hooks)
    {
        // Guard first, so a reloaded plan never takes a half-applied edit.
        bool Begin(bool rewrite)
        {
            if (hooks.Stale?.Invoke() == true) return false;
            hooks.BeforeEdit?.Invoke(line, rewrite);
            return true;
        }
        void Save() => hooks.Save?.Invoke();

        var style = ImGui.GetStyle();
        var h = ImGui.GetFrameHeight();
        var gap = style.ItemSpacing.X;
        var stepped = 62f + h * 2f + style.ItemInnerSpacing.X * 2f; // field + its two steppers
        // The gutter clears both the icon and the widest label standing in it.
        var startX = ImGui.GetCursorPosX();
        var labelW = MathF.Max(h + gap, ImGui.CalcTextSize("Offset").X + gap);
        _fieldX = startX + labelW;
        _fieldW = MathF.Max(252f, stepped + gap + ImGui.CalcTextSize("presses -00:00").X + 24f);
        _totalW = labelW + _fieldW;

        // ---- the call itself: icon, name, on/off ----
        DrawIconButton(line, hooks, h, Begin, Save);

        ImGui.SameLine(_fieldX);
        var act = line.Action;
        // The name wears its own mit color, the same one the sheet paints it.
        var kind = MitColors.Color(MitTypes.Classify(line.Action, line.Mechanic), c);
        if (kind != 0) ImGui.PushStyleColor(ImGuiCol.Text, kind);
        ImGui.SetNextItemWidth(_fieldW - h - gap);
        if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
        var typed = ImGui.InputTextWithHint("##act", "name a mit...", ref act, 256);
        if (kind != 0) ImGui.PopStyleColor();
        if (typed && Begin(true)) { line.Action = act; Save(); }
        DrawResetAction(line, hooks, Begin, Save);

        ImGui.SameLine(0, gap);
        var on = line.Enabled;
        if (Widgets.GreenCheckbox("##enabled", ref on) && Begin(false)) { line.Enabled = on; Save(); }
        if (Widgets.HoveredDelayed())
            ImGui.SetTooltip(line.Enabled ? "Called. Uncheck to keep it here but silent." : "Off: kept, never called.");

        if (!string.IsNullOrEmpty(hooks.Context))
        {
            ImGui.SetCursorPosX(_fieldX);
            ImGui.TextDisabled(hooks.Context);
        }

        // ---- timing ----
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Offset");
        ImGui.SameLine(_fieldX);
        var off = line.OffsetSeconds;
        var moved = line.OffsetSeconds != 0f;
        if (moved) ImGui.PushStyleColor(ImGuiCol.Text, Theme.Accent);
        ImGui.SetNextItemWidth(stepped);
        var nudged = ImGui.InputFloat("##off", ref off, 0.5f, 1f, "%.1f");
        if (moved) ImGui.PopStyleColor();
        if (nudged && Begin(false))
        {
            line.OffsetSeconds = Math.Clamp(off, -30f, 30f);
            line.OffsetManual = line.OffsetSeconds != 0; // hand-set timing stays put
            Save();
        }
        if (Widgets.HoveredDelayed()) ImGui.SetTooltip("+ earlier, - later.");

        // The number is abstract; the time it lands on is not.
        ImGui.SameLine(0, gap);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2f);
        Widgets.Chip("Presses", Fmt.MmssSigned(line.CueTime), moved ? Theme.Accent : Theme.Muted);

        // ---- who ----
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Jobs");
        ImGui.SameLine(_fieldX);
        DrawJobs(line, Begin, Save);

        if (ImGui.TreeNode("Advanced"))
        {
            // Back to the shared left edge: the tree's indent would shift only
            // the labels, and they would run into their own fields.
            ImGui.Unindent();
            var lead = line.LeadOverride;
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled("Ahead");
            ImGui.SameLine(_fieldX);
            ImGui.SetNextItemWidth(stepped);
            if (ImGui.InputFloat("##lead", ref lead, 0.5f, 1f, "%.1f") && Begin(false))
            {
                line.LeadOverride = MathF.Max(0f, lead);
                Save();
            }
            if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Seconds of warning before it. 0 = the lead from Settings.");

            var tts = line.Tts;
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled("Speak");
            ImGui.SameLine(_fieldX);
            ImGui.SetNextItemWidth(_fieldW - h - gap);
            if (ImGui.InputTextWithHint("##tts", "the action", ref tts, 128) && Begin(false)) { line.Tts = tts; Save(); }

            var sound = line.Sound;
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled("Audio");
            ImGui.SameLine(_fieldX);
            if (Widgets.GreenCheckbox("##sound", ref sound) && Begin(false)) { line.Sound = sound; Save(); }

            var useColor = line.Color != 0;
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled("Color");
            ImGui.SameLine(_fieldX);
            if (Widgets.GreenCheckbox("##usecolor", ref useColor) && Begin(false))
            {
                line.Color = useColor ? 0xFF55FFFF : 0u;
                Save();
            }
            if (line.Color != 0)
            {
                ImGui.SameLine(0, gap);
                var col = Theme.V(line.Color);
                if (ImGui.ColorEdit4("##col", ref col, ImGuiColorEditFlags.NoInputs) && Begin(false))
                {
                    line.Color = Widgets.ToColor(col);
                    Save();
                }
            }

            ImGui.Indent();
            ImGui.TreePop();
        }

        ImGui.Separator();
        DrawFooter(line, hooks);
    }

    // The two ways out, each behind a confirm: both throw away work, and the
    // editor opens on a double-click, so a stray one must not delete anything.
    private static void DrawFooter(MitLine line, Hooks hooks)
    {
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var half = (_totalW - gap) * 0.5f;
        var closeEditor = false;
        var named = string.IsNullOrWhiteSpace(line.Action) ? "this call" : $"\"{Ellipsis(line.Action, 28)}\"";

        if (hooks.Reset != null)
        {
            if (ImGui.Button("Reset", new Vector2(half, 0))) ImGui.OpenPopup("confirmreset");
            if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Back to the sheet's version of this call.");
            if (ImGui.BeginPopup("confirmreset"))
            {
                ImGui.TextUnformatted($"Reset {named} to the sheet?");
                ImGui.TextDisabled("Your wording, timing and jobs for it go.");
                ImGui.Spacing();
                if (ImGui.Button("Reset it", Theme.Sz(110f)))
                {
                    hooks.Reset();
                    closeEditor = true;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel", Theme.Sz(110f))) ImGui.CloseCurrentPopup();
                ImGui.SetItemDefaultFocus();
                ImGui.EndPopup();
            }
            ImGui.SameLine(0, gap);
        }

        if (Widgets.DangerOutlineButton("Delete", new Vector2(hooks.Reset != null ? half : _totalW, 0)))
            ImGui.OpenPopup("confirmdel");
        if (ImGui.BeginPopup("confirmdel"))
        {
            ImGui.TextUnformatted($"Delete {named}?");
            ImGui.TextDisabled(hooks.Reset != null
                ? "It stays gone through sheet updates. Reset brings it back."
                : "Nothing else calls it back.");
            ImGui.Spacing();
            var go = Widgets.DangerOutlineButton("Delete it", Theme.Sz(110f));
            if (go)
            {
                hooks.Delete?.Invoke();
                closeEditor = true;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", Theme.Sz(110f))) ImGui.CloseCurrentPopup();
            ImGui.SetItemDefaultFocus();
            ImGui.EndPopup();
        }

        // Only once the confirm is off the stack is the editor the current popup.
        if (closeEditor) ImGui.CloseCurrentPopup();
    }

    // The icon doubles as its own picker, since text alone often resolves to
    // nothing and the pick was buried two folds deep.
    private static void DrawIconButton(MitLine line, Hooks hooks, float h, Func<bool, bool> begin, Action save)
    {
        var resolved = Icons.For(line, hooks.Job);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(3, 3) * Theme.Scale);
        var clicked = resolved != 0
            ? Icons.Button(resolved, new Vector2(h - 6f, h - 6f), "##iconbtn")
            : ImGui.Button("?##iconbtn", new Vector2(h, h));
        ImGui.PopStyleVar();
        if (clicked) ImGui.OpenPopup("iconpick");
        if (Widgets.HoveredDelayed())
            ImGui.SetTooltip(line.IconId != 0 ? $"Pinned icon (#{line.IconId}). Click to change."
                : resolved != 0 ? "Icon read from the action. Click to pick another."
                : "No icon for this text. Click to pick one.");

        if (!ImGui.BeginPopup("iconpick")) return;

        Icons.Draw(resolved, new Vector2(40, 40) * Theme.Scale);
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextDisabled(line.IconId != 0 ? $"pinned (#{line.IconId})"
            : resolved != 0 ? "auto (action / status / keyword)" : "none");
        if (ImGui.Button("Use auto") && begin(false)) { line.IconId = 0; save(); }
        ImGui.SameLine();
        if (ImGui.Button("Potion") && begin(false)) { line.IconId = Icons.PotionIconFor(line); save(); }
        if (Widgets.HoveredDelayed()) ImGui.SetTooltip("Pin the potion (Gemdraught) icon to this line.");
        ImGui.EndGroup();

        ImGui.SetNextItemWidth(Theme.S(280f));
        ImGui.InputTextWithHint("##iconsearch", "search actions & statuses...", ref _iconSearch, 64);
        if (!string.IsNullOrWhiteSpace(_iconSearch))
        {
            var n = 0;
            foreach (var (name, ic) in Icons.Search(_iconSearch, 40))
            {
                if (Icons.Button(ic, new Vector2(32, 32) * Theme.Scale, $"##s{ic}_{n}") && begin(false)) { line.IconId = ic; save(); }
                if (Widgets.HoveredDelayed()) ImGui.SetTooltip($"{name}  (#{ic})");
                if (++n % 8 != 0) ImGui.SameLine();
            }
            ImGui.NewLine();
        }

        if (ImGui.TreeNode("Common mechanic icons"))
        {
            var n = 0;
            foreach (var (label, ic) in Icons.Common())
            {
                if (Icons.Button(ic, new Vector2(32, 32) * Theme.Scale, $"##c{ic}_{n}") && begin(false)) { line.IconId = ic; save(); }
                if (Widgets.HoveredDelayed()) ImGui.SetTooltip($"{label}  (#{ic})");
                if (++n % 8 != 0) ImGui.SameLine();
            }
            ImGui.NewLine();
            ImGui.TreePop();
        }
        ImGui.EndPopup();
    }

    // Right-click the name to take the sheet's wording back.
    private static void DrawResetAction(MitLine line, Hooks hooks, Func<bool, bool> begin, Action save)
    {
        var def = hooks.Default;
        if (def == null || !ImGui.BeginPopupContextItem("##actionctx_pop")) return;

        if (!string.Equals(def.Action.Trim(), line.Action.Trim(), StringComparison.OrdinalIgnoreCase)
            && ImGui.MenuItem($"Reset action to \"{Ellipsis(def.Action, 40)}\"") && begin(true))
        {
            line.Action = def.Action;
            save();
        }
        ImGui.EndPopup();
    }

    // Job filter: a button naming the current pick, opening role checkboxes.
    private static void DrawJobs(MitLine line, Func<bool, bool> begin, Action save)
    {
        var label = line.Jobs.Count == 0 ? "All jobs" : string.Join(",", line.Jobs);
        if (label.Length > 24) label = label[..22] + "...";
        if (ImGui.Button(label + "##jobs", new Vector2(_fieldW, 0)))
            ImGui.OpenPopup("jobspopup");

        if (!ImGui.BeginPopup("jobspopup")) return;

        if (ImGui.Button("All jobs") && begin(false)) { line.Jobs.Clear(); save(); }

        foreach (var role in Enum.GetValues<JobRole>())
        {
            Widgets.SectionHeader(RoleLabel(role));
            var first = true;
            foreach (var abbr in Jobs.AbbreviationsForRole(role))
            {
                if (!first) ImGui.SameLine();
                first = false;
                var has = line.Jobs.Contains(abbr, StringComparer.OrdinalIgnoreCase);
                if (Widgets.GreenCheckbox(abbr, ref has) && begin(false))
                {
                    if (has && !line.Jobs.Contains(abbr)) line.Jobs.Add(abbr);
                    else line.Jobs.RemoveAll(j => string.Equals(j, abbr, StringComparison.OrdinalIgnoreCase));
                    save();
                }
            }
            ImGui.SameLine();
            if (ImGui.SmallButton($"+all##{role}") && begin(false))
            {
                foreach (var abbr in Jobs.AbbreviationsForRole(role))
                    if (!line.Jobs.Contains(abbr)) line.Jobs.Add(abbr);
                save();
            }
        }
        ImGui.EndPopup();
    }

    // "PhysicalRanged" -> "Phys Ranged" for the role headers.
    private static string RoleLabel(JobRole role) => role switch
    {
        JobRole.PhysicalRanged => "Phys Ranged",
        _ => role.ToString(),
    };

    private static string Ellipsis(string s, int max) => s.Length > max ? s[..max] + "..." : s;
}
