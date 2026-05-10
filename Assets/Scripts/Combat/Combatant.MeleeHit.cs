using Sol.Grab;
using UnityEngine;

namespace Sol.Combat
{
    public partial class Combatant
    {
        public CombatHit CreateMeleeHit(Combatant target, float fallbackDamage, Vector3 hitDirection)
        {
            return CreateMeleeHit(target, fallbackDamage, hitDirection, CombatAttackProfile.Light);
        }

        public CombatHit CreateMeleeHit(
            Combatant target,
            float fallbackDamage,
            Vector3 hitDirection,
            CombatAttackProfile profile)
        {
            TryGetEquippedWeapon(out ItemComponent weapon, out _);
            CombatAttackKind attackKind = GetAttackKind(weapon);
            float staminaCost = profile.ApplyStaminaCost(GetAttackStaminaCost(weapon));
            float baseDamage = profile.ApplyDamage(GetAttackBaseDamage(weapon, fallbackDamage));
            return new CombatHit(
                this,
                target,
                weapon,
                baseDamage,
                fallbackDamage,
                staminaCost,
                attackKind,
                hitDirection,
                profile.Style,
                profile.StaggerMultiplier);
        }
    }
}
