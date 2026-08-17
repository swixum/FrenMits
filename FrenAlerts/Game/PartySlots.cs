using Dalamud.Game.ClientState.Objects.SubKinds;
using FrenAlerts.Engine;

namespace FrenAlerts.Game;

public static class PartySlots
{
    public static IPlayerCharacter? Me => Service.ObjectTable?[0] as IPlayerCharacter;

    // The seats somebody named by hand, and the book of who has run together. Both
    // handed over by the plugin; null until it is built, which is every test and every
    // frame before the config is read.
    public static Func<IReadOnlyDictionary<string, string>>? Seats { get; set; }

    public static Func<PartyBook>? Book { get; set; }

    public static void Fill(PartyContext party, IReadOnlyList<(uint EntityId, uint JobId)> members)
    {
        SlotResolver.Fill(party, members);

        var picks = Seats?.Invoke() ?? new Dictionary<string, string>();
        var book = Book?.Invoke();
        if (picks.Count == 0 && book is null) return;

        var roster = Roster(members);
        if (roster.Count == 0) return;

        // This group's own answers over the general ones, because a group that seats
        // its melee the other way round is not disagreeing with the general answer, it
        // is the reason the general answer was never enough.
        var effective = new Dictionary<string, string>(picks);
        if (book is not null)
        {
            book.Note(roster);
            foreach (var (slot, who) in book.SeatsFor(roster)) effective[slot] = who;
        }

        // After the jobs and before the player's own seat override, which is theirs
        // about themselves and wins over the group's answer about anybody.
        SeatPicks.Apply(party, roster, effective);

        // A group with no answers of its own takes up the general ones, so the seats
        // set once on the page belong to this group from their first night on.
        if (book is not null) book.Learn(book.Note(roster), picks, roster);
    }

    // The names and jobs behind the ids just read. An object whose name has not loaded
    // is dropped rather than passed on as an empty one, which would match a seat left
    // blank and hand it to whoever happened to be standing there.
    public static List<(uint Id, string Name, uint Job)> Roster(
        IReadOnlyList<(uint EntityId, uint JobId)> members)
    {
        var roster = new List<(uint, string, uint)>(members.Count);
        foreach (var (id, job) in members)
        {
            var name = Service.ObjectTable?.SearchByEntityId(id)?.Name.TextValue ?? "";
            if (name.Length > 0) roster.Add((id, name, job));
        }
        return roster;
    }

    public static List<(uint EntityId, uint JobId)> Read()
    {
        var members = new List<(uint, uint)>(8);
        foreach (var m in Service.PartyList)
        {
            if (m.GameObject is not IPlayerCharacter pc) continue;
            members.Add((pc.EntityId, pc.ClassJob.RowId));
        }

        // A replay has no party list, so the eight players standing in the object
        // table stand in for it. Without this every call in a replay says "someone".
        if (members.Count == 0)
            foreach (var pc in Watchers.StandingIn())
                members.Add((pc.EntityId, pc.ClassJob.RowId));

        // Solo, or the list has not populated yet, so at least place the player.
        if (members.Count == 0 && Me is { } me)
            members.Add((me.EntityId, me.ClassJob.RowId));

        return members;
    }
}
