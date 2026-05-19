using System.Collections.Generic;
using Sol.AI;
using Sol.Grab;
using Sol.Player;
using Sol.Rpg;
using UnityEngine;

namespace Sol.Combat
{
    [AddComponentMenu("Sol/Combat/Combatant")]
    [DisallowMultipleComponent]
    public partial class Combatant : MonoBehaviour
    {
        public const float DefaultUnarmedStaminaCost = 8f;
        public const float DefaultOneHandedStaminaCost = 12f;
        public const float DefaultTwoHandedStaminaCost = 18f;
        public const float DefaultStaminaRegenPerSecond = 8f;
        public const float DefaultStaminaRegenDelay = 1.25f;

        [Header("Stamina")]
        [SerializeField] private float _unarmedStaminaCost = DefaultUnarmedStaminaCost;
        [SerializeField] private float _oneHandedStaminaCost = DefaultOneHandedStaminaCost;
        [SerializeField] private float _twoHandedStaminaCost = DefaultTwoHandedStaminaCost;
        [SerializeField] private float _staminaRegenPerSecond = DefaultStaminaRegenPerSecond;
        [SerializeField] private float _staminaRegenDelay = DefaultStaminaRegenDelay;

        private readonly HashSet<ItemComponent> _armorScratch = new();
        private PlayerSoul _playerSoul;
        private NPCSoul _npcSoul;
        private IActorVitals _vitals;
        private Equipment _equipment;
        private float _lastStaminaSpendTime = float.NegativeInfinity;

        public PlayerSoul PlayerSoul => _playerSoul;
        public NPCSoul NpcSoul => _npcSoul;
        public IActorVitals Vitals
        {
            get
            {
                RefreshReferences();
                return _vitals;
            }
        }
        public Equipment Equipment => _equipment;
        public bool IsPlayer => _playerSoul != null;
        public bool IsNpc => _npcSoul != null;

        private void Awake()
        {
            RefreshReferences();
        }

        private void OnEnable()
        {
            RefreshReferences();
        }

        private void Update()
        {
            RegenerateStamina();
        }
    }
}
