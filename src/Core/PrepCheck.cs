using System;
using System.Collections.Concurrent;
using Lumina.Excel.Sheets;

namespace FrenMits;

// Pre-pull checks for food and potion.
public static class PrepCheck
{
    // Verified against the Status sheet, both rows are stable.
    public const uint WellFedStatus = 48;    // food
    public const uint MedicatedStatus = 49;  // tincture / pot

    public enum Grade
    {
        Ok,        // up with time to spare (nothing is drawn)
        Expiring,  // up, but under the warning threshold
        Missing,   // not up at all
    }

    // Param carries the dish, so the row can show the real food.
    public readonly record struct Buff(bool Present, float Remaining, ushort Param = 0);

    // The whole food decision, in one pure function.
    public static Grade GradeOf(Buff buff, float warnSeconds)
    {
        if (!buff.Present) return Grade.Missing;
        // No readable timer means untimed, not about to drop.
        if (buff.Remaining <= 0f) return Grade.Ok;
        return buff.Remaining <= warnSeconds ? Grade.Expiring : Grade.Ok;
    }

    // The warning threshold, clamped against nonsense values.
    public static float WarnSeconds(float minutes)
        => Math.Clamp(minutes, 1f, 60f) * 60f;

    // "m:ss", never negative.
    public static string Clock(float seconds)
    {
        var t = (int)MathF.Ceiling(MathF.Max(0f, seconds));
        return $"{t / 60}:{t % 60:00}";
    }

    // The food line, or "" when there's nothing to say.
    public static string FoodLine(Buff food, float warnSeconds)
        => GradeOf(food, warnSeconds) switch
        {
            Grade.Missing => "No food",
            Grade.Expiring => $"Food {Clock(food.Remaining)}",
            _ => "",
        };

    public const string PotionText = "Potion is Available!";

    // ---- the fuller food verdict ----

    // How loudly a line reads.
    public enum Level { None, Info, Warn, Danger }

    // Everything the checks need, so the verdict stays pure.
    public readonly record struct FoodOpts(
        float WarnSeconds,
        bool WarnWrongFood,
        bool WarnNq,
        bool AlwaysShow);

    public readonly record struct Verdict(string Text, Level Level)
    {
        public bool Any => Level != Level.None;
    }

    // The food line and its volume, worst problem first.
    public static Verdict FoodVerdict(Buff food, bool isBattleFood, bool isHq, FoodOpts o)
    {
        if (!food.Present) return new Verdict("No food", Level.Danger);

        // Crafter food outranks the timer, since it does nothing.
        if (o.WarnWrongFood && !isBattleFood) return new Verdict("Crafter food", Level.Danger);

        var grade = GradeOf(food, o.WarnSeconds);
        if (grade == Grade.Expiring) return new Verdict($"Food {Clock(food.Remaining)}", Level.Warn);

        if (o.WarnNq && !isHq) return new Verdict("Food is NQ", Level.Warn);

        if (o.AlwaysShow && food.Remaining > 0f)
            return new Verdict($"Food {Clock(food.Remaining)}", Level.Info);

        return new Verdict("", Level.None);
    }

    // The threshold: the slider, or the fight length when known.
    public static float WarnSecondsFor(bool useFightLength, float minutes, float fightSeconds)
        => useFightLength && fightSeconds > 0f ? fightSeconds : WarnSeconds(minutes);

    // How long this fight runs, 0 when that means nothing.
    public static float FightSeconds(FightProfile? fight)
    {
        // A baked duty packs several encounters onto one clock.
        if (fight == null || fight.TimelineOnly) return 0f;
        var last = 0f;
        foreach (var l in fight.Lines) if (l.Time > last) last = l.Time;
        foreach (var r in fight.CustomRows) if (r.Time > last) last = r.Time;
        return last;
    }

    // "(3 left)", or "" with no count worth showing.
    public static string Count(int n) => n > 0 ? $"  ({n} left)" : "";

    // ---- speech ----

    // What each food state is worth saying out loud.
    public static string SpeechFor(Grade grade) => grade switch
    {
        Grade.Missing => "No food",
        Grade.Expiring => "Food is running out",
        _ => "",
    };

    public const string PotionSpeech = "Potion is available";

    // Says each phrase once, when it becomes true.
    public sealed class Announcer
    {
        private string _said = "";

