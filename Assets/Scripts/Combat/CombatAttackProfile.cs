using UnityEngine;

namespace Sol.Combat
{
    public enum CombatAttackStyle
    {
        Light = 0,
        Power = 1
    }

    public readonly struct CombatAttackProfile
    {
        public CombatAttackStyle Style { get; }
        public float DamageMultiplier { get; }
        public float StaminaMultiplier { get; }
        public float CooldownMultiplier { get; }
        public float StaggerMultiplier { get; }

        public CombatAttackProfile(
            CombatAttackStyle style,
            float damageMultiplier,
            float staminaMultiplier,
            float cooldownMultiplier,
            float staggerMultiplier)
        {
            Style = style;
            DamageMultiplier = Mathf.Max(0f, damageMultiplier);
            StaminaMultiplier = Mathf.Max(0f, staminaMultiplier);
            CooldownMultiplier = Mathf.Max(0.01f, cooldownMultiplier);
            StaggerMultiplier = Mathf.Max(0f, staggerMultiplier);
        }

        public static CombatAttackProfile Light => new(CombatAttackStyle.Light, 1f, 1f, 1f, 1f);
        public static CombatAttackProfile Power => new(CombatAttackStyle.Power, 1.75f, 2f, 1.25f, 1.5f);

        public static CombatAttackProfile FromStyle(CombatAttackStyle style)
        {
            return style == CombatAttackStyle.Power ? Power : Light;
        }

        public float ApplyDamage(float baseDamage) => Mathf.Max(0f, baseDamage) * DamageMultiplier;
        public float ApplyStaminaCost(float baseCost) => Mathf.Max(0f, baseCost) * StaminaMultiplier;
        public float ApplyCooldown(float baseCooldown) => Mathf.Max(0.01f, baseCooldown) * CooldownMultiplier;
    }
}
