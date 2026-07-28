using System;
using System.Collections.Concurrent;
using Lumina.Excel.Sheets;

namespace FrenMits;

// Pre-pull checks: is your food actually up and going to last the pull, and is
// your potion there to be used?
//
// The game never tells you either. Food expiring four minutes into a nine-minute
// fight is a silent loss nobody notices until the parse, and walking in with no
// food at all is the kind of mistake you only catch after the wipe. Both are
// plain to see in your own status list, so the check costs nothing.
//
// Everything that decides anything here is pure and unit-tested; only the reads
// at the bottom touch the game.
public static class PrepCheck
{
    // Verified against the Status sheet: both rows are unique and have been
    // stable for the life of the game.
    public const uint WellFedStatus = 48;    // food
    public const uint MedicatedStatus = 49;  // tincture / pot

    public enum Grade
    {
        Ok,        // up with time to spare (nothing is drawn)
        Expiring,  // up, but under the warning threshold
        Missing,   // not up at all
    }

    // Param carries the dish itself on a Well Fed status, so the row can show
    // the food you actually ate rather than a generic buff icon.
    public readonly record struct Buff(bool Present, float Remaining, ushort Param = 0);

    // The whole food decision, in one pure function.
    public static Grade GradeOf(Buff buff, float warnSeconds)
    {
        if (!buff.Present) return Grade.Missing;
        // A present buff whose timer reads non-positive is a status we can't
        // time rather than one about to drop; saying nothing beats crying wolf
        // every frame for the rest of the duty.
        if (buff.Remaining <= 0f) return Grade.Ok;
        return buff.Remaining <= warnSeconds ? Grade.Expiring : Grade.Ok;
    }

    // The warning threshold in seconds, clamped so a nonsense config value can't
    // either silence the check or make it permanent.
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

    // ---- the fuller food verdict -------------------------------------------

    // How loudly a line reads. Info is for the always-on timer, which is a
    // readout rather than a complaint.
    public enum Level { None, Info, Warn, Danger }

    // Everything the optional checks need, so the verdict below stays pure and
    // the game reads all happen at the call site.
    public readonly record struct FoodOpts(
        float WarnSeconds,
        bool WarnWrongFood,
        bool WarnNq,
        bool AlwaysShow);

    public readonly record struct Verdict(string Text, Level Level)
    {
        public bool Any => Level != Level.None;
    }

    // The food line and how loudly to say it, worst problem first.
    //
    // `isBattleFood` and `isHq` are passed in because resolving them needs the
    // game's sheets; everything about what to DO with them lives here.
    public static Verdict FoodVerdict(Buff food, bool isBattleFood, bool isHq, FoodOpts o)
    {
        if (!food.Present) return new Verdict("No food", Level.Danger);

        // Raiding on crafter food outranks everything else: the timer is
        // irrelevant when the buff is doing nothing for you in the first place.
        if (o.WarnWrongFood && !isBattleFood) return new Verdict("Crafter food", Level.Danger);

        var grade = GradeOf(food, o.WarnSeconds);
        if (grade == Grade.Expiring) return new Verdict($"Food {Clock(food.Remaining)}", Level.Warn);

        if (o.WarnNq && !isHq) return new Verdict("Food is NQ", Level.Warn);

        if (o.AlwaysShow && food.Remaining > 0f)
            return new Verdict($"Food {Clock(food.Remaining)}", Level.Info);

        return new Verdict("", Level.None);
    }

    // The warning threshold: either the slider, or the length of the fight in
    // front of you when that's known.
    public static float WarnSecondsFor(bool useFightLength, float minutes, float fightSeconds)
        => useFightLength && fightSeconds > 0f ? fightSeconds : WarnSeconds(minutes);

    // How long this fight runs, or 0 when that isn't a meaningful question.
    public static float FightSeconds(FightProfile? fight)
    {
        // A baked duty timeline packs every encounter of an instance onto ONE
        // clock in 1000-second blocks, so its last time is not a fight length -
        // it's boss three's coordinate. Only a real sheet can answer this.
        if (fight == null || fight.TimelineOnly) return 0f;
        var last = 0f;
        foreach (var l in fight.Lines) if (l.Time > last) last = l.Time;
        foreach (var r in fight.CustomRows) if (r.Time > last) last = r.Time;
        return last;
    }

    // "(3 left)", or "" when we have no count worth showing.
    public static string Count(int n) => n > 0 ? $"  ({n} left)" : "";

    // ---- speech ------------------------------------------------------------

    // What each food state is worth saying out loud. The spoken phrase carries no
    // countdown on purpose: a phrase that changes every second is a phrase that
    // gets SPOKEN every second.
    public static string SpeechFor(Grade grade) => grade switch
    {
        Grade.Missing => "No food",
        Grade.Expiring => "Food is running out",
        _ => "",
    };

    public const string PotionSpeech = "Potion is available";

    // Says each phrase once, when it becomes true, instead of on every frame it
    // stays true. The food line sits on screen for as long as the problem lasts,
    // so without this it would be spoken a hundred times a second.
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

