using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FrenMits;

// Fren Meter's brain: drains the parser link, feeds log lines to the rDPS
// engine, and turns each summary update into the encounter snapshot the
// overlay draws. Finished pulls are kept as history.
public class Meter : IDisposable
{
    private readonly Plugin _plugin;
    private Configuration C => _plugin.Config;

    public MeterLink Link { get; }
    public RdpsEngine Engine { get; } = new();

    public MeterEncounter? Current { get; private set; }
    public List<MeterEncounter> History { get; } = new();
    private const int MaxHistory = 10;

    private DateTime _nextTrim = DateTime.MinValue;

    public Meter(Plugin plugin)
    {
        _plugin = plugin;
        Link = new MeterLink(plugin);
        Engine.IsLimitBreak = IsLimitBreak;
    }

    // Limit break action ids from the game sheet (category 9), resolved lazily
    // because sheets are not ready at load.
    private HashSet<uint>? _lbActions;

    private bool IsLimitBreak(uint actionId)
    {
        if (_lbActions == null)
        {
            var sheet = GameSheets.English<Lumina.Excel.Sheets.Action>();
            if (sheet == null) return false; // not ready: retry on a later hit
            var set = new HashSet<uint>();
            foreach (var row in sheet)
                if (row.ActionCategory.RowId == 9)
                    set.Add(row.RowId);
            _lbActions = set;
        }
        return _lbActions.Contains(actionId);
    }

    public void Update()
    {
        if (!C.MeterEnabled)
        {
            Link.EnsureStopped();
            return;
        }
        Link.EnsureStarted();

        // Everything queued since last tick; combat peaks are a few hundred
        // lines a second, far under the cap.
        var budget = 5000;
        while (budget-- > 0 && Link.TryDequeue(out var msg)) Handle(msg);

        if (DateTime.UtcNow >= _nextTrim)
        {
            _nextTrim = DateTime.UtcNow + TimeSpan.FromMinutes(1);
            Engine.Trim();
        }
    }

    private void Handle(JObject msg)
    {
        if (string.Equals(msg["type"]?.ToString(), "LogLine", StringComparison.Ordinal))
        {
            if (msg["line"] is JArray arr)
            {
                var line = new string[arr.Count];
                for (var i = 0; i < arr.Count; i++) line[i] = arr[i]?.ToString() ?? "";
                Engine.Process(line);
            }
            return;
        }

        if (MeterEncounter.Parse(msg) is not { Rows.Count: > 0 } enc) return;
        ApplyRdps(enc);

        // A pull's last living update freezes into history the moment the
        // parser calls it over.
        if (!enc.Active && Current is { Active: true })
        {
            History.Insert(0, enc);
            while (History.Count > MaxHistory) History.RemoveAt(History.Count - 1);
        }
        Current = enc;
    }

    private void ApplyRdps(MeterEncounter enc)
    {
        // The engine's newest event is "now" on the log clock; the encounter
        // window reaches back its own duration (a small pad absorbs the
        // summary feed lagging the line stream).
        var from = Engine.LatestSec - (long)enc.Seconds - 2;
        var totals = Engine.WindowTotals(from);
        var seconds = Math.Max(1f, enc.Seconds);
        var you = LocalName();

        enc.RaidRDps = 0;
        foreach (var row in enc.Rows)
        {
            // The parser reports the local player as "YOU".
            if (string.Equals(row.Name, "YOU", StringComparison.OrdinalIgnoreCase) && you.Length > 0)
                row.Display = you;
            row.RDps = row.Dps;
            if (totals.TryGetValue(row.Display, out var t))
                row.RDps = Math.Max(0, row.Dps + (t.Given - t.Received) / seconds);
            enc.RaidRDps += row.RDps;
        }
    }

    private string LocalName()
    {
        var name = Plugin.LocalPlayer?.Name.ToString() ?? "";
        return name.Length > 0 ? name : Engine.LocalPlayerName;
    }

    public void Clear()
    {
        Current = null;
        History.Clear();
    }

    public string StatusText => !C.MeterEnabled ? "off" : Link.Status switch
    {
        MeterLink.LinkStatus.Ipc => "connected to the parser (in-process)",
        MeterLink.LinkStatus.Socket => "connected to ACT (WebSocket)",
        MeterLink.LinkStatus.Searching => "searching for a parser...",
        _ => "starting...",
    };

    public bool Connected => Link.Status is MeterLink.LinkStatus.Ipc or MeterLink.LinkStatus.Socket;

    // A steady sample pull so the overlay can be placed and styled from Test mode.
    private MeterEncounter? _sample;

    public MeterEncounter Sample()
    {
        if (_sample != null) return _sample;
        var e = new MeterEncounter
        {
            Title = "Kefka (sample)", Duration = "04:12", Seconds = 252f, Active = false,
        };
        var rows = new (string Name, string Job, double Dps, double Edge)[]
        {
            ("Riko Snowpetal", "PCT", 21460, 1.06),
            ("Auri Vale", "VPR", 20110, 0.94),
            ("Sable Marsh", "SAM", 19230, 0.97),
            ("Nophica Reed", "MCH", 18040, 1.02),
            ("Ember Halcyon", "RDM", 17110, 1.11),
            ("Tia Windrun", "DRK", 12480, 1.05),
            ("Oren Bluewake", "GNB", 11930, 1.03),
            ("Lily Farsong", "SGE", 5810, 0.99),
        };
        foreach (var r in rows)
        {
            var c = new MeterCombatant
            {
                Name = r.Name, Display = r.Name, Job = r.Job,
                Dps = r.Dps, RDps = r.Dps * r.Edge, Damage = r.Dps * e.Seconds,
                CritPct = 18 + r.Dps % 13, DirectHitPct = 22 + r.Dps % 21,
                Hps = r.Job is "SGE" ? 9840 : r.Dps % 900,
                Healed = 0, OverhealPct = r.Job is "SGE" ? 21 : 4,
                Taken = 42000 + r.Dps % 9000, Deaths = r.Job is "VPR" ? 1 : 0,
            };
            c.Healed = c.Hps * e.Seconds;
            e.TotalDps += c.Dps;
            e.RaidRDps += c.RDps;
            e.TotalHps += c.Hps;
            e.TotalTaken += c.Taken;
            e.TotalDeaths += c.Deaths;
            e.Rows.Add(c);
        }
        e.TotalDamage = e.TotalDps * e.Seconds;
        foreach (var c in e.Rows)
            c.DamagePct = $"{c.Damage / e.TotalDamage * 100:0}%";
        return _sample = e;
    }

    public void Dispose() => Link.Dispose();
}
