using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FrenMits;

// A recording of what the parser fed the meter and the world it read it in, so
// a pull that came out wrong can be run again offline.
public static class MeterFeed
{
    public sealed class Message
    {
        public double At;              // seconds since the recording began
        public bool Active;            // the parser's own isActive
        public float Seconds;          // the parser's encounter clock
        public double Damage;          // its running total
        public bool InCombat;          // the world, as the stitch saw it
        public bool Cutscene;
        public bool SawBoss;
        public double LogLines;        // what the engine had counted by then
        public List<(string Name, string Job, double Damage, double Healed, double Taken, int Deaths)> Rows = new();
    }

    public static string Folder
        => Path.Combine(Service.PluginInterface.GetPluginConfigDirectory(), "meterfeed");

    // ---- writing ----------------------------------------------------------

    private static readonly List<string> _lines = new();
    private static DateTime _start = DateTime.MinValue;
    private const int MaxLines = 6000;   // ~90 minutes of once-a-second summaries

    public static bool Recording { get; private set; }

    // Seconds since the recording began.
    public static double Elapsed
        => _start == DateTime.MinValue ? 0 : (DateTime.UtcNow - _start).TotalSeconds;

    public static void Start()
    {
        _lines.Clear();
        _start = DateTime.UtcNow;
        Recording = true;
    }

    // A replay must not record itself.
    public static void Pause() => Recording = false;

    public static void Resume() => Recording = true;

    public static void Record(Message m)
    {
        if (!Recording || _lines.Count >= MaxLines) return;
        var sb = new StringBuilder(256);
        sb.Append(F(m.At)).Append('|').Append(m.Active ? 1 : 0).Append('|')
          .Append(F(m.Seconds)).Append('|').Append(F(m.Damage)).Append('|')
          .Append(m.InCombat ? 1 : 0).Append('|').Append(m.Cutscene ? 1 : 0).Append('|')
          .Append(m.SawBoss ? 1 : 0).Append('|').Append(F(m.LogLines));
        foreach (var r in m.Rows)
            sb.Append('|').Append(r.Name.Replace('|', '/')).Append('~').Append(r.Job).Append('~')
              .Append(F(r.Damage)).Append('~').Append(F(r.Healed)).Append('~')
              .Append(F(r.Taken)).Append('~').Append(r.Deaths);
        _lines.Add(sb.ToString());
    }

    // Close the recording out to its own file, and return where it landed.
    public static string Stop()
    {
        Recording = false;
        if (_lines.Count == 0) return "";
        try
        {
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, $"{_start.ToLocalTime():yyyyMMdd-HHmmss}.feed");
            File.WriteAllLines(path, _lines);
            _lines.Clear();
            return path;
        }
        catch (Exception ex) { Swallowed.Report("meter feed write", ex); return ""; }
    }

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    // ---- reading ----------------------------------------------------------

    public static List<Message> Load(string path)
    {
        var list = new List<Message>();
        foreach (var line in File.ReadAllLines(path))
        {
            var f = line.Split('|');
            if (f.Length < 8) continue;
            var m = new Message
            {
                At = D(f[0]), Active = f[1] == "1", Seconds = (float)D(f[2]), Damage = D(f[3]),
                InCombat = f[4] == "1", Cutscene = f[5] == "1", SawBoss = f[6] == "1", LogLines = D(f[7]),
            };
            for (var i = 8; i < f.Length; i++)
            {
                var r = f[i].Split('~');
                if (r.Length < 6) continue;
                m.Rows.Add((r[0], r[1], D(r[2]), D(r[3]), D(r[4]), (int)D(r[5])));
            }
            list.Add(m);
        }
        return list;
    }

    // The newest recording, or "" when nothing has been recorded yet.
    public static string Newest()
    {
        try
        {
            if (!Directory.Exists(Folder)) return "";
            var newest = "";
            var when = DateTime.MinValue;
            foreach (var f in Directory.EnumerateFiles(Folder, "*.feed"))
            {
                var t = File.GetLastWriteTimeUtc(f);
                if (t <= when) continue;
                when = t;
                newest = f;
            }
            return newest;
        }
        catch (Exception ex) { Swallowed.Report("meter feed scan", ex); return ""; }
    }

    private static double D(string s)
        => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

    // A recorded message turned back into what the parser handed over.
    public static MeterEncounter ToEncounter(Message m)
    {
        var e = new MeterEncounter
        {
            Title = "Replay", Active = m.Active, Seconds = m.Seconds, TotalDamage = m.Damage,
        };
        foreach (var r in m.Rows)
            e.Rows.Add(new MeterCombatant
            {
                Name = r.Name, Display = r.Name, Job = r.Job,
                LimitBreak = Jobs.ByAbbreviation(r.Job) == null,
                Damage = r.Damage, Healed = r.Healed, Taken = r.Taken, Deaths = r.Deaths,
                Dps = m.Seconds > 0 ? r.Damage / m.Seconds : 0,
            });
        return e;
    }
}
