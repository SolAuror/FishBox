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

        public void Lock(int lockLevel = 0)
        {
            if (!IsWorldContainer) return;
            _isLocked = true;
            _lockLevel = Mathf.Max(0, lockLevel);
        }


        public void SetRequiredKey(string itemName)
        {
            if (!IsWorldContainer) return;
            _requiredKeyItemName = string.IsNullOrWhiteSpace(itemName) ? string.Empty : itemName.Trim();
#if UNITY_EDITOR
            SyncRequiredKeyFieldsFromAuthoringData();
#endif
        }


        public void SetRequiredKeyId(string itemId)
        {
            if (!IsWorldContainer) return;
            _requiredKeyItemId = NormalizeItemIdOrEmpty(itemId);
#if UNITY_EDITOR
            SyncRequiredKeyFieldsFromAuthoringData();
#endif
        }


        public void SetRequiredKey(ItemComponent keyItem)
        {
            if (!IsWorldContainer) return;
            if (keyItem == null || keyItem.Type != ItemType.Key)
            {
                _requiredKeyItemName = string.Empty;
                _requiredKeyItemId = string.Empty;
                return;
            }

            _requiredKeyItemName = string.IsNullOrWhiteSpace(keyItem.ItemName)
                ? string.Empty
                : keyItem.ItemName.Trim();
            _requiredKeyItemId = NormalizeItemIdOrEmpty(keyItem.ItemId);
        }


        public void Unlock()
        {
            if (!IsWorldContainer) return;
            _isLocked = false;
            _lockLevel = 0;
        }


        public bool TryUnlock(int skillLevel)
        {
            if (!IsWorldContainer || !_isLocked) return true;
            if (skillLevel < _lockLevel) return false;
            Unlock();
            return true;
        }


        public bool IsOwnedBy(GameObject actor)
        {
            return ItemOwnershipUtility.IsOwnedBy(OwnerId, actor);
        }


        public bool HasRequiredKey(Interactor interactor)
        {
            if (interactor?.Inventory == null)
                return false;

            return !string.IsNullOrEmpty(GetUsableKeyLabel(interactor));
        }


        public string GetUsableKeyLabel(Interactor interactor)
        {
            if (interactor?.Inventory == null)
                return string.Empty;

            if (interactor.Inventory.TryGetKeyDisplayNameByItemId(SkeletonKeyItemId, out string skeletonKeyName))
                return skeletonKeyName;

            if (interactor.Inventory.HasNamedKey("Skeleton Key"))
                return "Skeleton Key";

            if (!string.IsNullOrWhiteSpace(_requiredKeyItemId))
            {
                if (interactor.Inventory.TryGetKeyDisplayNameByItemId(_requiredKeyItemId, out string keyByIdName))
                    return keyByIdName;
            }

            if (string.IsNullOrWhiteSpace(_requiredKeyItemName))
                return string.Empty;

            return interactor.Inventory.HasNamedKey(_requiredKeyItemName)
                ? _requiredKeyItemName
                : string.Empty;
        }


        public bool HasLockpick()
        {
            return HasItemByItemId(LockpickItemId)
                || HasItemNamed("Lockpick")
                || HasItemNamed("Lock Pick");
        }


        public bool HasNamedKey(string keyName)
        {
            if (string.IsNullOrWhiteSpace(keyName))
                return false;

            string target = keyName.Trim();
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot?.Item == null)
                    continue;

                if (slot.Item.Type != ItemType.Key)
                    continue;

                if (string.Equals(slot.Item.ItemName, target, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }


        public bool HasSkeletonKey()
        {
            return HasKeyByItemId(SkeletonKeyItemId) || HasNamedKey("Skeleton Key");
        }


        public bool HasKeyByItemId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            InventorySlot slot = FindByItemId(itemId);
            return slot?.Item != null && slot.Item.Type == ItemType.Key;
        }


        public bool TryConsumeLockpick()
        {
            InventorySlot slot = FindByItemId(LockpickItemId)
                ?? FindByName("Lockpick")
                ?? FindByName("Lock Pick");

            if (slot == null)
                return false;

            return Remove(slot, 1);
        }


        public InventoryAccessResult EvaluateAccess(Interactor interactor, bool enforceOwnership = true)
        {
            if (interactor == null || interactor.Owner == null)
                return InventoryAccessResult.InvalidInteractor;

            if (!IsWorldContainer)
                return InventoryAccessResult.Allowed;

            if (_isLocked && !HasRequiredKey(interactor))
                return InventoryAccessResult.Locked;

            if (enforceOwnership && HasOwner && !IsOwnedBy(interactor.Owner))
                return InventoryAccessResult.NotOwner;

            return InventoryAccessResult.Allowed;
        }


        public bool CanAccess(Interactor interactor, bool enforceOwnership = true)
        {
            return EvaluateAccess(interactor, enforceOwnership) == InventoryAccessResult.Allowed;
        }


        private bool HasItemByItemId(string itemId)
        {
            return FindByItemId(itemId)?.Item != null;
        }


        private bool TryGetKeyDisplayNameByItemId(string itemId, out string displayName)
        {
            displayName = string.Empty;
            InventorySlot keySlot = FindByItemId(itemId);
            if (keySlot?.Item == null || keySlot.Item.Type != ItemType.Key)
                return false;

            displayName = keySlot.Item.ItemName;
            return true;
        }
    }
}
