using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace FrenMits.Recap;

// Records enemy hits with the boss damage-downs that were up at snapshot.
public unsafe class DamageCapture : IDisposable
{
    private readonly Plugin _plugin;

    private delegate void ReceiveEffect(uint casterEntityId, Character* caster, System.Numerics.Vector3* targetPos,
        ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetIds);

    private readonly Hook<ReceiveEffect>? _hook;

    // One enemy action landing on the party, with the debuffs up.
    public readonly record struct EnemyHit(float Time, uint ActionId, string Action, int PlayerTargets, int DebuffMask);

    public List<EnemyHit> Hits { get; } = new();
    private const int MaxHits = 3000;

    // The last few hits a player took, as they landed.
    public readonly record struct PlayerHit(float Time, string Action, uint Amount, string Mits);

    public Dictionary<string, List<PlayerHit>> RecentHits { get; } = new(StringComparer.OrdinalIgnoreCase);
    private const int HitRing = 6;

    // Hooking can fail after a patch, so the recap falls back.
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
        try
        {
            NoteOwnPress(casterEntityId, caster, header);
            Record(casterEntityId, caster, header, effects, targetIds);
        }
        catch (Exception ex) { Swallowed.Report("damage capture", ex); }
    }

    // The cooldown press log feeds from here: server-confirmed, and pets count as their owner.
    private void NoteOwnPress(uint casterEntityId, Character* caster, ActionEffectHandler.Header* header)
    {
        if (Plugin.LocalPlayer is not { } me) return;
        var mine = casterEntityId == me.EntityId
                   || (caster != null && caster->GameObject.OwnerId == me.EntityId);
        if (!mine) return;
        CooldownTracker.NotePress(ActionNameOf(header->SpellId));
    }

    // Entry types that mean the action connected.
    private static bool Connected(byte type) => type is 1 or 3 or 5 or 6;

    private void Record(uint casterEntityId, Character* caster,
        ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetIds)
    {
        if (!_plugin.Config.RecapEnabled || !_plugin.Timer.Running) return;
        if (header->NumTargets == 0 || Hits.Count >= MaxHits) return;

        // Enemies only, so a pet's target dummy hits don't count.
        if (Service.ObjectTable.SearchById(casterEntityId) is not IBattleNpc npc
            || (byte)npc.BattleNpcKind != 5) return;

        // Same clock the recap logs into.
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
                // High bits of the amount ride in Param3.
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

    // ---- lookups ----

    private Dictionary<uint, int>? _debuffBits;

    // Status id to mit bit, matched by name so no id goes stale.
    private Dictionary<uint, int> DebuffBits()
    {
        if (_debuffBits != null) return _debuffBits;
        var map = new Dictionary<uint, int>();
        var sheet = GameData.English<Lumina.Excel.Sheets.Status>();
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
            name = GameData.English<Lumina.Excel.Sheets.Action>()?
                .GetRowOrDefault(id)?.Name.ExtractText() ?? "";
        }
        catch { /* sheet miss: cache the blank */ }
        return _actionNames[id] = name;
    }

    public void Dispose() => _hook?.Dispose();
}
