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

        private void Awake()
        {
            ResolveOwnerIdentity();
            SeedFromInspector();
            NormalizeGoldSlots();
            SyncContainedItemOwnersToContainer();
        }


        public void BeginBulkUpdate()
        {
            _deferChangedDepth++;
        }


        public void EndBulkUpdate()
        {
            if (_deferChangedDepth <= 0)
                return;

            _deferChangedDepth--;
            if (_deferChangedDepth == 0 && _changedDuringDefer)
            {
                _changedDuringDefer = false;
                OnChanged?.Invoke();
            }
        }


        public void SetContainerType(InventoryContainerType containerType)
        {
            if (_containerType == containerType) return;

            _containerType = containerType;
            if (!IsWorldContainer)
            {
                _isLocked = false;
                _isLockpickable = true;
                _lockLevel = 0;
                _requiredKeyItemName = string.Empty;
                _requiredKeyItemId = string.Empty;
            }
        }


        private void OnValidate()
        {
            _capacity = Mathf.Max(1, _capacity);
            _gold = Mathf.Max(0, _gold);
            ResolveOwnerIdentity();
            _lockLevel = Mathf.Max(0, _lockLevel);
            _requiredKeyItemName = _requiredKeyItemName?.Trim() ?? string.Empty;
            _requiredKeyItemId = NormalizeItemIdOrEmpty(_requiredKeyItemId);

            if (_inspectorContents != null)
            {
                for (int i = 0; i < _inspectorContents.Count; i++)
                {
                    InventorySeedEntry entry = _inspectorContents[i];
                    if (entry == null)
                        continue;

                    entry.Quantity = Mathf.Max(1, entry.Quantity);

#pragma warning disable CS0618
                    if (string.IsNullOrWhiteSpace(entry.ItemId) && entry.Item != null && !string.IsNullOrWhiteSpace(entry.Item.ItemId))
                        entry.ItemId = entry.Item.ItemId;
#pragma warning restore CS0618
                }
            }

            if (!IsWorldContainer)
            {
                _isLocked = false;
                _isLockpickable = true;
                _lockLevel = 0;
                _requiredKeyItemName = string.Empty;
                _requiredKeyItemId = string.Empty;
            }

#if UNITY_EDITOR
            if (IsWorldContainer)
                SyncRequiredKeyFieldsFromAuthoringData();
#endif
        }


        private void SeedFromInspector()
        {
            if (_slots.Count > 0 || _inspectorContents == null || _inspectorContents.Count == 0)
                return;

            for (int i = 0; i < _inspectorContents.Count; i++)
            {
                var entry = _inspectorContents[i];
                if (entry == null)
                    continue;

                ItemComponent template = ResolveSeedTemplate(entry);
                if (template == null)
                    continue;

                int quantity = Mathf.Max(1, entry.Quantity);
                for (int j = 0; j < quantity; j++)
                {
                    var runtimeItem = Instantiate(template, transform);
                    runtimeItem.gameObject.SetActive(false);

                    if (!Add(runtimeItem))
                    {
                        Destroy(runtimeItem.gameObject);
                        break;
                    }
                }
            }
        }

        private static ItemComponent ResolveSeedTemplate(InventorySeedEntry entry)
        {
            if (entry == null)
                return null;

            if (!string.IsNullOrWhiteSpace(entry.ItemId))
            {
                ItemRegistry registry = ItemRegistry.Get();
                ItemComponent prefab = registry != null ? registry.GetPrefab(entry.ItemId) : null;
                if (prefab != null)
                    return prefab;
            }

#pragma warning disable CS0618
            return entry.Item;
#pragma warning restore CS0618
        }


        private void ResolveOwnerIdentity()
        {
            if (!IsWorldContainer)
                return;

            if (_owner != null)
            {
                _ownerId = OwnerRegistry.ResolveOwnerId(_owner);
                return;
            }

            _ownerId = ItemOwnershipUtility.NormalizeOwnerIdOrEmpty(_ownerId);
        }


        private static string NormalizeOwnerIdOrEmpty(string rawOwnerId)
        {
            return ItemOwnershipUtility.NormalizeOwnerIdOrEmpty(rawOwnerId);
        }
    }
}
