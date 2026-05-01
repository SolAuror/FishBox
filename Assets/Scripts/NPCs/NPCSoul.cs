using System;
using UnityEngine;
using UnityEngine.Serialization;
using Sol;

namespace Sol.AI
{
    public enum SoulType
    {
        NPC = 0,
        Player = 1,
        Enemy = 2
    }

    /// <summary>
    /// Authoring template applied via the NPC Database window. Drives default component
    /// setup and validation hints. Mirrors <see cref="Sol.Grab.ItemAuthoringTemplate"/>.
    /// </summary>
    public enum NPCAuthoringTemplate
    {
        None = 0,
        Civilian = 1,
        Patroller = 2,
        Trader = 3,
        QuestGiver = 4
    }

    /// <summary>
    /// Unified runtime stats for actors (player, NPC, enemy).
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
        [Tooltip("Stable owner id used by theft/trade/ownership systems (OWN#####).")]
        [SerializeField] private string _ownerId = string.Empty;

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

        public string OwnerId => NormalizeOwnerId(_ownerId);

        /// <summary>Legacy compatibility alias. Prefer CharacterName in new code.</summary>
        public string NPCName
        {
            get => CharacterName;
            set => CharacterName = value;
        }

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

        [Header("Authoring")]
        [SerializeField] private NPCAuthoringTemplate _authoringTemplate = NPCAuthoringTemplate.None;
        [TextArea(2, 6)]
        [SerializeField] private string _authoringNotes = string.Empty;
        #endregion

        public NPCAuthoringTemplate AuthoringTemplate => _authoringTemplate;
        public string AuthoringNotes => _authoringNotes;

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
                NotifyVitalsChanged();
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
                if (!_healthStat.SetCurrent(value)) return;

                SyncLegacyVitals();
                NotifyVitalsChanged();
                if (wasAlive && _healthStat.IsEmpty) OnDeath?.Invoke();
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
                NotifyVitalsChanged();
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
                NotifyVitalsChanged();
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
        public event Action<NPCSoul> OnVitalsChanged;

        private void Awake()
        {
            EnsureOwnerId();
            OwnerRegistry.Register(OwnerId, gameObject);
            ClampVitals();
        }

        private void OnEnable()
        {
            EnsureOwnerId();
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

        public void TakeDamage(float amount)
        {
            EnsureSoulStatsMigrated();
            bool wasAlive = IsAlive;
            if (!_healthStat.Subtract(amount))
                return;

            SyncLegacyVitals();
            NotifyVitalsChanged();
            if (wasAlive && _healthStat.IsEmpty)
                OnDeath?.Invoke();
        }

        public void Heal(float amount)
        {
            EnsureSoulStatsMigrated();
            if (!_healthStat.Add(amount))
                return;

            SyncLegacyVitals();
            NotifyVitalsChanged();
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
            NotifyVitalsChanged();
            return true;
        }

        public void RestoreStamina(float amount)
        {
            EnsureSoulStatsMigrated();
            if (!_staminaStat.Add(amount))
                return;

            SyncLegacyVitals();
            NotifyVitalsChanged();
        }

        private void OnValidate()
        {
            _characterName = _characterName?.Trim() ?? string.Empty;
            EnsureOwnerId();
            ClampVitals();
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

        private void NotifyVitalsChanged()
        {
            OnVitalsChanged?.Invoke(this);
        }

        private void EnsureOwnerId()
        {
            if (_soulType == SoulType.Player || gameObject.CompareTag("Player"))
            {
                _ownerId = EntityCodeUtility.DefaultPlayerOwnerId;
                return;
            }

            _ownerId = EntityCodeUtility.NormalizeOrEmpty(_ownerId, EntityCodeUtility.OwnerPrefix);
#if UNITY_EDITOR
            _ownerId = EntityCodeUtility.EnsureAssignedCode(
                this,
                _ownerId,
                EntityCodeUtility.OwnerPrefix,
                static soul => soul._ownerId);
#endif
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

        private static string NormalizeOwnerId(string rawOwnerId)
        {
            if (string.IsNullOrWhiteSpace(rawOwnerId))
                return string.Empty;

            string trimmed = rawOwnerId.Trim();
            if (string.Equals(trimmed, EntityCodeUtility.DefaultPlayerOwnerId, StringComparison.OrdinalIgnoreCase))
                return EntityCodeUtility.DefaultPlayerOwnerId;

            return EntityCodeUtility.NormalizeOrEmpty(trimmed, EntityCodeUtility.OwnerPrefix);
        }
    }
}
