using System;
using UnityEngine;
using UnityEngine.Serialization;
using Sol;
using Sol.Rpg;

namespace Sol.AI
{
    public enum EntityType
    {
        NPC = 0,
        Player = 1
    }

    /// <summary>
    /// NPC archetype applied via the NPC Database window. Drives default component
    /// setup and validation hints.
    /// </summary>
    public enum NPCArchetype
    {
        None = 0,
        Civilian = 1,
        Guard = 2,
        QuestGiver = 4,
        Unique = 5,
        Bandit = 6
    }

    /// <summary>
    /// Section visibility rules for the NPC authoring inspector.
    /// </summary>
    public static class NPCArchetypeRules
    {
        public static bool ShowAISection(NPCArchetype archetype, bool hasComponent) => true;

        public static bool ShowInventorySection(NPCArchetype archetype, bool hasComponent) => true;

        public static bool ShowTradingSection(bool canTrade, bool hasComponent)
        {
            return hasComponent || canTrade;
        }
    }

    /// <summary>
    /// Unified runtime stats for actors (player and NPC).
    /// </summary>
    public class NPCSoul : MonoBehaviour, IGameplayTagProvider
    {
        #region Inspector Settings
        [Header("Identity")]
        [Tooltip("Broad entity category. Hostility is controlled separately.")]
        [FormerlySerializedAs("_soulType")]
        [SerializeField] private EntityType _entityType = EntityType.NPC;
        [FormerlySerializedAs("NPCName")]
        [Tooltip("Inspector: tunes character name.")]
        [SerializeField] private string _characterName = string.Empty;
        [Tooltip("Stable owner id used by theft/trade/ownership systems (OWN#####).")]
        [SerializeField] private string _ownerId = string.Empty;

        public EntityType EntityKind
        {
            get
            {
                EnsureIdentityMigration();
                return _entityType;
            }
            set => _entityType = value;
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
        [FormerlySerializedAs("_authoringTemplate")]
        [SerializeField] private NPCArchetype _npcArchetype = NPCArchetype.None;
        [SerializeField] private bool _canTrade;
        [SerializeField] private bool _isHostile;
        [SerializeField] private GameplayTagSet _tags = new();
        [TextArea(2, 6)]
        [SerializeField] private string _authoringNotes = string.Empty;
        [HideInInspector]
        [SerializeField] private bool _identityMigratedV2;
        #endregion

        public NPCArchetype Archetype
        {
            get
            {
                EnsureIdentityMigration();
                return _npcArchetype;
            }
        }

        public bool CanTrade
        {
            get
            {
                EnsureIdentityMigration();
                return _canTrade;
            }
            set
            {
                _canTrade = value;
                RefreshRuntimeIdentityTags();
            }
        }

        public bool IsHostile
        {
            get
            {
                EnsureIdentityMigration();
                return _isHostile;
            }
            set
            {
                _isHostile = value;
                RefreshRuntimeIdentityTags();
            }
        }

        public string AuthoringNotes => _authoringNotes;
        public GameplayTagSet Tags => _tags ?? GameplayTagSet.Empty;

        public float MaxHealth
        {
            get
            {
                EnsureSoulStatsMigrated();
                return GameplayStatSystem.Evaluate(GameplayStatIds.MaxHealth, _healthStat.Max, gameObject);
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
                return GameplayStatSystem.Evaluate(GameplayStatIds.MaxStamina, _staminaStat.Max, gameObject);
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
                float max = MaxHealth;
                return max > 0f ? Mathf.Clamp01(Health / max) : 0f;
            }
        }

        public float StaminaNorm
        {
            get
            {
                EnsureSoulStatsMigrated();
                float max = MaxStamina;
                return max > 0f ? Mathf.Clamp01(Stamina / max) : 0f;
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
            EnsureIdentityMigration();
            EnsureOwnerId();
            RefreshRuntimeIdentityTags();
            OwnerRegistry.Register(OwnerId, gameObject);
            ClampVitals();
        }

        private void OnEnable()
        {
            EnsureIdentityMigration();
            EnsureOwnerId();
            RefreshRuntimeIdentityTags();
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
            EnsureIdentityMigration();
            _characterName = _characterName?.Trim() ?? string.Empty;
            _tags ??= new GameplayTagSet();
            _tags.Normalize();
            RefreshRuntimeIdentityTags();
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
            if (_entityType == EntityType.Player || gameObject.CompareTag("Player"))
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

        private void EnsureIdentityMigration()
        {
            if ((int)_entityType == 2)
            {
                _entityType = EntityType.NPC;
                _isHostile = true;
            }

            if (!_identityMigratedV2)
            {
                if (_npcArchetype == NPCArchetype.QuestGiver)
                    _canTrade = true;
                if (_npcArchetype == NPCArchetype.Bandit)
                    _isHostile = true;

                _identityMigratedV2 = true;
            }
        }

        private void RefreshRuntimeIdentityTags()
        {
            _tags ??= new GameplayTagSet();
            _tags.RemoveRuntimeTagPath(_entityType == EntityType.Player ? "Actor.NPC" : "Actor.Player");
            _tags.AddRuntimeTagPath(_entityType == EntityType.Player ? "Actor.Player" : "Actor.NPC");
            _tags.RemoveRuntimeTagPath(_isHostile ? "Actor.Civilian" : "Actor.Hostile");
            _tags.AddRuntimeTagPath(_isHostile ? "Actor.Hostile" : "Actor.Civilian");
            if (_canTrade)
                _tags.AddRuntimeTagPath("Job.Trader");
            else
                _tags.RemoveRuntimeTagPath("Job.Trader");
        }
    }
}
