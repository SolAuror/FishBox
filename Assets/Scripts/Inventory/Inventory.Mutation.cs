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

        /// <summary>Try to add an item. Returns true if it was added.</summary>
        public bool Add(ItemComponent item)
        {
            return AddInternal(item, convertGoldToBalance: true);
        }


        /// <summary>
        /// Add an item as a physical inventory entry, even if it is a Gold item.
        /// Used for corpse-loot itemization so gold can be visibly looted.
        /// </summary>
        public bool AddPhysical(ItemComponent item)
        {
            return AddInternal(item, convertGoldToBalance: false);
        }


        private bool AddInternal(ItemComponent item, bool convertGoldToBalance)
        {
            if (item == null) return false;

            if (convertGoldToBalance && item.Type == ItemType.Gold)
            {
                Gold += Mathf.Max(1, item.Value);
                if (item.gameObject != null)
                    UnityEngine.Object.Destroy(item.gameObject);
                return true;
            }

            ApplyContainerOwnerToItem(item);

            // Try stacking first.
            if (item.IsStackable)
            {
                for (int i = 0; i < _slots.Count; i++)
                {
                    var slot = _slots[i];
                    if (slot.Item == null) continue;
                    if (!AreStackCompatible(slot.Item, item))
                        continue;

                    int slotMaxStack = Mathf.Max(1, slot.Item.MaxStackSize);
                    if (slot.Count >= slotMaxStack)
                        continue;

                    slot.PushExtra(item);
                    slot.Count++;
                    NotifyChanged();
                    return true;
                }
            }

            // New slot.
            if (GetCapacityUsedSlotCount() >= _capacity) return false;

            _slots.Add(new InventorySlot(item));
            NotifyChanged();
            return true;
        }


        /// <summary>Remove amount from a slot. Removes the slot entirely when count reaches 0.</summary>
        public bool Remove(InventorySlot slot, int amount = 1)
        {
            if (slot == null || !_slots.Contains(slot)) return false;
            slot.Count -= amount;
            if (slot.Count <= 0)
            {
                _slots.Remove(slot);
                // Note: do NOT deregister the fish here. Remove is used for relocation
                // (drop to world, trade to another inventory) as well as destruction.
                // Deregistration must be tied to the fish actually being destroyed/consumed
                // (see Inventory.Use for the consumable path), otherwise the registry entry
 // vanishes while the world object still carries its FishCode - after a
                // save/load round-trip the fish's stats (size, weight, etc.) would be lost.
            }
            NotifyChanged();
            return true;
        }


        /// <summary>Use an item from a slot. Consumables apply stats and are removed.</summary>
        public bool Use(InventorySlot slot, Interactor interactor)
        {
            if (slot == null || slot.Item == null) return false;

            var item = slot.Item;

            if (item.CanUseFromInventory)
            {
                item.ApplyUseEffects(interactor);

                // Pop the actual item reference so stacked objects don't leak.
                var consumed = slot.PopItem();
                Remove(slot, 1);

                // Deregister caught fish instance if present (extra safety if Remove didn't do it)
                if (!string.IsNullOrEmpty(slot.FishCode))
                {
                    Sol.AI.FishRegistry.Instance.RemoveFish(slot.FishCode);
                }

                // Destroy the consumed world object.
                // For stacked items this is an extra copy; for the last item
                // consumed == slot.Item, but the slot has already been removed
                // from the list so the reference is orphaned - destroy it too.
                if (consumed != null)
                    UnityEngine.Object.Destroy(consumed.gameObject);

                return true;
            }

            return false;
        }


        private void ApplyContainerOwnerToItem(ItemComponent item)
        {
            if (item == null || !IsWorldContainer || !HasOwner)
                return;

            item.SetOwnerId(OwnerId);
        }


        private void SyncContainedItemOwnersToContainer()
        {
            if (!IsWorldContainer || !HasOwner)
                return;

            for (int i = 0; i < _slots.Count; i++)
            {
                InventorySlot slot = _slots[i];
                if (slot == null)
                    continue;

                foreach (ItemComponent item in slot.EnumerateItems())
                {
                    if (item != null)
                        item.SetOwnerId(OwnerId);
                }
            }
        }


        private void NormalizeGoldSlots()
        {
            if (_slots.Count == 0)
                return;

            int convertedGold = 0;
            var slotsToRemove = new List<InventorySlot>();

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot?.Item == null || slot.Item.Type != ItemType.Gold)
                    continue;

                foreach (var item in slot.EnumerateItems())
                {
                    if (item == null)
                        continue;

                    convertedGold += Mathf.Max(1, item.Value);
                    UnityEngine.Object.Destroy(item.gameObject);
                }

                slotsToRemove.Add(slot);
            }

            if (slotsToRemove.Count == 0)
                return;

            for (int i = 0; i < slotsToRemove.Count; i++)
                _slots.Remove(slotsToRemove[i]);

            _gold = Mathf.Max(0, _gold + convertedGold);
            NotifyChanged();
        }


        private void NotifyChanged()
        {
            if (_deferChangedDepth > 0)
            {
                _changedDuringDefer = true;
                return;
            }

            OnChanged?.Invoke();
        }


        /// <summary>
        /// Insert a pre-constructed slot, for special cases like uniquely caught fish with FishCode. Returns true if added.
        /// </summary>
        public bool AddSlot(InventorySlot slot)
        {
            if (slot == null) return false;
            if (GetCapacityUsedSlotCount() >= _capacity) return false;
            _slots.Add(slot);
            NotifyChanged();
            return true;
        }
    }
}
