using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;

namespace FrenMits;

// Watches every mitigation in the fight through a pull — the damage-down debuffs
// that land ON the boss (Reprisal / Feint / Addle / Dismantle) AND the
// damage-reduction buffs party members put on themselves / the party (Rampart,
// Sacred Soil, Kerachole, Reprisal-the-buff, etc.). Logs when each goes up and by
// whom, and snapshots on pull end, so after a wipe you get a full recap and can
// see what was missing. Read-only game state, fully guarded.
public class MitRecap
{
    private readonly Plugin _plugin;
    private bool _wasRunning;
    private DateTime _lastScan;
    private readonly HashSet<string> _active = new(StringComparer.OrdinalIgnoreCase); // "source|mit" currently up

    // Damage-down debuffs a full party lands on the boss.
    public static readonly string[] StandardRaidMits = { "Reprisal", "Feint", "Addle", "Dismantle" };

    public sealed record Applied(float Time, string Mit, string Source, MitTypes.Kind Kind, bool OnBoss, uint Icon);
    public sealed record Active(uint Icon, string Mit, string Source, float Remaining, MitTypes.Kind Kind, bool OnBoss);

    public List<Applied> Log { get; } = new();
    public List<Applied> LastLog { get; private set; } = new();
    public List<Active> Snapshot { get; private set; } = new();
    public DateTime CapturedAt { get; private set; }
    public bool PopupDismissed { get; private set; }

    // Hide the post-wipe popup without clearing the recap data.
    public void Dismiss() => PopupDismissed = true;

    public MitRecap(Plugin plugin) => _plugin = plugin;

    public void Update()
    {
        try
        {
            if (!_plugin.Config.RecapAutoCapture) { _wasRunning = false; return; }

            var running = _plugin.Timer.Running && !Plugin.InCutscene;
            if (running && !_wasRunning) { Log.Clear(); _active.Clear(); }
            else if (!running && _wasRunning && Log.Count > 0) FinalizePull(); // pull ended -> freeze recap
            _wasRunning = running;
            if (!running) return;

            // Mits last seconds — scanning a few times a second is plenty and keeps
            // the per-tick status sweep cheap.
            if ((DateTime.UtcNow - _lastScan).TotalSeconds < 0.25) return;
            _lastScan = DateTime.UtcNow;

            var fight = _plugin.ActiveFight();
            var elapsed = fight != null ? _plugin.ElapsedFor(fight) : _plugin.Timer.Elapsed;
            var now = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var live = new List<Active>();

            foreach (var (src, onBoss, chara) in Sources())
            {
                foreach (var m in MitsOn(chara, onBoss))
                {
                    var key = src + "|" + m.Mit;
                    now.Add(key);
                    if (_active.Add(key)) Log.Add(new Applied(elapsed, m.Mit, src, m.Kind, onBoss, m.Icon));
                    live.Add(new Active(m.Icon, m.Mit, src, m.Remaining, m.Kind, onBoss));
                }
            }
            _active.RemoveWhere(k => !now.Contains(k)); // dropped -> can log again on re-apply
            Snapshot = live; // keep "what's up" current, so the wipe snapshot has the boss mits
                             // from the last live moment (the boss resets the instant combat ends)
        }
        catch { /* never disturb the tick */ }
    }

    // Freeze the recap when a pull ends: keep the live Snapshot (the boss has reset
    // by now) and copy the timeline.
    private void FinalizePull()
    {
        LastLog = new List<Applied>(Log);
        CapturedAt = DateTime.UtcNow;
        PopupDismissed = false;
    }

    // Manual capture ("Capture now") — re-scans the current state right now.
    public void Capture()
    {
        try
        {
            LastLog = new List<Applied>(Log);
            var snap = new List<Active>();
            foreach (var (src, onBoss, chara) in Sources())
                foreach (var m in MitsOn(chara, onBoss))
                    snap.Add(new Active(m.Icon, m.Mit, src, m.Remaining, m.Kind, onBoss));
            Snapshot = snap;
            CapturedAt = DateTime.UtcNow;
            PopupDismissed = false;
        }
        catch { /* ignore */ }
    }

