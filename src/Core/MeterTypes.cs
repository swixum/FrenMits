using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace FrenMits;

// One line of a player's breakdown: an ability they used, an enemy they hit,
// or something that hit them.
public sealed class AbilityStat
{
    public string Name = "";
    public int Hits;
    public int Crits;
    public int Dhs;
    public double Damage;
    public double Max;
    // The game id behind the row, for its icon: an action, or a status when
    // the damage came from an effect ticking. Zero for anything unresolved.
    public uint Id;
    public bool IsStatus;
    // Healing that landed on a full bar. Zero on every damage row.
    public double Over;
    // What this row breaks down into a level further: the buffs behind a
    // player's share of the credit. Null everywhere else.
    public List<AbilityStat>? Parts;

    public double Average => Hits > 0 ? Damage / Hits : 0;
    public double CritPct => Hits > 0 ? Crits * 100.0 / Hits : 0;
    public double DhPct => Hits > 0 ? Dhs * 100.0 / Hits : 0;
    public double Raw => Damage + Over;
    public double OverPct => Raw > 0 ? Over * 100.0 / Raw : 0;
}

// One thing that landed on a player shortly before they died.
public sealed class DeathHit
{
    public string Name = "";
    public double Amount;
    public long Sec;
    public bool Heal;
}

// One death: when it happened, what finished them, and the run-up to it.
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
    public double Dps;
    public double ADps;            // the parser's own active-time DPS, idle taken out
    public double RDps;            // Dps adjusted by buff credits given and received
    public double Damage;
    public string DamagePct = "";
    public double CritPct;
    public double DirectHitPct;
    public double Hps;
    public double Healed;
    public double OverhealPct;
    public double Taken;
    public int Deaths;
    public string MaxHit = "";
}

// One encounter snapshot from the parser's summary feed, plus the rDPS numbers
// this plugin works out itself from the line stream.
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
    public DateTime When = DateTime.Now;
    public List<MeterCombatant> Rows = new();

    // Per-player breakdowns, filled in when the pull finishes so a look back
    // through history still has them.
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

    // Buff credit traded with the rest of the party, the two halves of rDPS.
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
        // The raw-seconds field beats re-parsing the pretty m:ss string.
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
                    Hps = Num(c["enchps"]),
                    Healed = Num(c["healed"]),
                    OverhealPct = Num(c["OverHealPct"]),
                    Taken = Num(c["damagetaken"]),
                    Deaths = (int)Num(c["deaths"]),
                    MaxHit = c["maxhit"]?.ToString() ?? "",
                };
                row.Display = StripOwner(row.Name);
                row.RDps = row.Dps;
                // Real jobs and the Limit Break row; the parser's oddments
                // (unmerged pets, the blank server row) stay off the meter.
                if (Jobs.ByAbbreviation(row.Job) != null
                    || string.Equals(row.Name, "Limit Break", StringComparison.OrdinalIgnoreCase))
                    e.Rows.Add(row);
            }

        return e;
    }

    // The parser tags anything the game marks as owned with its owner, so a
    // duty support ally arrives as "G'raha Tia (YOU)". Character names never
    // carry brackets, and the bare name is what the log lines use, so the tail
    // comes off for both matching and display.
    public static string StripOwner(string name)
    {
        if (!name.EndsWith(")", StringComparison.Ordinal)) return name;
        var open = name.LastIndexOf(" (", StringComparison.Ordinal);
        return open > 0 ? name[..open] : name;
    }

    // Parser numbers arrive as strings and can be "---", "∞" or carry a "%".
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
