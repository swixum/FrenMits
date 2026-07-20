using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Windows;

// The mit auto-planner: from a custom sheet's graded rows it fills every column
// with cooldowns the way the reference sheets play it - big buttons saved for the
// big hits, short kits rolled, tank lanes and personals layered. Split out of the
// SheetViewWindow partial to keep the pure planning logic away from the grid UI.
public partial class SheetViewWindow
{
    // Each job's CORE party-wide mitigation for auto-planning, mirroring what
    // the reference sheets put in their main columns: personal mits live in the
    // tank tabs, and extras-card abilities (Dismantle, Magick Barrier, Nature's
    // Minne, ...) stay optional extras. Recasts here are fallbacks; the game's
    // own numbers (Cooldowns.PlanInfo) win when available.
    private static readonly Dictionary<string, (string Name, float Recast)[]> JobPartyKit =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["WAR"] = new[] { ("Reprisal", 60f), ("Shake It Off", 90f) },
            ["PLD"] = new[] { ("Reprisal", 60f), ("Divine Veil", 90f) },
            ["DRK"] = new[] { ("Reprisal", 60f), ("Dark Missionary", 90f) },
            ["GNB"] = new[] { ("Reprisal", 60f), ("Heart of Light", 90f) },
            ["WHM"] = new[] { ("Temperance", 120f), ("Liturgy of the Bell", 180f), ("Asylum", 90f), ("Plenary Indulgence", 60f) },
            ["SCH"] = new[] { ("Expedient", 120f), ("Seraphism", 180f), ("Seraph", 120f), ("Fey Illumination", 120f), ("Whispering Dawn", 60f), ("Sacred Soil", 30f) },
            ["AST"] = new[] { ("Neutral Sect", 120f), ("Macrocosmos", 180f), ("Earthly Star", 60f), ("Collective Unconscious", 60f), ("Celestial Opposition", 60f) },
            ["SGE"] = new[] { ("Holos", 120f), ("Panhaima", 120f), ("Philosophia", 180f), ("Physis II", 60f), ("Kerachole", 30f) },
            ["MNK"] = new[] { ("Feint", 90f) }, ["DRG"] = new[] { ("Feint", 90f) },
            ["NIN"] = new[] { ("Feint", 90f) }, ["SAM"] = new[] { ("Feint", 90f) },
            ["RPR"] = new[] { ("Feint", 90f) }, ["VPR"] = new[] { ("Feint", 90f) },
            ["BRD"] = new[] { ("Troubadour", 90f) },
            // Dismantle / Magick Barrier / Tempera Grassa are deliberately NOT
            // here: they are JobExtras (per-job add-on cards with their own
            // log-derived schedules), and extras stay extras.
            ["MCH"] = new[] { ("Tactician", 90f) },
            ["DNC"] = new[] { ("Shield Samba", 90f) },
            ["BLM"] = new[] { ("Addle", 90f) }, ["SMN"] = new[] { ("Addle", 90f) },
            ["RDM"] = new[] { ("Addle", 90f) },
            ["PCT"] = new[] { ("Addle", 90f) },
        };

    // A column's toolset. A column NAMED for a job (WHM, SGE, MCH...) plans
    // with that job's real party kit; a role column (MT, H1, D3...) gets the
    // generic terms that resolve per job at call time.
    private static (string Term, float Recast)[] PoolFor(string slot)
    {
        var t = slot.Trim().ToUpperInvariant();
        if (JobPartyKit.TryGetValue(t, out var kit))
            return kit.Select(k => (k.Name, Cooldowns.PlanInfo(k.Name)?.Recast is { } r and > 5f ? r : k.Recast)).ToArray();
        return t switch
        {
            "MT" or "OT" or "T" or "T1" or "T2" or "TANK" => new[] { ("Reprisal", 60f), ("Party Mit", 90f) },
            "D1" or "D2" or "M1" or "M2" or "MELEE" or "D" or "DPS" => new[] { ("Feint", 90f) },
            "D3" or "R1" => new[] { ("Party Mit", 90f) },
            "D4" or "R2" => new[] { ("Addle", 90f), ("Party Mit", 120f) },
            // Healer party mits differ per job; the generic term resolves at
            // call time, spaced to the slowest of them so the button is never dead.
            var h when h.StartsWith("H") => new[] { ("Party Mit", 120f) },
            _ => Array.Empty<(string, float)>(),
        };
    }

    // Mits that land as a debuff ON THE ENEMY: a second source on the same hit
    // is wasted, so the planner allows one of each per hit, party-wide.
    private static readonly HashSet<string> DebuffMits = new(StringComparer.OrdinalIgnoreCase)
        { "Reprisal", "Feint", "Addle", "Dismantle" };

    private static readonly HashSet<string> TankJobAbbrs = new(StringComparer.OrdinalIgnoreCase)
        { "WAR", "PLD", "DRK", "GNB" };

    // Cooldowns whose TOOLTIP says their value scales with the NUMBER of
    // damage instances while active: Liturgy of the Bell heals on each hit
    // taken, Panhaima re-shields as its stacks break, Macrocosmos compiles
    // damage taken and heals it back. They belong on multi-hit strings; on a
    // lone hit most of their effect never triggers.
    private static readonly HashSet<string> OnDamageMits = new(StringComparer.OrdinalIgnoreCase)
        { "Liturgy of the Bell", "Panhaima", "Macrocosmos" };

    private static bool IsTankColumn(string slot)
    {
        var t = slot.Trim().ToUpperInvariant();
        return t is "MT" or "OT" or "T" or "T1" or "T2" or "TANK" || TankJobAbbrs.Contains(t);
    }

    // Buster-lane generics, spelled out for a column NAMED after a tank job
    // (an MT/OT column keeps the generic, which resolves per player at call time).
    private static readonly Dictionary<string, Dictionary<string, string>> TankTermByJob =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Invulnerability"] = new(StringComparer.OrdinalIgnoreCase)
            { ["WAR"] = "Holmgang", ["PLD"] = "Hallowed Ground", ["DRK"] = "Living Dead", ["GNB"] = "Superbolide" },
            ["Short Mit"] = new(StringComparer.OrdinalIgnoreCase)
            { ["WAR"] = "Bloodwhetting", ["PLD"] = "Holy Sheltron", ["DRK"] = "The Blackest Night", ["GNB"] = "Heart of Corundum" },
            ["Buddy Mit"] = new(StringComparer.OrdinalIgnoreCase)
            { ["WAR"] = "Nascent Flash", ["PLD"] = "Intervention", ["DRK"] = "The Blackest Night", ["GNB"] = "Heart of Corundum" },
        };

    private static string TankTerm(string slot, string term)
        => TankTermByJob.TryGetValue(term, out var map) && map.TryGetValue(slot.Trim(), out var real)
            ? real : term;

    private sealed class PlanTool
    {
        public string Slot = "";
        public string Term = "";
        public float Recast;
        public float ReadyAt = -9999f; // ready even for pre-pull (negative-time) rows
        public float LastUse = -9999f;
        public int Order;
        // Float-early state: the last SOLO line this run planned for this tool,
        // and how many seconds it could still move earlier (bounded by its buff
        // still covering its own row, and by when the tool was ready before it).
        public MitLine? LastLine;
        public float FloatSlack;
        // Times the USER's own cells press this tool; the planner may not land
        // inside any of their recast windows (either direction).
        public List<float> UserTimes = new();
    }

    // The planner, patterned on how the reference sheets actually play:
    // - Deadly hits stack the whole party (every column contributes; healers
    //   and named-job columns may pair two mits, "Plenary Indulgence + Asylum"
    //   style). Hurts takes about half the party; light takes one press;
    //   ungraded rows the planner sizes itself from how graded the sheet is.
    // - Big cooldowns are saved for big hits: a long mit is not spent on a
    //   light row when a deadly row lands inside its recast.
    // - Every press respects its recast (the game's own numbers when
    //   available), the load rotates least-recently-used first, hits within
    //   15s share one press, and cells you wrote are never touched.
    private int AutoPlanMits(FightProfile fight)
    {
        var rows = fight.CustomRows.OrderBy(r => r.Time).ToList();
        if (rows.Count == 0) return 0;
        // Deadly PARTY hits only: a deadly buster is the tanks' problem, so it
        // must never hold the party's big cooldowns hostage.
        var deadlyTimes = rows.Where(r => !r.Buster && r.Hurt >= 3).Select(r => r.Time).ToList();
        var sync = Cooldowns.DutySyncLevel(fight.TerritoryId);

        // Multi-hit strings (Trophy Weapons style: back-to-back-to-back hits):
        // how many non-buster hits land inside a window opened at t0. This is
        // what an on-damage cooldown pressed there actually ticks through.
        int HitsWithin(float t0, float dur)
            => rows.Count(r2 => !r2.Buster && r2.Time >= t0 - 0.01f && r2.Time <= t0 + dur + 0.01f);
        // Each on-damage cooldown is judged over its REAL buff window (game
        // data; ~18s when unknown), so Bell is scored over Bell's duration.
        static float OnDmgDur(string term)
            => Cooldowns.PlanInfo(term)?.Duration is { } d and > 5f ? d : 18f;
        int TickScore(PlanTool t, CustomRow r)
            => OnDamageMits.Contains(t.Term) ? HitsWithin(r.Time, OnDmgDur(t.Term)) : 0;
        // Tooltip-aware hold: never spend an on-damage cooldown where it ticks
        // little when a strictly DENSER string it could ride starts within its
        // recast - pressing it now is exactly what would rob that string. A
        // lone hit yields to any 2+ string; a 2-hit string yields to a 3+ one.
        bool HoldForCluster(PlanTool t, CustomRow r)
        {
            if (!OnDamageMits.Contains(t.Term)) return false;
            var floor = Math.Max(2, TickScore(t, r) + 1);
            return rows.Any(r2 => !r2.Buster && r2.Time > r.Time && r2.Time <= r.Time + t.Recast
                                  && HitsWithin(r2.Time, OnDmgDur(t.Term)) >= floor);
        }
        // A dense string is one big hit split into ticks: a row that OPENS a
        // 3+ hit string is sized one grade harder, so the plan stacks for the
        // string's TOTAL damage instead of its first tick.
        int EffHurt(CustomRow r)
            => !r.Buster && r.Hurt is 1 or 2 && HitsWithin(r.Time, 18f) >= 3 ? r.Hurt + 1 : r.Hurt;

        // How long a planned line's mitigation actually lasts: the shortest
        // buff in it (game data; generic party terms fall back to a Reprisal-ish
        // 15s, the short tank generics to their real ~8-10s).
        static float LineCover(MitLine l) => l.Action
            .Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => Cooldowns.PlanInfo(part)?.Duration is { } d and > 0f ? d
                : part.Equals("Short Mit", StringComparison.OrdinalIgnoreCase) ? 8f
                : part.Equals("Buddy Mit", StringComparison.OrdinalIgnoreCase) ? 8f
                : part.Equals("Invulnerability", StringComparison.OrdinalIgnoreCase) ? 10f
                : 15f)
            .DefaultIfEmpty(15f).Min();

        var tools = new List<PlanTool>();
        var lists = new Dictionary<string, List<MitLine>>(StringComparer.OrdinalIgnoreCase);
        var order = 0;
        foreach (var slot in fight.CustomSlots)
        {
            if (!fight.SavedSlots.TryGetValue(slot, out var list))
            {
                list = string.Equals(slot, fight.Slot, StringComparison.OrdinalIgnoreCase)
                    ? fight.Lines : new List<MitLine>();
                fight.SavedSlots[slot] = list;
            }
            lists[slot] = list;
            foreach (var (term, recast) in PoolFor(slot))
            {
                // Old synced duties: skip anything the sync level locks out
                // (generic terms carry no level and always pass).
                if (sync > 0 && Cooldowns.PlanInfo(term)?.Level is { } lv and > 0 && lv > sync) continue;
                tools.Add(new PlanTool { Slot = slot, Term = term, Recast = recast, Order = order++ });
            }
        }
        if (tools.Count == 0) return 0;

        // Cooldowns the USER already spent: a hand-written Bell at 4:00 blocks
        // the planner from any press whose recast would overlap it - in either
        // direction - while the stretch clear of it stays fair game, the same
        // way the red-check reasons. Word-boundary matched so an annotated
        // cell ("Kerachole ASAP") still counts as a Kerachole press.
        static bool ActionHas(string action, string term)
        {
            var i = action.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            while (i >= 0)
            {
                var after = i + term.Length;
                if ((i == 0 || !char.IsLetter(action[i - 1]))
                    && (after >= action.Length || !char.IsLetter(action[after]))) return true;
                i = action.IndexOf(term, i + 1, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
        foreach (var t in tools)
        {
            foreach (var x in lists[t.Slot])
                if (!string.IsNullOrWhiteSpace(x.Action) && ActionHas(x.Action, t.Term))
                    t.UserTimes.Add(x.Time);
            t.UserTimes.Sort();
        }
        static bool UserBlocked(PlanTool t, float time)
        {
            foreach (var u in t.UserTimes)
                if (time > u - t.Recast + 0.01f && time < u + t.Recast - 0.01f) return true;
            return false;
        }

        // Spending this tool now would steal it from an upcoming deadly hit.
        // Top-log play never presses a raid cooldown just because it is up: the
        // 90s workhorses (Feint, Addle, party mits) AND the 60s ones (Reprisal,
        // Aurora, Collective Unconscious, the healer 60s CDs) all sit near ~55%
        // utilization, held for the real raidwides. 55s catches that whole band;
        // burning one on a LIGHT hit inside a deadly hit's window is the classic
        // wasted press the sheets never make. Short rollers (Kerachole-class,
        // <55s) stay below the gate and keep covering the light rows.
        bool StealsFromDeadly(PlanTool t, float now)
            => t.Recast >= 55f && deadlyTimes.Any(td => td > now && td < now + t.Recast);

        // Press-early bridging, the way the sheets play it: when a tool misses
        // a hit by a few seconds, its PREVIOUS press (if it was a solo line
        // this run wrote and nothing rides it yet) floats earlier - the buff
        // still covers its own row from the tail end - so the recast comes
        // back in time. "Kerachole at :10 covers the :20 hit AND is back for
        // the :45 one."
        float BuffDur(PlanTool t)
            => Cooldowns.PlanInfo(t.Term)?.Duration is { } d and > 0f ? d : 15f;
        bool CanReach(PlanTool t, float time)
        {
            if (UserBlocked(t, time)) return false;
            if (t.ReadyAt <= time + 0.01f) return true;
            // Bridging float: only a solo line this run wrote, that nothing
            // rides yet (CoverUntil vs its OWN time - a pull-time ride stamps
            // 0), landing clear of user windows and other cells.
            var l = t.LastLine;
            if (l == null || l.CoverUntil > l.Time - 0.01f) return false;
            var shift = t.ReadyAt - time;
            if (shift > t.FloatSlack + 0.01f) return false;
            var dest = l.Time - shift;
            if (UserBlocked(t, dest)) return false;
            if (lists[t.Slot].Any(x => !ReferenceEquals(x, l) && MathF.Abs(x.Time - dest) < 1f)) return false;
            return true;
        }
        void ApplyFloat(PlanTool t, float time)
        {
            var shift = t.ReadyAt - time;
            if (shift <= 0.01f) return;
            var l = t.LastLine!;
            l.CoverUntil = l.Time;   // the buff still covers its own row
            l.Time -= shift;
            t.FloatSlack -= shift;
            t.ReadyAt -= shift;
        }
        void NotePress(PlanTool t, MitLine line, float time, int setSize)
        {
            // Only a solo line can float later: shifting a combined line would
            // silently retime every other mit written into it.
            if (setSize == 1)
            {
                t.LastLine = line;
                // How far this press can later move earlier: the buff must
                // still cover its own row (D-1) and the tool must have been
                // ready that early.
                t.FloatSlack = MathF.Min(BuffDur(t) - 1f, MathF.Max(0f, time - t.ReadyAt));
            }
            else t.LastLine = null;
            t.ReadyAt = time + t.Recast;
            t.LastUse = time;
        }

        var added = 0;
        var lastCovered = -9999f;
        var lastCoveredHurt = 0;
        var lastAdded = new List<MitLine>(); // this run's presses at lastCovered
        var ungradedTarget = rows.Any(r => r.Hurt > 0) ? 1 : Math.Max(2, lists.Count / 3);

        // Tank personal timers: ONE timeline per tank, shared between buster
        // rows and the personal presses on heavy raid hits, so the plan can
        // never ask for a Rampart that a buster already spent.
        var tanks = fight.CustomSlots.Where(IsTankColumn).ToList();
        // -9999 not 0: pre-pull (negative-time) rows must see everything ready.
        var invulnAt = tanks.ToDictionary(t2 => t2, _ => -9999f, StringComparer.OrdinalIgnoreCase);
        var rampartAt = tanks.ToDictionary(t2 => t2, _ => -9999f, StringComparer.OrdinalIgnoreCase);
        var shortAt = tanks.ToDictionary(t2 => t2, _ => -9999f, StringComparer.OrdinalIgnoreCase);
        const float ShortRecast = 25f;
        // Tank cooldowns the user already wrote into cells: same window rule
        // as the party seeding - the planner may not land inside their recast
        // in either direction, but the clear stretches stay usable.
        var invulnNames = new[] { "Invulnerability", "Holmgang", "Hallowed Ground", "Living Dead", "Superbolide" };
        var shortNames = new[] { "Short Mit", "Buddy Mit", "Bloodwhetting", "Nascent Flash", "Holy Sheltron", "Intervention", "The Blackest Night", "Heart of Corundum" };
        var invulnUser = tanks.ToDictionary(t2 => t2, _ => new List<float>(), StringComparer.OrdinalIgnoreCase);
        var rampartUser = tanks.ToDictionary(t2 => t2, _ => new List<float>(), StringComparer.OrdinalIgnoreCase);
        var shortUser = tanks.ToDictionary(t2 => t2, _ => new List<float>(), StringComparer.OrdinalIgnoreCase);
        foreach (var tk in tanks)
            foreach (var x in lists[tk])
            {
                if (string.IsNullOrWhiteSpace(x.Action)) continue;
                if (invulnNames.Any(n => ActionHas(x.Action, n))) invulnUser[tk].Add(x.Time);
                if (ActionHas(x.Action, "Rampart")) rampartUser[tk].Add(x.Time);
                if (shortNames.Any(n => ActionHas(x.Action, n))) shortUser[tk].Add(x.Time);
            }
        static bool WindowFree(List<float> users, float time, float recast)
        {
            foreach (var u in users)
                if (time > u - recast + 0.01f && time < u + recast - 0.01f) return false;
            return true;
        }
        var rot = 0;
        var lastTb = -9999f;
        var lastTbHurt = 0;
        var lastTbLines = new List<MitLine>();
        var busterTimes = rows.Where(r => r.Buster && r.Hurt >= 2).Select(r => r.Time).ToList();

        foreach (var row in rows)
        {
            if (row.Buster)
            {
                // ---- tank-buster lane: the sheets' tank-tab pattern --------
                if (tanks.Count == 0) continue;
                // Ride only an EQUAL-OR-HARDER previous buster: a deadly buster
                // 8s after a light one must still get its invuln, not coast on
                // a short mit sized for the light hit.
                if (row.Time - lastTb < 10f && row.Hurt <= lastTbHurt)
                {
                    foreach (var l in lastTbLines)
                        if (l.CoverUntil < row.Time && row.Time <= l.Time + LineCover(l) + 0.01f)
                            l.CoverUntil = row.Time;
                    continue;
                }
                // A cell the user already filled on any tank = handled.
                if (tanks.Any(t2 => lists[t2].Any(x => MathF.Abs(x.Time - row.Time) < 1f)))
                {
                    lastTb = row.Time;
                    lastTbHurt = row.Hurt;
                    lastTbLines = new List<MitLine>();
                    continue;
                }

                var activeTank = tanks[rot % tanks.Count];
                rot++;
                var srdy = shortAt[activeTank] <= row.Time && WindowFree(shortUser[activeTank], row.Time, ShortRecast);
                string? act = null;
                if (row.Hurt >= 3 && invulnAt[activeTank] <= row.Time && WindowFree(invulnUser[activeTank], row.Time, 420f))
                {
                    act = TankTerm(activeTank, "Invulnerability");
                    invulnAt[activeTank] = row.Time + 420f; // slowest invuln; never a dead call
                }
                else if (rampartAt[activeTank] <= row.Time && WindowFree(rampartUser[activeTank], row.Time, 90f) && row.Hurt >= 2)
                {
                    act = srdy ? "Rampart + " + TankTerm(activeTank, "Short Mit") : "Rampart";
                    rampartAt[activeTank] = row.Time + 90f;
                    if (srdy) shortAt[activeTank] = row.Time + ShortRecast;
                }
                else if (srdy)
                {
                    act = TankTerm(activeTank, "Short Mit");
                    shortAt[activeTank] = row.Time + ShortRecast;
                }
                else if (rampartAt[activeTank] <= row.Time && WindowFree(rampartUser[activeTank], row.Time, 90f))
                {
                    act = "Rampart";
                    rampartAt[activeTank] = row.Time + 90f;
                }

                lastTbLines = new List<MitLine>();
                if (act != null)
                {
                    var mine = new MitLine
                    {
                        Time = row.Time, Mechanic = row.Mechanic, Action = act,
                        Enabled = true, Custom = true,
                    };
                    lists[activeTank].Add(mine);
                    added++;
                    lastTbLines.Add(mine);
                }
                if (row.Hurt >= 2 && tanks.Count > 1)
                {
                    var co = tanks[rot % tanks.Count];
                    if (shortAt[co] <= row.Time && WindowFree(shortUser[co], row.Time, ShortRecast))
                    {
                        var buddy = new MitLine
                        {
                            Time = row.Time, Mechanic = row.Mechanic,
                            Action = TankTerm(co, "Buddy Mit"),
                            Enabled = true, Custom = true,
                        };
                        lists[co].Add(buddy);
                        added++;
                        lastTbLines.Add(buddy);
                        shortAt[co] = row.Time + ShortRecast;
                    }
                }
                lastTb = row.Time;
                lastTbHurt = row.Hurt;
                continue;
            }
            // Hits inside the previous press's window ride it, UNLESS this one
            // is graded harder than what that press was sized for, or none of
            // our presses' buffs actually last this long (real durations from
            // the game data). Riding presses get a press window (CoverUntil)
            // capped to what each buff can truly cover, so the grid never
            // promises coverage a 10s buff cannot deliver.
            // Ride on EFFECTIVE grades (same scale the sizing below uses), so a
            // row that OPENS a dense string breaks out of the previous press's
            // window and gets stacked for the whole string.
            var eff = EffHurt(row);
            if (row.Time - lastCovered < 15f && eff <= lastCoveredHurt
                && (lastAdded.Count == 0 || lastAdded.Any(l => row.Time <= l.Time + LineCover(l) + 0.01f)))
            {
                foreach (var l in lastAdded)
                    if (l.CoverUntil < row.Time && row.Time <= l.Time + LineCover(l) + 0.01f)
                        l.CoverUntil = row.Time;
                continue;
            }
            var have = lists.Values.Count(l => l.Any(x =>
                MathF.Abs(x.Time - row.Time) < 1f && !string.IsNullOrWhiteSpace(x.Action)));
            // Depth per severity, matching the reference sheets' stacking. The
            // planner decides ungraded rows itself: on a graded sheet they're
            // the leftovers (light); on a fully ungraded sheet they get a solid
            // baseline and the use-it-or-lose-it pass rolls the rest. Dense
            // strings size off their effective grade (the whole string's load).
            var target = eff switch
            {
                3 => lists.Count,
                2 => Math.Max(3, lists.Count / 2),
                1 => 1,
                _ => ungradedTarget,
            };
            // A row already at target still runs the later passes: covered
            // priority-wise is not the same as cooldowns rolling.
            var need = target - have;

            var ready = tools
                .Where(t => CanReach(t, row.Time))
                .Where(t => !lists[t.Slot].Any(x => MathF.Abs(x.Time - row.Time) < 1f))
                .ToList();
            // Save the big buttons for the big hits (effective grade: a row
            // opening a dense string counts as the string, not its first tick).
            if (eff is 1 or 0)
                ready.RemoveAll(t => StealsFromDeadly(t, row.Time));
            // The cluster hold never keeps anything from a DEADLY hit: a lone
            // deadly is worth a Bell/Panhaima burst, and Macrocosmos compiles
            // one huge hit at full value (its tooltip counts damage, not hits).
            if (row.Hurt < 3)
                ready.RemoveAll(t => HoldForCluster(t, row));

            // Enemy debuffs don't stack from two sources: one Reprisal, one
            // Feint, one Addle per hit, party-wide; the sheets rotate WHO casts
            // them and the LRU ordering reproduces that. Seed with whatever the
            // user already wrote on this row.
            var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in lists.Values)
                foreach (var x in l)
                    if (x.Time <= row.Time + 1f && row.Time - x.Time < 10f)
                        foreach (var d in DebuffMits)
                            if (x.Action.Contains(d, StringComparison.OrdinalIgnoreCase)) claimed.Add(d);

            // Per column, its candidates in preference order: the biggest ready
            // mit on deadly rows (two of them joined, like the sheets' worst
            // hits), the smallest that does the job on light rows, and on
            // medium rows whatever does not rob an upcoming deadly hit.
            var byCol = ready.GroupBy(t => t.Slot).Select(g => (eff switch
                {
                    // On multi-hit strings the on-damage cooldowns come first,
                    // ranked by how many hits they'd tick through (tooltip
                    // behavior: more ticks = more value).
                    3 => g.OrderByDescending(t => TickScore(t, row) >= 2 ? TickScore(t, row) : 0)
                          .ThenByDescending(t => t.Recast).ThenBy(t => t.Order),
                    1 or 0 => g.OrderBy(t => t.Recast).ThenBy(t => t.Order),
                    _ => g.OrderByDescending(t => TickScore(t, row) >= 2 ? TickScore(t, row) : 0)
                          .ThenBy(t => StealsFromDeadly(t, row.Time) ? 1 : 0)
                          .ThenByDescending(t => t.Recast).ThenBy(t => t.Order),
                }).ToList())
                .OrderBy(opts => eff == 2 && StealsFromDeadly(opts[0], row.Time) ? 1 : 0)
                .ThenBy(opts => opts[0].LastUse).ThenBy(opts => opts[0].Order)
                .ToList();

            var rowLines = new List<MitLine>();
            foreach (var opts in byCol)
            {
                if (rowLines.Count >= need) break;
                var set = new List<PlanTool>();
                foreach (var t in opts)
                {
                    // One player CAN layer several of their own mits on one
                    // mechanic: deadly hits stack up to three per cell (the
                    // sheets' triple cells), hurts pairs, light takes one.
                    if (set.Count >= (eff >= 3 ? 3 : eff >= 2 ? 2 : 1)) break;
                    if (DebuffMits.Contains(t.Term) && claimed.Contains(t.Term)) continue;
                    set.Add(t);
                }
                if (set.Count == 0) continue;
                var line = new MitLine
                {
                    Time = row.Time,
                    Mechanic = row.Mechanic,
                    Action = string.Join(" + ", set.Select(t => t.Term)),
                    Enabled = true,
                    Custom = true,
                };
                lists[set[0].Slot].Add(line);
                rowLines.Add(line);
                foreach (var t in set)
                {
                    if (DebuffMits.Contains(t.Term)) claimed.Add(t.Term);
                    ApplyFloat(t, row.Time);
                    NotePress(t, line, row.Time, set.Count);
                }
                added++;
            }

            // Saturation: the sheets' use-it-or-lose-it rule. Any cooldown
            // that is back, not owed to an upcoming deadly hit, and not a
            // debuff already on this hit goes on it NOW, so healer kits roll
            // continuously instead of sitting unused between big moments.
            // Hard rows (2+) may spend even the big buttons, exactly like the
            // targeted pass: harder-now beats maybe-later, non-stealing first.
            foreach (var g in tools.GroupBy(t => t.Slot))
            {
                var col = lists[g.Key];
                if (col.Any(x => MathF.Abs(x.Time - row.Time) < 1f)) continue; // cell taken
                if (col.Any(x => row.Time - x.Time > 0.5f && row.Time - x.Time < 12f)) continue; // just pressed: ride it
                var satOrder = g
                    .Where(t => CanReach(t, row.Time))
                    .Where(t => !(DebuffMits.Contains(t.Term) && claimed.Contains(t.Term)))
                    .Where(t => eff >= 2 || !StealsFromDeadly(t, row.Time))
                    .Where(t => row.Hurt >= 3 || !HoldForCluster(t, row))
                    .OrderByDescending(t => TickScore(t, row) >= 2 ? TickScore(t, row) : 0)
                    .ThenBy(t => StealsFromDeadly(t, row.Time) ? 1 : 0)
                    .ThenBy(t => t.Recast).ThenBy(t => t.Order);
                // Deadly hits stack up to three from one player, hurts pairs,
                // and on lighter hits a QUICK second tool (60s or less) may
                // still join, so the short-recast kit pieces roll instead of
                // queuing behind one cell per row.
                var satCap = eff >= 3 ? 3 : 2;
                var picks = new List<PlanTool>();
                foreach (var t in satOrder)
                {
                    if (picks.Count >= satCap) break;
                    if (picks.Count == 1 && eff < 2 && t.Recast > 60f) break;
                    picks.Add(t);
                }
                if (picks.Count == 0) continue;
                var sat = new MitLine
                {
                    Time = row.Time,
                    Mechanic = row.Mechanic,
                    Action = string.Join(" + ", picks.Select(t => t.Term)),
                    Enabled = true,
                    Custom = true,
                };
                col.Add(sat);
                rowLines.Add(sat); // its buffs cover ridden hits like any press
                added++;
                foreach (var t in picks)
                {
                    if (DebuffMits.Contains(t.Term)) claimed.Add(t.Term);
                    ApplyFloat(t, row.Time);
                    NotePress(t, sat, row.Time, picks.Count);
                }
            }

            // Tank personals on heavy raid hits, tank-tab style (Rampart and
            // the short mit on the big raid moments), sharing the buster
            // lane's timers and never robbing an upcoming buster.
            if (row.Hurt >= 2)
                foreach (var tk in tanks)
                {
                    var canRampart = rampartAt[tk] <= row.Time
                        && WindowFree(rampartUser[tk], row.Time, 90f)
                        && !busterTimes.Any(tb => tb > row.Time && tb < row.Time + 90f);
                    var canShort = shortAt[tk] <= row.Time
                        && WindowFree(shortUser[tk], row.Time, ShortRecast)
                        && !busterTimes.Any(tb => tb > row.Time && tb < row.Time + ShortRecast);
                    if (!canRampart && !canShort) continue;
                    var col = lists[tk];
                    var mineLine = col.FirstOrDefault(x => MathF.Abs(x.Time - row.Time) < 1f);
                    if (mineLine != null && !rowLines.Contains(mineLine)) continue; // user's cell
                    var parts = new List<string>();
                    if (canRampart) { parts.Add("Rampart"); rampartAt[tk] = row.Time + 90f; }
                    if (canShort) { parts.Add(TankTerm(tk, "Short Mit")); shortAt[tk] = row.Time + ShortRecast; }
                    if (mineLine != null)
                    {
                        mineLine.Action += " + " + string.Join(" + ", parts);
                        // No longer a solo press: floating it later would
                        // silently retime the parts just appended.
                        foreach (var t2 in tools)
                            if (ReferenceEquals(t2.LastLine, mineLine)) t2.LastLine = null;
                    }
                    else
                    {
                        var pl = new MitLine
                        {
                            Time = row.Time,
                            Mechanic = row.Mechanic,
                            Action = string.Join(" + ", parts),
                            Enabled = true,
                            Custom = true,
                        };
                        col.Add(pl);
                        rowLines.Add(pl);
                        added++;
                    }
                }

            // Coverage bookkeeping AFTER every pass, so a row covered only by
            // saturation or tank personals still starts a ride window and its
            // buffs get CoverUntil stamps on the hits they carry.
            if (rowLines.Count > 0 || have > 0)
            {
                lastCovered = row.Time;
                lastCoveredHurt = eff; // effective: a string-opener press covers the string
                lastAdded = rowLines;
            }
        }

        foreach (var l in lists.Values)
        {
            var sorted = l.OrderBy(x => x.Time).ToList();
            l.Clear();
            l.AddRange(sorted);
        }
        return added;
    }
}
