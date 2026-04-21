using UnityEngine;
using Sol.Grab;

namespace Sol
{
    public static class ItemActionSystem
    {
        public static bool CanExecute(ItemActionType action, InventorySlot slot, Inventory inventory, Interactor instigator)
        {
            if (slot == null || slot.Item == null || inventory == null || instigator == null)
                return false;

            ItemComponent item = slot.Item;
            if (!ItemTypeRules.SupportsAction(item.Type, item.IsConsumable, action))
                return false;

            if (action != ItemActionType.Equip)
                return true;

            if (instigator.Owner == null || !instigator.Owner.TryGetComponent(out Equipment equipment))
                return false;

            return equipment.CanEquip(item) || equipment.IsEquipped(item);
        }

        public static bool Execute(ItemActionType action, InventorySlot slot, Inventory inventory, Interactor instigator)
        {
            if (!CanExecute(action, slot, inventory, instigator))
                return false;

            return action switch
            {
                ItemActionType.Use => ExecuteUse(slot, inventory, instigator),
                ItemActionType.Equip => ExecuteEquip(slot, inventory, instigator),
                ItemActionType.Drop => ExecuteDrop(slot, inventory, instigator),
                _ => false
            };
        }

        private static bool ExecuteUse(InventorySlot slot, Inventory inventory, Interactor instigator)
        {
            return inventory.Use(slot, instigator);
        }

        private static bool ExecuteEquip(InventorySlot slot, Inventory inventory, Interactor instigator)
        {
            Equipment equipment = instigator.Owner.GetComponent<Equipment>();
            if (equipment == null)
                return false;

            ItemComponent item = slot.Item;
            if (equipment.IsEquipped(item))
            {
                equipment.UnequipItem(item);
                return true;
            }

            return equipment.Equip(item);
        }

        private static bool ExecuteDrop(InventorySlot slot, Inventory inventory, Interactor instigator)
        {
            if (slot?.Item == null)
                return false;

            Equipment equipment = instigator.Owner.GetComponent<Equipment>();
            if (equipment != null && equipment.IsEquipped(slot.Item))
            {
                ItemComponent equippedItem = equipment.DetachForDrop(slot.Item);
                if (equippedItem == null)
                    return false;

                if (!slot.TryRemoveItemReference(equippedItem))
                    return false;

                inventory.Remove(slot, 1);
                return true;
            }

            ItemComponent item = slot.PopItem();
            if (item == null)
                return false;

            inventory.Remove(slot, 1);

            Transform t = instigator.Transform;
            item.transform.SetParent(null);
            item.transform.position = t.position + t.forward * 1.5f + Vector3.up * 0.5f;
            item.transform.rotation = Quaternion.identity;
            item.gameObject.SetActive(true);

            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            Collider col = item.GetComponent<Collider>();
            if (col != null)
                col.enabled = true;

            return true;
        }
    }
}
