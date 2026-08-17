namespace FrenAlerts.Engine;

// Somebody you have raided with, kept so they can be seated while they are offline.
public sealed class KnownPlayer
{
    public string Name { get; set; } = "";

    public uint Job { get; set; }

    // The run they were last stood in. Used to decide who is dropped when the book
    // is full, so the people you raid with survive and a stranger from one duty
    // finder night does not push them out.
    public int Seen { get; set; }
}

// A set of people who ran together, and the seats they were called by.
public sealed class KnownGroup
{
    // Their names, lowercased and sorted, joined. The set is the identity: the same
    // eight in a different order are the same group, and one person different is a
    // different one.
    public string Key { get; set; } = "";

    // What somebody called them. Empty for a group that was only ever run with, which
    // is most of them: a name is how a group is picked out of the list on purpose,
    // and a named group is never dropped to make room.
    public string Name { get; set; } = "";

    private Dictionary<string, string> _seats = new();

    public Dictionary<string, string> Seats
    {
        get => _seats;
        set => _seats = value ?? new Dictionary<string, string>();
    }

    public int Seen { get; set; }

    public int Runs { get; set; }
}

// What the party read has learned from actually running.
//
// Two statics seat their melee differently and both are right. Seats held only as one
// list of names means the second group's answer overwrites the first, and the first
// group's next night is called wrong until somebody notices and sets it back.
//
// So the answers are kept against the set of people they were used with. Nothing is
// learned that nobody said: only seats named by hand are written down, because a seat
// worked out from jobs is worked out again next time for free and remembering it would
// freeze a guess into an answer.
public sealed class PartyBook
{
    // Both bounded, both evicted oldest-run first. A book that grows forever is read
    // on every party poll for the lifetime of the install.
    public const int MaxPeople = 64;
    public const int MaxGroups = 12;

    // Long enough for "Tuesday static", short enough that the dropdown stays a
    // dropdown. Cut rather than refused, so a long paste still saves.
    public const int MaxName = 24;

    // How many different sets of people have been stood in. Only ever counts up, and
    // only when the set changes, so it stamps who was seen recently without a clock.
    public int Runs { get; set; }

    private List<KnownPlayer> _people = new();
    private List<KnownGroup> _groups = new();

    public List<KnownPlayer> People
    {
        get => _people;
        set => _people = Trim(value ?? new List<KnownPlayer>(), MaxPeople, p => p.Seen);
    }

    public List<KnownGroup> Groups
    {
        get => _groups;
        set => _groups = TrimGroups(value ?? new List<KnownGroup>());
    }

    private string _last = "";

