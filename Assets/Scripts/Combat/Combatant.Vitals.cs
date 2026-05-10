using UnityEngine;

namespace Sol.Combat
{
    public partial class Combatant
    {
        public bool IsAlive
        {
            get
            {
                RefreshReferences();
                if (_playerSoul != null)
                    return _playerSoul.IsAlive;
                if (_npcSoul != null)
                    return _npcSoul.IsAlive;
                return true;
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
                if (_playerSoul != null)
                    return _playerSoul.Health;
                if (_npcSoul != null)
                    return _npcSoul.Health;
                return 0f;
            }
        }

        public float MaxHealth
        {
            get
            {
                RefreshReferences();
                if (_playerSoul != null)
                    return _playerSoul.MaxHealth;
                if (_npcSoul != null)
                    return _npcSoul.MaxHealth;
                return 0f;
            }
        }

        public float Stamina
        {
            get
            {
                RefreshReferences();
                if (_playerSoul != null)
                    return _playerSoul.Stamina;
                if (_npcSoul != null)
                    return _npcSoul.Stamina;
                return float.PositiveInfinity;
            }
        }

        public float MaxStamina
        {
            get
            {
                RefreshReferences();
                if (_playerSoul != null)
                    return _playerSoul.MaxStamina;
                if (_npcSoul != null)
                    return _npcSoul.MaxStamina;
                return float.PositiveInfinity;
            }
        }

        public bool CanSpendStamina(float amount)
        {
            RefreshReferences();
            float cost = Mathf.Max(0f, amount);
            if (cost <= 0f)
                return true;

            if (_playerSoul == null && _npcSoul == null)
                return true;

            return Stamina >= cost;
        }

        public bool TrySpendStamina(float amount)
        {
            RefreshReferences();
            float cost = Mathf.Max(0f, amount);
            if (cost <= 0f)
                return true;

            bool spent;
            if (_playerSoul != null)
                spent = _playerSoul.SpendStamina(cost);
            else if (_npcSoul != null)
                spent = _npcSoul.SpendStamina(cost);
            else
                spent = true;

            if (spent)
                _lastStaminaSpendTime = Time.time;

            return spent;
        }

        public void TakeDamage(float amount)
        {
            RefreshReferences();
            if (_playerSoul != null)
            {
                _playerSoul.TakeDamage(amount);
                return;
            }

            if (_npcSoul != null)
                _npcSoul.TakeDamage(amount);
        }

        private void RegenerateStamina()
        {
            if (!IsAlive || Time.time < _lastStaminaSpendTime + Mathf.Max(0f, _staminaRegenDelay))
                return;

            float regen = Mathf.Max(0f, _staminaRegenPerSecond);
            if (regen <= 0f || Stamina >= MaxStamina)
                return;

            float amount = regen * Time.deltaTime;
            if (_playerSoul != null)
                _playerSoul.RestoreStamina(amount);
            else if (_npcSoul != null)
                _npcSoul.RestoreStamina(amount);
        }
    }
}
