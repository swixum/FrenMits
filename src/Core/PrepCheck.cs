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
    public static bool ShouldShow(bool enabled, bool inDuty, bool inCombat)
        => enabled && inDuty && !inCombat;

    // ---- the potion timer --------------------------------------------------

    // The potion note is NOT a pre-pull check. Telling you a pot is available
    // while you're stood there about to pull is telling you something you already
    // know - the useful moment is mid-fight, when the one you opened with comes
    // back up and it's time for the second.
    //
    // So it says nothing until it has actually seen you use a pot: Medicated
    // appearing starts the clock, and the note fires once when the recast is up.
    //
    // Pure: the caller supplies the clock, so every path is testable.
    public sealed class PotionTimer
    {
        // Only used when the item's own recast can't be read. Every tincture and
        // gemdraught in the game carries Cooldowns = 300, checked across all
        // grades and expansions, so this is the answer rather than a guess.
        public const float DefaultCooldownSeconds = 300f;
        public const float ShowSeconds = 5f;

        private bool _wasUp;
        private double _readyAt;
        private bool _pending;                             // a use is being timed
        private double _firedAt = double.NegativeInfinity;

        // Returns whether the note should be on screen this frame. Must be called
        // EVERY frame, in combat included: Medicated is only up for 30 seconds,
        // and missing that edge means missing the use entirely.
        //
        // The recast is passed in (rather than assumed) so the real number from
        // the pot you actually drank is used when we can resolve it.
        public bool Update(bool medicatedUp, float recastSeconds, double now)
        {
            // Rising edge: a pot was just used. Time it.
            if (medicatedUp && !_wasUp)
            {
                _readyAt = now + (recastSeconds > 0f ? recastSeconds : DefaultCooldownSeconds);
                _pending = true;
            }
            _wasUp = medicatedUp;

            // Back off recast: say so, once.
            if (_pending && now >= _readyAt)
            {
                _pending = false;
                _firedAt = now;
            }

            return now - _firedAt < ShowSeconds;
        }

        // Forgotten on leaving the duty, and only then: the clock has to survive
        // combat starting and ending, or it would never reach the full recast.
        public void Reset()
        {
            _wasUp = false;
            _pending = false;
            _readyAt = 0;
            _firedAt = double.NegativeInfinity;
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
