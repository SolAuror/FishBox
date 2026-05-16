using Sol.Grab;
using System.Collections.Generic;
using UnityEngine;

namespace Sol.Combat
{
    public enum CombatAttackKind
    {
        Unarmed = 0,
        OneHanded = 1,
        TwoHanded = 2
    }

    public readonly struct CombatHit
    {
        public Combatant Attacker { get; }
        public Combatant Target { get; }
        public ItemComponent SourceWeapon { get; }
        public float BaseDamage { get; }
        public float LegacyFallbackDamage { get; }
        public float StaminaCost { get; }
        public CombatAttackKind AttackKind { get; }
        public CombatAttackStyle AttackStyle { get; }
        public float StaggerMultiplier { get; }
        public Vector3 HitDirection { get; }
        public IReadOnlyList<string> DamageTagPaths { get; }

        public CombatHit(
            Combatant attacker,
            Combatant target,
            ItemComponent sourceWeapon,
            float baseDamage,
            float legacyFallbackDamage,
            float staminaCost,
            CombatAttackKind attackKind,
            Vector3 hitDirection,
            CombatAttackStyle attackStyle = CombatAttackStyle.Light,
            float staggerMultiplier = 1f,
            IReadOnlyList<string> damageTagPaths = null)
        {
            Attacker = attacker;
            Target = target;
            SourceWeapon = sourceWeapon;
            BaseDamage = baseDamage;
            LegacyFallbackDamage = legacyFallbackDamage;
            StaminaCost = staminaCost;
            AttackKind = attackKind;
            AttackStyle = attackStyle;
            StaggerMultiplier = Mathf.Max(0f, staggerMultiplier);
            HitDirection = hitDirection;
            DamageTagPaths = damageTagPaths ?? DefaultDamageTags.Physical;
        }
    }

    public static class DefaultDamageTags
    {
        public static readonly IReadOnlyList<string> Physical = new[] { "Damage.Physical" };
    }
}
