using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Sol.AI
{
    public enum SoulType
    {
        NPC = 0,
        Player = 1,
        Enemy = 2
    }

    /// <summary>
    /// Unified runtime stats for actors (player, NPC, enemy).
    /// The simplified project keeps only health as live gameplay state.
    /// </summary>
    public class NPCSoul : MonoBehaviour
    {
        #region Inspector Settings
        [Header("Identity")]
        [Tooltip("Inspector: tunes soul type.")]
        [SerializeField] private SoulType _soulType = SoulType.NPC;
        [FormerlySerializedAs("NPCName")]
        [Tooltip("Inspector: tunes character name.")]
        [SerializeField] private string _characterName = string.Empty;

        public SoulType SoulKind
        {
            get => _soulType;
            set => _soulType = value;
        }

        public string CharacterName
        {
            get => _characterName;
            set => _characterName = value?.Trim() ?? string.Empty;
        }

        /// <summary>Legacy compatibility alias. Prefer CharacterName in new code.</summary>
        public string NPCName
        {
            get => CharacterName;
            set => CharacterName = value;
        }

        [Header("Vitals")]
        [Tooltip("Inspector: tunes health.")]
        [SerializeField] private float _health = 100f; public float MaxHealth = 100f;
        #endregion

        public float Health
        {
            get => _health;
            set
            {
                bool wasAlive = _health > 0f;
                float clamped = Mathf.Clamp(value, 0f, MaxHealth);
                if (Mathf.Approximately(_health, clamped)) return;

                _health = clamped;
                NotifyVitalsChanged();
                if (wasAlive && _health <= 0f) OnDeath?.Invoke();
            }
        }

        public float HealthNorm => MaxHealth > 0f ? _health / MaxHealth : 0f;

        public bool IsAlive => _health > 0f;

        public event Action OnDeath;
        public event Action<NPCSoul> OnVitalsChanged;

        private void Awake()
        {
            ClampVitals();
        }

        public void TakeDamage(float amount) => Health -= Mathf.Abs(amount);
        public void Heal(float amount) => Health += Mathf.Abs(amount);

        private void OnValidate()
        {
            _characterName = _characterName?.Trim() ?? string.Empty;
            ClampVitals();
        }

        private void ClampVitals()
        {
            _health = Mathf.Clamp(_health, 0f, MaxHealth);
        }

        private void NotifyVitalsChanged()
        {
            OnVitalsChanged?.Invoke(this);
        }
    }
}
