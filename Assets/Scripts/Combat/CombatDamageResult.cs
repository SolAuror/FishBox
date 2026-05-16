using Sol.Grab;
using System.Collections.Generic;

namespace Sol.Combat
{
    public readonly struct CombatDamageResult
    {
        public Combatant Attacker { get; }
        public Combatant Target { get; }
        public ItemComponent SourceWeapon { get; }
        public float OriginalDamage { get; }
        public float FinalDamage { get; }
        public float ArmorRating { get; }
        public float Mitigation { get; }
        public bool Killed { get; }
        public bool CanStagger { get; }
        public bool DidStagger { get; }
        public bool Applied { get; }
        public IReadOnlyList<string> DamageTagPaths { get; }

        public CombatDamageResult(
            Combatant attacker,
            Combatant target,
            ItemComponent sourceWeapon,
            float originalDamage,
            float finalDamage,
            float armorRating,
            float mitigation,
            bool killed,
            bool canStagger,
            bool didStagger,
            bool applied,
            IReadOnlyList<string> damageTagPaths = null)
        {
            Attacker = attacker;
            Target = target;
            SourceWeapon = sourceWeapon;
            OriginalDamage = originalDamage;
            FinalDamage = finalDamage;
            ArmorRating = armorRating;
            Mitigation = mitigation;
            Killed = killed;
            CanStagger = canStagger;
            DidStagger = didStagger;
            Applied = applied;
            DamageTagPaths = damageTagPaths ?? DefaultDamageTags.Physical;
        }

        public static CombatDamageResult Rejected(CombatHit hit)
        {
            return new CombatDamageResult(
                hit.Attacker,
                hit.Target,
                hit.SourceWeapon,
                hit.BaseDamage,
                0f,
                0f,
                0f,
                killed: false,
                canStagger: false,
                didStagger: false,
                applied: false,
                hit.DamageTagPaths);
        }
    }
}
