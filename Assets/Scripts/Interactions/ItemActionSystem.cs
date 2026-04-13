using UnityEngine;
using Sol.Grab;

namespace Sol
{
    /// <summary>
    /// Centralized execution path for all item actions.
    /// Called by UI, AI, or any system — never duplicated.
    /// </summary>
    public static class ItemActionSystem
    {
        /// <summary>
        /// Execute an action on an inventory slot. Returns true if the action succeeded.
        /// </summary>
        public static bool Execute(ItemActionType action, InventorySlot slot, Inventory inventory, Interactor instigator)
        {
            if (slot == null || slot.Item == null || inventory == null || instigator == null)
                return false;

            return action switch
            {
                ItemActionType.Use   => ExecuteUse(slot, inventory, instigator),
                ItemActionType.Equip => ExecuteEquip(slot, inventory, instigator),
                ItemActionType.Drop  => ExecuteDrop(slot, inventory, instigator),
                _                    => false
            };
        }

        private static bool ExecuteUse(InventorySlot slot, Inventory inventory, Interactor instigator)
        {
            return inventory.Use(slot, instigator);
        }

        private static bool ExecuteEquip(InventorySlot slot, Inventory inventory, Interactor instigator)
        {
            var equipment = instigator.Owner.GetComponent<Equipment>();
            if (equipment == null) return false;

            var item = slot.Item;

            // Toggle: if already equipped, unequip instead.
            if (equipment.IsEquipped(item))
            {
                equipment.UnequipItem(item);
                return true;
            }

            // Not yet equipped — equip but keep the inventory slot.
            return equipment.Equip(item);
        }

        private static bool ExecuteDrop(InventorySlot slot, Inventory inventory, Interactor instigator)
        {
            if (slot?.Item == null) return false;

            var equipment = instigator.Owner.GetComponent<Equipment>();

            if (equipment != null && equipment.IsEquipped(slot.Item))
            {
                // Item is on a bone — detach and let it fall from its current world position.
                equipment.DetachForDrop(slot.Item);
                inventory.Remove(slot, 1);
                return true;
            }

            // Pop the correct item reference (extras first, then primary).
            var item = slot.PopItem();
            inventory.Remove(slot, 1);

            // Place in front of the instigator.
            var t = instigator.Transform;
            item.transform.SetParent(null);
            item.transform.position = t.position + t.forward * 1.5f + Vector3.up * 0.5f;
            item.transform.rotation = Quaternion.identity;
            item.gameObject.SetActive(true);

            var rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            var col = item.GetComponent<Collider>();
            if (col != null) col.enabled = true;

            return true;
        }
    }
}