        // The phrase to speak this frame, or null for silence.
        public string? Next(string phrase)
        {
            if (string.Equals(phrase, _said, StringComparison.Ordinal)) return null;
            _said = phrase;
            return phrase.Length > 0 ? phrase : null;
        }

        public void Reset() => _said = "";
    }

    // Worth drawing inside a duty, out of combat.
    public static bool ShouldShow(bool enabled, bool inDuty, bool inCombat, bool readyCheck)
        => enabled && (readyCheck || (inDuty && !inCombat));

    // True while the ready check window is up.
    public static unsafe bool ReadyCheckActive()
    {
        try
        {
            var agent = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentReadyCheck.Instance();
            return agent != null && agent->IsAgentActive();
        }
        catch (Exception ex) { Swallowed.Report("prep ready check", ex); return false; }
    }

    // ---- the potion timer ----

    // The potion note is not a pre-pull check.
    public sealed class PotionTimer
    {
        // Only used when the item's own recast won't read.
        public const float DefaultCooldownSeconds = 300f;
        public const float ShowSeconds = 5f;

        // A status list that blinks empty is not the buff dropping.
        public const float BlinkGraceSeconds = 2f;

        // How long combat stays off before the pull counts as over.
        public const float PullOverSeconds = 3f;

        private bool _wasUp;
        private double _lastUpAt = double.NegativeInfinity;
        private double _readyAt;
        private bool _pending;                             // a use is being timed
        private double _firedAt = double.NegativeInfinity;
        private bool _sawCombat;                           // a pull has been under way
        private double _leftCombatAt = double.NegativeInfinity;

        // Whether the note should be on screen this frame.
        public bool Update(bool medicatedUp, float recastSeconds, double now, bool inCombat)
        {
            // Treat a blink as still up, so only a real expiry ends it.
            if (medicatedUp) _lastUpAt = now;
            var up = medicatedUp || now - _lastUpAt < BlinkGraceSeconds;

            // Rising edge: a pot was just used.
            if (up && !_wasUp)
            {
                _readyAt = now + (recastSeconds > 0f ? recastSeconds : DefaultCooldownSeconds);
                _pending = true;
            }
            _wasUp = up;

            // The pull ended.
            if (inCombat)
            {
                _sawCombat = true;
                _leftCombatAt = double.NegativeInfinity;
            }
            else if (_sawCombat)
            {
                if (double.IsNegativeInfinity(_leftCombatAt)) _leftCombatAt = now;
                else if (now - _leftCombatAt >= PullOverSeconds)
                {
                    // Leave the buff edge alone, since a late pot can still be up.
                    _pending = false;
                    _firedAt = double.NegativeInfinity;
                    _sawCombat = false;
                    _leftCombatAt = double.NegativeInfinity;
                }
            }

            // Back off recast: say so once, and only in a pull.
            if (_pending && now >= _readyAt && inCombat)
            {
                _pending = false;
                _firedAt = now;
            }

            return now - _firedAt < ShowSeconds;
        }

        // Seconds until the pot is back, 0 when nothing is timed.
        public float Remaining(double now)
            => _pending ? (float)Math.Max(0.0, _readyAt - now) : 0f;

        // Forgotten on leaving the duty, and only then.
        public void Reset()
        {
            _wasUp = false;
            _lastUpAt = double.NegativeInfinity;
            _pending = false;
            _readyAt = 0;
            _firedAt = double.NegativeInfinity;
            _sawCombat = false;
            _leftCombatAt = double.NegativeInfinity;
        }
    }

    // ---- game reads ----

    // The named status on the local player, if it's up.
    public static Buff Read(uint statusId)
    {
        try
        {
            var me = Plugin.LocalPlayer;
            if (me == null) return default;
            foreach (var st in me.StatusList)
            {
                if (st is null || st.StatusId != statusId) continue;
                // RemainingTime comes through negative on some statuses.
                return new Buff(true, MathF.Abs(st.RemainingTime), st.Param);
            }
        }
        catch (Exception ex)
        {
            // A failed read looks like no food, so leave a trail.
            Swallowed.Report("prep buff read", ex);
        }
        return default;
    }

