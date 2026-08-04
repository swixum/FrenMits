using System;
using System.Collections.Generic;
using System.Globalization;

namespace FrenMits.Meter;

// Splits each hit's raid-buff gain between the buffers behind it.
public class RdpsEngine
{
    // Crit pays 1.35 plus crit rate; a direct hit is +25%.
    public const float McBase = 1.35f;
    public const float DhMult = 1.25f;

    // Stand-ins until a player's real rates are learned.
    public const float DefaultCritRate = 0.25f;
    public const float DefaultDirectHitRate = 0.30f;
    public const int MinRateSamples = 40;

    // ---- per-second credit buckets ----

    private sealed class Sums { public double Given; public double Received; }

    private readonly Dictionary<long, Dictionary<string, Sums>> _buckets = new();

    // The newest event second, the anchor for window queries.
    public long LatestSec { get; private set; }

    // Player name off the swap line, for the parser's rows.
    public string LocalPlayerName { get; private set; } = "";

    // Limit break damage is unbuffable, so it pays nothing.
    public Func<uint, bool>? IsLimitBreak;

    // The limit break this fight saw, for its row icon.
    public uint LastLimitBreak { get; private set; }

    // ---- id lookups ----

    // English name to ids, null while the sheets load.
    public Func<string, List<uint>?>? ResolveStatusIds;
    public Func<string, List<uint>?>? ResolveActionIds;

    private bool _idsReady;
    private int _idGate;
    private Dictionary<uint, RaidBuffs.Buff>? _buffIds;
    private Dictionary<uint, Guard>? _guardIds;
    private HashSet<uint>? _songIds;
    private HashSet<uint>? _critIds;
    private HashSet<uint>? _critDhIds;
    private HashSet<uint>? _opoActionIds;
    private HashSet<uint>? _irActionIds;
    private Dictionary<uint, (bool Tech, float Mult)>? _finishIds;

    // The build waits on the sheets, so retry off the clock, not the feed's volume.
    public void PrimeIds() => TryBuildIds();

    // Ids stay true in every client language.
    private void TryBuildIds()
    {
        if (_idsReady || ResolveStatusIds == null) return;
        if (_idGate-- > 0) return;
        _idGate = 256;
        if (ResolveStatusIds("Embolden") == null) return;

        var buffs = new Dictionary<uint, RaidBuffs.Buff>();
        foreach (var b in RaidBuffs.All)
            foreach (var id in ResolveStatusIds(b.Name) ?? new List<uint>())
                buffs.TryAdd(id, b);

        var guards = new Dictionary<uint, Guard>();
        void G(string name, Guard g)
        {
            foreach (var id in ResolveStatusIds(name) ?? new List<uint>())
                guards.TryAdd(id, g);
        }
        G("Life Surge", Guard.Crit);
        G("Reassembled", Guard.Crit | Guard.Dh);
        G("Inner Release", Guard.InnerRelease);
        G("Opo-opo Form", Guard.OpoForm);
        G("Formless Fist", Guard.OpoForm);

        var songs = new HashSet<uint>();
        foreach (var s in new[] { "Mage's Ballad", "Army's Paeon", "The Wanderer's Minuet" })
            foreach (var id in ResolveStatusIds(s) ?? new List<uint>())
                songs.Add(id);

        HashSet<uint>? Acts(IEnumerable<string> names)
        {
            if (ResolveActionIds == null) return null;
            var set = new HashSet<uint>();
            foreach (var n in names)
                foreach (var id in ResolveActionIds(n) ?? new List<uint>())
                    set.Add(id);
            return set;
        }
        _critIds = Acts(AutoCrit);
        _critDhIds = Acts(AutoCritDh);
        _opoActionIds = Acts(OpoActions);
        _irActionIds = Acts(InnerReleaseActions);

        if (ResolveActionIds != null)
        {
            var fin = new Dictionary<uint, (bool, float)>();
            void F(string name, bool tech, float mult)
            {
                foreach (var id in ResolveActionIds(name) ?? new List<uint>())
                    fin.TryAdd(id, (tech, mult));
            }
            F("Single Technical Finish", true, 1.01f);
            F("Double Technical Finish", true, 1.02f);
            F("Triple Technical Finish", true, 1.03f);
            F("Quadruple Technical Finish", true, 1.05f);
            F("Single Standard Finish", false, 1.02f);
            F("Double Standard Finish", false, 1.05f);
            _finishIds = fin;
        }

        _buffIds = buffs;
        _guardIds = guards;
        _songIds = songs;
        _idsReady = true;
    }

    // Whoever the party is hitting, for naming the encounter.
    public string CurrentEnemy { get; private set; } = "";

    // ---- actor bookkeeping ----

    private readonly Dictionary<uint, string> _names = new();
    private readonly Dictionary<uint, uint> _owner = new();     // pet -> owning player
    private readonly Dictionary<uint, JobRole> _roles = new();  // player -> role, for cards

    private sealed class ActiveBuff
    {
        public RaidBuffs.Buff Def = null!;
        public uint SourceId;
        public string SourceName = "";
        public int Stacks;
        public long ExpireSec;
        // Strengths only the application knows.
        public RaidBuffs.Effect[]? Resolved;
    }

    // Buffs by the actor carrying them, player or enemy.
    private readonly Dictionary<uint, List<ActiveBuff>> _buffs = new();

    // ---- learned crit and direct hit rates ----

    private sealed class Rates
    {
        public int CHits, Crits, DHits, Dhs;
        public double BuffC, BuffD;
    }

    private readonly Dictionary<uint, Rates> _rates = new();

    // Gear rates: observed, with the buffed share backed out.
    private (double Cs, double Ds) BaseRates(uint owner)
    {
        var cs = (double)DefaultCritRate;
        var ds = (double)DefaultDirectHitRate;
        if (_rates.TryGetValue(owner, out var r))
        {
            if (r.CHits >= MinRateSamples)
                cs = Math.Clamp((r.Crits - r.BuffC) / r.CHits, 0.05, 0.40);
            if (r.DHits >= MinRateSamples)
                ds = Math.Clamp((r.Dhs - r.BuffD) / r.DHits, 0.00, 0.60);
        }
        return (cs, ds);
    }

