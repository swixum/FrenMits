using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace FrenMits;

// One breakdown line: an ability, an enemy, or a hit taken.
public sealed class AbilityStat
{
    public string Name = "";
    public int Hits;
    public int Crits;
    public int Dhs;
    // Rolls that were both, which is the one that pays.
    public int Cdhs;
    public double Damage;
    public double Max;
    // The action or status behind the row, for its icon.
    public uint Id;
    public bool IsStatus;
    // Healing that landed on a full bar.
    public double Over;
    // The buffs behind a player's share of the credit.
    public List<AbilityStat>? Parts;

    public double Average => Hits > 0 ? Damage / Hits : 0;
    public double CritPct => Hits > 0 ? Crits * 100.0 / Hits : 0;
    public double DhPct => Hits > 0 ? Dhs * 100.0 / Hits : 0;
    public double CdhPct => Hits > 0 ? Cdhs * 100.0 / Hits : 0;
    public double Raw => Damage + Over;
    public double OverPct => Raw > 0 ? Over * 100.0 / Raw : 0;
}

// How a pull finished, Unknown when it can't be vouched for.
public enum PullEnd { Unknown, Kill, Wipe }

// One thing that landed shortly before a player died.
public sealed class DeathHit
{
    public string Name = "";
    public double Amount;
    public long Sec;
    public bool Heal;
}

// One death, with the run-up to it.
public sealed class DeathRecord
{
    public string Name = "";
    public long Sec;           // log time, absolute
    public float At;           // seconds into the fight, set when the pull is banked
    public string Killer = "";
    public double KillingBlow;
    public List<DeathHit> Lead = new();
}

// One combatant row of a parsed encounter update.
public sealed class MeterCombatant
{
    public string Name = "";       // as the parser reports it ("YOU" for yourself)
    public string Display = "";    // resolved name shown on the bar
    public string Job = "";        // abbreviation, empty for Limit Break
    public bool LimitBreak;        // the party's shared limit break, not a player
    public double Dps;
    public double ADps;            // the parser's own active-time DPS, idle taken out
    public double RDps;            // Dps adjusted by buff credits given and received
    public double Damage;
    public string DamagePct = "";
    public double CritPct;
    public double DirectHitPct;
    // Hits that crit and direct hit at once.
    public double CritDirectHitPct;
    public double Hps;
    public double Healed;
    public string HealedPct = "";
    // Absorbed by shields, already inside Healed.
    public double Shielded;
    public double OverhealPct;
    public double Taken;
    public int Deaths;
    public string MaxHit = "";
}

// One parsed encounter, plus the rDPS worked out here.
public sealed class MeterEncounter
{
    public string Title = "";
    public string Duration = "";
    public float Seconds;
    public bool Active;
    public double TotalDps;
    public double TotalDamage;
    public double TotalHps;
    public double TotalTaken;
    public int TotalDeaths;
    public double RaidRDps;
    // Boss health left at the close, below zero if unread.
    public float BossLeft = -1f;
    // A boss health bar was read during the pull, so this was not trash.
    public bool Boss;
    // The limit break this pull used, for its row icon.
    public uint LimitBreakAction;
    public PullEnd Ended = PullEnd.Unknown;
    public DateTime When = DateTime.Now;
    public List<MeterCombatant> Rows = new();

    // Per-player breakdowns, banked when the pull finishes.
    public Dictionary<string, List<AbilityStat>> Dealt { get; }
        = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<AbilityStat>> Targets { get; }
        = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<AbilityStat>> Taken { get; }
        = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<AbilityStat>> Heals { get; }
        = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<AbilityStat>> HealTargets { get; }
        = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<AbilityStat>> HealFrom { get; }
        = new(StringComparer.OrdinalIgnoreCase);

    // Buff credit traded with the party, the halves of rDPS.
    public Dictionary<string, List<AbilityStat>> Given { get; }
        = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<AbilityStat>> Received { get; }
        = new(StringComparer.OrdinalIgnoreCase);

    public List<DeathRecord> Deaths { get; } = new();

    public static MeterEncounter? Parse(JObject msg)
    {
        if (msg["Encounter"] is not JObject enc) return null;
        var e = new MeterEncounter
        {
            Title = enc["title"]?.ToString() ?? "",
            Duration = enc["duration"]?.ToString() ?? "",
            Active = string.Equals(msg["isActive"]?.ToString(), "true", StringComparison.OrdinalIgnoreCase),
            TotalDps = Num(enc["encdps"]),
            TotalDamage = Num(enc["damage"]),
            TotalHps = Num(enc["enchps"]),
            TotalTaken = Num(enc["damagetaken"]),
            TotalDeaths = (int)Num(enc["deaths"]),
        };
        // The raw-seconds field beats re-parsing the m:ss string.
        e.Seconds = (float)Num(enc["DURATION"]);
        if (e.Seconds <= 0f) e.Seconds = ParseMmss(e.Duration);

        if (msg["Combatant"] is JObject combatants)
            foreach (var kv in combatants)
            {
                if (kv.Value is not JObject c) continue;
                var row = new MeterCombatant
                {
                    Name = c["name"]?.ToString() ?? kv.Key,
                    Job = (c["Job"]?.ToString() ?? "").ToUpperInvariant(),
                    Dps = Num(c["encdps"]),
                    ADps = Num(c["dps"]),
                    Damage = Num(c["damage"]),
                    DamagePct = c["damage%"]?.ToString() ?? "",
                    CritPct = Num(c["crithit%"]),
                    DirectHitPct = Num(c["DirectHitPct"]),
                    CritDirectHitPct = Num(c["CritDirectHitPct"]),
                    Hps = Num(c["enchps"]),
                    Healed = Num(c["healed"]),
                    HealedPct = c["healed%"]?.ToString() ?? "",
                    Shielded = Num(c["damageShield"]),
                    OverhealPct = Num(c["OverHealPct"]),
                    Taken = Num(c["damagetaken"]),
                    Deaths = (int)Num(c["deaths"]),
                    MaxHit = c["maxhit"]?.ToString() ?? "",
                };
                row.Display = StripOwner(row.Name);
                row.RDps = row.Dps;
                // Real jobs and the limit break only.
                var job = Jobs.ByAbbreviation(row.Job);
                if (job == null && !IsLimitBreakName(row.Name)) continue;
                row.LimitBreak = job == null;
                e.Rows.Add(row);
            }

        return e;
    }

    // The limit break name in every log language.
    public static bool IsLimitBreakName(string name)
        => name.Equals("Limit Break", StringComparison.OrdinalIgnoreCase)
           || name.Equals("リミットブレイク", StringComparison.Ordinal)
           || name.Equals("Limitrausch", StringComparison.OrdinalIgnoreCase)
           || name.Equals("Transcendance", StringComparison.OrdinalIgnoreCase);

    // Drops the owner tail the parser adds to an ally.
    public static string StripOwner(string name)
    {
        if (!name.EndsWith(")", StringComparison.Ordinal)) return name;
        var open = name.LastIndexOf(" (", StringComparison.Ordinal);
        return open > 0 ? name[..open] : name;
    }

    // Parser numbers arrive as strings and can be junk.
    private static double Num(JToken? tok)
    {
        var s = tok?.ToString();
        if (string.IsNullOrEmpty(s)) return 0;
        s = s.TrimEnd('%');
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            || double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out v))
            return double.IsFinite(v) ? v : 0;
        return 0;
    }

    private static float ParseMmss(string s)
    {
        var parts = s.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out var m) && int.TryParse(parts[1], out var sec))
            return m * 60 + sec;
        return 0f;
    }
}
