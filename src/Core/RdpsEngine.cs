using System;
using System.Collections.Generic;
using System.Globalization;

namespace FrenMits;

// Prices every raid buff per damage event from the raw log stream: a flat
// multiplier m is worth damage * (1 - 1/m), rate buffs their expected value,
// bucketed per second so any encounter window can be summed later.
public class RdpsEngine
{
    // Crit lands at +40% (the community baseline; gear moves it a little);
    // direct hits are +25% exactly.
    public const float CritBonus = 0.40f;
    public const float DirectHitBonus = 0.25f;

    // ---- per-second credit buckets ----------------------------------------

    private sealed class Sums { public double Given; public double Received; }

    private readonly Dictionary<long, Dictionary<string, Sums>> _buckets = new();

    // The newest event second seen, the anchor "now" for window queries.
    public long LatestSec { get; private set; }

    // Player name from the swap-to-character line, for mapping the parser's
    // "YOU" rows when the game object isn't reachable.
    public string LocalPlayerName { get; private set; } = "";

    // Limit break actions deal fixed, unbuffable damage, so their hits must
    // never pay buff credits. The host supplies the id check (game sheet).
    public Func<uint, bool>? IsLimitBreak;

    // Whoever the party is actually hitting, for naming the encounter.
    public string CurrentEnemy { get; private set; } = "";

