using System.Collections.Generic;
using Sol.Grab;
using UnityEngine;

namespace Sol.Combat
{
    public partial class Combatant
    {
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
    }
}
