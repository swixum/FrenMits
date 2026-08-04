using System;
using System.Collections.Generic;
using System.Linq;

namespace FrenMits.Planning;

// The planner: fills every column from a sheet's graded rows.
public partial class SheetViewWindow
{
    // Each job's core party-wide mitigation for auto-planning.
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
            // These three are extras, and extras stay extras.
            ["MCH"] = new[] { ("Tactician", 90f) },
            ["DNC"] = new[] { ("Shield Samba", 90f) },
            ["BLM"] = new[] { ("Addle", 90f) }, ["SMN"] = new[] { ("Addle", 90f) },
            ["RDM"] = new[] { ("Addle", 90f) },
            ["PCT"] = new[] { ("Addle", 90f) },
        };

    // A column's toolset: a job kit, or generics for a role.
    private static (string Term, float Recast)[] PoolFor(string slot)
    {
        var t = slot.Trim().ToUpperInvariant();
        if (JobPartyKit.TryGetValue(t, out var kit))
            return kit.Select(k => (k.Name, CooldownTracker.PlanInfo(k.Name)?.Recast is { } r and > 5f ? r : k.Recast)).ToArray();
        return t switch
        {
            "MT" or "OT" or "T" or "TANK" => new[] { ("Reprisal", 60f), ("Party Mit", 90f) },
            "D1" or "D2" or "M1" or "M2" or "MELEE" or "D" or "DPS" => new[] { ("Feint", 90f) },
            "D3" or "R1" => new[] { ("Party Mit", 90f) },
            // Casters get Addle and nothing else.
            "D4" or "R2" => new[] { ("Addle", 90f) },
            // Healer party mits differ, so space to the slowest of them.
            var h when h.StartsWith("H") => new[] { ("Party Mit", 120f) },
            _ => Array.Empty<(string, float)>(),
        };
    }

    // Enemy debuffs don't stack, so one of each per hit.
    private static readonly HashSet<string> DebuffMits = new(StringComparer.OrdinalIgnoreCase)
        { "Reprisal", "Feint", "Addle", "Dismantle" };

    private static readonly HashSet<string> TankJobAbbrs = new(StringComparer.OrdinalIgnoreCase)
        { "WAR", "PLD", "DRK", "GNB" };

    // Cooldowns worth more the more hits land while up.
    private static readonly HashSet<string> OnDamageMits = new(StringComparer.OrdinalIgnoreCase)
        { "Liturgy of the Bell", "Panhaima", "Macrocosmos" };

    private static bool IsTankColumn(string slot)
    {
        var t = slot.Trim().ToUpperInvariant();
        return t is "MT" or "OT" or "T" or "TANK" || TankJobAbbrs.Contains(t);
    }

    // Buster generics spelled out for a column named after a job.
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
        // Float-early state: the last solo line, and its room to move.
        public MitLine? LastLine;
        public float FloatSlack;
        // Times the user presses this tool, which the planner avoids.
        public List<float> UserTimes = new();
    }


    private int AutoPlanMits(FightProfile fight)
    {
        // The fight's timer is not a mechanic.
        var rows = fight.CustomRows
            .Where(r => !Enrages.IsEnrageRow(fight.TerritoryId, r))
            .OrderBy(r => r.Time).ToList();
        if (rows.Count == 0) return 0;
        // Deadly party hits only, since a buster is the tanks' problem.
        var deadlyTimes = rows.Where(r => !r.Buster && r.Hurt >= 3).Select(r => r.Time).ToList();
        var sync = CooldownTracker.DutySyncLevel(fight.TerritoryId);

        // How many non-buster hits land inside a window opened at t0.
        int HitsWithin(float t0, float dur)
            => rows.Count(r2 => !r2.Buster && r2.Time >= t0 - 0.01f && r2.Time <= t0 + dur + 0.01f);
        // Each on-damage cooldown is judged over its real buff window.
        static float OnDmgDur(string term)
            => CooldownTracker.PlanInfo(term)?.Duration is { } d and > 5f ? d : 18f;
        int TickScore(PlanTool t, CustomRow r)
            => OnDamageMits.Contains(t.Term) ? HitsWithin(r.Time, OnDmgDur(t.Term)) : 0;
        // Never spend one where a denser string starts within its recast.
        bool HoldForCluster(PlanTool t, CustomRow r)
        {
            if (!OnDamageMits.Contains(t.Term)) return false;
            var floor = Math.Max(2, TickScore(t, r) + 1);
            return rows.Any(r2 => !r2.Buster && r2.Time > r.Time && r2.Time <= r.Time + t.Recast
                                  && HitsWithin(r2.Time, OnDmgDur(t.Term)) >= floor);
        }
        // A dense string is one big hit, so its opener grades harder.
        int EffHurt(CustomRow r)
            => !r.Buster && r.Hurt is 1 or 2 && HitsWithin(r.Time, 18f) >= 3 ? r.Hurt + 1 : r.Hurt;

        // How long a line's mitigation lasts: its shortest buff.
        static float LineCover(MitLine l) => l.Action
            .Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => CooldownTracker.PlanInfo(part)?.Duration is { } d and > 0f ? d
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
                // Skip anything the duty's sync level locks out.
                if (sync > 0 && CooldownTracker.PlanInfo(term)?.Level is { } lv and > 0 && lv > sync) continue;
                tools.Add(new PlanTool { Slot = slot, Term = term, Recast = recast, Order = order++ });
            }
        }
        if (tools.Count == 0) return 0;

        // Cooldowns already spent block an overlapping press.
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

        // Spending this tool now would steal it from an upcoming harder hit.
        // For debuff mits (Feint/Addle/Reprisal) specifically: spending on a light
        // hit wastes it if a harder hit is coming within the recast window.
        bool StealsFromBetterHit(PlanTool t, float now, int currentEff)
        {
            if (t.Recast < 55f) return false;
            // Always hold for deadly hits.
            if (deadlyTimes.Any(td => td > now && td < now + t.Recast)) return true;
            // Also hold debuff mits for harder hits than the current row.
            if (currentEff < 2 && DebuffMits.Contains(t.Term))
                return rows.Any(r2 => !r2.Buster && r2.Time > now && r2.Time < now + t.Recast
                                     && EffHurt(r2) > currentEff && EffHurt(r2) >= 2);
            return false;
        }
        bool StealsFromDeadly(PlanTool t, float now) => StealsFromBetterHit(t, now, 3);

        // Bridging: a solo press floats earlier so the recast returns.
        float BuffDur(PlanTool t)
            => CooldownTracker.PlanInfo(t.Term)?.Duration is { } d and > 0f ? d : 15f;
        bool CanReach(PlanTool t, float time)
        {
            if (UserBlocked(t, time)) return false;
            if (t.ReadyAt <= time + 0.01f) return true;
            // Only a solo line this run wrote can float.
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
            // Only a solo line can float later, or others get retimed.
            if (setSize == 1)
            {
                t.LastLine = line;
                // How far this press can later move earlier.
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

        // Tank personal timers, shared with the buster rows.
        var tanks = fight.CustomSlots.Where(IsTankColumn).ToList();
        // Pre-pull rows must see everything ready.
        var invulnAt = tanks.ToDictionary(t2 => t2, _ => -9999f, StringComparer.OrdinalIgnoreCase);
        var rampartAt = tanks.ToDictionary(t2 => t2, _ => -9999f, StringComparer.OrdinalIgnoreCase);
        var shortAt = tanks.ToDictionary(t2 => t2, _ => -9999f, StringComparer.OrdinalIgnoreCase);
        const float ShortRecast = 25f;
        // Tank cooldowns the user wrote in, same window rule.
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
                // Ride only an equal-or-harder previous buster.
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
            // Hits inside the previous window ride it, unless graded harder.
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
            // Depth per severity, matching the reference sheets' stacking.
            var target = eff switch
            {
                3 => lists.Count,
                2 => Math.Max(3, lists.Count / 2),
                1 => 1,
                _ => ungradedTarget,
            };
            // A row at target still runs later passes, since cooldowns roll.
            var need = target - have;

            var ready = tools
                .Where(t => CanReach(t, row.Time))
                .Where(t => !lists[t.Slot].Any(x => MathF.Abs(x.Time - row.Time) < 1f))
                .ToList();
            // Save the big buttons for the big hits.
            if (eff is 1 or 0)
                ready.RemoveAll(t => StealsFromDeadly(t, row.Time));
            // The cluster hold never keeps anything from a deadly hit.
            if (row.Hurt < 3)
                ready.RemoveAll(t => HoldForCluster(t, row));

            // Enemy debuffs don't stack: one Reprisal, one Feint, one Addle.
            var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in lists.Values)
                foreach (var x in l)
                    if (x.Time <= row.Time + 1f && row.Time - x.Time < 10f)
                        foreach (var d in DebuffMits)
                            if (x.Action.Contains(d, StringComparison.OrdinalIgnoreCase)) claimed.Add(d);

            // Per column, its candidates in preference order.
            var byCol = ready.GroupBy(t => t.Slot).Select(g => (eff switch
                {
                    // On strings the on-damage cooldowns come first.
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
                    // One player can layer several mits, by how hard the hit is.
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

            // Saturation: use it or lose it, so healer kits roll.
            foreach (var g in tools.GroupBy(t => t.Slot))
            {
                var col = lists[g.Key];
                if (col.Any(x => MathF.Abs(x.Time - row.Time) < 1f)) continue; // cell taken
                if (col.Any(x => row.Time - x.Time > 0.5f && row.Time - x.Time < 12f)) continue; // just pressed: ride it
                var satOrder = g
                    .Where(t => CanReach(t, row.Time))
                    .Where(t => !(DebuffMits.Contains(t.Term) && claimed.Contains(t.Term)))
                    .Where(t => !StealsFromBetterHit(t, row.Time, eff))
                    .Where(t => row.Hurt >= 3 || !HoldForCluster(t, row))
                    .OrderByDescending(t => TickScore(t, row) >= 2 ? TickScore(t, row) : 0)
                    .ThenBy(t => StealsFromDeadly(t, row.Time) ? 1 : 0)
                    .ThenBy(t => t.Recast).ThenBy(t => t.Order);
                // On lighter hits a quick second tool may still join.
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

            // Tank personals on heavy raid hits, sharing the lane.
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
                        // No longer solo, so floating it would retime the new parts.
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

            // Coverage bookkeeping after every pass.
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
