using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace FrenMits;

// One combatant row of a parsed encounter update.
public sealed class MeterCombatant
{
    public string Name = "";       // as the parser reports it ("YOU" for yourself)
    public string Display = "";    // resolved name shown on the bar
    public string Job = "";        // abbreviation, empty for Limit Break
    public double Dps;
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
                row.Display = row.Name;
                row.RDps = row.Dps;
                // Real jobs and the Limit Break row; the parser's oddments
                // (unmerged pets, the blank server row) stay off the meter.
                if (Jobs.ByAbbreviation(row.Job) != null
                    || string.Equals(row.Name, "Limit Break", StringComparison.OrdinalIgnoreCase))
                    e.Rows.Add(row);
            }

        return e;
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
