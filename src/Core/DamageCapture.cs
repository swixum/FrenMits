using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace FrenMits;

// Watches the game process each incoming action-effect (the packet that carries
// every hit an action dealt), and records enemy hits on the party WITH which
// boss damage-downs were on the attacker at that instant.
//
// This is the only honest way to grade a Feint or Reprisal: the game decides a
// hit's damage from the state at its snapshot, so whether the debuff was up
// half a second later (when a status scan happens to look) proves nothing. The
// packet arrives at the snapshot, so what's on the attacker's status list right
// now is exactly what the damage was calculated from.
public unsafe class DamageCapture : IDisposable
{
    private readonly Plugin _plugin;

    private delegate void ReceiveEffect(uint casterEntityId, Character* caster, System.Numerics.Vector3* targetPos,
        ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetIds);

    private readonly Hook<ReceiveEffect>? _hook;

    // One enemy action landing on the party: when (pull clock), what, on how
    // many players, and which boss damage-downs were up as it was calculated.
    // Mask bits index MitRecap.StandardRaidMits.
    public readonly record struct EnemyHit(float Time, uint ActionId, string Action, int PlayerTargets, int DebuffMask);

    public List<EnemyHit> Hits { get; } = new();
    private const int MaxHits = 3000;

    // The last few enemy hits each player took: what struck them, for how
    // much, and what they had up AS IT LANDED. The death story reads this
    // instead of a status scan that can be a beat stale by the time HP shows
    // zero, and the recap's death detail plays the ring back hit by hit.
    public readonly record struct PlayerHit(float Time, string Action, uint Amount, string Mits);

    public Dictionary<string, List<PlayerHit>> RecentHits { get; } = new(StringComparer.OrdinalIgnoreCase);
    private const int HitRing = 6;

    // Hooking can fail after a game patch; the recap then falls back to its
    // status-scan grading rather than losing the feature outright.
    public bool Available => _hook != null;

    public DamageCapture(Plugin plugin)
    {
        _plugin = plugin;
        try
        {
            _hook = Service.GameInterop.HookFromSignature<ReceiveEffect>(
                ActionEffectHandler.Addresses.Receive.String, OnEffect);
            _hook.Enable();
        }
        catch (Exception ex)
        {
            _hook = null;
            Service.Log.Warning(ex, "[FrenMits] action-effect hook unavailable; recap grades from status scans only");
        }
    }

    public void Clear()
    {
        Hits.Clear();
        RecentHits.Clear();
    }

    private void OnEffect(uint casterEntityId, Character* caster, System.Numerics.Vector3* targetPos,
        ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetIds)
    {
        _hook!.Original(casterEntityId, caster, targetPos, header, effects, targetIds);
        // The detour must never disturb packet processing.
        try { Record(casterEntityId, caster, header, effects, targetIds); }
        catch (Exception ex) { Swallowed.Report("damage capture", ex); }
    }

    // Effect entry types that mean the action connected (hit, blocked, parried,
    // or missed outright); everything else on the entry list is bookkeeping.
    private static bool Connected(byte type) => type is 1 or 3 or 5 or 6;

    private void Record(uint casterEntityId, Character* caster,
        ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetIds)
    {
        if (!_plugin.Config.RecapEnabled || !_plugin.Timer.Running) return;
        if (header->NumTargets == 0 || Hits.Count >= MaxHits) return;

        // Enemies only; a pet or trust NPC attacking its practice target must
        // not read as boss damage. Subkind 5 = enemy (stable game data).
        if (Service.ObjectTable.SearchById(casterEntityId) is not IBattleNpc npc
            || (byte)npc.BattleNpcKind != 5) return;

        // Same clock the recap logs into, so the plan check compares like with
        // like.
        var fight = _plugin.ActiveFight();
        var elapsed = fight != null ? _plugin.ElapsedFor(fight) : _plugin.Timer.Elapsed;
        var action = ActionNameOf(header->SpellId);

        var players = 0;
        for (var i = 0; i < header->NumTargets; i++)
        {
            if (Service.ObjectTable.SearchById(targetIds[i].ObjectId) is not IPlayerCharacter pc) continue;
            var connected = false;
            var amount = 0u;
            foreach (var e in effects[i].Effects)
            {
                if (!Connected(e.Type)) continue;
                connected = true;
                // Damage entries carry their amount split: high bits ride in
                // Param3 when Param4 flags them.
                if (e.Type is 3 or 5 or 6)
                    amount += (e.Param4 & 0x40) != 0 ? e.Value + ((uint)e.Param3 << 16) : e.Value;
            }
            if (!connected) continue;
            players++;
            var mits = string.Join(", ", MitRecap.MitNamesOn(pc));
            var shield = pc.ShieldPercentage;
            if (shield > 0) mits = mits.Length > 0 ? $"{mits}, {shield}% shield" : $"{shield}% shield";
            var who = pc.Name.ToString();
            if (!RecentHits.TryGetValue(who, out var ring)) ring = RecentHits[who] = new List<PlayerHit>();
            ring.Add(new PlayerHit(elapsed, action, amount, mits));
            if (ring.Count > HitRing) ring.RemoveAt(0);
        }
        if (players == 0) return;

        var mask = 0;
        var sm = caster->GetStatusManager();
        if (sm != null)
            foreach (ref var st in sm->Status)
                if (st.StatusId != 0 && DebuffBits().TryGetValue(st.StatusId, out var bit))
                    mask |= bit;

        Hits.Add(new EnemyHit(elapsed, header->SpellId, action, players, mask));
    }

    // ---- lookups -----------------------------------------------------------

    private Dictionary<uint, int>? _debuffBits;

    // Status id -> StandardRaidMits bit, by name so no id list goes stale. The
    // status sheet spells Dismantle's debuff "Dismantled"; Contains covers it,
    // the same match IsBossMit uses.
    private Dictionary<uint, int> DebuffBits()
    {
        if (_debuffBits != null) return _debuffBits;
        var map = new Dictionary<uint, int>();
        var sheet = GameSheets.English<Lumina.Excel.Sheets.Status>();
        if (sheet == null) return map; // sheets not ready: retry next hit
        foreach (var row in sheet)
        {
            var name = row.Name.ExtractText();
            if (string.IsNullOrEmpty(name)) continue;
            for (var i = 0; i < MitRecap.StandardRaidMits.Length; i++)
                if (name.Contains(MitRecap.StandardRaidMits[i], StringComparison.OrdinalIgnoreCase))
                    map[row.RowId] = 1 << i;
        }
        return _debuffBits = map;
    }

    public static int BitOf(string mit)
    {
        for (var i = 0; i < MitRecap.StandardRaidMits.Length; i++)
            if (mit.Contains(MitRecap.StandardRaidMits[i], StringComparison.OrdinalIgnoreCase))
                return 1 << i;
        return 0;
    }

    private readonly Dictionary<uint, string> _actionNames = new();

    private string ActionNameOf(uint id)
    {
        if (_actionNames.TryGetValue(id, out var known)) return known;
        var name = "";
        try
        {
            name = GameSheets.English<Lumina.Excel.Sheets.Action>()?
                .GetRowOrDefault(id)?.Name.ExtractText() ?? "";
        }
        catch { /* sheet miss: cache the blank */ }
        return _actionNames[id] = name;
    }

    public void Dispose() => _hook?.Dispose();
}
