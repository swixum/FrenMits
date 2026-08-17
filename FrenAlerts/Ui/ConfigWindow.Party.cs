using System;
using System.Collections.Generic;
using System.Linq;
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

    // What is in each box while it is being typed in, and which box that is. A seat is
    // written down when the box is left, not per keystroke: every letter of a name would
    // otherwise be a seat change and a config save.
    private readonly Dictionary<string, string> _seatTyped = new(8);
    private string _seatEditing = "";

    // Which group the rows are about. Empty is the party stood in, which is what it
    // reads as every time the page is opened.
    private string _seatGroup = "";
    private string _seatCalled = "";

    private void DrawRolesPage()
    {
        ReadParty();

        var group = Selected();
        var named = group?.Seats.Count ?? C.PartySeats.Count;

        PageHead("Roles", named > 0
            ? $"{named} role{(named == 1 ? "" : "s")} set"
            : "worked out from jobs", false, hasMaster: false,
            reset: named > 0 ? ClearSeats : null,
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
            Widgets.RowNote("No party read yet, so every role is worked out from jobs.");
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

        if (_seatRoster.Count > 0)
        {
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Users, "Current Party"))
                WriteGuessDown();
            Tip("Fills every role with who the game worked out, so you can fix the one it got wrong.");
            ImGui.SameLine(0, Theme.S(8f));
        }

        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Eraser, "Clear"))
            ClearSeats();
        Tip("Back to working every role out from jobs, for this group too.");

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
        var away = picked.Length > 0
                   && !_seatNames.Contains(picked, StringComparer.OrdinalIgnoreCase);

        var hint = picked.Length > 0 ? (away ? "not here right now" : "your call")
            : guessed.Length > 0 ? $"{guessed}, from their job"
            : "";

        var boxW = Theme.S(190f);
        var arrowW = ImGui.GetFrameHeight();
        var gap = Theme.S(4f);

        Widgets.RowBegin(slot, hint, boxW, id: $"seat{slot}", check: picked.Length > 0);

        // The box holds what is being typed while it is being typed in, and whoever has
        // the role the rest of the time.
        if (_seatEditing != slot) _seatTyped[slot] = picked;
        var typed = _seatTyped.GetValueOrDefault(slot, picked);

        ImGui.SetNextItemWidth(boxW - arrowW - gap);
        ImGui.InputTextWithHint($"##seat{slot}",
            guessed.Length > 0 ? guessed : "work it out", ref typed, PartyBook.MaxName);
        _seatTyped[slot] = typed;

        if (ImGui.IsItemActivated()) _seatEditing = slot;
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            Seat(slot, typed.Trim());
            _seatEditing = "";
        }
        else if (_seatEditing == slot && ImGui.IsItemDeactivated())
        {
            _seatEditing = "";
        }

        Tip(picked.Length > 0
            ? $"{slot} is {picked} whenever they are in the party. Kicks in on the next party read."
            : "Type a name, or pick one from the party.");

        ImGui.SameLine(0, gap);
        DrawSeatPick(slot, picked, names);

        Widgets.RowEnd();
    }

    private void DrawSeatPick(string slot, string picked, List<string> names)
    {
        if (!ImGui.BeginCombo($"##pick{slot}", "", ImGuiComboFlags.NoPreview))
        {
            Tip("Pick somebody from the party.");
            return;
        }

        if (names.Count == 0) ImGui.TextDisabled("nobody in the party");

        foreach (var who in names)
        {
            var on = string.Equals(who, picked, StringComparison.OrdinalIgnoreCase);
            if (!ImGui.Selectable($"{who}##{slot}{who}", on)) continue;
            Seat(slot, who);
            _seatTyped[slot] = who;
            _seatEditing = "";
        }

        if (picked.Length > 0)
        {
            ImGui.Separator();
            if (ImGui.Selectable($"Work it out##clear{slot}"))
            {
                Seat(slot, "");
                _seatTyped[slot] = "";
                _seatEditing = "";
            }
        }

        ImGui.EndCombo();
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

    private void ClearSeats()
    {
        if (Selected() is { } group) C.PartyBook.ForgetIn(group.Key);
        if (Here()) C.ClearPartySeats();
        C.Save();
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
