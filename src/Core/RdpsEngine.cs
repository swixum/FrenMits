using System;
using System.Collections.Generic;
using System.Globalization;

namespace FrenMits;

// Splits every damage event's raid-buff gain between the buffers, from the raw
// log stream. Stacked percent buffs divide the exact combined gain N*(M-1)/M
// by log ratio, so two small buffs earn what one equal product buff would.
// Crit and direct hit rate buffs are paid only on hits that actually rolled
// one, in proportion to how likely the roll came from the buff, with the crit
// multiplier learned per player from their observed rates. Everything lands in
// per-second buckets so any encounter window can be summed later.
public class RdpsEngine
{
    // The game's crit multiplier is 1.35 plus the player's crit rate; direct
    // hits are +25% exactly.
    public const float McBase = 1.35f;
    public const float DhMult = 1.25f;

    // Stand-ins until enough hits are seen to learn a player's real rates.
    public const float DefaultCritRate = 0.25f;
    public const float DefaultDirectHitRate = 0.30f;
    public const int MinRateSamples = 40;

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
        // Strengths only the moment of application knows (finish steps, codas).
        public RaidBuffs.Effect[]? Resolved;
    }

    // Buffs by the actor carrying them: players for party buffs, enemies for debuffs.
    private readonly Dictionary<uint, List<ActiveBuff>> _buffs = new();

    // ---- learned crit / direct hit rates ----------------------------------

    private sealed class Rates
    {
        public int CHits, Crits, DHits, Dhs;
        public double BuffC, BuffD;
    }

    private readonly Dictionary<uint, Rates> _rates = new();

    // Base (gear) rates: observed frequency with the buffed share backed out.
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

    // ---- guaranteed crits and direct hits ----------------------------------

    [Flags]
    private enum Guard { None = 0, Crit = 1, Dh = 2, InnerRelease = 4 }

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
        "Bootshine", "Leaping Opo", "Shadow of the Destroyer",
    };

    private (bool Crit, bool Dh) Guarantee(uint owner, string action)
    {
        var g = _guards.TryGetValue(owner, out var v) ? v : Guard.None;
        if (action.Length > 0)
        {
            if (AutoCritDh.Contains(action)) return (true, true);
            if (AutoCrit.Contains(action)) return (true, false);
            if ((g & Guard.InnerRelease) != 0
                && (action.Equals("Fell Cleave", StringComparison.OrdinalIgnoreCase)
                    || action.Equals("Decimate", StringComparison.OrdinalIgnoreCase)))
                return (true, true);
        }
        return ((g & Guard.Crit) != 0, (g & Guard.Dh) != 0);
    }

    // ---- bard songs, for Radiant Finale's coda count -----------------------

    private readonly Dictionary<uint, HashSet<string>> _songs = new();
    private readonly Dictionary<uint, (float Mult, long At)> _finale = new();

    private static bool IsSong(string status)
        => status.Equals("Mage's Ballad", StringComparison.OrdinalIgnoreCase)
           || status.Equals("Army's Paeon", StringComparison.OrdinalIgnoreCase)
           || status.Equals("The Wanderer's Minuet", StringComparison.OrdinalIgnoreCase);

    private float FinaleMult(uint bard, long sec)
    {
        // One resolve covers the whole party's applications of that press.
        if (_finale.TryGetValue(bard, out var f) && sec - f.At <= 4) return f.Mult;
        var codas = _songs.TryGetValue(bard, out var s) ? s.Count : 0;
        var mult = codas switch { 1 => 1.02f, 2 => 1.04f, _ => 1.06f };
        _finale[bard] = (mult, sec);
        s?.Clear();
        return mult;
    }

    // ---- dance finish steps ------------------------------------------------

    private readonly Dictionary<uint, float> _finishTech = new();
    private readonly Dictionary<uint, float> _finishStd = new();

    private void Sniff(uint owner, string action)
    {
        switch (action.ToLowerInvariant())
        {
            case "single technical finish": _finishTech[owner] = 1.01f; break;
            case "double technical finish": _finishTech[owner] = 1.02f; break;
            case "triple technical finish": _finishTech[owner] = 1.03f; break;
            case "quadruple technical finish": _finishTech[owner] = 1.05f; break;
            case "single standard finish": _finishStd[owner] = 1.02f; break;
            case "double standard finish": _finishStd[owner] = 1.05f; break;
        }
    }

    // ---- per-player breakdowns ---------------------------------------------

    // What each player used, who they hit with it, and what hit them. Kept for
    // the running fight only; a finished pull keeps its own copy.
    private readonly Dictionary<string, Dictionary<string, AbilityStat>> _dealt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, AbilityStat>> _targets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, AbilityStat>> _taken = new(StringComparer.OrdinalIgnoreCase);

    // The same three, for healing: what each healer cast, who they healed, and
    // who healed each player.
    private readonly Dictionary<string, Dictionary<string, AbilityStat>> _healDealt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, AbilityStat>> _healTargets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, AbilityStat>> _healFrom = new(StringComparer.OrdinalIgnoreCase);

    // Status names by id, so a damage-over-time tick can be named.
    private readonly Dictionary<uint, string> _effectNames = new();

    private static void Tally(Dictionary<string, Dictionary<string, AbilityStat>> table,
        string who, string what, double dmg, bool crit, bool dh, uint id = 0, bool status = false,
        double over = 0)
    {
        if (who.Length == 0 || what.Length == 0) return;
        // A heal swallowed entirely by a full health bar still happened.
        if (dmg <= 0 && over <= 0) return;
        if (!table.TryGetValue(who, out var by))
            table[who] = by = new Dictionary<string, AbilityStat>(StringComparer.OrdinalIgnoreCase);
        if (!by.TryGetValue(what, out var s))
            by[what] = s = new AbilityStat { Name = what, Id = id, IsStatus = status };
        s.Hits++;
        if (crit) s.Crits++;
        if (dh) s.Dhs++;
        s.Damage += dmg;
        s.Over += over;
        if (dmg > s.Max) s.Max = dmg;
    }

    private static List<AbilityStat> Ranked(Dictionary<string, Dictionary<string, AbilityStat>> table, string who)
    {
        var list = new List<AbilityStat>();
        if (who.Length > 0 && table.TryGetValue(who, out var by)) list.AddRange(by.Values);
        // Overhealing only breaks ties, so a spell that did nothing but overheal
        // still sits below one that landed.
        list.Sort((a, b) => a.Damage != b.Damage ? b.Damage.CompareTo(a.Damage) : b.Over.CompareTo(a.Over));
        return list;
    }

    public List<AbilityStat> Dealt(string player) => Ranked(_dealt, player);
    public List<AbilityStat> Targets(string player) => Ranked(_targets, player);
    public List<AbilityStat> Taken(string player) => Ranked(_taken, player);
    public List<AbilityStat> Heals(string player) => Ranked(_healDealt, player);
    public List<AbilityStat> HealTargets(string player) => Ranked(_healTargets, player);
    public List<AbilityStat> HealFrom(string player) => Ranked(_healFrom, player);

    // ---- buff credit, player to player -------------------------------------

    // Who fed whose rDPS, over the same fight the breakdowns cover. The
    // per-second buckets answer "how much" for a window; this answers "from
    // whom".
    private readonly Dictionary<string, Dictionary<string, double>> _pairs = new(StringComparer.OrdinalIgnoreCase);

    public List<AbilityStat> Given(string player)
    {
        var list = new List<AbilityStat>();
        if (player.Length > 0 && _pairs.TryGetValue(player, out var by))
            foreach (var (to, amount) in by)
                if (amount > 0)
                    list.Add(new AbilityStat { Name = to, Damage = amount });
        list.Sort((a, b) => b.Damage.CompareTo(a.Damage));
        return list;
    }

    public List<AbilityStat> Received(string player)
    {
        var list = new List<AbilityStat>();
        if (player.Length > 0)
            foreach (var (from, by) in _pairs)
                if (by.TryGetValue(player, out var amount) && amount > 0)
                    list.Add(new AbilityStat { Name = from, Damage = amount });
        list.Sort((a, b) => b.Damage.CompareTo(a.Damage));
        return list;
    }

    // ---- deaths ------------------------------------------------------------

    // The last few things that landed on each player, kept only long enough to
    // become the run-up to a death.
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
            // The last thing that hurt them is the killing blow; everything
            // else is what led there.
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

    // ---- overhealing -------------------------------------------------------

    // A heal line carries the target's health as it stood BEFORE the heal, so
    // the room left in the bar is right there on the line and nothing has to be
    // tracked between events. Below zero means the line did not say, and then
    // none of the heal gets called overhealing.
    private static int Room(string cur, string max)
    {
        var c = Dec(cur);
        var m = Dec(max);
        return c < 0 || m <= 0 ? -1 : Math.Max(0, m - c);
    }

    private static int Dec(string s)
        => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : -1;

    // What a heal does to the room it had, as (landed, overhealed).
    private static (double Landed, double Over) Landing(uint amount, ref int room)
    {
        if (room < 0) return (amount, 0);
        double landed = Math.Min(amount, room);
        room -= (int)landed;
        return (landed, amount - landed);
    }

    // Healing rolls a crit in its own flag bit, not the one damage uses.
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

    // ---- damage over time snapshots ---------------------------------------

    // The game locks a damage-over-time effect's buffs in when it is applied,
    // not per tick, so each application on an enemy freezes the caster's buff
    // state and every tick prices against that.
    private sealed class DotSnap
    {
        public List<(uint Src, string Name, double Mult)> Flat = new();
        public List<(uint Src, string Name, double Rate)> Crit = new();
        public List<(uint Src, string Name, double Rate)> Dh = new();
        public double SelfCrit, SelfDh;
        public long ExpireSec;
    }

    private readonly Dictionary<(uint Target, uint Effect, uint Source), DotSnap> _dotSnaps = new();

    private static bool IsPlayer(uint id) => id is >= 0x10000000 and < 0x20000000;

    // Allies that fight for themselves while the game still marks them as
    // owned: the duty support and trust characters. A pet has no job of its
    // own, so a job is what tells the two apart.
    private readonly HashSet<uint> _allies = new();

    private bool IsCombatant(uint id) => IsPlayer(id) || _allies.Contains(id);

    // Who a damage source belongs to: itself if it fights for itself, else the
    // owner it is a pet of.
    private uint OwnerOf(uint id)
    {
        if (IsCombatant(id)) return id;
        return _owner.TryGetValue(id, out var o) && IsCombatant(o) ? o : 0;
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
                // Ids belong to whoever holds them in the zone being entered.
                _allies.Clear();
                _owner.Clear();
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
        if (owner != 0) _owner[id] = owner;
        if (Jobs.ByRowId(Hex(f[4])) is not { } job)
        {
            // The game reuses object ids, so whatever this one used to be, it
            // has no job now: it must not stay an ally, or a later pet's damage
            // would stop reaching its owner.
            _allies.Remove(id);
            _roles.Remove(id);
            return;
        }
        _roles[id] = job.Role;
        // Carrying a job means it acts on its own, whoever the game says owns it.
        if (!IsPlayer(id)) _allies.Add(id);
    }

    private void OnGain(string[] f)
    {
        if (f.Length < 10) return;
        var status = f[3];
        var tgt = Hex(f[7]);
        var src = Hex(f[5]);
        var sec = Sec(f[1]);
        if (status.Length > 0) _effectNames[Hex(f[2])] = status;

        // Any player-sourced status landing on an enemy could be a damage over
        // time effect: freeze the caster's buff state for its ticks.
        if (tgt >= 0x40000000 && OwnerOf(src) != 0)
        {
            float.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var dotDur);
            SnapshotDot(tgt, Hex(f[2]), src, sec, dotDur);
        }

        // Statuses that turn later hits into guaranteed crits or direct hits.
        if (IsCombatant(tgt))
        {
            var add = status.ToLowerInvariant() switch
            {
                "life surge" => Guard.Crit,
                "reassembled" => Guard.Crit | Guard.Dh,
                "inner release" => Guard.InnerRelease,
                _ => Guard.None,
            };
            if (add != Guard.None)
                _guards[tgt] = (_guards.TryGetValue(tgt, out var g) ? g : Guard.None) | add;
        }

        // A song starting is a coda banked for that bard's next finale.
        if (IsSong(status) && IsCombatant(src))
            (_songs.TryGetValue(src, out var set) ? set : _songs[src] = new HashSet<string>())
                .Add(status.ToLowerInvariant());

        if (RaidBuffs.Find(status) is not { } def) return;
        if (tgt == 0) return;
        var sourceName = f[6];
        if (sourceName.Length > 0 && src != 0) _names[src] = sourceName;
        else if (sourceName.Length == 0) _names.TryGetValue(src, out sourceName!);

        float.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var dur);
        // A missing or zero duration gets a sane cap so nothing sticks forever.
        var expire = sec + (long)MathF.Ceiling(dur > 0f ? dur : 30f) + 1;

        // Strengths the status name alone can't tell.
        RaidBuffs.Effect[]? resolved = status.ToLowerInvariant() switch
        {
            "radiant finale" => new[] { new RaidBuffs.Effect(RaidBuffs.Kind.Damage, FinaleMult(src, sec)) },
            "technical finish" => new[]
            {
                new RaidBuffs.Effect(RaidBuffs.Kind.Damage, _finishTech.TryGetValue(src, out var t) ? t : 1.05f),
            },
            "standard finish" => new[]
            {
                new RaidBuffs.Effect(RaidBuffs.Kind.Damage, _finishStd.TryGetValue(src, out var st) ? st : 1.05f),
            },
            _ => null,
        };

        if (!_buffs.TryGetValue(tgt, out var list)) _buffs[tgt] = list = new List<ActiveBuff>();
        // A refresh replaces the running copy from the same source.
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
        var tgt = Hex(f[7]);
        var src = Hex(f[5]);
        _dotSnaps.Remove((tgt, Hex(f[2]), src));

        if (IsCombatant(tgt))
        {
            var drop = f[3].ToLowerInvariant() switch
            {
                "life surge" => Guard.Crit,
                "reassembled" => Guard.Crit | Guard.Dh,
                "inner release" => Guard.InnerRelease,
                _ => Guard.None,
            };
            if (drop != Guard.None && _guards.TryGetValue(tgt, out var g))
            {
                g &= ~drop;
                if (g == Guard.None) _guards.Remove(tgt); else _guards[tgt] = g;
            }
        }

        if (RaidBuffs.Find(f[3]) is not { } def) return;
        if (!_buffs.TryGetValue(tgt, out var list)) return;
        list.RemoveAll(b => b.Def == def && (src == 0 || b.SourceId == src));
    }

    // ---- damage events -----------------------------------------------------

    private void OnAbility(string[] f)
    {
        if (f.Length < 10) return;
        var src = Hex(f[2]);
        var owner = OwnerOf(src);
        var target = Hex(f[6]);
        if (owner == 0)
        {
            // An enemy swinging at the party: only the taken breakdown wants it.
            if (IsCombatant(target)) OnTaken(f, target);
            return;
        }
        var action = f[5];
        if (action.Length > 0) Sniff(owner, action);
        // Anything aimed at the party is healing, not damage.
        if (IsCombatant(target)) { OnHeal(f, owner, target); return; }
        if (target < 0x40000000) return;     // only damage into enemies counts
        if (f[7].Length > 0) CurrentEnemy = f[7];
        if (IsLimitBreak?.Invoke(Hex(f[4])) == true) return;

        var sec = Sec(f[1]);
        if (sec > LatestSec) LatestSec = sec;
        if (src == owner && f[3].Length > 0) _names[owner] = f[3];
        if (!_names.TryGetValue(owner, out var ownerName) || ownerName.Length == 0) return;

        var (gc, gd) = Guarantee(owner, action);

        // A hit into an enemy can heal the one who landed it: a combo finisher
        // that leeches, or anything under a buff that heals on damage. The
        // caster's own health rides further along the same line, at 34 and 35.
        var mine = src == owner;
        var selfRoom = mine && f.Length > 35 ? Room(f[34], f[35]) : -1;

        // Every connected damage pair (fields 8..23 are eight flag|value
        // pairs): low byte 03/05/06 is damage, 0x100 crit, 0x200 direct hit.
        // Bookkeeping entries and the odd shifted pair fall through.
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
            var crit = (flags & 0x100) != 0;
            var dh = (flags & 0x200) != 0;

            Tally(_dealt, ownerName, action.Length > 0 ? action : "Attack", dmg, crit, dh, Hex(f[4]));
            if (f[7].Length > 0) Tally(_targets, ownerName, f[7], dmg, crit, dh);

            Gather(owner, target, sec);
            Learn(owner, crit, dh, gc && crit, gd && dh);
            Allocate(owner, ownerName, dmg, crit, dh, gc && crit, gd && dh, sec);
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
            Tally(_taken, who, action, dmg, (flags & 0x100) != 0, (flags & 0x200) != 0, id);
            Recent(who, action, dmg, sec, heal: false);
        }
    }

    // A party-facing ability: healing, and the part of it the health bar had no
    // room left for.
    private void OnHeal(string[] f, uint owner, uint target)
    {
        var who = f[7].Length > 0 ? f[7] : _names.TryGetValue(target, out var tn) ? tn : "";
        if (!_names.TryGetValue(owner, out var healer)) healer = "";
        if (who.Length == 0 || healer.Length == 0) return;

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
        if (f[3].Length > 0) Tally(_targets, ownerName, f[3], dmg, crit: false, dh: false);

        // Ticks price against the buffs frozen at application; a tick with no
        // snapshot (ground effects) falls back to what is up right now.
        if (_dotSnaps.TryGetValue((target, Hex(f[5]), src), out var snap) && sec <= snap.ExpireSec)
            LoadSnap(snap);
        else
            Gather(owner, target, sec);
        AllocateTick(owner, ownerName, dmg, sec);
    }

    // A regeneration or a lingering effect ticking on a party member: no buff
    // credit is involved, but the healing and taken breakdowns both want it.
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

        // A tick whose source the log never named still counts as healing the
        // player received, it just has nobody to credit it to.
        var room = Room(f[7], f[8]);
        var healer = owner != 0 && _names.TryGetValue(owner, out var hn) ? hn : "";
        Heal(healer, who, name, effect, status: true, amount, crit: false, ref room, sec);
    }

    // ---- buff state for one event ------------------------------------------

    private readonly List<(uint Src, string Name, double Mult)> _extFlat = new();
    private readonly List<(uint Src, string Name, double Rate)> _extCrit = new();
    private readonly List<(uint Src, string Name, double Rate)> _extDh = new();
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
                        // A player's own percent buffs stay personal: they are
                        // neither shared out nor divided from anyone's base.
                        if (!self && e.Amount > 1.0001f)
                            _extFlat.Add((b.SourceId, b.SourceName, e.Amount));
                        break;
                    case RaidBuffs.Kind.CritRate:
                        if (self) _selfCrit += e.Amount;
                        else _extCrit.Add((b.SourceId, b.SourceName, e.Amount));
                        break;
                    default:
                        if (self) _selfDh += e.Amount;
                        else _extDh.Add((b.SourceId, b.SourceName, e.Amount));
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

    // ---- the split ---------------------------------------------------------

    private void Allocate(uint owner, string ownerName, double dmg,
        bool crit, bool dh, bool gCrit, bool gDh, long sec)
    {
        if (_extFlat.Count == 0 && _extCrit.Count == 0 && _extDh.Count == 0) return;

        var (cs, ds) = BaseRates(owner);
        var mc = McBase + cs;
        double extC = 0, extD = 0;
        foreach (var b in _extCrit) extC += b.Rate;
        foreach (var b in _extDh) extD += b.Rate;

        // How much of a rolled crit was the buffs' doing, versus a roll the
        // player's own rate covered anyway.
        var cb = Math.Min(1.0, cs + _selfCrit + extC);
        var cu = Math.Min(cb, cs + _selfCrit);
        var critShare = crit && !gCrit && extC > 0 ? (cb - cu) / cb : 0.0;
        var db = Math.Min(1.0, ds + _selfDh + extD);
        var du = Math.Min(db, ds + _selfDh);
        var dhShare = dh && !gDh && extD > 0 ? (db - du) / db : 0.0;

        // A guaranteed roll pays its rate buffs as the flat bonus the game
        // grants them there instead: each point of rate adds that share of the
        // roll's damage bonus.
        var rc = gCrit && extC > 0
            ? (1.0 + (mc - 1.0) * (_selfCrit + extC)) / (1.0 + (mc - 1.0) * _selfCrit) : 1.0;
        var rd = gDh && extD > 0
            ? (1.0 + (DhMult - 1.0) * (_selfDh + extD)) / (1.0 + (DhMult - 1.0) * _selfDh) : 1.0;

        // Four corners: the buff-caused and self-covered slices of each roll.
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

    // One slice: percent buffs, plus whichever roll multipliers act as
    // external buffs on it, split the slice's total gain by log ratio.
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
            Credit(sec, b.Name, ownerName, gain * Math.Log(b.Mult) / lnM);

        // The roll multipliers' shares go to the rate buffers, each by how
        // much rate they contributed.
        var critGain = gain * (Math.Log(critMult) + Math.Log(rc)) / lnM;
        if (critGain > 0 && extC > 0)
            foreach (var b in _extCrit)
                Credit(sec, b.Name, ownerName, critGain * b.Rate / extC);

        var dhGain = gain * (Math.Log(dhMult) + Math.Log(rd)) / lnM;
        if (dhGain > 0 && extD > 0)
            foreach (var b in _extDh)
                Credit(sec, b.Name, ownerName, dhGain * b.Rate / extD);
    }

    // A tick carries no roll flags, so it is the odds-weighted blend of the
    // four outcomes its snapshotted rates allowed.
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

    private void Credit(long sec, string from, string to, double amount)
    {
        if (amount <= 0 || from.Length == 0) return;
        Bucket(sec, from).Given += amount;
        Bucket(sec, to).Received += amount;
        if (!_pairs.TryGetValue(from, out var by))
            _pairs[from] = by = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        by[to] = by.TryGetValue(to, out var running) ? running + amount : amount;
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
