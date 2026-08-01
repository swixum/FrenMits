using System;
using System.Collections.Generic;

namespace FrenMits;

// Every party-wide damage buff the rDPS split recognizes, at current live
// values a balance patch only needs to edit here.
public static class RaidBuffs
{
    public enum Kind { Damage, CritRate, DirectHitRate }

    public readonly record struct Effect(Kind Kind, float Amount);

    public sealed class Buff
    {
        public string Name = "";
        public Effect[] Effects = Array.Empty<Effect>();
        // Sits on the enemy (Chain Stratagem, Dokumori) instead of on party members.
        public bool OnEnemy;
        // Cards swing 6%/3% by the receiver's role; Radiant Finale by codas banked.
        public Func<int, JobRole?, Effect[]>? Dynamic;

        public Effect[] For(int stacks, JobRole? role) => Dynamic?.Invoke(stacks, role) ?? Effects;
    }

    private static Buff Dmg(string name, float mult, bool onEnemy = false) => new()
    { Name = name, Effects = new[] { new Effect(Kind.Damage, mult) }, OnEnemy = onEnemy };

    private static Buff Crit(string name, float rate, bool onEnemy = false) => new()
    { Name = name, Effects = new[] { new Effect(Kind.CritRate, rate) }, OnEnemy = onEnemy };

    private static Buff Dh(string name, float rate) => new()
    { Name = name, Effects = new[] { new Effect(Kind.DirectHitRate, rate) } };

    private static readonly Buff[] Table =
    {
        // Flat party multipliers.
        Dmg("Embolden", 1.05f),          // RDM
        Dmg("Searing Light", 1.05f),     // SMN
        Dmg("Divination", 1.06f),        // AST
        Dmg("Brotherhood", 1.05f),       // MNK
        Dmg("Arcane Circle", 1.03f),     // RPR
        Dmg("Starry Muse", 1.05f),       // PCT

        // Crit / direct-hit rate buffs, paid on the rolls they cause.
        Crit("Battle Litany", 0.10f),    // DRG
        Dh("Battle Voice", 0.20f),       // BRD
        new()                            // DNC, self + dance partner
        {
            Name = "Devilment",
            Effects = new[] { new Effect(Kind.CritRate, 0.20f), new Effect(Kind.DirectHitRate, 0.20f) },
        },

        // Songs sit on the whole party for most of a fight.
        Dmg("Mage's Ballad", 1.01f),           // BRD
        Dh("Army's Paeon", 0.03f),             // BRD
        Crit("The Wanderer's Minuet", 0.02f),  // BRD

        // Enemy-side debuffs: everyone's damage into that target is louder.
        Crit("Chain Stratagem", 0.10f, onEnemy: true), // SCH
        Dmg("Dokumori", 1.05f, onEnemy: true),         // NIN
        Dmg("Mug", 1.05f, onEnemy: true),              // NIN, low level / synced
        // Kunai's Bane stays absent because ranked kills show Dokumori alone
        // already accounts for a ninja's measured contribution.

        // Dance finishes: one status name for every step count; the engine
        // reads the finishing move to price partial dances, full is the default.
        Dmg("Technical Finish", 1.05f),
        Dmg("Standard Finish", 1.05f),

        // Radiant Finale scales with the codas banked; the engine counts the
        // bard's songs to price each press, full codas being the default.
        Dmg("Radiant Finale", 1.06f),

        // Cards: 6% on the matching role, 3% otherwise (unknown job reads as matching).
        new()
        {
            Name = "The Balance",
            Dynamic = (_, role) => new[]
            {
                new Effect(Kind.Damage, role is null or JobRole.Melee or JobRole.Tank ? 1.06f : 1.03f),
            },
        },
        new()
        {
            Name = "The Spear",
            Dynamic = (_, role) => new[]
            {
                new Effect(Kind.Damage,
                    role is null or JobRole.PhysicalRanged or JobRole.Caster or JobRole.Healer ? 1.06f : 1.03f),
            },
        },
    };

    // The whole table, for building id lookups against the game sheets.
    public static IReadOnlyList<Buff> All => Table;

    private static readonly Dictionary<string, Buff> ByName = Build();

    private static Dictionary<string, Buff> Build()
    {
        var map = new Dictionary<string, Buff>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in Table) map[b.Name] = b;
        return map;
    }

    public static Buff? Find(string statusName)
        => ByName.TryGetValue(statusName, out var b) ? b : null;
}
