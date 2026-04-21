using System;
using UnityEngine;
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
        [SerializeField] private float _health = 100f;
        [SerializeField] private float _maxHealth = 100f;
        #endregion

        public string CharacterName
        {
            get => _characterName;
            set => _characterName = value?.Trim() ?? string.Empty;
        }

        public string OwnerId => EntityCodeUtility.DefaultPlayerOwnerId;

        public float MaxHealth
        {
            get => _maxHealth;
            set
            {
                _maxHealth = Mathf.Max(1f, value);
                _health = Mathf.Clamp(_health, 0f, _maxHealth);
                OnVitalsChanged?.Invoke(this);
            }
        }

        public float Health
        {
            get => _health;
            set
            {
                bool wasAlive = _health > 0f;
                float clamped = Mathf.Clamp(value, 0f, MaxHealth);
                if (Mathf.Approximately(_health, clamped))
                    return;

                _health = clamped;
                OnVitalsChanged?.Invoke(this);
                if (wasAlive && _health <= 0f)
                    OnDeath?.Invoke();
            }
        }

        public float HealthNorm => MaxHealth > 0f ? _health / MaxHealth : 0f;
        public bool IsAlive => _health > 0f;

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

        public void TakeDamage(float amount) => Health -= Mathf.Abs(amount);
        public void Heal(float amount) => Health += Mathf.Abs(amount);

        private void EnsurePlayerIdentity()
        {
            if (!CompareTag("Player"))
                gameObject.tag = "Player";

            _characterName = string.IsNullOrWhiteSpace(_characterName) ? "Player" : _characterName.Trim();
        }

        private void ClampVitals()
        {
            _maxHealth = Mathf.Max(1f, _maxHealth);
            _health = Mathf.Clamp(_health, 0f, _maxHealth);
        }
    }
}
