using System;
using System.Collections.Generic;
using UnityEngine;
using Sol.Grab;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sol
{
    public partial class Inventory : MonoBehaviour
    {

        public bool HasItemNamed(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return false;

            string target = itemName.Trim();
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot?.Item == null)
                    continue;

                if (string.Equals(slot.Item.ItemName, target, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }


        private static bool AreStackCompatible(ItemComponent existingItem, ItemComponent incomingItem)
        {
            if (existingItem == null || incomingItem == null)
                return false;

            if (!existingItem.IsStackable || !incomingItem.IsStackable)
                return false;

            bool existingHasId = TryGetNormalizedItemId(existingItem, out string existingId);
            bool incomingHasId = TryGetNormalizedItemId(incomingItem, out string incomingId);
            if (existingHasId || incomingHasId)
                return existingHasId
                    && incomingHasId
                    && string.Equals(existingId, incomingId, StringComparison.OrdinalIgnoreCase);

            string existingName = existingItem.ItemName?.Trim() ?? string.Empty;
            string incomingName = incomingItem.ItemName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(existingName) || string.IsNullOrEmpty(incomingName))
                return false;

            return existingItem.Type == incomingItem.Type
                && existingItem.MaxStackSize == incomingItem.MaxStackSize
                && string.Equals(existingName, incomingName, StringComparison.OrdinalIgnoreCase);
        }


        private static bool TryGetNormalizedItemId(ItemComponent item, out string normalizedItemId)
        {
            normalizedItemId = item == null ? string.Empty : NormalizeItemIdOrEmpty(item.ItemId);
            return !string.IsNullOrEmpty(normalizedItemId);
        }


        /// <summary>Check if inventory contains at least one item of the given type.</summary>
        public bool Has(ItemType type)
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].Item != null && _slots[i].Item.Type == type) return true;
            return false;
        }


        /// <summary>Find the first slot matching the given item type.</summary>
        public InventorySlot Find(ItemType type)
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].Item != null && _slots[i].Item.Type == type) return _slots[i];
            return null;
        }


        private InventorySlot FindByName(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return null;

            string target = itemName.Trim();
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Item != null && string.Equals(_slots[i].Item.ItemName, target, StringComparison.OrdinalIgnoreCase))
                    return _slots[i];
            }

            return null;
        }


        private InventorySlot FindByItemId(string itemId)
        {
            string target = NormalizeItemIdOrEmpty(itemId);
            if (string.IsNullOrEmpty(target))
                return null;

            for (int i = 0; i < _slots.Count; i++)
            {
                ItemComponent item = _slots[i].Item;
                if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
                    continue;

                if (string.Equals(item.ItemId, target, StringComparison.OrdinalIgnoreCase))
                    return _slots[i];
            }

            return null;
        }



        private int GetCapacityUsedSlotCount()
        {
            if (_slots.Count == 0)
                return 0;

            Equipment equipment = GetComponent<Equipment>();
            if (equipment == null)
                return _slots.Count;

            int used = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                InventorySlot slot = _slots[i];
                if (slot?.Item == null)
                    continue;

                if (equipment.IsEquipped(slot.Item))
                    continue;

                used++;
            }

            return used;
        }


        private static string NormalizeItemIdOrEmpty(string rawItemId)
        {
            return EntityCodeUtility.NormalizeOrEmpty(rawItemId, EntityCodeUtility.ItemPrefix);
        }
    }
}
