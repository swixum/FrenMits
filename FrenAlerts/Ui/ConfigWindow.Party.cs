using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using FrenAlerts.Engine;
using FrenAlerts.Game;

namespace FrenAlerts.Ui;

// Who sits in each seat.
//
// Worked out from jobs everywhere nobody says otherwise, and that is wrong as often as
// a group's order differs from job order: two melee, two ranged, two of anything. The
// calls that split a pair then name the other person, on time and confidently, which
// is the hardest kind of wrong call to notice.
//
// What is set here is remembered against the people it was set with, and a group can be
// given a name so it is picked out of a list rather than recognised by who is online.
public partial class ConfigWindow
{
    private const string ThisParty = "This party";
    private const double SeatPollSeconds = 1d;

    private double _seatsReadAt = -99d;
    private List<(uint Id, string Name, uint Job)> _seatRoster = [];
    private List<string> _seatNames = [];
    private readonly Dictionary<string, string> _seatGuess = new(8);
    private int _seatRuns;

    // What is in each list's find box, and which list is being opened this frame so its
    // box can be given the keyboard once. Nothing here is written to the config: a seat
    // is set when a name is taken, not per keystroke, or every letter typed would be a
    // seat change and a config save.
    private readonly Dictionary<string, string> _seatTyped = new(8);
    private string _seatOpening = "";

    // The field, at rest and lit. Not FrameBg from the theme: this is drawn on the draw
    // list rather than by a widget, so it cannot read the pushed style the way an
    // InputText does.
    private const uint FieldBg = 0xFF2B1B22;   // #221B2B, the theme's frame
    private const uint FieldHot = 0xFF39242E;  // #2E2439, the theme's hovered frame

    // Which group the rows are about. Empty is the party stood in, which is what it
    // reads as every time the page is opened.
    private string _seatGroup = "";
    private string _seatCalled = "";

    // Clearing every role takes the answers away, and what is left on screen is eight
    // boxes showing the worked-out name as a placeholder, which reads exactly like
    // eight roles still set. So the clear says so out loud for a moment, on top of the
    // ticks, the green names and the button itself all going.
    private const double ClearedFor = 4d;
    private double _seatClearedAt = -99d;

    private void DrawRolesPage()
    {
        ReadParty();

        var group = Selected();
        var named = Named(group);

        PageHead("Roles", named > 0
            ? $"{named} role{(named == 1 ? "" : "s")} set"
            : "worked out from jobs", false, hasMaster: false,
            reset: named > 0 ? ClearRoles : null,
            icon: FontAwesomeIcon.UserFriends);

        Widgets.ListBegin();
        Widgets.RowNote("What the roles mean when a call says MT, M1, R2");
        Widgets.RowNote(_seatRuns > 1
            ? $"You have run with this group {_seatRuns} times, and their roles come back with them"
            : "Set the ones your group runs out of job order, leave the rest");
        Widgets.ListEnd();
        ImGui.Spacing();

        DrawGroupPicker(group);
        DrawPartySeats(group);
    }

    private void DrawGroupPicker(KnownGroup? group)
    {
        var saved = C.PartyBook.Saved();

        var labels = new List<string> { ThisParty };
        labels.AddRange(saved.Select(g => g.Name));

        var at = 0;
        if (_seatGroup.Length > 0)
        {
            var found = saved.FindIndex(g => g.Key == _seatGroup);
            at = found < 0 ? 0 : found + 1;
        }

        Widgets.GroupLabel("Group");
        Widgets.ListBegin();

        if (Widgets.RowCombo("Setting up", Whose(group), ref at, labels.ToArray(),
            width: 190f, changed: at > 0, id: "seatgroup"))
        {
            _seatGroup = at <= 0 ? "" : saved[at - 1].Key;
            _seatCalled = at <= 0 ? "" : saved[at - 1].Name;
        }
        Tip("The party you are in, or a group you saved. The rows below are theirs.");

        var called = _seatCalled;
        if (Widgets.RowText("Saved Party Name", ref called, "seatname", width: 190f,
            changed: called.Length > 0))
        {
            _seatCalled = called.Length > PartyBook.MaxName ? called[..PartyBook.MaxName] : called;
        }
        Tip("Static, Tuesday group, whatever you call them.");

        Widgets.ListEnd();
        ImGui.Spacing();

        var isSaved = group is { Name.Length: > 0 };
        var canSave = _seatCalled.Trim().Length > 0 && (group is not null || _seatRoster.Count > 0);

        if (canSave && ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Save,
                isSaved ? "Rename" : "Save this group"))
        {
            SaveGroup();
        }
        if (canSave) Tip(isSaved
            ? "Changes what they are called. Their roles stay as they are."
            : "Saves who is in the party right now under that name.");

