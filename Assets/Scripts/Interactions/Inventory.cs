using System;
using System.Collections.Generic;
using UnityEngine;
using Sol.Grab;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sol
{
    [Serializable]
    public class InventorySeedEntry
    {
        [Tooltip("Item prefab/reference to add to this inventory at startup.")]
        public ItemComponent Item;
        [Min(1)]
        public int Quantity = 1;
    }

    public enum InventoryContainerType
    {
        Inventory = 0,
        Container = 1
    }

    public enum InventoryAccessResult
    {
        Allowed = 0,
        InvalidInteractor = 1,
        Locked = 2,
        NotOwner = 3
    }

    /// <summary>
    /// Elder Scrolls-style list inventory.
    /// Actor inventories are standard "Inventory" type (player/NPC),
    /// while world containers can be locked and owned.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        private const string SkeletonKeyItemId = "ITM00006";
        private const string LockpickItemId = "ITM00007";

        [Header("Inventory")]
        [SerializeField] private int _capacity = 30;
        [SerializeField] private int _gold = 0;
        [SerializeField] private InventoryContainerType _containerType = InventoryContainerType.Inventory;
        [SerializeField] private List<InventorySeedEntry> _inspectorContents = new();

        [Header("World Container Security")]
        [SerializeField] private bool _isLocked = false;
        [SerializeField] private bool _isLockpickable = true;
        [Tooltip("Required lock skill level when opening without a key.")]
        [SerializeField] private int _lockLevel = 0;
        [Tooltip("Optional key item name in the interactor inventory that can open this while locked.")]
        [SerializeField] private string _requiredKeyItemName = string.Empty;
        [Tooltip("Optional key item id in the interactor inventory that can open this while locked.")]
        [SerializeField] private string _requiredKeyItemId = string.Empty;

        private readonly List<InventorySlot> _slots = new();
        private int _deferChangedDepth;
        private bool _changedDuringDefer;

        private void Awake()
        {
            SeedFromInspector();
            NormalizeGoldSlots();
            SyncContainedItemOwnersToContainer();
        }

        /// <summary>Read-only view of all slots.</summary>
        public IReadOnlyList<InventorySlot> Slots => _slots;
        public int Capacity => _capacity;
        public int Count => _slots.Count;
        public InventoryContainerType ContainerType => _containerType;
        public bool IsContainer => _containerType == InventoryContainerType.Container;
        public bool IsWorldContainer => IsContainer;
        public bool IsLocked => IsWorldContainer && _isLocked;
        public bool IsLockpickable => IsWorldContainer && _isLockpickable;
        public int LockLevel => IsWorldContainer ? Mathf.Max(0, _lockLevel) : 0;
        public string RequiredKeyItemName => IsWorldContainer ? _requiredKeyItemName : string.Empty;
        public string RequiredKeyItemId => IsWorldContainer ? _requiredKeyItemId : string.Empty;
        public bool RequiresKey => IsWorldContainer
            && (!string.IsNullOrWhiteSpace(_requiredKeyItemName) || !string.IsNullOrWhiteSpace(_requiredKeyItemId));

        /// <summary>Current gold balance.</summary>
        public int Gold
        {
            get => _gold;
            set
            {
                _gold = Mathf.Max(0, value);
                NotifyChanged();
            }
        }

        /// <summary>Raised whenever the contents change (add / remove / use).</summary>
        public event Action OnChanged;

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
            return true;
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

            return InventoryAccessResult.Allowed;
        }

        public bool CanAccess(Interactor interactor, bool enforceOwnership = true)
        {
            return EvaluateAccess(interactor, enforceOwnership) == InventoryAccessResult.Allowed;
        }

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
            if (_slots.Count >= _capacity) return false;

            _slots.Add(new InventorySlot(item));
            NotifyChanged();
            return true;
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

        /// <summary>Remove amount from a slot. Removes the slot entirely when count reaches 0.</summary>
        public bool Remove(InventorySlot slot, int amount = 1)
        {
            if (slot == null || !_slots.Contains(slot)) return false;
            slot.Count -= amount;
            if (slot.Count <= 0)
                _slots.Remove(slot);
            NotifyChanged();
            return true;
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

        /// <summary>Use an item from a slot. Consumables apply stats and are removed.</summary>
        public bool Use(InventorySlot slot, Interactor interactor)
        {
            if (slot == null || slot.Item == null) return false;

            var item = slot.Item;

            if (item.IsConsumable)
            {
                // Pop the actual item reference so stacked objects don't leak.
                var consumed = slot.PopItem();
                Remove(slot, 1);

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

        private void OnValidate()
        {
            _capacity = Mathf.Max(1, _capacity);
            _gold = Mathf.Max(0, _gold);
            _lockLevel = Mathf.Max(0, _lockLevel);
            _requiredKeyItemName = _requiredKeyItemName?.Trim() ?? string.Empty;
            _requiredKeyItemId = NormalizeItemIdOrEmpty(_requiredKeyItemId);

            if (_inspectorContents != null)
            {
                for (int i = 0; i < _inspectorContents.Count; i++)
                {
                    if (_inspectorContents[i] != null)
                        _inspectorContents[i].Quantity = Mathf.Max(1, _inspectorContents[i].Quantity);
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
                if (entry == null || entry.Item == null)
                    continue;

                int quantity = Mathf.Max(1, entry.Quantity);
                for (int j = 0; j < quantity; j++)
                {
                    var runtimeItem = Instantiate(entry.Item, transform);
                    runtimeItem.gameObject.SetActive(false);

                    if (!Add(runtimeItem))
                    {
                        Destroy(runtimeItem.gameObject);
                        break;
                    }
                }
            }
        }

        private void ApplyContainerOwnerToItem(ItemComponent item)
        {
        }

        private void SyncContainedItemOwnersToContainer()
        {
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

        private static string NormalizeItemIdOrEmpty(string rawItemId)
        {
            return EntityCodeUtility.NormalizeOrEmpty(rawItemId, EntityCodeUtility.ItemPrefix);
        }

#if UNITY_EDITOR
        private void SyncRequiredKeyFieldsFromAuthoringData()
        {
            _requiredKeyItemName = _requiredKeyItemName?.Trim() ?? string.Empty;
            _requiredKeyItemId = NormalizeItemIdOrEmpty(_requiredKeyItemId);

            if (string.IsNullOrEmpty(_requiredKeyItemId) && string.IsNullOrEmpty(_requiredKeyItemName))
                return;

            if (!string.IsNullOrEmpty(_requiredKeyItemId))
            {
                if (TryResolveKeyNameById(_requiredKeyItemId, out string keyNameById))
                {
                    _requiredKeyItemName = keyNameById;
                    return;
                }

                _requiredKeyItemId = string.Empty;
            }

            if (!string.IsNullOrEmpty(_requiredKeyItemName)
                && TryResolveKeyIdByName(_requiredKeyItemName, out string keyIdByName, out string canonicalName))
            {
                _requiredKeyItemId = keyIdByName;
                _requiredKeyItemName = canonicalName;
            }
        }

        private static bool TryResolveKeyNameById(string itemId, out string itemName)
        {
            itemName = string.Empty;
            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            string normalizedId = NormalizeItemIdOrEmpty(itemId);
            if (string.IsNullOrEmpty(normalizedId))
                return false;

            ItemComponent[] keyPrefabs = LoadAllKeyPrefabs();
            for (int i = 0; i < keyPrefabs.Length; i++)
            {
                ItemComponent key = keyPrefabs[i];
                if (key == null || !string.Equals(key.ItemId, normalizedId, StringComparison.OrdinalIgnoreCase))
                    continue;

                itemName = key.ItemName?.Trim() ?? string.Empty;
                return !string.IsNullOrEmpty(itemName);
            }

            return false;
        }

        private static bool TryResolveKeyIdByName(string itemName, out string itemId, out string canonicalName)
        {
            itemId = string.Empty;
            canonicalName = string.Empty;
            if (string.IsNullOrWhiteSpace(itemName))
                return false;

            string normalizedName = itemName.Trim();
            ItemComponent[] keyPrefabs = LoadAllKeyPrefabs();
            for (int i = 0; i < keyPrefabs.Length; i++)
            {
                ItemComponent key = keyPrefabs[i];
                if (key == null || !string.Equals(key.ItemName, normalizedName, StringComparison.OrdinalIgnoreCase))
                    continue;

                string normalizedId = NormalizeItemIdOrEmpty(key.ItemId);
                if (string.IsNullOrEmpty(normalizedId))
                    continue;

                itemId = normalizedId;
                canonicalName = key.ItemName?.Trim() ?? normalizedName;
                return true;
            }

            return false;
        }

        private static ItemComponent[] LoadAllKeyPrefabs()
        {
            List<ItemComponent> keys = new();
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabRoot == null)
                    continue;

                ItemComponent keyItem = prefabRoot.GetComponentInChildren<ItemComponent>(true);
                if (keyItem == null || keyItem.Type != ItemType.Key)
                    continue;

                keys.Add(keyItem);
            }

            return keys.ToArray();
        }
#endif

    }
}
