using UnityEngine;
using Sol.Rpg;

namespace Sol.Combat
{
    public partial class Combatant
    {
        public bool IsAlive
        {
            get
            {
                RefreshReferences();
                return _vitals?.IsAlive ?? true;
            }
        }

        public bool IsHostile
        {
            get
            {
                RefreshReferences();
                return _npcSoul != null && _npcSoul.IsHostile;
            }
        }

        public float Health
        {
            get
            {
                RefreshReferences();
                return _vitals?.Health ?? 0f;
            }
        }

        public float MaxHealth
        {
            get
            {
                RefreshReferences();
                return _vitals?.MaxHealth ?? 0f;
            }
        }

        public float Stamina
        {
            get
            {
                RefreshReferences();
                return _vitals?.Stamina ?? float.PositiveInfinity;
            }
        }

        public float MaxStamina
        {
            get
            {
                RefreshReferences();
                return _vitals?.MaxStamina ?? float.PositiveInfinity;
            }
        }

        public bool CanSpendStamina(float amount)
        {
            RefreshReferences();
            float cost = Mathf.Max(0f, amount);
            if (cost <= 0f)
                return true;

            cost = GameplayStatSystem.Evaluate(GameplayStatIds.StaminaCost, cost, gameObject);
            if (cost <= 0f || IgnoresStaminaCosts())
                return true;

            if (_vitals == null)
                return true;

            return Stamina >= cost;
        }

        public bool TrySpendStamina(float amount)
        {
            RefreshReferences();
            float cost = Mathf.Max(0f, amount);
            if (cost <= 0f)
                return true;

            cost = GameplayStatSystem.Evaluate(GameplayStatIds.StaminaCost, cost, gameObject);
            if (cost <= 0f || IgnoresStaminaCosts())
                return true;

            bool spent = _vitals?.SpendStamina(cost) ?? true;

            if (spent)
                _lastStaminaSpendTime = Time.time;

            return spent;
        }

        public void TakeDamage(float amount)
        {
            RefreshReferences();
            _vitals?.TakeDamage(amount);
        }

        private void RegenerateStamina()
        {
            if (!IsAlive || Time.time < _lastStaminaSpendTime + Mathf.Max(0f, _staminaRegenDelay))
                return;

            float regen = Mathf.Max(0f, GameplayStatSystem.Evaluate(GameplayStatIds.StaminaRegen, _staminaRegenPerSecond, gameObject));
            if (regen <= 0f || Stamina >= MaxStamina)
                return;

            float amount = regen * Time.deltaTime;
            _vitals?.RestoreStamina(amount);
        }

        private bool IgnoresStaminaCosts()
        {
            GameplayTagSet tags = gameObject.GetGameplayTags();
            if (tags.HasExact("Trait.Tireless") || tags.HasTagOrChild("Trait.Tireless"))
                return true;

            TraitController traits = GetComponent<TraitController>();
            return traits != null && traits.HasReaction(TraitReaction.IgnoreStaminaCosts);
        }
    }
}