    public static string KeyFor(IEnumerable<string> names) =>
        string.Join("|", names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal));

    // The party as it stands, written down. Returns the group it belongs to, made if
    // this set has not been stood in before.
    public KnownGroup Note(IReadOnlyList<(uint Id, string Name, uint Job)> roster)
    {
        var key = KeyFor(roster.Select(r => r.Name));
        if (key.Length == 0) return new KnownGroup();

        // Only a set that has changed counts as a run, or standing still in a duty
        // would age the whole book out by itself.
        var moved = key != _last;
        if (moved)
        {
            _last = key;
            Runs++;
        }

        foreach (var (_, name, job) in roster) Remember(name, job);

        var group = _groups.FirstOrDefault(g => g.Key == key);
        if (group is null)
        {
            group = new KnownGroup { Key = key };
            _groups.Add(group);
        }

        group.Seen = Runs;
        if (moved) group.Runs++;
        _groups = TrimGroups(_groups);

        return group;
    }

    private void Remember(string name, uint job)
    {
        var who = name.Trim();
        if (who.Length == 0) return;

        var known = _people.FirstOrDefault(p =>
            string.Equals(p.Name, who, StringComparison.OrdinalIgnoreCase));

        if (known is null)
        {
            known = new KnownPlayer { Name = who };
            _people.Add(known);
        }

        known.Job = job;
        known.Seen = Runs;
        _people = Trim(_people, MaxPeople, p => p.Seen);
    }

    // The general answers, taken up by a group that has none of its own yet.
    //
    // Only the seats this group has not already answered: a group that has its own
    // answer keeps it, or the last group somebody set up would quietly overwrite the
    // other one every time they raided. Changes made on the page are written straight
    // in by Seat, so this only ever fills gaps.
    public void Learn(KnownGroup group, IReadOnlyDictionary<string, string> named,
        IReadOnlyList<(uint Id, string Name, uint Job)> roster)
    {
        if (group.Key.Length == 0 || named.Count == 0) return;

        var here = new HashSet<string>(roster.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var slot in Audience.Slots)
            if (!group.Seats.ContainsKey(slot)
                && named.TryGetValue(slot, out var who) && here.Contains(who))
                group.Seats[slot] = who;
    }

    // A seat set by hand while this group is stood here, so their answer is theirs and
    // the next group's answer cannot land on it.
    public void Seat(IReadOnlyList<(uint Id, string Name, uint Job)> roster,
        string slot, string name) => SeatIn(Note(roster).Key, slot, name);

    // The same, for a group being set up while standing somewhere else: the night is
    // planned before anybody zones in as often as during.
    public void SeatIn(string key, string slot, string name)
    {
        if (!Audience.IsSlot(slot)) return;
        if (Group(key) is not { } group) return;

        var seat = slot.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(name))
        {
            group.Seats.Remove(seat);
            return;
        }

        var who = name.Trim();
        foreach (var other in Audience.Slots)
            if (other != seat && group.Seats.TryGetValue(other, out var sat)
                && string.Equals(sat, who, StringComparison.OrdinalIgnoreCase))
                group.Seats.Remove(other);

        group.Seats[seat] = who;
    }

    // Back to working this group out from jobs. Their own answers go, and the people
    // stay: forgetting who you raid with is not what clearing a seat means.
    public void Forget(IReadOnlyList<(uint Id, string Name, uint Job)> roster) =>
        ForgetIn(KeyFor(roster.Select(r => r.Name)));

    public void ForgetIn(string key) => Group(key)?.Seats.Clear();

    public KnownGroup? Group(string key) =>
        key.Length == 0 ? null : _groups.FirstOrDefault(g => g.Key == key);

    // The groups somebody named, which is the list the page offers. In name order
    // rather than run order, so the same group is in the same place every time.
    public List<KnownGroup> Saved() =>
        _groups.Where(g => g.Name.Length > 0)
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    // Naming the party stood here, which is what saving one is. Made if this set has
    // not been stood in before, so a group can be named the moment it forms.
    public KnownGroup Save(IReadOnlyList<(uint Id, string Name, uint Job)> roster, string name)
    {
        var group = Note(roster);
        if (group.Key.Length > 0) Rename(group.Key, name);
        return group;
    }

    // One name to a group: two called Static is a list nobody can read, and the second
    // one takes the name.
    public void Rename(string key, string name)
    {
        if (Group(key) is not { } group) return;

        var called = (name ?? "").Trim();
        if (called.Length > MaxName) called = called[..MaxName];

        if (called.Length > 0)
            foreach (var other in _groups)
                if (other.Key != key && string.Equals(other.Name, called, StringComparison.OrdinalIgnoreCase))
                    other.Name = "";

        group.Name = called;
    }

    // Dropped whole, seats and name and all. The people in it stay: they are still
    // people you have raided with, and they are still nameable in every other group.
    public bool Remove(string key)
    {
        if (Group(key) is not { } group) return false;
        return _groups.Remove(group);
    }

    // What this group was called by last time. An exact set first; failing that, the
    // nearest set that shares most of these people, which is the same eight with one
    // stand-in and is the common case on any given night.
    //
    // Only the people actually stood here are handed back, so a near match can never
    // name somebody who is not in the duty.
    public Dictionary<string, string> SeatsFor(IReadOnlyList<(uint Id, string Name, uint Job)> roster)
    {
        var seats = new Dictionary<string, string>(8);
        if (roster.Count == 0 || _groups.Count == 0) return seats;

        var here = new HashSet<string>(roster.Select(r => r.Name.Trim().ToLowerInvariant()),
            StringComparer.Ordinal);

        var key = KeyFor(roster.Select(r => r.Name));
        var group = _groups.FirstOrDefault(g => g.Key == key);

        if (group is null)
        {
            // Over half the group shared, or it is not the same group with a
            // stand-in, it is a different group that happens to share somebody.
            var need = Math.Max(2, (here.Count / 2) + 1);
            group = _groups
                .Where(g => g.Seats.Count > 0)
                .Select(g => (Group: g, Shared: Overlap(g.Key, here)))
                .Where(x => x.Shared >= need)
                .OrderByDescending(x => x.Shared)
                .ThenByDescending(x => x.Group.Seen)
                .Select(x => x.Group)
                .FirstOrDefault();
        }

        if (group is null) return seats;

        foreach (var slot in Audience.Slots)
            if (group.Seats.TryGetValue(slot, out var who)
                && here.Contains(who.Trim().ToLowerInvariant()))
                seats[slot] = who;

        return seats;
    }

    // How many times this exact set has been stood in, for the page to say so.
    public int RunsWith(IReadOnlyList<(uint Id, string Name, uint Job)> roster)
    {
        var key = KeyFor(roster.Select(r => r.Name));
        return _groups.FirstOrDefault(g => g.Key == key)?.Runs ?? 0;
    }

    // Everybody the book holds, most recently seen first, for the seat picker: the
    // person who was not online when you sat down to set this up is still nameable.
    public List<string> Everyone() =>
        _people.OrderByDescending(p => p.Seen).Select(p => p.Name).ToList();

    private static int Overlap(string key, HashSet<string> here)
    {
        var shared = 0;
        foreach (var name in key.Split('|', StringSplitOptions.RemoveEmptyEntries))
            if (here.Contains(name)) shared++;
        return shared;
    }

    // A named group is kept whatever its age: somebody typed that name on purpose,
    // and the nights between two statics' raids are long enough to age one out.
    private static List<KnownGroup> TrimGroups(List<KnownGroup> groups) =>
        groups.Count <= MaxGroups
            ? groups
            : groups
                .OrderByDescending(g => g.Name.Length > 0)
                .ThenByDescending(g => g.Seen)
                .Take(MaxGroups)
                .ToList();

    // Keeps the newest by run stamp and drops the rest, so the bound holds however the
    // list arrived: grown here, or read off a config file somebody edited.
    private static List<T> Trim<T>(List<T> items, int max, Func<T, int> seen) =>
        items.Count <= max
            ? items
            : items.OrderByDescending(seen).Take(max).ToList();
}