    // Make the popup + window appear now (for placing them) without touching data.
    public void ShowTestPopup()
    {
        CapturedAt = DateTime.UtcNow;
        PopupDismissed = false;
    }

    // Standard raid damage-downs that never landed on the boss this pull
    // (informational — comp-dependent).
    public List<string> NotSeen()
        => StandardRaidMits
            .Where(s => !LastLog.Any(a => a.OnBoss && a.Mit.Contains(s, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    public bool HasData => LastLog.Count > 0 || Snapshot.Count > 0;

    // A plain-text recap for the clipboard (paste into Discord / notes).
    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Party Mit Recap");
        var missed = NotSeen();
        sb.AppendLine(missed.Count == 0
            ? "All four standard raid mits landed."
            : "Never landed: " + string.Join(", ", missed));

        if (Snapshot.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Up at capture:");
            foreach (var m in Snapshot.OrderByDescending(m => m.OnBoss).ThenBy(m => m.Source))
                sb.AppendLine($"  {m.Mit} — {(m.OnBoss ? "on boss" : m.Source)} ({m.Remaining:0}s)");
        }

        if (LastLog.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Applied this pull:");
            foreach (var a in LastLog.OrderBy(a => a.Time))
                sb.AppendLine($"  {(int)a.Time / 60}:{(int)a.Time % 60:00}  {a.Mit} — {(a.OnBoss ? "on boss" : a.Source)}");
        }
        return sb.ToString();
    }

    // The things we read statuses off: the boss (debuffs) + every party player (buffs).
    private IEnumerable<(string source, bool onBoss, IBattleChara chara)> Sources()
    {
        var boss = FindBoss();
        if (boss != null) yield return ("Boss", true, boss);

        foreach (var o in Service.ObjectTable)
            if (o is IPlayerCharacter pc && pc.MaxHp > 0)
                yield return (pc.Name.ToString(), false, pc);
    }

    private static IBattleNpc? FindBoss()
    {
        IBattleNpc? boss = null;
        foreach (var o in Service.ObjectTable)
            if (o is IBattleNpc n && n.MaxHp > 1_000_000 && (boss is null || n.MaxHp > boss.MaxHp))
                boss = n;
        return boss;
    }

    private readonly record struct Hit(uint Icon, string Mit, float Remaining, MitTypes.Kind Kind);

    private static List<Hit> MitsOn(IBattleChara chara, bool onBoss)
    {
        var list = new List<Hit>();
        var sheet = Service.DataManager.GetExcelSheet<Status>();
        if (sheet == null) return list;
        foreach (var st in chara.StatusList)
        {
            if (st is null || st.StatusId == 0) continue;
            if (sheet.GetRowOrDefault(st.StatusId) is not { } row) continue;
            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (onBoss ? !IsBossMit(name) : !IsPartyMit(name)) continue;
            list.Add(new Hit((uint)row.Icon, name, MathF.Abs(st.RemainingTime), MitTypes.Classify(name)));
        }
        return list;
    }

    private static bool IsBossMit(string name)
    {
        foreach (var s in StandardRaidMits)
            if (name.Contains(s, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // Damage-reduction buffs on players. Recognised mit kinds, minus the pure heals
    // that share keywords (we want mitigation, not healing).
    private static readonly string[] HealNoise = { "medica", "cure", "regen", "benediction", "physis", "asylum" };
    private static bool IsPartyMit(string name)
    {
        if (MitTypes.Classify(name) == MitTypes.Kind.Other) return false;
        var l = name.ToLowerInvariant();
        foreach (var h in HealNoise)
            if (l.Contains(h)) return false;
        return true;
    }
}
