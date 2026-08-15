using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Network;

namespace FrenMits.Recap;

// Everything the recap reads off the wire: enemy actions, dot ticks, deaths and
// every mit going up or down. Statuses and deaths used to come off a four-times-
// a-second scan, which is why a fast death read as "nothing up" and a dot kill
// had no story at all. All three arrive here the moment the server says so.
public unsafe class DamageCapture : IDisposable
{
    private readonly Plugin _plugin;

    private delegate void ReceiveEffect(uint casterEntityId, Character* caster, System.Numerics.Vector3* targetPos,
        ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetIds);

    private delegate void ActorControl(uint entityId, uint category, uint arg1, uint arg2, uint arg3,
        uint arg4, uint arg5, uint arg6, uint arg7, uint arg8, GameObjectId targetId, bool isRecorded);

    private delegate bool SetStatusDelegate(StatusManager* self, int index, ushort statusId, float remaining,
        ushort param, GameObjectId source, bool refreshFlags);

    private readonly Hook<ReceiveEffect>? _effectHook;
    private readonly Hook<ActorControl>? _controlHook;
    private readonly Hook<SetStatusDelegate>? _statusHook;

    // One enemy action landing on the party, with the debuffs up.
    public readonly record struct EnemyHit(float Time, uint ActionId, string Action, int PlayerTargets, int DebuffMask);

    public List<EnemyHit> Hits { get; } = new();
    private const int MaxHits = 3000;

    // The hits a player took, as they landed. Hp is their reading as the packet
    // was processed; whether the hit is already in it is the game's business,
    // and either way it beats a quarter-second-old scan.
    public readonly record struct PlayerHit(float Time, string Action, uint Amount, string Mits,
        uint Hp = 0, uint MaxHp = 0, bool OverTime = false);

    public Dictionary<string, List<PlayerHit>> RecentHits { get; } = new(StringComparer.OrdinalIgnoreCase);
    // How much of a run-in a death can ask for, and a cap so a long pull can't grow.
    private const float HitTrail = 20f;
    private const int MaxHitsPerPlayer = 64;

    // A mit going up or down, stamped when the server said so.
    public readonly record struct StatusChange(float Time, string Who, bool OnEnemy, string Mit,
        MitTypes.Kind Kind, uint Icon, float Duration, bool Applied);

    // A death, stamped by the server rather than caught by a scan.
    public readonly record struct DeathStamp(float Time, string Who);

    // Drained by the recap each tick.
    public List<StatusChange> StatusFeed { get; } = new();
    public List<DeathStamp> DeathFeed { get; } = new();
    // A backstop against the drain stopping, not a budget: a pull never gets near it.
    private const int MaxFeed = 4000;

    // Hooking can fail after a patch, so the recap falls back to its scan.
    public bool Available => _effectHook != null;

    public DamageCapture(Plugin plugin)
    {
        _plugin = plugin;
        _effectHook = Install<ReceiveEffect>(ActionEffectHandler.Addresses.Receive.String, OnEffect,
            "action effects", "recap grades from status scans only");
        _controlHook = Install<ActorControl>(PacketDispatcher.Addresses.HandleActorControlPacket.String, OnActorControl,
            "actor control", "deaths fall back to the status scan and dot kills lose their story");
        _statusHook = Install<SetStatusDelegate>(StatusManager.Addresses.SetStatus.String, OnSetStatus,
            "status changes", "mit windows fall back to the status scan");
    }

