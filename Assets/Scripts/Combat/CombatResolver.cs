using System;
using Sol.AI;
using Sol.Player;
using UnityEngine;

namespace Sol.Combat
{
    public static class CombatResolver
    {
        public const float ArmorMitigationCap = 0.8f;
        public const float ArmorMitigationScale = 100f;
        public const float StaggerHealthFraction = 0.2f;

        public static event Action<CombatHit> OnAttackStarted;
        public static event Action<CombatHit> OnAttackRejected;
        public static event Action<CombatDamageResult> OnHitResolved;
        public static event Action<CombatDamageResult> OnActorKilled;

        public static void RaiseAttackStarted(CombatHit hit)
        {
            OnAttackStarted?.Invoke(hit);
        }

        public static void RaiseAttackRejected(CombatHit hit)
        {
            OnAttackRejected?.Invoke(hit);
        }

        public static CombatDamageResult ApplyHit(CombatHit hit)
        {
            if (hit.Attacker == null || hit.Target == null || !hit.Target.IsAlive)
            {
                CombatDamageResult rejected = CombatDamageResult.Rejected(hit);
                OnHitResolved?.Invoke(rejected);
                return rejected;
            }

            float originalDamage = Mathf.Max(0f, hit.BaseDamage);
            if (originalDamage <= 0f)
            {
                CombatDamageResult rejected = CombatDamageResult.Rejected(hit);
                OnHitResolved?.Invoke(rejected);
                return rejected;
            }

            float armorRating = hit.Target.GetArmorRating();
            float mitigation = CalculateArmorMitigation(armorRating);
            float finalDamage = Mathf.Max(1f, originalDamage * (1f - mitigation));
            bool wasAlive = hit.Target.IsAlive;
            float targetMaxHealth = Mathf.Max(0f, hit.Target.MaxHealth);

            hit.Target.TakeDamage(finalDamage);

            bool killed = wasAlive && !hit.Target.IsAlive;
            bool canStagger = targetMaxHealth > 0f && finalDamage >= targetMaxHealth * StaggerHealthFraction;
            bool didStagger = canStagger && hit.Target.IsAlive;
            CombatDamageResult result = new(
                hit.Attacker,
                hit.Target,
                hit.SourceWeapon,
                originalDamage,
                finalDamage,
                armorRating,
                mitigation,
                killed,
                canStagger,
                didStagger,
                applied: true);

            TriggerHitReaction(hit, result);
            OnHitResolved?.Invoke(result);
            if (killed)
                OnActorKilled?.Invoke(result);

            return result;
        }

        public static float CalculateArmorMitigation(float armorRating)
        {
            armorRating = Mathf.Max(0f, armorRating);
            if (armorRating <= 0f)
                return 0f;

            return Mathf.Min(ArmorMitigationCap, armorRating / (armorRating + ArmorMitigationScale));
        }

        private static void TriggerHitReaction(CombatHit hit, CombatDamageResult result)
        {
            if (!result.Applied || hit.Target == null || !hit.Target.IsNpc || !hit.Target.IsAlive)
                return;

            NPCSoul targetSoul = hit.Target.NpcSoul;
            if (targetSoul == null)
                return;

            HitReaction hitReaction = targetSoul.GetComponent<HitReaction>();
            if (hitReaction == null)
                hitReaction = targetSoul.GetComponentInChildren<HitReaction>();

            if (hitReaction == null)
                return;

            Vector3 direction = hit.HitDirection;
            if (direction.sqrMagnitude < 0.0001f && hit.Attacker != null)
                direction = targetSoul.transform.position - hit.Attacker.transform.position;

            if (direction.sqrMagnitude < 0.0001f)
                direction = targetSoul.transform.forward;

            hitReaction.PlayHitReaction(direction.normalized);
        }
    }
}
