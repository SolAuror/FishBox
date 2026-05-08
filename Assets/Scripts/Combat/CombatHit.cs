using Sol.Grab;
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
        public Vector3 HitDirection { get; }

        public CombatHit(
            Combatant attacker,
            Combatant target,
            ItemComponent sourceWeapon,
            float baseDamage,
            float legacyFallbackDamage,
            float staminaCost,
            CombatAttackKind attackKind,
            Vector3 hitDirection)
        {
            Attacker = attacker;
            Target = target;
            SourceWeapon = sourceWeapon;
            BaseDamage = baseDamage;
            LegacyFallbackDamage = legacyFallbackDamage;
            StaminaCost = staminaCost;
            AttackKind = attackKind;
            HitDirection = hitDirection;
        }
    }
}