    // The dish you ate when it resolves, else the Well Fed icon.
    public static uint FoodIcon(Buff food)
    {
        if (food.Present && food.Param != 0)
        {
            // Param is the item id, +10000 when the meal was HQ.
            var itemId = (uint)(food.Param > 10000 ? food.Param - 10000 : food.Param);
            var icon = ItemIcon(itemId);
            if (icon != 0) return icon;
        }
        return StatusIcon(WellFedStatus);
    }

    private static readonly ConcurrentDictionary<uint, uint> _statusIcons = new();
    private static readonly ConcurrentDictionary<uint, uint> _itemIcons = new();

    // A status's own icon.
    public static uint StatusIcon(uint statusId)
        => Cached(_statusIcons, statusId, id =>
            GameSheets.English<Status>()?.GetRowOrDefault(id) is { } row ? (uint)row.Icon : 0u,
            "prep status icon");

    private static uint ItemIcon(uint itemId)
        => Cached(_itemIcons, itemId, id =>
            GameSheets.English<Item>()?.GetRowOrDefault(id) is { } row ? (uint)row.Icon : 0u,
            "prep food icon");

    // The recast of the pot that's up, or 0 when unresolved.
    public static float RecastFor(Buff medicated)
    {
        if (!medicated.Present || medicated.Param == 0) return 0f;
        // Medicated carries the tincture in Param, HQ at +10000.
        var itemId = (uint)(medicated.Param > 10000 ? medicated.Param - 10000 : medicated.Param);
        return Cached(_itemRecasts, itemId, id =>
            GameSheets.English<Item>()?.GetRowOrDefault(id) is { } row ? row.Cooldowns : 0u,
            "prep potion recast");
    }

    private static readonly ConcurrentDictionary<uint, uint> _itemRecasts = new();

    // The item behind the status, HQ at +10000.
    public static uint ItemOf(Buff buff)
        => buff.Param == 0 ? 0u : (uint)(buff.Param > 10000 ? buff.Param - 10000 : buff.Param);

    public static bool IsHq(Buff buff) => buff.Param > 10000;

    // Crafting and gathering stats, by BaseParam row.
    private static readonly uint[] CraftParams = { 10, 11, 70, 71, 72, 73 };

    // True when the dish boosts a stat that matters in a fight.
    public static bool IsBattleFood(Buff food)
    {
        var itemId = ItemOf(food);
        if (itemId == 0) return true;
        // Both answers are non-zero, since Cached reads 0 as a miss.
        return Cached(_battleFood, itemId, id =>
        {
            var item = GameSheets.English<Item>()?.GetRowOrDefault(id);
            if (item is not { } row) return Battle;
            // Food hangs its stats off ItemAction's ItemFood row.
            var act = GameSheets.English<ItemAction>()?.GetRowOrDefault(row.ItemAction.RowId);
            if (act is not { } a) return Battle;
            var stats = GameSheets.English<ItemFood>()?.GetRowOrDefault(a.Data[1]);
            if (stats is not { } f) return Battle;
            var sawAny = false;
            foreach (var p in f.Params)
            {
                var bp = p.BaseParam.RowId;
                if (bp == 0) continue;
                sawAny = true;
                if (Array.IndexOf(CraftParams, bp) < 0) return Battle;
            }
            // Every stat was a crafting one.
            return sawAny ? Crafter : Battle;
        }, "prep food stats") != Crafter;
    }

    private const uint Crafter = 1;
    private const uint Battle = 2;

    private static readonly ConcurrentDictionary<uint, uint> _battleFood = new();

    // How many are in your bags, 0 when it can't be read.
    public static unsafe int BagCount(uint itemId, bool hq)
    {
        if (itemId == 0) return 0;
        try
        {
            var inv = FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Instance();
            if (inv == null) return 0;
            return Math.Max(0, inv->GetInventoryItemCount(itemId, hq, false, false, 0));
        }
        catch (Exception ex) { Swallowed.Report("prep bag count", ex); return 0; }
    }

    private static uint Cached(ConcurrentDictionary<uint, uint> cache, uint key,
                               Func<uint, uint> lookup, string site)
    {
        if (cache.TryGetValue(key, out var hit)) return hit;
        uint icon = 0;
        try { icon = lookup(key); }
        catch (Exception ex) { Swallowed.Report(site, ex); }
        // Only memoize a real answer, so a miss can resolve later.
        if (icon != 0) cache[key] = icon;
        return icon;
    }
}