    // When the FOOD warning is worth drawing: inside a duty, out of combat. Mid
    // fight there is nothing you can do about your food, and in the open world
    // it's nagging. (The potion note below runs on its own rules and does show
    // in combat.)
    //
    // A ready check overrides both of those gates. "Stood in a duty, out of
    // combat" also describes everyone afk, watching a video or arguing about
    // uptime; a ready check is somebody asking, right now, whether you're ready,
    // and it's the only unambiguous "we are about to pull" signal the game has.
    public static bool ShouldShow(bool enabled, bool inDuty, bool inCombat, bool readyCheck)
        => enabled && (readyCheck || (inDuty && !inCombat));

    // True while the ready check window is up, whether you called it or somebody
    // else did. The agent stays active for the results as well, so the answer
    // lingers a few seconds past the last person responding - which is exactly
    // when you'd want to still be looking at it.
    public static unsafe bool ReadyCheckActive()
    {
        try
        {
            var agent = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentReadyCheck.Instance();
            return agent != null && agent->IsAgentActive();
        }
        catch (Exception ex) { Swallowed.Report("prep ready check", ex); return false; }
    }

    // ---- the potion timer --------------------------------------------------

    // The potion note is NOT a pre-pull check. Telling you a pot is available
    // while you're stood there about to pull is telling you something you already
    // know - the useful moment is mid-fight, when the one you opened with comes
    // back up and it's time for the second.
    //
    // So it says nothing until it has actually seen you use a pot: Medicated
    // appearing starts the clock, and the note fires once when the recast is up
    // AND you are in the pull that pot was spent on. Both halves of that are the
    // point. A bare five-minute clock that outlives the pull it belongs to lands
    // wherever the party happens to be five minutes later - stood at the wall
    // after a wipe, walking to the boss, partway through the next attempt - which
    // is indistinguishable from firing at random.
    //
    // Pure: the caller supplies the clock, so every path is testable.
    public sealed class PotionTimer
    {
        // Only used when the item's own recast can't be read. Every tincture and
        // gemdraught in the game carries Cooldowns = 300, checked across all
        // grades and expansions, so this is the answer rather than a guess.
        public const float DefaultCooldownSeconds = 300f;
        public const float ShowSeconds = 5f;

        // A status list that reads empty for a frame or two - zoning, a raise, a
        // busy frame, a read that threw - is not the buff falling off. Bridging
        // that gap matters because the far side of it looks exactly like a fresh
        // pot: without this a single blink re-arms the clock and moves the note a
        // whole recast later, to a moment with no relationship to anything.
        public const float BlinkGraceSeconds = 2f;

        // How long combat has to stay off before the pull counts as over. Long
        // enough to ride out the flicker at a phase transition, short enough that
        // the clock is clear well before anyone pulls again.
        public const float PullOverSeconds = 3f;

        private bool _wasUp;
        private double _lastUpAt = double.NegativeInfinity;
        private double _readyAt;
        private bool _pending;                             // a use is being timed
        private double _firedAt = double.NegativeInfinity;
        private bool _sawCombat;                           // a pull has been under way
        private double _leftCombatAt = double.NegativeInfinity;

        // Returns whether the note should be on screen this frame. Must be called
        // EVERY frame, in combat included: Medicated is only up for 30 seconds,
        // and missing that edge means missing the use entirely.
        //
        // The recast is passed in (rather than assumed) so the real number from
        // the pot you actually drank is used when we can resolve it.
        public bool Update(bool medicatedUp, float recastSeconds, double now, bool inCombat)
        {
            // Treat a blink as still up, so only a real expiry ends the buff.
            if (medicatedUp) _lastUpAt = now;
            var up = medicatedUp || now - _lastUpAt < BlinkGraceSeconds;

            // Rising edge: a pot was just used. Time it.
            if (up && !_wasUp)
            {
                _readyAt = now + (recastSeconds > 0f ? recastSeconds : DefaultCooldownSeconds);
                _pending = true;
            }
            _wasUp = up;

            // The pull ended. Whatever you spent on it is a dead pull's problem:
            // you will open the next one with another pot, and this clock would
            // otherwise run straight across the wipe and surface partway through
            // whatever happens next.
            //
            // Deliberately NOT "out of combat", which would throw away a pot taken
            // during the countdown. Only combat that was running and has now
            // stopped ends a pull.
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
                    // The buff edge above is left alone on purpose: a pot used in
                    // the last seconds of the pull can still be up, and clearing
                    // _wasUp here would read it as a brand new use.
                    _pending = false;
                    _firedAt = double.NegativeInfinity;
                    _sawCombat = false;
                    _leftCombatAt = double.NegativeInfinity;
                }
            }

            // Back off recast: say so, once, and only in a pull. Out of combat
            // there is nothing to do with the news, and the option says as much.
            // The pending use is kept rather than dropped, so a recast that lands
            // inside a phase-transition cutscene is still announced on the far
            // side of it.
            if (_pending && now >= _readyAt && inCombat)
            {
                _pending = false;
                _firedAt = now;
            }