    private static Hook<T>? Install<T>(string signature, T detour, string what, string fallback) where T : Delegate
    {
        try
        {
            var hook = Service.GameInterop.HookFromSignature(signature, detour);
            hook.Enable();
            return hook;
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, $"[FrenMits] {what} hook unavailable; {fallback}");
            return null;
        }
    }

    public void Clear()
    {
        Hits.Clear();
        RecentHits.Clear();
        StatusFeed.Clear();
        DeathFeed.Clear();
    }

    private long _clockAt = -1;
    private float _clock;

    // The clock the recap logs into. Every status change asks for it, and a
    // frame's worth of packets all happened at the same instant, so hold it.
    private float Elapsed()
    {
        var now = Environment.TickCount64;
        if (now == _clockAt) return _clock;
        _clockAt = now;
        var fight = _plugin.ActiveFight();
        return _clock = fight != null ? _plugin.ElapsedFor(fight) : _plugin.Timer.Elapsed;
    }

    private bool Recording => _plugin.Config.RecapEnabled && _plugin.Timer.Running;

    // ---- action effects ----

    private void OnEffect(uint casterEntityId, Character* caster, System.Numerics.Vector3* targetPos,
        ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetIds)
    {
        _effectHook!.Original(casterEntityId, caster, targetPos, header, effects, targetIds);
        // The detour must never disturb packet processing.
        try
        {
            NoteOwnPress(casterEntityId, caster, header);
            LearnStatuses(header, effects);
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

    // Effect entries that mean the action landed on that target.
    private static bool Connected(byte type) => type is EffectDamage or EffectBlocked or EffectParried;

    // Effect entry kinds, as the action packet numbers them.
    private const byte EffectDamage = 3;
    private const byte EffectBlocked = 5;
    private const byte EffectParried = 6;
    private const byte EffectStatusOnTarget = 14;
    private const byte EffectStatusOnSource = 15;

    // An action that says which statuses it applied is the whole binding a
    // status id needs; no follow-up buff has to be named by hand.
    private void LearnStatuses(ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects)
    {
        if (!_plugin.Config.RecapEnabled || header->NumTargets == 0) return;
        var action = ActionNameOf(header->SpellId);
        if (action.Length == 0 || !MitStatusBook.IsTrackedAction(action)) return;
        for (var i = 0; i < header->NumTargets; i++)
            foreach (var e in effects[i].Effects)
                if (e.Type is EffectStatusOnTarget or EffectStatusOnSource)
                    MitStatusBook.Learn(e.Value, action);
    }

    private void Record(uint casterEntityId, Character* caster,
        ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetIds)
    {
        if (!Recording || header->NumTargets == 0) return;

        // Enemies only, so a pet's target dummy hits don't count.
        if (Service.ObjectTable.SearchById(casterEntityId) is not IBattleNpc npc
            || (byte)npc.BattleNpcKind != 5) return;

        var elapsed = Elapsed();
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
                amount += (e.Param4 & 0x40) != 0 ? e.Value + ((uint)e.Param3 << 16) : e.Value;
            }
            if (!connected) continue;
            players++;
            AddPlayerHit(pc, elapsed, action, amount, overTime: false);
        }
        if (players == 0) return;

        var mask = 0;
        var sm = caster->GetStatusManager();
        if (sm != null)
            foreach (ref var st in sm->Status)
                if (st.StatusId != 0 && MitStatusBook.Resolve(st.StatusId) is { } e)
                    mask |= MitStatusBook.BitOf(e.Mit);

        // A long pull rolls rather than going quiet, so its tail survives.
        Hits.Add(new EnemyHit(elapsed, header->SpellId, action, players, mask));
        if (Hits.Count > MaxHits) Hits.RemoveRange(0, Hits.Count - MaxHits);
    }

    // What the hit was calculated against, read the instant it landed.
    private void AddPlayerHit(IPlayerCharacter pc, float elapsed, string action, uint amount, bool overTime)
    {
        var mits = string.Join(", ", MitRecap.MitNamesOn(pc));
        var shield = pc.ShieldPercentage;
        if (shield > 0) mits = mits.Length > 0 ? $"{mits}, {shield}% shield" : $"{shield}% shield";
        var who = pc.Name.ToString();
        if (!RecentHits.TryGetValue(who, out var ring)) ring = RecentHits[who] = new List<PlayerHit>();
        ring.Add(new PlayerHit(elapsed, action, amount, mits, pc.CurrentHp, pc.MaxHp, overTime));
        // Bound by the run-in a death can ask for, not by a count of autos.
        while (ring.Count > 0 && (elapsed - ring[0].Time > HitTrail || ring.Count > MaxHitsPerPlayer))
            ring.RemoveAt(0);
    }

    // ---- actor control: deaths and ticks ----

    // The actor-control categories the recap reads.
    private const uint ControlDeath = 6;
    private const uint ControlDoT = 1541;

    private void OnActorControl(uint entityId, uint category, uint arg1, uint arg2, uint arg3,
        uint arg4, uint arg5, uint arg6, uint arg7, uint arg8, GameObjectId targetId, bool isRecorded)
    {
        _controlHook!.Original(entityId, category, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, targetId, isRecorded);
        try
        {
            if (category is not (ControlDeath or ControlDoT) || !Recording) return;
            if (Service.ObjectTable.SearchById(entityId) is not IPlayerCharacter pc) return;
            if (category == ControlDeath)
            {
                if (DeathFeed.Count < MaxFeed) DeathFeed.Add(new DeathStamp(Elapsed(), pc.Name.ToString()));
                return;
            }
            // A tick names the status that dealt it, so the story can say what killed them.
            AddPlayerHit(pc, Elapsed(), StatusNameOf(arg1), arg2, overTime: true);
        }
        catch (Exception ex) { Swallowed.Report("actor control capture", ex); }
    }

    // ---- status changes ----

    private bool OnSetStatus(StatusManager* self, int index, ushort statusId, float remaining,
        ushort param, GameObjectId source, bool refreshFlags)
    {
        // What sat in the slot is only readable before the call replaces it.
        ushort was = 0;
        try
        {
            if (self != null && index >= 0 && index < self->Status.Length) was = self->Status[index].StatusId;
        }
        catch { /* the call still has to happen */ }

        var ret = _statusHook!.Original(self, index, statusId, remaining, param, source, refreshFlags);

        try { NoteStatus(self, was, statusId, remaining); }
        catch (Exception ex) { Swallowed.Report("status capture", ex); }
        return ret;
    }

    private void NoteStatus(StatusManager* self, ushort was, ushort now, float remaining)
    {
        if (was == now || self == null || StatusFeed.Count >= MaxFeed) return;
        var gained = MitStatusBook.Resolve(now);
        var lost = MitStatusBook.Resolve(was);
        if (gained == null && lost == null) return;
        if (!Recording) return;

        var owner = self->Owner;
        if (owner == null) return;
        var obj = Service.ObjectTable.SearchById(owner->GameObject.EntityId);
        var onEnemy = obj is IBattleNpc npc && (byte)npc.BattleNpcKind == 5;
        if (obj is not IPlayerCharacter && !onEnemy) return;
        var who = obj!.Name.ToString();
        var t = Elapsed();

        // A slot being reused is not the mit ending: one button can hold several
        // statuses, and the game moves them around. Only call it off once none
        // of them is left on the holder.
        if (lost is { } off && !StillHolds(self, off.Mit))
            StatusFeed.Add(new StatusChange(t, who, onEnemy, off.Mit, off.Kind, off.Icon, 0f, false));
        if (gained is { } on)
            StatusFeed.Add(new StatusChange(t, who, onEnemy, on.Mit, on.Kind, on.Icon, MathF.Abs(remaining), true));
    }

    // Whether any status still on this holder belongs to that mit.
    private static bool StillHolds(StatusManager* self, string mit)
    {
        foreach (ref var st in self->Status)
            if (st.StatusId != 0 && MitStatusBook.Resolve(st.StatusId) is { } e
                && string.Equals(e.Mit, mit, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    // ---- lookups ----

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

    private readonly Dictionary<uint, string> _statusNames = new();

    private string StatusNameOf(uint id)
    {
        if (_statusNames.TryGetValue(id, out var known)) return known;
        var name = "";
        try
        {
            name = GameData.English<Lumina.Excel.Sheets.Status>()?
                .GetRowOrDefault(id)?.Name.ExtractText() ?? "";
        }
        catch { /* sheet miss: cache the blank */ }
        return _statusNames[id] = name;
    }

    public void Dispose()
    {
        _effectHook?.Dispose();
        _controlHook?.Dispose();
        _statusHook?.Dispose();
    }
}