        if (group is not null)
        {
            if (canSave) ImGui.SameLine(0, Theme.S(8f));
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.TrashAlt, "Remove"))
                RemoveGroup(group);
            Tip("Drops this group and their roles. The people stay, for every other group.");
        }

        ImGui.Spacing();
    }

    private void DrawPartySeats(KnownGroup? group)
    {
        Widgets.GroupLabel("Party Roles");
        Widgets.ListBegin();

        if (_seatNames.Count == 0 && group is null && C.PartySeats.Count == 0)
        {
            Widgets.RowNote("No party read yet, so roles come from jobs");
            Widgets.ListEnd();
            ImGui.Spacing();
            return;
        }

        // Only people who are actually in the group being set up: the party stood in, or
        // the saved group's own eight. A list of everybody ever raided with is a list of
        // sixty-four names, and the one wanted is never near the top of it.
        var names = Offered(group);

        foreach (var slot in Audience.Slots) DrawSeatRow(group, slot, names);

        Widgets.ListEnd();
        ImGui.Spacing();

        var drew = false;

        if (_seatRoster.Count > 0)
        {
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Users, "Current Party"))
                WriteGuessDown();
            Tip("Fills every role from jobs, so you fix the one it got wrong.");
            drew = true;
        }

        // Only offered while there is something to clear, so the button going is itself
        // the answer to whether it worked.
        if (Named(group) > 0)
        {
            if (drew) ImGui.SameLine(0, Theme.S(8f));
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Eraser, "Clear"))
                ClearRoles();
            Tip("Every role back to job order, this group too.");
            drew = true;
        }

        if (ImGui.GetTime() - _seatClearedAt < ClearedFor)
        {
            if (drew) ImGui.SameLine(0, Theme.S(10f));
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(Theme.V(Theme.Good), "Cleared, back to job order");
        }

        ImGui.Spacing();
    }

    // Who the list offers. The party stood in, or the members of the saved group being
    // set up, which the book keeps lowercased so they are matched back to the name the
    // game spelled.
    private List<string> Offered(KnownGroup? group)
    {
        if (Here() || group is null) return _seatNames;

        var known = C.PartyBook.Everyone();
        return group.Key.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(low => known.FirstOrDefault(n =>
                string.Equals(n, low, StringComparison.OrdinalIgnoreCase)) ?? low)
            .ToList();
    }

    private void DrawSeatRow(KnownGroup? group, string slot, List<string> names)
    {
        var picked = SeatFor(group, slot);
        var guessed = Here() ? _seatGuess.GetValueOrDefault(slot, "") : "";
        // Measured against the party stood in, so it is only asked about that party.
        //
        // A saved group is set up on a night nobody is online, which is most of the
        // point of saving one, and every filled row of it read "not here right now".
        // Eight rows of a warning about a thing that is not wrong.
        //
        // An empty read is the same mistake one step further on: nobody has been read
        // yet is not the same as nobody is here, and the worked-out name beside it is
        // already held back for exactly that reason.
        var away = Here() && _seatNames.Count > 0 && picked.Length > 0
                   && !_seatNames.Contains(picked, StringComparer.OrdinalIgnoreCase);

        // Their name, in green, beside the tick. The box shows the worked-out name as a
        // placeholder whether a role is set or not, so a hint reading yours-or-ours said
        // nothing about which of the two was on screen.
        var hint = picked.Length > 0 ? (away ? $"{picked}, not here right now" : picked)
            : guessed.Length > 0 ? $"{guessed}, from their job"
            : "";
        var hintCol = picked.Length == 0 ? 0u : away ? Theme.Warn : Theme.Good;

        var btnW = ImGui.GetFrameHeight();
        var gap = Theme.S(4f);
        var boxW = Theme.S(230f);

        Widgets.RowBegin(slot, hint, boxW, id: $"seat{slot}", check: picked.Length > 0,
            hintCol: hintCol);

        DrawSeatField(slot, picked, guessed, names, boxW - btnW - gap);

        ImGui.SameLine(0, gap);
        DrawSeatDrop(slot, picked, btnW);

        Widgets.RowEnd();
    }

    // The whole control is the list.
    //
    // It was a text box with a caret beside it the size of a checkbox, and that caret was
    // the only way in: the smallest thing on the page, holding the thing the page is for.
    // Now the field itself opens the party, at its own width, flush under itself. The box
    // at the top of it filters as it is typed and takes a name that is not in the party,
    // so the typing did not go anywhere, it moved inside the list where the eight names
    // it is being matched against are on screen beside it.
    private void DrawSeatField(string slot, string picked, string guessed, List<string> names, float w)
    {
        var pop = $"seatpick{slot}";
        var open = ImGui.IsPopupOpen(pop);

        var h = ImGui.GetFrameHeight();
        var at = ImGui.GetCursorScreenPos();
        var pad = Theme.S(7f);

        if (ImGui.InvisibleButton($"##seatfield{slot}", new Vector2(w, h)))
        {
            _seatOpening = slot;
            _seatTyped[slot] = "";
            ImGui.OpenPopup(pop);
        }

        var hot = ImGui.IsItemHovered();
        if (hot) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        // Framed and lit like every other control here, so it reads as something to
        // press rather than as a label that happens to sit on the right.
        var dl = ImGui.GetWindowDrawList();
        var box = new Vector2(w, h);
        dl.AddRectFilled(at, at + box, open || hot ? FieldHot : FieldBg, Theme.S(3f));
        dl.AddRect(at, at + box, open ? Theme.Accent : hot ? Theme.AccentHover : Widgets.CardBorder,
            Theme.S(3f));

        var caret = FontAwesomeIcon.CaretDown.ToIconString();
        float caretW;
        using (Service.PluginInterface.UiBuilder.IconFontHandle.Push())
        {
            var csz = ImGui.CalcTextSize(caret);
            caretW = csz.X;
            dl.AddText(new Vector2(at.X + w - pad - csz.X, at.Y + (h - csz.Y) * 0.5f),
                open ? Theme.Accent : Theme.Muted, caret);
        }

        // Whoever has it, or the worked-out name in the muted tone a placeholder uses,
        // so the two never read the same at a glance.
        var shown = picked.Length > 0 ? picked : guessed.Length > 0 ? guessed : "work it out";
        var lineH = ImGui.GetTextLineHeight();
        dl.AddText(new Vector2(at.X + pad, at.Y + (h - lineH) * 0.5f),
            picked.Length > 0 ? Theme.TextBright : Theme.Muted,
            Widgets.Elide(shown, w - pad * 2f - caretW - Theme.S(4f)));

        Tip(picked.Length > 0
            ? $"{slot} is {picked} whenever they are in the party."
            : "Pick somebody, or type a name.");

        ImGui.SetNextWindowPos(new Vector2(at.X, at.Y + h + Theme.S(3f)));
        ImGui.SetNextWindowSize(new Vector2(w, 0f));
        if (!ImGui.BeginPopup(pop)) return;

        DrawSeatList(slot, picked, names);
        ImGui.EndPopup();
    }

    private void DrawSeatList(string slot, string picked, List<string> names)
    {
        var find = _seatTyped.GetValueOrDefault(slot, "");

        // Focused on the frame the list opens, so the keyboard route is click and type
        // rather than click, aim at the box, click again.
        if (_seatOpening == slot)
        {
            ImGui.SetKeyboardFocusHere();
            _seatOpening = "";
        }

        ImGui.SetNextItemWidth(-1f);
        var entered = ImGui.InputTextWithHint($"##find{slot}", "type a name", ref find,
            PartyBook.MaxName, ImGuiInputTextFlags.EnterReturnsTrue);
        _seatTyped[slot] = find;

        var typed = find.Trim();
        var matching = SeatFind.Matching(names, typed);

        if (entered && typed.Length > 0)
        {
            Take(slot, SeatFind.Taken(names, typed));
            return;
        }

        ImGui.Separator();

        if (names.Count == 0) ImGui.TextDisabled("nobody in the party");
        else if (matching.Count == 0) ImGui.TextDisabled("nobody by that name");

        foreach (var who in matching) DrawSeatName(slot, who, picked);

        // What was typed, offered as itself, so a name the party does not have is one
        // click rather than a leap of faith about what Enter will do.
        if (typed.Length > 0 && !SeatFind.Known(names, typed))
        {
            ImGui.Separator();
            if (ImGui.Selectable($"Use \"{Widgets.Elide(typed, ImGui.GetContentRegionAvail().X - Theme.S(40f))}\"##use{slot}"))
                Take(slot, typed);
        }

        if (picked.Length == 0) return;

        ImGui.Separator();
        if (ImGui.Selectable($"Work it out##clear{slot}"))
        {
            Drop(slot);
            ImGui.CloseCurrentPopup();
        }
    }

    // One name in the list: who they are on the left, and on the right the job they are
    // on and the role the game worked out for them. That pair is the whole reason
    // anybody opens this list: the two melee are in the wrong order and which is which
    // cannot be told from two names.
    private void DrawSeatName(string slot, string who, string picked)
    {
        var on = string.Equals(who, picked, StringComparison.OrdinalIgnoreCase);
        var room = ImGui.GetContentRegionAvail().X;
        var top = ImGui.GetCursorScreenPos();

        if (ImGui.Selectable($"##who{slot}{who}", on)) { Take(slot, who); return; }

        var dl = ImGui.GetWindowDrawList();
        var detail = Detail(who);
        var detailW = detail.Length > 0 ? ImGui.CalcTextSize(detail).X : 0f;

        dl.AddText(top, on ? Theme.Accent : Theme.TextBright,
            Widgets.Elide(who, room - detailW - Theme.S(10f)));

        if (detail.Length > 0)
            dl.AddText(new Vector2(top.X + room - detailW, top.Y), Theme.Muted, detail);
    }

    // The job and the worked-out role, for somebody who is actually here. A saved group
    // set up on a night nobody is online has neither, and gets nothing rather than a
    // guess: last week's job is not this week's job.
    private string Detail(string who)
    {
        if (!Here()) return "";

        var job = _seatRoster
            .FirstOrDefault(r => string.Equals(r.Name, who, StringComparison.OrdinalIgnoreCase)).Job;
        var abbrev = job == 0 ? "" : JobNames.Abbrev(job);

        var worked = "";
        foreach (var (seat, name) in _seatGuess)
            if (string.Equals(name, who, StringComparison.OrdinalIgnoreCase)) { worked = seat; break; }

        return abbrev.Length > 0 && worked.Length > 0 ? $"{abbrev}  {worked}"
            : abbrev.Length > 0 ? abbrev
            : worked;
    }

    private void Take(string slot, string who)
    {
        Seat(slot, who);
        _seatTyped[slot] = "";
        ImGui.CloseCurrentPopup();
    }

    // Taking one name off one role, without opening the list or clearing the box by hand.
    // Its room is held either way, so the row does not shift when a name lands.
    private void DrawSeatDrop(string slot, string picked, float btnW)
    {
        if (picked.Length == 0)
        {
            ImGui.Dummy(new Vector2(btnW, btnW));
            return;
        }

        Widgets.PushDangerOutline();
        var hit = Widgets.IconSquare($"drop{slot}", FontAwesomeIcon.Times, btnW);
        Widgets.PopDanger();

        if (hit) Drop(slot);
        Tip($"Takes {picked} off {slot}.");
    }

    private void Drop(string slot)
    {
        Seat(slot, "");
        _seatTyped[slot] = "";
    }

    // The group the rows are about: the one picked from the list, or the party stood
    // in. Null before anybody has been read and before that party has ever run.
    private KnownGroup? Selected() =>
        _seatGroup.Length > 0
            ? C.PartyBook.Group(_seatGroup)
            : C.PartyBook.Group(PartyBook.KeyFor(_seatRoster.Select(r => r.Name)));

    // Whether the rows are about the party actually stood in, which is the only time
    // the worked-out names mean anything and the only time the general answer moves.
    private bool Here() => _seatGroup.Length == 0;

    private string Whose(KnownGroup? group) =>
        group is { Name.Length: > 0 } named ? named.Name
        : Here() ? (_seatRoster.Count > 0 ? $"{_seatRoster.Count} here" : "nobody read yet")
        : "";

    private string SeatFor(KnownGroup? group, string slot) =>
        group is not null ? group.Seats.GetValueOrDefault(slot, "")
        : Here() ? C.PartySeatFor(slot)
        : "";

    // Both books at once: the general answer, and this group's own. Set on the page is
    // the only place a seat is ever named, so it is the one place both are written.
    private void Seat(string slot, string name)
    {
        var key = _seatGroup.Length > 0
            ? _seatGroup
            : C.PartyBook.Note(_seatRoster).Key;

        if (key.Length > 0) C.PartyBook.SeatIn(key, slot, name);

        // The general answer is about you and whoever you raid with, so it only moves
        // for the party stood in. Setting up another group must not reseat tonight.
        if (Here()) C.SetPartySeat(slot, name);

        C.Save();
    }

    // How many roles are set on the group being looked at, which is what the head
    // counts and what decides whether there is anything left to clear.
    private int Named(KnownGroup? group) => group?.Seats.Count ?? C.PartySeats.Count;

    private void ClearSeats()
    {
        if (Selected() is { } group) C.PartyBook.ForgetIn(group.Key);
        if (Here()) C.ClearPartySeats();
        C.Save();
    }

    // Cleared by hand, so it is said out loud. Current Party clears on the way to
    // filling every row in, and that answers itself.
    private void ClearRoles()
    {
        ClearSeats();
        _seatClearedAt = ImGui.GetTime();
    }

    private void SaveGroup()
    {
        var called = _seatCalled.Trim();
        if (called.Length == 0) return;

        if (_seatGroup.Length > 0) C.PartyBook.Rename(_seatGroup, called);
        else _seatGroup = C.PartyBook.Save(_seatRoster, called).Key;

        C.Save();
    }

    private void RemoveGroup(KnownGroup group)
    {
        C.PartyBook.Remove(group.Key);
        _seatGroup = "";
        _seatCalled = "";
        C.Save();
    }

    // The seats the jobs give, worked out the same way the fight does rather than read
    // off it: the runner holds one party and this page is drawn for a group that may
    // not be in a duty at all.
    private void ReadParty()
    {
        var now = ImGui.GetTime();
        if (now - _seatsReadAt < SeatPollSeconds) return;
        _seatsReadAt = now;

        var members = PartySlots.Read();
        _seatRoster = PartySlots.Roster(members);
        _seatNames = _seatRoster.Select(r => r.Name).ToList();
        _seatRuns = C.PartyBook.RunsWith(_seatRoster);

        // A group removed elsewhere, or aged out, leaves the page pointing at nothing.
        if (_seatGroup.Length > 0 && C.PartyBook.Group(_seatGroup) is null)
        {
            _seatGroup = "";
            _seatCalled = "";
        }

        var party = new PartyContext();
        SlotResolver.Fill(party, members);

        _seatGuess.Clear();
        foreach (var (id, name, _) in _seatRoster)
        {
            var seat = party.SlotOf(id);
            if (seat.Length > 0) _seatGuess[seat] = name;
        }
    }

    private void WriteGuessDown()
    {
        ClearSeats();
        foreach (var slot in Audience.Slots)
            if (_seatGuess.GetValueOrDefault(slot, "") is { Length: > 0 } name)
                Seat(slot, name);
    }
}
