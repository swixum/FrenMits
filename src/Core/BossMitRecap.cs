using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;

namespace FrenMits;

// Watches the boss's mitigation debuffs (the damage-downs a party lands ON the
// boss: Reprisal / Feint / Addle / Dismantle) through a pull, so after a wipe you
// can pull a recap: what was on the boss, when each went up, and which of the
// standard raid mits never landed. Read-only game state, fully guarded.
public class BossMitRecap
{
    private readonly Plugin _plugin;
    private bool _wasRunning;
    private readonly HashSet<string> _onBoss = new(StringComparer.OrdinalIgnoreCase);

    // Damage-down debuffs that land on the boss. A full party can supply all four
    // (tank Reprisal, melee Feint, caster Addle, MCH Dismantle).
    public static readonly string[] StandardRaidMits = { "Reprisal", "Feint", "Addle", "Dismantle" };

    public sealed record Applied(float Time, string Name);
    public sealed record Active(uint Icon, string Name, float Remaining);

    public List<Applied> Log { get; } = new();              // live, this pull
    public List<Applied> LastLog { get; private set; } = new();   // last completed pull
    public List<Active> Snapshot { get; private set; } = new();   // on the boss at last capture
    public DateTime CapturedAt { get; private set; }

    public BossMitRecap(Plugin plugin) => _plugin = plugin;

    public void Update()
    {
        try
        {
            var running = _plugin.Timer.Running && !Plugin.InCutscene;
            if (running && !_wasRunning) { Log.Clear(); _onBoss.Clear(); }
            else if (!running && _wasRunning && Log.Count > 0) Capture(); // pull ended -> recap
            _wasRunning = running;
            if (!running) return;

            var boss = FindBoss();
            if (boss == null) return;
            var fight = _plugin.ActiveFight();
            var elapsed = fight != null ? _plugin.ElapsedFor(fight) : _plugin.Timer.Elapsed;

            var now = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in MitsOn(boss))
            {
                now.Add(m.Name);
                if (_onBoss.Add(m.Name)) Log.Add(new Applied(elapsed, m.Name)); // newly applied
            }
            _onBoss.RemoveWhere(n => !now.Contains(n)); // dropped -> can log again on re-apply
        }
        catch { /* never disturb the tick */ }
    }

    // Snapshot the boss's current mits + the pull's log. Called automatically when
    // a pull ends and manually by the on-screen Recap button.
    public void Capture()
    {
        try
        {
            LastLog = new List<Applied>(Log);
            var boss = FindBoss();
            Snapshot = boss != null ? MitsOn(boss) : new List<Active>();
            CapturedAt = DateTime.UtcNow;
        }
        catch { /* ignore */ }
    }

    // Standard raid mits that never landed on the boss this pull (informational —
    // a comp without a caster/MCH simply can't supply Addle/Dismantle).
    public List<string> NotSeen()
        => StandardRaidMits
            .Where(s => !LastLog.Any(a => a.Name.Contains(s, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    public bool HasData => LastLog.Count > 0 || Snapshot.Count > 0;

    private static IBattleNpc? FindBoss()
    {
        IBattleNpc? boss = null;
        foreach (var o in Service.ObjectTable)
            if (o is IBattleNpc n && n.MaxHp > 1_000_000 && (boss is null || n.MaxHp > boss.MaxHp))
                boss = n;
        return boss;
    }

    private static List<Active> MitsOn(IBattleNpc boss)
    {
        var list = new List<Active>();
        var sheet = Service.DataManager.GetExcelSheet<Status>();
        if (sheet == null) return list;
        foreach (var st in boss.StatusList)
        {
            if (st is null || st.StatusId == 0) continue;
            if (sheet.GetRowOrDefault(st.StatusId) is not { } row) continue;
            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!IsBossMit(name)) continue;
            list.Add(new Active((uint)row.Icon, name, MathF.Abs(st.RemainingTime)));
        }
        return list;
    }

    private static bool IsBossMit(string name)
    {
        foreach (var s in StandardRaidMits)
            if (name.Contains(s, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