            return now - _firedAt < ShowSeconds;
        }

        // Seconds until the pot is back, or 0 when nothing is being timed. Only
        // meaningful once a use has been seen.
        public float Remaining(double now)
            => _pending ? (float)Math.Max(0.0, _readyAt - now) : 0f;

        // Forgotten on leaving the duty, and only then: the clock has to survive
        // combat starting and ending, or it would never reach the full recast.
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

    // ---- game reads --------------------------------------------------------

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
            // A missing buff and an unreadable status list look identical on
            // screen, so leave a trail rather than silently reporting "no food".
            Swallowed.Report("prep buff read", ex);
        }
        return default;
    }

    // The icon for a food row: the dish you actually ate when we can resolve it,
    // otherwise the generic Well Fed icon (which is all there is to show when
    // you have no food up at all).
    public static uint FoodIcon(Buff food)
    {
        if (food.Present && food.Param != 0)
        {
            // Well Fed's Param is the item id, +10000 when the meal was HQ.
            var itemId = (uint)(food.Param > 10000 ? food.Param - 10000 : food.Param);
            var icon = ItemIcon(itemId);
            if (icon != 0) return icon;
        }
        return StatusIcon(WellFedStatus);
    }

    private static readonly ConcurrentDictionary<uint, uint> _statusIcons = new();
    private static readonly ConcurrentDictionary<uint, uint> _itemIcons = new();

    // A status's own icon. Cached, since a missing buff has no status row on the
    // player to read one from and has to come from the sheet.
    public static uint StatusIcon(uint statusId)
        => Cached(_statusIcons, statusId, id =>
            GameSheets.English<Status>()?.GetRowOrDefault(id) is { } row ? (uint)row.Icon : 0u,
            "prep status icon");

    private static uint ItemIcon(uint itemId)
        => Cached(_itemIcons, itemId, id =>
            GameSheets.English<Item>()?.GetRowOrDefault(id) is { } row ? (uint)row.Icon : 0u,
            "prep food icon");

    // The recast of the pot that's actually up, straight from its item row, or 0
    // when it can't be resolved (the timer then falls back to the standard 300s).
    public static float RecastFor(Buff medicated)
    {
        if (!medicated.Present || medicated.Param == 0) return 0f;
        // Medicated carries the tincture in Param the same way Well Fed carries
        // the dish: item id, +10000 when HQ.
        var itemId = (uint)(medicated.Param > 10000 ? medicated.Param - 10000 : medicated.Param);
        return Cached(_itemRecasts, itemId, id =>
            GameSheets.English<Item>()?.GetRowOrDefault(id) is { } row ? row.Cooldowns : 0u,
            "prep potion recast");
    }

    private static readonly ConcurrentDictionary<uint, uint> _itemRecasts = new();

    // The item behind a Well Fed / Medicated status: its Param is the item id,
    // +10000 when the meal or pot was HQ.
    public static uint ItemOf(Buff buff)
        => buff.Param == 0 ? 0u : (uint)(buff.Param > 10000 ? buff.Param - 10000 : buff.Param);

    public static bool IsHq(Buff buff) => buff.Param > 10000;

    // Crafting and gathering stats, by BaseParam row: GP, CP, Craftsmanship,
    // Control, Gathering, Perception. Read off the sheet and confirmed stable.
    private static readonly uint[] CraftParams = { 10, 11, 70, 71, 72, 73 };

    // True when the dish boosts at least one stat that matters in a fight. A
    // meal whose every stat is a crafting one is crafter food, and wearing it
    // into a raid does nothing at all.
    //
    // Unknown food counts as battle food: never accuse on a failed lookup.
    public static bool IsBattleFood(Buff food)
    {
        var itemId = ItemOf(food);
        if (itemId == 0) return true;
        // BOTH answers are encoded non-zero (1 crafter, 2 battle) because Cached
        // treats 0 as "the lookup failed, ask again later". Returning a plain
        // false/0 for crafter food would mean re-walking three sheets every
        // single frame for exactly the case the warning is on screen for.
        return Cached(_battleFood, itemId, id =>
        {
            var item = GameSheets.English<Item>()?.GetRowOrDefault(id);
            if (item is not { } row) return Battle;
            // Food hangs its stats off ItemAction: Data[1] names the ItemFood row.
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
            // Every stat was a crafting one. No stats at all is not an accusation.
            return sawAny ? Crafter : Battle;
        }, "prep food stats") != Crafter;
    }

    private const uint Crafter = 1;
    private const uint Battle = 2;

    private static readonly ConcurrentDictionary<uint, uint> _battleFood = new();

    // How many of an item are in your bags, or 0 when it can't be read. Knowing
    // you're on your last pot changes whether you use it.
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
        // Only memoize a real answer, so a lookup that failed before the sheets
        // were ready can still resolve later.
        if (icon != 0) cache[key] = icon;
        return icon;
    }
}
