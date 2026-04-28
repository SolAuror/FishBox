using System;
using UnityEngine;
using UnityEngine.Serialization;
using Sol;

namespace Sol.Player
{
    /// <summary>
    /// Player-specific vitals and identity. Kept independent from NPCSoul.
    /// </summary>
    [AddComponentMenu("Sol/Player/Player Soul")]
    public class PlayerSoul : MonoBehaviour
    {
        #region Inspector Settings
        [Header("Identity")]
        [SerializeField] private string _characterName = "Player";

        [Header("Vitals")]
        [SerializeField] private SoulStat _healthStat = new SoulStat(100f, 100f);
        [SerializeField] private SoulStat _staminaStat = new SoulStat(100f, 100f);

        [HideInInspector]
        [SerializeField] private float _health = 100f;
        [FormerlySerializedAs("MaxHealth")]
        [HideInInspector]
        [SerializeField] private float _maxHealth = 100f;
        [HideInInspector]
        [SerializeField] private float _stamina = 100f;
        [FormerlySerializedAs("MaxStamina")]
        [HideInInspector]
        [SerializeField] private float _maxStamina = 100f;
        [HideInInspector]
        [SerializeField] private bool _soulStatsMigrated;
        #endregion

        public string CharacterName
        {
            get => _characterName;
            set => _characterName = value?.Trim() ?? string.Empty;
        }

        public string OwnerId => EntityCodeUtility.DefaultPlayerOwnerId;

        public float MaxHealth
        {
            get
            {
                EnsureSoulStatsMigrated();
                return _healthStat.Max;
            }
            set
            {
                EnsureSoulStatsMigrated();
                if (!_healthStat.SetMax(value))
                    return;

                SyncLegacyVitals();
                OnVitalsChanged?.Invoke(this);
            }
        }

        public float Health
        {
            get
            {
                EnsureSoulStatsMigrated();
                return _healthStat.Current;
            }
            set
            {
                EnsureSoulStatsMigrated();
                bool wasAlive = IsAlive;
                if (!_healthStat.SetCurrent(value))
                    return;

                SyncLegacyVitals();
                OnVitalsChanged?.Invoke(this);
                if (wasAlive && _healthStat.IsEmpty)
                    OnDeath?.Invoke();
            }
        }

        public float MaxStamina
        {
            get
            {
                EnsureSoulStatsMigrated();
                return _staminaStat.Max;
            }
            set
            {
                EnsureSoulStatsMigrated();
                if (!_staminaStat.SetMax(value))
                    return;

                SyncLegacyVitals();
                OnVitalsChanged?.Invoke(this);
            }
        }

        public float Stamina
        {
            get
            {
                EnsureSoulStatsMigrated();
                return _staminaStat.Current;
            }
            set
            {
                EnsureSoulStatsMigrated();
                if (!_staminaStat.SetCurrent(value))
                    return;

                SyncLegacyVitals();
                OnVitalsChanged?.Invoke(this);
            }
        }

        public float HealthNorm
        {
            get
            {
                EnsureSoulStatsMigrated();
                return _healthStat.Normalized;
            }
        }

        public float StaminaNorm
        {
            get
            {
                EnsureSoulStatsMigrated();
                return _staminaStat.Normalized;
            }
        }

        public bool IsAlive
        {
            get
            {
                EnsureSoulStatsMigrated();
                return !_healthStat.IsEmpty;
            }
        }

        public event Action OnDeath;
        public event Action<PlayerSoul> OnVitalsChanged;

        private void Awake()
        {
            EnsurePlayerIdentity();
            ClampVitals();
            OwnerRegistry.Register(OwnerId, gameObject);
        }

        private void OnEnable()
        {
            EnsurePlayerIdentity();
            OwnerRegistry.Register(OwnerId, gameObject);
        }

        private void OnDisable()
        {
            OwnerRegistry.Unregister(OwnerId, gameObject);
        }

        private void OnDestroy()
        {
            OwnerRegistry.Unregister(OwnerId, gameObject);
        }

        private void Reset()
        {
            EnsurePlayerIdentity();
        }

        private void OnValidate()
        {
            EnsurePlayerIdentity();
            ClampVitals();
        }

        public void TakeDamage(float amount)
        {
            EnsureSoulStatsMigrated();
            bool wasAlive = IsAlive;
            if (!_healthStat.Subtract(amount))
                return;

            SyncLegacyVitals();
            OnVitalsChanged?.Invoke(this);
            if (wasAlive && _healthStat.IsEmpty)
                OnDeath?.Invoke();
        }

        public void Heal(float amount)
        {
            EnsureSoulStatsMigrated();
            if (!_healthStat.Add(amount))
                return;

            SyncLegacyVitals();
            OnVitalsChanged?.Invoke(this);
        }

        public bool SpendStamina(float amount)
        {
            EnsureSoulStatsMigrated();
            float cost = Mathf.Abs(amount);
            if (cost <= 0f)
                return true;

            if (_staminaStat.Current < cost)
                return false;

            if (!_staminaStat.Subtract(cost))
                return true;

            SyncLegacyVitals();
            OnVitalsChanged?.Invoke(this);
            return true;
        }

        public void RestoreStamina(float amount)
        {
            EnsureSoulStatsMigrated();
            if (!_staminaStat.Add(amount))
                return;

            SyncLegacyVitals();
            OnVitalsChanged?.Invoke(this);
        }

        private void EnsurePlayerIdentity()
        {
            if (!CompareTag("Player"))
                gameObject.tag = "Player";

            _characterName = string.IsNullOrWhiteSpace(_characterName) ? "Player" : _characterName.Trim();
        }

        private void ClampVitals()
        {
            EnsureSoulStatsMigrated();
            bool changed = false;
            changed |= _healthStat.Clamp();
            changed |= _staminaStat.Clamp();
            if (changed)
                SyncLegacyVitals();
        }

        private void EnsureSoulStatsMigrated()
        {
            _healthStat ??= new SoulStat(_health, _maxHealth);
            _staminaStat ??= new SoulStat(_stamina, _maxStamina);

            if (_soulStatsMigrated)
                return;

            _healthStat.Set(_health, _maxHealth);
            _staminaStat.Set(_stamina, _maxStamina);
            _soulStatsMigrated = true;
            SyncLegacyVitals();
        }

        private void SyncLegacyVitals()
        {
            _health = _healthStat.Current;
            _maxHealth = _healthStat.Max;
            _stamina = _staminaStat.Current;
            _maxStamina = _staminaStat.Max;
        }
    }
}