    // ---- guaranteed crits and direct hits ----

    [Flags]
    private enum Guard { None = 0, Crit = 1, Dh = 2, InnerRelease = 4, OpoForm = 8 }

    private readonly Dictionary<uint, Guard> _guards = new();

    private static readonly HashSet<string> AutoCritDh = new(StringComparer.OrdinalIgnoreCase)
    {
        "Inner Chaos", "Chaotic Cyclone", "Primal Rend", "Primal Ruination",
        "Full Metal Field", "Hammer Stamp", "Hammer Brush", "Polishing Hammer",
        "Starfall Dance",
    };

    private static readonly HashSet<string> AutoCrit = new(StringComparer.OrdinalIgnoreCase)
    {
        "Midare Setsugekka", "Kaeshi: Setsugekka", "Tendo Setsugekka",
        "Tendo Kaeshi Setsugekka", "Ogi Namikiri", "Kaeshi: Namikiri",
        "Shadow of the Destroyer",
    };

    // Guaranteed crits only under an opo-opo or formless form.
    private static readonly HashSet<string> OpoActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bootshine", "Leaping Opo",
    };

    private static readonly HashSet<string> InnerReleaseActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Fell Cleave", "Decimate",
    };

    // Public so a test can ask what a hit was entitled to.
    public (bool Crit, bool Dh) GuaranteeFor(uint owner, string action, uint actionId)
        => Guarantee(owner, action, actionId);

    private (bool Crit, bool Dh) Guarantee(uint owner, string action, uint actionId)
    {
        var g = _guards.TryGetValue(owner, out var v) ? v : Guard.None;
        if (actionId != 0 && _critDhIds != null && _critIds != null
            && _opoActionIds != null && _irActionIds != null)
        {
            if (_critDhIds.Contains(actionId)) return (true, true);
            if (_critIds.Contains(actionId)) return (true, false);
            if (_opoActionIds.Contains(actionId)) return ((g & Guard.OpoForm) != 0, (g & Guard.Dh) != 0);
            if ((g & Guard.InnerRelease) != 0 && _irActionIds.Contains(actionId)) return (true, true);
        }
        if (action.Length > 0)
        {
            if (AutoCritDh.Contains(action)) return (true, true);
            if (AutoCrit.Contains(action)) return (true, false);
            if (OpoActions.Contains(action)) return ((g & Guard.OpoForm) != 0, (g & Guard.Dh) != 0);
            if ((g & Guard.InnerRelease) != 0 && InnerReleaseActions.Contains(action))
                return (true, true);
        }
        return ((g & Guard.Crit) != 0, (g & Guard.Dh) != 0);
    }

    // ---- bard songs, for Radiant Finale ----

    private readonly Dictionary<uint, HashSet<string>> _songs = new();
    private readonly Dictionary<uint, (float Mult, long At)> _finale = new();

    private bool IsSong(uint statusId, string status)
        => (_songIds != null && _songIds.Contains(statusId))
           || status.Equals("Mage's Ballad", StringComparison.OrdinalIgnoreCase)
           || status.Equals("Army's Paeon", StringComparison.OrdinalIgnoreCase)
           || status.Equals("The Wanderer's Minuet", StringComparison.OrdinalIgnoreCase);

    private float FinaleMult(uint bard, long sec)
    {
        // One resolve covers the whole party's applications.
        if (_finale.TryGetValue(bard, out var f) && sec - f.At <= 4) return f.Mult;
        var codas = _songs.TryGetValue(bard, out var s) ? s.Count : 0;
        var mult = codas switch { 1 => 1.02f, 2 => 1.04f, _ => 1.06f };
        _finale[bard] = (mult, sec);
        s?.Clear();
        return mult;
    }

    // ---- dance finish steps ----

    private readonly Dictionary<uint, float> _finishTech = new();
    private readonly Dictionary<uint, float> _finishStd = new();

    private void Sniff(uint owner, string action, uint actionId)
    {
        if (_finishIds != null && actionId != 0 && _finishIds.TryGetValue(actionId, out var f))
        {
            if (f.Tech) _finishTech[owner] = f.Mult;
            else _finishStd[owner] = f.Mult;
            return;
        }
        // Name fallback without the lowercase allocation.
        if (!action.EndsWith("Finish", StringComparison.OrdinalIgnoreCase)) return;
        if (Eq(action, "Single Technical Finish")) _finishTech[owner] = 1.01f;
        else if (Eq(action, "Double Technical Finish")) _finishTech[owner] = 1.02f;
        else if (Eq(action, "Triple Technical Finish")) _finishTech[owner] = 1.03f;
        else if (Eq(action, "Quadruple Technical Finish")) _finishTech[owner] = 1.05f;
        else if (Eq(action, "Single Standard Finish")) _finishStd[owner] = 1.02f;
        else if (Eq(action, "Double Standard Finish")) _finishStd[owner] = 1.05f;
    }

    private static bool Eq(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);

    // ---- per-player breakdowns ----

    // What each player used, hit, and was hit by.
    private readonly Dictionary<string, Dictionary<string, AbilityStat>> _dealt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, AbilityStat>> _targets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, AbilityStat>> _taken = new(StringComparer.OrdinalIgnoreCase);

    // The same three for healing.
    private readonly Dictionary<string, Dictionary<string, AbilityStat>> _healDealt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, AbilityStat>> _healTargets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, AbilityStat>> _healFrom = new(StringComparer.OrdinalIgnoreCase);

    // Status names by id, so a damage-over-time tick is named.
    private readonly Dictionary<uint, string> _effectNames = new();

    private static void Tally(Dictionary<string, Dictionary<string, AbilityStat>> table,
        string who, string what, double dmg, bool crit, bool dh, uint id = 0, bool status = false,
        double over = 0)
    {
        if (who.Length == 0 || what.Length == 0) return;
        // A heal swallowed by a full bar still happened.
        if (dmg <= 0 && over <= 0) return;
        if (!table.TryGetValue(who, out var by))
            table[who] = by = new Dictionary<string, AbilityStat>(StringComparer.OrdinalIgnoreCase);
        if (!by.TryGetValue(what, out var s))
            by[what] = s = new AbilityStat { Name = what, Id = id, IsStatus = status };
        s.Hits++;
        if (crit) s.Crits++;
        if (dh) s.Dhs++;
        if (crit && dh) s.Cdhs++;
        s.Damage += dmg;
        s.Over += over;
        if (dmg > s.Max) s.Max = dmg;
    }

    private static List<AbilityStat> Ranked(Dictionary<string, Dictionary<string, AbilityStat>> table, string who)
    {
        var list = new List<AbilityStat>();
        if (who.Length > 0 && table.TryGetValue(who, out var by)) list.AddRange(by.Values);
        // Overhealing only breaks ties, so wasted casts sit below.
        list.Sort((a, b) => a.Damage != b.Damage ? b.Damage.CompareTo(a.Damage) : b.Over.CompareTo(a.Over));
        return list;
    }

    // Everyone seen dealing damage this fight.
    public IEnumerable<string> Dealers() => _dealt.Keys;

    // Every point counted off the log lines this fight.
    public double DealtTotal { get; private set; }

    // Event-exact roll counts and biggest hit.
    public (int Hits, int Crits, int Dhs, int Cdhs, double MaxHit, string MaxHitName) DealtFacts(string player)
    {
        int hits = 0, crits = 0, dhs = 0, cdhs = 0;
        double max = 0;
        var maxName = "";
        if (player.Length > 0 && _dealt.TryGetValue(player, out var by))
            foreach (var a in by.Values)
            {
                hits += a.Hits;
                crits += a.Crits;
                dhs += a.Dhs;
                cdhs += a.Cdhs;
                if (a.Max > max) { max = a.Max; maxName = a.Name; }
            }
        return (hits, crits, dhs, cdhs, max, maxName);
    }

    // The same for healing, for an exact overheal share.
    public (double Landed, double Over) HealFacts(string player)
    {
        double landed = 0, over = 0;
        if (player.Length > 0 && _healDealt.TryGetValue(player, out var by))
            foreach (var a in by.Values)
            {
                landed += a.Damage;
                over += a.Over;
            }
        return (landed, over);
    }

    public List<AbilityStat> Dealt(string player) => Ranked(_dealt, player);
    public List<AbilityStat> Targets(string player) => Ranked(_targets, player);
    public List<AbilityStat> Taken(string player) => Ranked(_taken, player);
    public List<AbilityStat> Heals(string player) => Ranked(_healDealt, player);
    public List<AbilityStat> HealTargets(string player) => Ranked(_healTargets, player);
    public List<AbilityStat> HealFrom(string player) => Ranked(_healFrom, player);

    // ---- buff credit, player to player ----

    // Who fed whose rDPS, and off which buff.
    private readonly Dictionary<string, Dictionary<string, Dictionary<string, double>>> _pairs
        = new(StringComparer.OrdinalIgnoreCase);

    // One player's share, with the buffs behind it.
    private static AbilityStat Trade(string who, Dictionary<string, double> buffs)
    {
        var row = new AbilityStat { Name = who, Parts = new List<AbilityStat>() };
        foreach (var (buff, amount) in buffs)
        {
            if (amount <= 0) continue;
            row.Damage += amount;
            row.Parts.Add(new AbilityStat { Name = buff, Damage = amount });
        }
        row.Parts.Sort((a, b) => b.Damage.CompareTo(a.Damage));
        return row;
    }

    public List<AbilityStat> Given(string player)
    {
        var list = new List<AbilityStat>();
        if (player.Length > 0 && _pairs.TryGetValue(player, out var by))
            foreach (var (to, buffs) in by)
                if (Trade(to, buffs) is { Damage: > 0 } row)
                    list.Add(row);
        list.Sort((a, b) => b.Damage.CompareTo(a.Damage));
        return list;
    }

    public List<AbilityStat> Received(string player)
    {
        var list = new List<AbilityStat>();
        if (player.Length > 0)
            foreach (var (from, by) in _pairs)
                if (by.TryGetValue(player, out var buffs) && Trade(from, buffs) is { Damage: > 0 } row)
                    list.Add(row);
        list.Sort((a, b) => b.Damage.CompareTo(a.Damage));
        return list;
    }

    // ---- deaths ----

    // The last few things that landed on each player.
    private readonly Dictionary<string, List<DeathHit>> _recent = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<DeathRecord> _deaths = new();
    private const int LeadIn = 6;

    private void Recent(string who, string what, double amount, long sec, bool heal)
    {
        if (who.Length == 0 || what.Length == 0 || amount <= 0) return;
        if (!_recent.TryGetValue(who, out var list)) _recent[who] = list = new List<DeathHit>();
        list.Add(new DeathHit { Name = what, Amount = amount, Sec = sec, Heal = heal });
        while (list.Count > LeadIn + 1) list.RemoveAt(0);
    }

    private void RecordDeath(string who, long sec)
    {
        var rec = new DeathRecord { Name = who, Sec = sec };
        if (_recent.TryGetValue(who, out var list))
        {
            // The last thing that hurt them is the killing blow.
            var blow = -1;
            for (var i = list.Count - 1; i >= 0; i--)
                if (!list[i].Heal) { blow = i; break; }
            if (blow >= 0)
            {
                rec.Killer = list[blow].Name;
                rec.KillingBlow = list[blow].Amount;
            }
            for (var i = 0; i < list.Count; i++)
                if (i != blow)
                    rec.Lead.Add(list[i]);
            list.Clear();
        }
        _deaths.Add(rec);
        while (_deaths.Count > 64) _deaths.RemoveAt(0);
    }

    public List<DeathRecord> Deaths() => new(_deaths);

    // ---- overhealing ----

    // A heal line carries the health it landed on.
    private static int Room(string cur, string max)
    {
        var c = Dec(cur);
        var m = Dec(max);
        return c < 0 || m <= 0 ? -1 : Math.Max(0, m - c);
    }

    private static int Dec(string s)
        => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : -1;

    // What a heal does to the room it had.
    private static (double Landed, double Over) Landing(uint amount, ref int room)
    {
        if (room < 0) return (amount, 0);
        double landed = Math.Min(amount, room);
        room -= (int)landed;
        return (landed, amount - landed);
    }

    // Damage rolls sit in the high nibble of the second byte, healing in a bit of its own.
    private const uint DamageCrit = 0x2000;
    private const uint DamageDh = 0x4000;
    private const uint HealCrit = 0x200000;

    // One heal, onto every table that wants it.
    private void Heal(string healer, string who, string what, uint id, bool status,
        uint amount, bool crit, ref int room, long sec)
    {
        if (amount == 0 || who.Length == 0) return;
        var (landed, over) = Landing(amount, ref room);
        if (healer.Length > 0)
        {
            Tally(_healDealt, healer, what, landed, crit, dh: false, id, status, over);
            Tally(_healTargets, healer, who, landed, crit, dh: false, over: over);
            Tally(_healFrom, who, healer, landed, crit, dh: false, over: over);
        }
        Recent(who, what, landed, sec, heal: true);
    }

    public void ClearBreakdown()
    {
        DealtTotal = 0;
        LastLimitBreak = 0;
        _dealt.Clear();
        _targets.Clear();
        _taken.Clear();
        _healDealt.Clear();
        _healTargets.Clear();
        _healFrom.Clear();
        _pairs.Clear();
        _recent.Clear();
        _deaths.Clear();
    }

    // ---- damage over time snapshots ----

    // A dot locks its buffs in at application, not per tick.
    private sealed class DotSnap
    {
        public List<(uint Src, string Name, string Buff, double Mult)> Flat = new();
        public List<(uint Src, string Name, string Buff, double Rate)> Crit = new();
        public List<(uint Src, string Name, string Buff, double Rate)> Dh = new();
        public double SelfCrit, SelfDh;
        public long ExpireSec;
    }

    private readonly Dictionary<(uint Target, uint Effect, uint Source), DotSnap> _dotSnaps = new();

    private static bool IsPlayer(uint id) => id is >= 0x10000000 and < 0x20000000;

    // Owned allies that fight for themselves, told apart by job.
    private readonly HashSet<uint> _allies = new();

    private bool IsCombatant(uint id) => IsPlayer(id) || _allies.Contains(id);

    // Who a damage source belongs to.
    // A party member the log never introduced. Duty Support allies are only ever announced
    // by an AddCombatant line, so loading mid-duty leaves them unrecognised for the whole run.
    public void NoteAlly(uint id, string name, uint jobRowId)
    {
        if (id is 0 or 0xE0000000) return;
        if (name.Length > 0) _names[id] = name;
        if (Jobs.ByRowId(jobRowId) is { } job) _roles[id] = job.Role;
        // Being in the party is the point, not the job: the log announces Duty Support
        // allies with no job at all, and without this they fold into their owner's row.
        if (!IsPlayer(id)) _allies.Add(id);
    }

    private uint OwnerOf(uint id)
    {
        if (IsCombatant(id)) return id;
        return _owner.TryGetValue(id, out var o) && IsCombatant(o) ? o : 0;
    }

    // ---- line dispatch ----

    // The line types dispatched below, so a feed can skip the rest before building one.
    public static bool Handles(string opcode)
        => opcode is "21" or "22" or "24" or "26" or "30" or "03" or "25" or "02" or "01";

    public void Process(string[] f)
    {
        if (f.Length < 2 || !Handles(f[0])) return;
        TryBuildIds();
        switch (f[0])
        {
            case "21" or "22": OnAbility(f); break;
            case "24": OnTick(f); break;
            case "26": OnGain(f); break;
            case "30": OnLose(f); break;
            case "03": OnAdd(f); break;
            case "25":
                if (f.Length > 2)
                {
                    var dead = Hex(f[2]);
                    _buffs.Remove(dead);
                    _guards.Remove(dead);
                    DropSnaps(dead);
                    if (IsCombatant(dead))
                    {
                        var who = f.Length > 3 && f[3].Length > 0
                            ? f[3]
                            : _names.TryGetValue(dead, out var n) ? n : "";
                        if (who.Length > 0) RecordDeath(who, Sec(f[1]));
                    }
                }
                break;
            case "02":
                if (f.Length > 3) { _names[Hex(f[2])] = f[3]; LocalPlayerName = f[3]; }
                break;
            case "01":
                _buffs.Clear();
                _dotSnaps.Clear();
                _guards.Clear();
                _songs.Clear();
                _finale.Clear();
                _finishTech.Clear();
                _finishStd.Clear();
                // Ids belong to whoever holds them in the new zone.
                _allies.Clear();
                _owner.Clear();
                _names.Clear();
                _roles.Clear();
                _rates.Clear();
                _effectNames.Clear();
                ClearBreakdown();
                break;
        }
    }

    private void OnAdd(string[] f)
    {
        if (f.Length < 7) return;
        var id = Hex(f[2]);
        if (id == 0) return;
        _names[id] = f[3];
        var owner = Hex(f[6]);
        // An ownerless re-add on a recycled id must not inherit the old pet's owner.
        if (owner != 0) _owner[id] = owner;
        else _owner.Remove(id);
        if (Jobs.ByRowId(Hex(f[4])) is not { } job)
        {
            // Ids get reused, so a jobless one is no longer an ally.
            _allies.Remove(id);
            _roles.Remove(id);
            return;
        }
        _roles[id] = job.Role;
        // Carrying a job means it acts on its own.
        if (!IsPlayer(id)) _allies.Add(id);
    }

    // A guard status by id first, then by English name.
    private Guard GuardOf(uint statusId, string status)
    {
        if (_guardIds != null && _guardIds.TryGetValue(statusId, out var byId)) return byId;
        if (Eq(status, "Life Surge")) return Guard.Crit;
        if (Eq(status, "Reassembled")) return Guard.Crit | Guard.Dh;
        if (Eq(status, "Inner Release")) return Guard.InnerRelease;
        if (Eq(status, "Opo-opo Form") || Eq(status, "Formless Fist")) return Guard.OpoForm;
        return Guard.None;
    }

    private RaidBuffs.Buff? FindBuff(uint statusId, string status)
        => _buffIds != null && _buffIds.TryGetValue(statusId, out var byId)
            ? byId
            : RaidBuffs.Find(status);

    private void OnGain(string[] f)
    {
        if (f.Length < 10) return;
        var status = f[3];
        var statusId = Hex(f[2]);
        var tgt = Hex(f[7]);
        var src = Hex(f[5]);
        var sec = Sec(f[1]);
        if (status.Length > 0) _effectNames[statusId] = status;

        // Any player status on an enemy could tick, so freeze it.
        if (tgt >= 0x40000000 && OwnerOf(src) != 0)
        {
            float.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var dotDur);
            SnapshotDot(tgt, statusId, src, sec, dotDur);
        }

        // Statuses that make later hits guaranteed rolls.
        if (IsCombatant(tgt))
        {
            var add = GuardOf(statusId, status);
            if (add != Guard.None)
                _guards[tgt] = (_guards.TryGetValue(tgt, out var g) ? g : Guard.None) | add;
        }

        // A song starting is a coda banked for the next finale.
        if (IsSong(statusId, status) && IsCombatant(src))
            (_songs.TryGetValue(src, out var set)
                ? set
                : _songs[src] = new HashSet<string>(StringComparer.OrdinalIgnoreCase))
                .Add(status);

        if (FindBuff(statusId, status) is not { } def) return;
        if (tgt == 0) return;
        var sourceName = f[6];
        if (sourceName.Length > 0 && src != 0) _names[src] = sourceName;
        else if (sourceName.Length == 0) _names.TryGetValue(src, out sourceName!);
        // A buff can name its target before any add line does.
        if (f[8].Length > 0 && tgt != 0) _names[tgt] = f[8];

        float.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var dur);
        // A missing duration gets a cap, so nothing sticks forever.
        var expire = sec + (long)MathF.Ceiling(dur > 0f ? dur : 30f) + 1;

        // Strengths the status name alone can't tell.
        RaidBuffs.Effect[]? resolved =
            Eq(def.Name, "Radiant Finale")
                ? new[] { new RaidBuffs.Effect(RaidBuffs.Kind.Damage, FinaleMult(src, sec)) }
            : Eq(def.Name, "Technical Finish")
                ? new[]
                {
                    new RaidBuffs.Effect(RaidBuffs.Kind.Damage, _finishTech.TryGetValue(src, out var t) ? t : 1.05f),
                }
            : Eq(def.Name, "Standard Finish")
                ? new[]
                {
                    new RaidBuffs.Effect(RaidBuffs.Kind.Damage, _finishStd.TryGetValue(src, out var st) ? st : 1.05f),
                }
            : null;

        if (!_buffs.TryGetValue(tgt, out var list)) _buffs[tgt] = list = new List<ActiveBuff>();
        // A refresh replaces the copy from the same source.
        list.RemoveAll(b => b.Def == def && b.SourceId == src);
        list.Add(new ActiveBuff
        {
            Def = def, SourceId = src, SourceName = sourceName ?? "",
            Stacks = (int)Hex(f[9]), ExpireSec = expire, Resolved = resolved,
        });
    }

    private void OnLose(string[] f)
    {
        if (f.Length < 9) return;
        var statusId = Hex(f[2]);
        var tgt = Hex(f[7]);
        var src = Hex(f[5]);
        _dotSnaps.Remove((tgt, statusId, src));

        if (IsCombatant(tgt))
        {
            var drop = GuardOf(statusId, f[3]);
            if (drop != Guard.None && _guards.TryGetValue(tgt, out var g))
            {
                g &= ~drop;
                if (g == Guard.None) _guards.Remove(tgt); else _guards[tgt] = g;
            }
        }

        if (FindBuff(statusId, f[3]) is not { } def) return;
        if (!_buffs.TryGetValue(tgt, out var list)) return;
        list.RemoveAll(b => b.Def == def && (src == 0 || b.SourceId == src));
    }

    // ---- damage events ----

    private void OnAbility(string[] f)
    {
        if (f.Length < 10) return;
        var src = Hex(f[2]);
        var owner = OwnerOf(src);
        var target = Hex(f[6]);
        if (owner == 0)
        {
            // An enemy swinging: only the taken breakdown wants it.
            if (IsCombatant(target)) OnTaken(f, target);
            return;
        }
        var action = f[5];
        var actionId = Hex(f[4]);
        if (action.Length > 0) Sniff(owner, action, actionId);
        // Anything aimed at the party is healing.
        if (IsCombatant(target)) { OnHeal(f, owner, target); return; }
        if (target < 0x40000000) return;     // only damage into enemies counts
        if (f[7].Length > 0) CurrentEnemy = f[7];
        var sec = Sec(f[1]);
        if (sec > LatestSec) LatestSec = sec;
        if (IsLimitBreak?.Invoke(actionId) == true)
        {
            LastLimitBreak = actionId;
            // Counted under its own row, paying no credit.
            TallyLimitBreak(f, action, actionId);
            return;
        }

        if (src == owner && f[3].Length > 0) _names[owner] = f[3];
        if (!_names.TryGetValue(owner, out var ownerName) || ownerName.Length == 0) return;

        var (gc, gd) = Guarantee(owner, action, actionId);

        // A hit into an enemy can heal whoever landed it.
        var mine = src == owner;
        var selfRoom = mine && f.Length > 35 ? Room(f[34], f[35]) : -1;

        // Eight flag and value pairs, damage in the low byte.
        for (var i = 8; i + 1 < f.Length && i <= 22; i += 2)
        {
            var flags = Hex(f[i]);
            if (mine && (flags & 0xFF) == 0x04)
            {
                Heal(ownerName, ownerName, action, Hex(f[4]), status: false,
                    Unscramble(HexLong(f[i + 1])), (flags & HealCrit) != 0, ref selfRoom, sec);
                continue;
            }
            if ((flags & 0xFF) is not (0x03 or 0x05 or 0x06)) continue;
            var dmg = Unscramble(HexLong(f[i + 1]));
            if (dmg == 0) continue;
            var crit = (flags & DamageCrit) != 0;
            var dh = (flags & DamageDh) != 0;

            Tally(_dealt, ownerName, action.Length > 0 ? action : "Attack", dmg, crit, dh, Hex(f[4]));
            DealtTotal += dmg;
            if (f[7].Length > 0) Tally(_targets, ownerName, f[7], dmg, crit, dh);

            Gather(owner, target, sec);
            Learn(owner, crit, dh, gc && crit, gd && dh);
            Allocate(owner, ownerName, dmg, crit, dh, gc && crit, gd && dh, sec);
        }
    }

    // The row the parser files limit break damage under.
    public const string LimitBreakName = "Limit Break";

    private void TallyLimitBreak(string[] f, string action, uint actionId)
    {
        for (var i = 8; i + 1 < f.Length && i <= 22; i += 2)
        {
            var flags = Hex(f[i]);
            if ((flags & 0xFF) is not (0x03 or 0x05 or 0x06)) continue;
            var dmg = Unscramble(HexLong(f[i + 1]));
            if (dmg == 0) continue;
            Tally(_dealt, LimitBreakName, action.Length > 0 ? action : "Limit Break", dmg,
                crit: false, dh: false, actionId);
            DealtTotal += dmg;
            if (f[7].Length > 0) Tally(_targets, LimitBreakName, f[7], dmg, crit: false, dh: false);
        }
    }

    // Enemy damage into a player, for their taken breakdown.
    private void OnTaken(string[] f, uint target)
    {
        var who = f[7];
        if (who.Length == 0 && !_names.TryGetValue(target, out who!)) return;
        var action = f[5].Length > 0 ? f[5] : "Attack";
        var id = Hex(f[4]);
        var sec = Sec(f[1]);
        for (var i = 8; i + 1 < f.Length && i <= 22; i += 2)
        {
            var flags = Hex(f[i]);
            if ((flags & 0xFF) is not (0x03 or 0x05 or 0x06)) continue;
            var dmg = Unscramble(HexLong(f[i + 1]));
            if (dmg == 0) continue;
            Tally(_taken, who, action, dmg, (flags & DamageCrit) != 0, (flags & DamageDh) != 0, id);
            Recent(who, action, dmg, sec, heal: false);
        }
    }

    // A party-facing ability, plus what had no room.
    private void OnHeal(string[] f, uint owner, uint target)
    {
        var who = f[7].Length > 0 ? f[7] : _names.TryGetValue(target, out var tn) ? tn : "";
        if (!_names.TryGetValue(owner, out var healer)) healer = "";
        // An unnamed healer still lands on the death recap, it just credits nobody.
        if (who.Length == 0) return;

        var room = f.Length > 25 ? Room(f[24], f[25]) : -1;
        var action = f[5].Length > 0 ? f[5] : "Heal";
        var id = Hex(f[4]);
        var sec = Sec(f[1]);
        for (var i = 8; i + 1 < f.Length && i <= 22; i += 2)
        {
            var flags = Hex(f[i]);
            if ((flags & 0xFF) != 0x04) continue;
            Heal(healer, who, action, id, status: false,
                Unscramble(HexLong(f[i + 1])), (flags & HealCrit) != 0, ref room, sec);
        }
    }

    private void OnTick(string[] f)
    {
        if (f.Length < 19) return;
        var hot = f[4] == "HoT";
        if (!hot && f[4] != "DoT") return;
        var src = Hex(f[17]);
        var owner = OwnerOf(src);
        var target = Hex(f[2]);
        if (IsCombatant(target)) { OnPartyTick(f, owner, target, hot); return; }
        if (owner == 0 || hot) return;
        if (target < 0x40000000) return;
        var dmg = (uint)HexLong(f[6]);
        if (dmg == 0) return;
        var sec = Sec(f[1]);
        if (sec > LatestSec) LatestSec = sec;
        if (!_names.TryGetValue(owner, out var ownerName) || ownerName.Length == 0) return;

        var effect = Hex(f[5]);
        Tally(_dealt, ownerName,
            _effectNames.TryGetValue(effect, out var en) && en.Length > 0 ? en : "Damage over time",
            dmg, crit: false, dh: false, effect, status: true);
        DealtTotal += dmg;
        if (f[3].Length > 0) Tally(_targets, ownerName, f[3], dmg, crit: false, dh: false);

        // Ticks price against the frozen buffs, else what is up.
        if (_dotSnaps.TryGetValue((target, Hex(f[5]), src), out var snap) && sec <= snap.ExpireSec)
            LoadSnap(snap);
        else
            Gather(owner, target, sec);
        AllocateTick(owner, ownerName, dmg, sec);
    }

    // A tick on a party member, which pays no credit.
    private void OnPartyTick(string[] f, uint owner, uint target, bool hot)
    {
        var amount = (uint)HexLong(f[6]);
        var who = f[3].Length > 0 ? f[3] : _names.TryGetValue(target, out var tn) ? tn : "";
        if (amount == 0 || who.Length == 0) return;

        var effect = Hex(f[5]);
        var name = _effectNames.TryGetValue(effect, out var en) && en.Length > 0
            ? en
            : hot ? "Regeneration" : "Damage over time";
        var sec = Sec(f[1]);

        if (!hot)
        {
            Tally(_taken, who, name, amount, crit: false, dh: false, effect, status: true);
            Recent(who, name, amount, sec, heal: false);
            return;
        }

        // An unsourced tick still counts, it just credits nobody.
        var room = Room(f[7], f[8]);
        var healer = owner != 0 && _names.TryGetValue(owner, out var hn) ? hn : "";
        Heal(healer, who, name, effect, status: true, amount, crit: false, ref room, sec);
    }

    // ---- buff state for one event ----

    private readonly List<(uint Src, string Name, string Buff, double Mult)> _extFlat = new();
    private readonly List<(uint Src, string Name, string Buff, double Rate)> _extCrit = new();
    private readonly List<(uint Src, string Name, string Buff, double Rate)> _extDh = new();
    private double _selfCrit, _selfDh;

    private void Gather(uint owner, uint enemy, long sec)
    {
        _extFlat.Clear(); _extCrit.Clear(); _extDh.Clear();
        _selfCrit = _selfDh = 0;
        GatherFrom(owner, onEnemy: false, owner, sec);
        GatherFrom(enemy, onEnemy: true, owner, sec);
    }

    private void GatherFrom(uint carrier, bool onEnemy, uint owner, long sec)
    {
        if (!_buffs.TryGetValue(carrier, out var list)) return;
        _roles.TryGetValue(owner, out var role0);
        JobRole? role = _roles.ContainsKey(owner) ? role0 : null;
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var b = list[i];
            if (sec > b.ExpireSec) { list.RemoveAt(i); continue; }
            if (b.Def.OnEnemy != onEnemy) continue;
            var self = b.SourceId == owner;
            if (!self && b.SourceName.Length == 0) continue;
            foreach (var e in b.Resolved ?? b.Def.For(b.Stacks, role))
            {
                switch (e.Kind)
                {
                    case RaidBuffs.Kind.Damage:
                        // A player's own percent buffs stay personal.
                        if (!self && e.Amount > 1.0001f)
                            _extFlat.Add((b.SourceId, b.SourceName, b.Def.Name, e.Amount));
                        break;
                    case RaidBuffs.Kind.CritRate:
                        if (self) _selfCrit += e.Amount;
                        else _extCrit.Add((b.SourceId, b.SourceName, b.Def.Name, e.Amount));
                        break;
                    default:
                        if (self) _selfDh += e.Amount;
                        else _extDh.Add((b.SourceId, b.SourceName, b.Def.Name, e.Amount));
                        break;
                }
            }
        }
    }

    private void LoadSnap(DotSnap snap)
    {
        _extFlat.Clear(); _extCrit.Clear(); _extDh.Clear();
        _extFlat.AddRange(snap.Flat);
        _extCrit.AddRange(snap.Crit);
        _extDh.AddRange(snap.Dh);
        _selfCrit = snap.SelfCrit;
        _selfDh = snap.SelfDh;
    }

    private void Learn(uint owner, bool crit, bool dh, bool gCrit, bool gDh)
    {
        if (!_rates.TryGetValue(owner, out var r)) _rates[owner] = r = new Rates();
        double extC = 0, extD = 0;
        foreach (var b in _extCrit) extC += b.Rate;
        foreach (var b in _extDh) extD += b.Rate;
        if (!gCrit)
        {
            r.CHits++;
            if (crit) r.Crits++;
            r.BuffC += _selfCrit + extC;
        }
        if (!gDh)
        {
            r.DHits++;
            if (dh) r.Dhs++;
            r.BuffD += _selfDh + extD;
        }
    }

    // ---- the split ----

    private void Allocate(uint owner, string ownerName, double dmg,
        bool crit, bool dh, bool gCrit, bool gDh, long sec)
    {
        if (_extFlat.Count == 0 && _extCrit.Count == 0 && _extDh.Count == 0) return;

        var (cs, ds) = BaseRates(owner);
        var mc = McBase + cs;
        double extC = 0, extD = 0;
        foreach (var b in _extCrit) extC += b.Rate;
        foreach (var b in _extDh) extD += b.Rate;

        // How much of a rolled crit the buffs caused.
        var cb = Math.Min(1.0, cs + _selfCrit + extC);
        var cu = Math.Min(cb, cs + _selfCrit);
        var critShare = crit && !gCrit && extC > 0 ? (cb - cu) / cb : 0.0;
        var db = Math.Min(1.0, ds + _selfDh + extD);
        var du = Math.Min(db, ds + _selfDh);
        var dhShare = dh && !gDh && extD > 0 ? (db - du) / db : 0.0;

        // A guaranteed roll pays its rate buffs as flat bonus.
        var rc = gCrit && extC > 0
            ? (1.0 + (mc - 1.0) * (_selfCrit + extC)) / (1.0 + (mc - 1.0) * _selfCrit) : 1.0;
        var rd = gDh && extD > 0
            ? (1.0 + (DhMult - 1.0) * (_selfDh + extD)) / (1.0 + (DhMult - 1.0) * _selfDh) : 1.0;

        // Four corners: buff-caused and self-covered slices.
        Span<double> critW = stackalloc double[2] { 1.0 - critShare, critShare };
        Span<double> dhW = stackalloc double[2] { 1.0 - dhShare, dhShare };
        for (var ci = 0; ci < 2; ci++)
        {
            for (var di = 0; di < 2; di++)
            {
                var part = dmg * critW[ci] * dhW[di];
                if (part <= 0) continue;
                Split(owner, ownerName, part,
                    critMult: ci == 1 ? mc : 1.0,
                    dhMult: di == 1 ? DhMult : 1.0,
                    rc, rd, extC, extD, sec);
            }
        }
    }

    // One slice, split by log ratio across the buffs.
    private void Split(uint owner, string ownerName, double part,
        double critMult, double dhMult, double rc, double rd,
        double extC, double extD, long sec)
    {
        var m = critMult * dhMult * rc * rd;
        foreach (var b in _extFlat) m *= b.Mult;
        if (m <= 1.0001) return;
        var lnM = Math.Log(m);
        var gain = part * (m - 1.0) / m;

        foreach (var b in _extFlat)
            Credit(sec, b.Name, ownerName, b.Buff, gain * Math.Log(b.Mult) / lnM);

        // Roll multipliers pay by how much rate each buff gave.
        var critGain = gain * (Math.Log(critMult) + Math.Log(rc)) / lnM;
        if (critGain > 0 && extC > 0)
            foreach (var b in _extCrit)
                Credit(sec, b.Name, ownerName, b.Buff, critGain * b.Rate / extC);

        var dhGain = gain * (Math.Log(dhMult) + Math.Log(rd)) / lnM;
        if (dhGain > 0 && extD > 0)
            foreach (var b in _extDh)
                Credit(sec, b.Name, ownerName, b.Buff, dhGain * b.Rate / extD);
    }

    // A tick has no roll flags, so blend by the odds.
    private void AllocateTick(uint owner, string ownerName, double dmg, long sec)
    {
        if (_extFlat.Count == 0 && _extCrit.Count == 0 && _extDh.Count == 0) return;

        var (cs, ds) = BaseRates(owner);
        var mc = McBase + cs;
        double extC = 0, extD = 0;
        foreach (var b in _extCrit) extC += b.Rate;
        foreach (var b in _extDh) extD += b.Rate;
        var cb = Math.Min(1.0, cs + _selfCrit + extC);
        var db = Math.Min(1.0, ds + _selfDh + extD);

        Span<double> w = stackalloc double[4]
        {
            (1 - cb) * (1 - db),
            cb * (1 - db) * mc,
            (1 - cb) * db * DhMult,
            cb * db * mc * DhMult,
        };
        var t = w[0] + w[1] + w[2] + w[3];
        for (var i = 0; i < 4; i++)
        {
            var part = dmg * w[i] / t;
            if (part <= 0) continue;
            Allocate(owner, ownerName, part, crit: (i & 1) != 0, dh: (i & 2) != 0,
                gCrit: false, gDh: false, sec);
        }
    }

    private void Credit(long sec, string from, string to, string buff, double amount)
    {
        if (amount <= 0 || from.Length == 0) return;
        Bucket(sec, from).Given += amount;
        Bucket(sec, to).Received += amount;

        if (!_pairs.TryGetValue(from, out var by))
            _pairs[from] = by = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);
        if (!by.TryGetValue(to, out var buffs))
            by[to] = buffs = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var what = buff.Length > 0 ? buff : "Buff";
        buffs[what] = buffs.TryGetValue(what, out var running) ? running + amount : amount;
    }

    private void SnapshotDot(uint target, uint effectId, uint source, long sec, float dur)
    {
        var owner = OwnerOf(source);
        Gather(owner, target, sec);
        var snap = new DotSnap
        {
            SelfCrit = _selfCrit, SelfDh = _selfDh,
            ExpireSec = sec + (long)MathF.Ceiling(dur > 0f ? dur : 60f) + 3,
        };
        snap.Flat.AddRange(_extFlat);
        snap.Crit.AddRange(_extCrit);
        snap.Dh.AddRange(_extDh);
        _dotSnaps[(target, effectId, source)] = snap;
    }

    private void DropSnaps(uint actor)
    {
        List<(uint, uint, uint)>? stale = null;
        foreach (var key in _dotSnaps.Keys)
            if (key.Target == actor || key.Source == actor)
                (stale ??= new List<(uint, uint, uint)>()).Add(key);
        if (stale != null)
            foreach (var key in stale)
                _dotSnaps.Remove(key);
    }

    private Sums Bucket(long sec, string name)
    {
        if (!_buckets.TryGetValue(sec, out var bySec)) _buckets[sec] = bySec = new Dictionary<string, Sums>();
        if (!bySec.TryGetValue(name, out var s)) bySec[name] = s = new Sums();
        return s;
    }

    // ---- queries ----

    // Per-player given and received totals from fromSec.
    public Dictionary<string, (double Given, double Received)> WindowTotals(long fromSec)
    {
        var totals = new Dictionary<string, (double, double)>();
        foreach (var (sec, bySec) in _buckets)
        {
            if (sec < fromSec) continue;
            foreach (var (name, s) in bySec)
            {
                totals.TryGetValue(name, out var t);
                totals[name] = (t.Item1 + s.Given, t.Item2 + s.Received);
            }
        }
        return totals;
    }

    // Drop buckets no live window can still need.
    public void Trim()
    {
        if (_dotSnaps.Count > 500)
        {
            List<(uint, uint, uint)>? dead = null;
            foreach (var (key, snap) in _dotSnaps)
                if (snap.ExpireSec < LatestSec)
                    (dead ??= new List<(uint, uint, uint)>()).Add(key);
            if (dead != null)
                foreach (var key in dead)
                    _dotSnaps.Remove(key);
        }
        if (_buckets.Count < 3000) return;
        var floor = LatestSec - 2700;
        var stale = new List<long>();
        foreach (var sec in _buckets.Keys)
            if (sec < floor)
                stale.Add(sec);
        foreach (var sec in stale) _buckets.Remove(sec);
    }

    // ---- parsing helpers ----

    private long _lastSec;
    private string _tsPrefix = "";
    private long _tsSec;

    // Unix second of the timestamp, cached within a second.
    private long Sec(string ts)
    {
        if (ts.Length >= 19 && _tsPrefix.Length == 19
            && ts.AsSpan(0, 19).SequenceEqual(_tsPrefix))
            return _lastSec = _tsSec;
        if (!DateTimeOffset.TryParse(ts, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
            return _lastSec;
        _tsSec = t.ToUnixTimeSeconds();
        _tsPrefix = ts.Length >= 19 ? ts[..19] : "";
        return _lastSec = _tsSec;
    }

    private static uint Hex(string s)
        => uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : 0u;

    private static ulong HexLong(string s)
        => ulong.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : 0ul;

    // The amount rides the high half, huge hits wrapping.
    public static uint Unscramble(ulong v)
    {
        var dmg = (uint)(v >> 16);
        if ((v & 0x4000) != 0)
        {
            var hi = (uint)(v & 0xFF);
            dmg = dmg - hi + (hi << 16);
        }
        return dmg;
    }
}
