using System.Collections.Generic;
using Sol.AI;
using Sol.Grab;
using Sol.Player;
using UnityEngine;

namespace Sol.Combat
{
    [AddComponentMenu("Sol/Combat/Combatant")]
    [DisallowMultipleComponent]
    public class Combatant : MonoBehaviour
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
        private Equipment _equipment;
        private float _lastStaminaSpendTime = float.NegativeInfinity;

        public PlayerSoul PlayerSoul => _playerSoul;
        public NPCSoul NpcSoul => _npcSoul;
        public Equipment Equipment => _equipment;
        public bool IsPlayer => _playerSoul != null;
        public bool IsNpc => _npcSoul != null;

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

        public void RefreshReferences()
        {
            if (_playerSoul == null)
                _playerSoul = GetComponent<PlayerSoul>();
            if (_npcSoul == null)
                _npcSoul = GetComponent<NPCSoul>();
            if (_equipment == null)
                _equipment = GetComponent<Equipment>();
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

        public float GetArmorRating()
        {
            RefreshReferences();
            if (_equipment == null)
                return 0f;

            float armor = 0f;
            _armorScratch.Clear();
            foreach (KeyValuePair<EquipmentSlotType, ItemComponent> pair in _equipment.Equipped)
            {
                ItemComponent item = pair.Value;
                if (item == null || !_armorScratch.Add(item))
                    continue;

                if (item.Type == ItemType.Armor)
                    armor += Mathf.Max(0f, item.Defense);
            }

            return armor;
        }

        public bool TryGetEquippedWeapon(out ItemComponent weapon, out EquipmentSlotType slot)
        {
            RefreshReferences();
            weapon = null;
            slot = default;
            if (_equipment == null)
                return false;

            if (TryGetWeaponInSlot(EquipmentSlotType.RightHand, out weapon))
            {
                slot = EquipmentSlotType.RightHand;
                return true;
            }

            if (TryGetWeaponInSlot(EquipmentSlotType.LeftHand, out weapon))
            {
                slot = EquipmentSlotType.LeftHand;
                return true;
            }

            foreach (KeyValuePair<EquipmentSlotType, ItemComponent> pair in _equipment.Equipped)
            {
                ItemComponent candidate = pair.Value;
                if (candidate == null || candidate.Type != ItemType.Weapon)
                    continue;

                weapon = candidate;
                slot = pair.Key;
                return true;
            }

            return false;
        }

        public CombatAttackKind GetAttackKind(ItemComponent weapon)
        {
            if (weapon == null)
                return CombatAttackKind.Unarmed;

            return weapon.WeaponHanding == WeaponHanding.TwoHanded
                ? CombatAttackKind.TwoHanded
                : CombatAttackKind.OneHanded;
        }

        public float GetAttackStaminaCost(ItemComponent weapon)
        {
            return GetAttackKind(weapon) switch
            {
                CombatAttackKind.TwoHanded => Mathf.Max(0f, _twoHandedStaminaCost),
                CombatAttackKind.OneHanded => Mathf.Max(0f, _oneHandedStaminaCost),
                _ => Mathf.Max(0f, _unarmedStaminaCost)
            };
        }

        public float GetAttackBaseDamage(ItemComponent weapon, float fallbackDamage)
        {
            if (weapon != null && weapon.Damage > 0f)
                return weapon.Damage;

            return Mathf.Max(0f, fallbackDamage);
        }

        public CombatHit CreateMeleeHit(Combatant target, float fallbackDamage, Vector3 hitDirection)
        {
            TryGetEquippedWeapon(out ItemComponent weapon, out _);
            CombatAttackKind attackKind = GetAttackKind(weapon);
            float staminaCost = GetAttackStaminaCost(weapon);
            float baseDamage = GetAttackBaseDamage(weapon, fallbackDamage);
            return new CombatHit(this, target, weapon, baseDamage, fallbackDamage, staminaCost, attackKind, hitDirection);
        }

        public static Combatant ResolveOrAdd(GameObject actor)
        {
            if (actor == null)
                return null;

            Combatant combatant = actor.GetComponent<Combatant>();
            if (combatant == null)
                combatant = actor.AddComponent<Combatant>();

            combatant.RefreshReferences();
            return combatant;
        }

        public static Combatant ResolveOrAdd(Transform target)
        {
            if (target == null)
                return null;

            Combatant combatant = target.GetComponentInParent<Combatant>();
            if (combatant != null)
            {
                combatant.RefreshReferences();
                return combatant;
            }

            PlayerSoul playerSoul = target.GetComponentInParent<PlayerSoul>();
            if (playerSoul != null)
                return ResolveOrAdd(playerSoul.gameObject);

            NPCSoul npcSoul = target.GetComponentInParent<NPCSoul>();
            if (npcSoul != null)
                return ResolveOrAdd(npcSoul.gameObject);

            return null;
        }

        private bool TryGetWeaponInSlot(EquipmentSlotType slot, out ItemComponent weapon)
        {
            weapon = null;
            if (_equipment == null || !_equipment.Equipped.TryGetValue(slot, out ItemComponent item))
                return false;

            if (item == null || item.Type != ItemType.Weapon)
                return false;

            weapon = item;
            return true;
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
