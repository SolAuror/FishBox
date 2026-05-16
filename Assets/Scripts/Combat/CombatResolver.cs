using System;
using Sol.AI;
using Sol.Player;
using Sol.Rpg;
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

            return ApplyDamagePacket(DamagePacket.FromHit(hit), hit);
        }

        public static CombatDamageResult ApplyDamagePacket(DamagePacket packet)
        {
            return ApplyDamagePacket(packet, default);
        }

        private static CombatDamageResult ApplyDamagePacket(DamagePacket packet, CombatHit hit)
        {
            if (packet == null || packet.Target == null || !packet.Target.IsAlive)
            {
                CombatDamageResult rejected = hit.Target != null
                    ? CombatDamageResult.Rejected(hit)
                    : new CombatDamageResult(
                        packet?.Attacker,
                        packet?.Target,
                        packet?.SourceWeapon,
                        packet?.OriginalDamage ?? 0f,
                        0f,
                        0f,
                        0f,
                        killed: false,
                        canStagger: false,
                        didStagger: false,
                        applied: false,
                        packet?.DamageTagPaths);
                OnHitResolved?.Invoke(rejected);
                GameplayEvents.RaiseDamageResolved(rejected);
                return rejected;
            }

            GameplayEvents.RaiseDamagePreparing(packet);

            float originalDamage = Mathf.Max(0f, packet.OriginalDamage);
            if (originalDamage <= 0f)
            {
                CombatDamageResult rejected = new(
                    packet.Attacker,
                    packet.Target,
                    packet.SourceWeapon,
                    originalDamage,
                    0f,
                    0f,
                    0f,
                    killed: false,
                    canStagger: false,
                    didStagger: false,
                    applied: false,
                    packet.DamageTagPaths);
                OnHitResolved?.Invoke(rejected);
                GameplayEvents.RaiseDamageResolved(rejected);
                return rejected;
            }

            float armorRating = packet.Target.GetArmorRating();
            float mitigation = CalculateArmorMitigation(armorRating);
            float finalDamage = Mathf.Max(0f, packet.Amount * (1f - mitigation));
            finalDamage = ApplyDamageTagDefenses(packet, finalDamage);
            finalDamage = GameplayStatSystem.Evaluate(
                GameplayStatIds.IncomingDamage,
                finalDamage,
                packet.Target.gameObject,
                packet.Attacker != null ? packet.Attacker.gameObject : null);

            bool wasAlive = packet.Target.IsAlive;
            float targetHealth = Mathf.Max(0f, packet.Target.Health);
            float targetMaxHealth = Mathf.Max(0f, packet.Target.MaxHealth);
            bool preventsDeath = PreventsDeath(packet.Target);
            if (preventsDeath && finalDamage >= targetHealth)
                finalDamage = Mathf.Max(0f, targetHealth - 1f);

            if (finalDamage > 0f)
                packet.Target.TakeDamage(finalDamage);

            bool killed = wasAlive && !packet.Target.IsAlive;
            float staggerDamage = finalDamage * Mathf.Max(0f, hit.StaggerMultiplier);
            bool canStagger = targetMaxHealth > 0f && staggerDamage >= targetMaxHealth * StaggerHealthFraction;
            bool didStagger = canStagger && packet.Target.IsAlive;
            CombatDamageResult result = new(
                packet.Attacker,
                packet.Target,
                packet.SourceWeapon,
                originalDamage,
                finalDamage,
                armorRating,
                mitigation,
                killed,
                canStagger,
                didStagger,
                applied: finalDamage > 0f,
                packet.DamageTagPaths);

            TriggerHitReaction(hit, result);
            OnHitResolved?.Invoke(result);
            GameplayEvents.RaiseDamageResolved(result);
            if (killed)
            {
                OnActorKilled?.Invoke(result);
                GameplayEvents.RaiseActorKilled(result);
            }

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

        private static float ApplyDamageTagDefenses(DamagePacket packet, float damage)
        {
            if (packet.Target == null || damage <= 0f)
                return 0f;

            GameplayTagSet targetTags = packet.Target.gameObject.GetGameplayTags();
            for (int i = 0; i < packet.DamageTagPaths.Count; i++)
            {
                string damageTag = packet.DamageTagPaths[i];
                string leaf = Leaf(damageTag);
                if (string.IsNullOrEmpty(leaf))
                    continue;

                if (targetTags.HasExact("Immune." + leaf) || targetTags.HasTagOrChild("Immune." + leaf))
                    return 0f;

                if (targetTags.HasExact("Resist." + leaf) || targetTags.HasTagOrChild("Resist." + leaf))
                    damage *= 0.5f;
            }

            return damage;
        }

        private static bool PreventsDeath(Combatant target)
        {
            if (target == null)
                return false;

            GameplayTagSet tags = target.gameObject.GetGameplayTags();
            if (tags.HasExact("Trait.Immortal") || tags.HasTagOrChild("Trait.Immortal"))
                return true;

            TraitController traits = target.GetComponent<TraitController>();
            return traits != null && traits.HasReaction(TraitReaction.PreventDeath);
        }

        private static string Leaf(string tagPath)
        {
            if (string.IsNullOrWhiteSpace(tagPath))
                return string.Empty;

            int dot = tagPath.LastIndexOf('.');
            return dot >= 0 && dot < tagPath.Length - 1 ? tagPath.Substring(dot + 1) : tagPath;
        }
    }
}
