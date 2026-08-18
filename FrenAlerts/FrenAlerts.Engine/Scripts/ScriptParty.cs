using System.Text;

namespace FrenAlerts.Engine.Scripts;

// The party reads their fights make, under the names their own helpers call for.
//
// Their prelude builds `data.party` out of seven host functions, and a stub with one
// method on it is not a smaller version of that: `party.isDPS` and `party.buddy` are
// missing rather than wrong, so the trigger reading one throws and says nothing, and
// `party.member` handing back a name where a job was asked for makes every melee read
// as not melee. That is a tower called for the wrong half of the group.
public sealed class ScriptParty
{
    // One seat: who, on what, in which role, in which slot.
    public readonly record struct Seat(string Name, string Job, string Role, string Slot);

    private readonly List<Seat> _seats = [];

    // Rebuilt whole on every party read, so somebody swapping job or leaving cannot
    // leave their old seat behind to be named by a call.
    public void Learn(IEnumerable<Seat> seats)
    {
        _seats.Clear();
        _seats.AddRange(seats);
    }

    public int Count => _seats.Count;

    public void Bind(Jint.Engine js)
    {
        js.SetValue("__partyMember", JobOf);
        js.SetValue("__partyIsDPS", IsDps);
        js.SetValue("__partyRoster", Roster);
        js.SetValue("__roleSlot", SlotOf);
        js.SetValue("__roleName", NameIn);
        js.SetValue("__manualRoleSlot", SlotOf);
        js.SetValue("__manualRoleName", NameIn);
    }

    // Their own fallback: a name nobody knows a job for answers as itself, which is
    // what their calls print when they name somebody.
    public string JobOf(string name) =>
        Find(name) is { Job.Length: > 0 } seat ? seat.Job : name ?? "";

    public bool IsDps(string name) => Find(name)?.Role == "dps";

    public string SlotOf(string name) => Find(name)?.Slot ?? "";

    public string NameIn(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return "";

        var want = slot.Trim().ToUpperInvariant();
        foreach (var seat in _seats)
            if (string.Equals(seat.Slot, want, StringComparison.Ordinal)) return seat.Name;
        return "";
    }

    // Their shape, as JSON, because that is what their `partyNames` and `details`
    // parse.
    public string Roster()
    {
        var json = new StringBuilder("[");
        var first = true;

        foreach (var seat in _seats)
        {
            if (!first) json.Append(',');
            first = false;
            json.Append("{\"name\":\"").Append(Escape(seat.Name))
                .Append("\",\"job\":\"").Append(Escape(seat.Job))
                .Append("\",\"role\":\"").Append(Escape(seat.Role))
                .Append("\",\"slot\":\"").Append(Escape(seat.Slot)).Append("\"}");
        }

        return json.Append(']').ToString();
    }

    private Seat? Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        foreach (var seat in _seats)
            if (string.Equals(seat.Name, name, StringComparison.OrdinalIgnoreCase)) return seat;
        return null;
    }

    private static string Escape(string text) =>
        (text ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
}