    // ---- actor bookkeeping -------------------------------------------------

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
    }

    // Buffs by the actor carrying them: players for party buffs, enemies for debuffs.
    private readonly Dictionary<uint, List<ActiveBuff>> _buffs = new();

    private static bool IsPlayer(uint id) => id is >= 0x10000000 and < 0x20000000;

    // The player a damage source resolves to: the player itself, or a pet's owner.
    private uint OwnerOf(uint id)
    {
        if (IsPlayer(id)) return id;
        return _owner.TryGetValue(id, out var o) && IsPlayer(o) ? o : 0;
    }

    // ---- line dispatch -----------------------------------------------------

    public void Process(string[] f)
    {
        if (f.Length < 2) return;
        switch (f[0])
        {
            case "21" or "22": OnAbility(f); break;
            case "24": OnTick(f); break;
            case "26": OnGain(f); break;
            case "30": OnLose(f); break;
            case "03": OnAdd(f); break;
            case "25": if (f.Length > 2) _buffs.Remove(Hex(f[2])); break;
            case "02":
                if (f.Length > 3) { _names[Hex(f[2])] = f[3]; LocalPlayerName = f[3]; }
                break;
            case "01": _buffs.Clear(); break;
        }
    }

    private void OnAdd(string[] f)
    {
        if (f.Length < 7) return;
        var id = Hex(f[2]);
        if (id == 0) return;
        _names[id] = f[3];
        var owner = Hex(f[6]);
        if (owner != 0) _owner[id] = owner;
        if (IsPlayer(id) && Jobs.ByRowId(Hex(f[4])) is { } job) _roles[id] = job.Role;
    }

    private void OnGain(string[] f)
    {
        if (f.Length < 10 || RaidBuffs.Find(f[3]) is not { } def) return;
        var target = Hex(f[7]);
        if (target == 0) return;
        var source = Hex(f[5]);
        var sourceName = f[6];
        if (sourceName.Length > 0 && source != 0) _names[source] = sourceName;
        else if (sourceName.Length == 0) _names.TryGetValue(source, out sourceName!);

        float.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var dur);
        // A missing or zero duration gets a sane cap so nothing sticks forever.
        var expire = Sec(f[1]) + (long)MathF.Ceiling(dur > 0f ? dur : 30f) + 1;

        if (!_buffs.TryGetValue(target, out var list)) _buffs[target] = list = new List<ActiveBuff>();
        // A refresh replaces the running copy from the same source.
        list.RemoveAll(b => b.Def == def && b.SourceId == source);
        list.Add(new ActiveBuff
        {
            Def = def, SourceId = source, SourceName = sourceName ?? "",
            Stacks = (int)Hex(f[9]), ExpireSec = expire,
        });
    }

    private void OnLose(string[] f)
    {
        if (f.Length < 9 || RaidBuffs.Find(f[3]) is not { } def) return;
        if (!_buffs.TryGetValue(Hex(f[7]), out var list)) return;
        var source = Hex(f[5]);
        list.RemoveAll(b => b.Def == def && (source == 0 || b.SourceId == source));
    }

    // ---- damage events -----------------------------------------------------

    private void OnAbility(string[] f)
    {
        if (f.Length < 10) return;
        var src = Hex(f[2]);
        var owner = OwnerOf(src);
        if (owner == 0) return;              // not a player, not a player's pet
        var target = Hex(f[6]);
        if (target < 0x40000000) return;     // only damage into enemies counts
        if (f[7].Length > 0) CurrentEnemy = f[7];
        if (IsLimitBreak?.Invoke(Hex(f[4])) == true) return;

        // The first connected damage pair (fields 8..23 are eight flag|value
        // pairs); heals and pure bookkeeping entries are skipped.
        for (var i = 8; i + 1 < f.Length && i <= 22; i += 2)
        {
            var flags = Hex(f[i]);
            if ((flags & 0xFF) != 0x03) continue;
            var dmg = Unscramble(HexLong(f[i + 1]));
            if (dmg == 0) return;
            Credit(owner, src == owner ? f[3] : "", target, dmg,
                crit: (flags & 0x2000) != 0, dh: (flags & 0x4000) != 0, Sec(f[1]));
            return;
        }
    }

    private void OnTick(string[] f)
    {
        // Damage-over-time ticks carry no crit flag; they are priced as normal
        // hits under whatever is on the source right now.
        if (f.Length < 19 || f[4] != "DoT") return;
        var owner = OwnerOf(Hex(f[17]));
        if (owner == 0) return;
        var target = Hex(f[2]);
        if (target < 0x40000000) return;
        var dmg = HexLong(f[6]);
        if (dmg == 0) return;
        Credit(owner, "", target, (uint)dmg, crit: false, dh: false, Sec(f[1]));
    }

    private readonly List<(ActiveBuff Buff, RaidBuffs.Effect[] Effects)> _scratch = new();

    private void Credit(uint owner, string ownerName, uint enemy, uint dmg, bool crit, bool dh, long sec)
    {
        if (sec > LatestSec) LatestSec = sec;
        if (ownerName.Length > 0) _names[owner] = ownerName;
        else if (!_names.TryGetValue(owner, out ownerName!)) return;

        _scratch.Clear();
        _roles.TryGetValue(owner, out var role0);
        JobRole? role = _roles.ContainsKey(owner) ? role0 : null;
        CollectBuffs(owner, onEnemy: false, sec, role);
        CollectBuffs(enemy, onEnemy: true, sec, role);
        if (_scratch.Count == 0) return;

        // Unbuffed base for pricing rate buffs: divide out every tracked
        // multiplier plus the crit / direct-hit bonus the hit actually rolled.
        var mult = 1.0;
        foreach (var (_, effects) in _scratch)
            foreach (var e in effects)
                if (e.Kind == RaidBuffs.Kind.Damage) mult *= e.Amount;
        var based = dmg / mult;
        if (crit) based /= 1.0 + CritBonus;
        if (dh) based /= 1.0 + DirectHitBonus;

        foreach (var (buff, effects) in _scratch)
        {
            // Your own buff's lift on your own damage stays yours.
            if (buff.SourceId == owner || buff.SourceName.Length == 0) continue;
            var credit = 0.0;
            foreach (var e in effects)
                credit += e.Kind switch
                {
                    RaidBuffs.Kind.Damage => dmg * (1.0 - 1.0 / e.Amount),
                    RaidBuffs.Kind.CritRate => based * e.Amount * CritBonus,
                    _ => based * e.Amount * DirectHitBonus,
                };
            if (credit <= 0) continue;
            Bucket(sec, buff.SourceName).Given += credit;
            Bucket(sec, ownerName).Received += credit;
        }
    }

    private void CollectBuffs(uint carrier, bool onEnemy, long sec, JobRole? role)
    {
        if (!_buffs.TryGetValue(carrier, out var list)) return;
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var b = list[i];
            if (sec > b.ExpireSec) { list.RemoveAt(i); continue; }
            if (b.Def.OnEnemy != onEnemy) continue;
            _scratch.Add((b, b.Def.For(b.Stacks, role)));
        }
    }

    private Sums Bucket(long sec, string name)
    {
        if (!_buckets.TryGetValue(sec, out var bySec)) _buckets[sec] = bySec = new Dictionary<string, Sums>();
        if (!bySec.TryGetValue(name, out var s)) bySec[name] = s = new Sums();
        return s;
    }

    // ---- queries -----------------------------------------------------------

    // Per-player (given, received) totals for events at or after fromSec.
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

    // Drop buckets old enough that no live encounter window can still need them.
    public void Trim()
    {
        if (_buckets.Count < 3000) return;
        var floor = LatestSec - 2700;
        var stale = new List<long>();
        foreach (var sec in _buckets.Keys)
            if (sec < floor)
                stale.Add(sec);
        foreach (var sec in stale) _buckets.Remove(sec);
    }

    // ---- parsing helpers ---------------------------------------------------

    private long _lastSec;

    // Unix second of a log timestamp like 2026-07-30T21:15:32.123+02:00.
    private long Sec(string ts)
        => DateTimeOffset.TryParse(ts, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
            ? _lastSec = t.ToUnixTimeSeconds()
            : _lastSec;

    private static uint Hex(string s)
        => uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : 0u;

    private static ulong HexLong(string s)
        => ulong.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : 0ul;

    // The damage dword rides its amount in the high half; huge hits set 0x4000
    // and wrap their top byte into the low byte.
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
