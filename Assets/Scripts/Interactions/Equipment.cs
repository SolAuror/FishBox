using System;
using System.Collections.Generic;
using UnityEngine;
using Sol.Grab;
using Sol.Locomotion;

namespace Sol
{
    [Serializable]
    public class AuthoredEquippedItemEntry
    {
        public EquipmentSlotType Slot = EquipmentSlotType.RightHand;
        [ItemIdDropdown]
        public string ItemId = string.Empty;
        public bool AutoEquipOnStart = true;
    }

    [Serializable]
    public class RuntimeEquippedInspectorEntry
    {
        public EquipmentSlotType Slot = EquipmentSlotType.RightHand;
        [ItemIdDropdown]
        public string ItemId = string.Empty;
        public ItemComponent Item;
    }

    public readonly struct EquippedRuntimeEntry
    {
        public EquipmentSlotType Slot { get; }
        public ItemComponent Item { get; }

        public EquippedRuntimeEntry(EquipmentSlotType slot, ItemComponent item)
        {
            Slot = slot;
            Item = item;
        }
    }

    public class Equipment : MonoBehaviour
    {
        [Header("Authored Equipped Items")]
        [SerializeField] private List<AuthoredEquippedItemEntry> _authoredEquippedItems = new();
        [Header("Runtime Equipped Snapshot (Read-Only)")]
        [SerializeField] private List<RuntimeEquippedInspectorEntry> _runtimeEquippedItems = new();

        private readonly Dictionary<EquipmentSlotType, ItemComponent> _equipped = new();
        private readonly Dictionary<ItemComponent, EquipmentSlotType> _itemPrimarySlots = new();
        private readonly List<EquipmentSlotType> _slotScratch = new();
        private Dictionary<string, Transform> _boneCache;
        private LocomotionIK _locoIK;

        public IReadOnlyDictionary<EquipmentSlotType, ItemComponent> Equipped => _equipped;
        public IReadOnlyList<AuthoredEquippedItemEntry> AuthoredEquippedItems => _authoredEquippedItems;

        public event Action OnChanged;

        private void Awake()
        {
            _locoIK = GetComponent<LocomotionIK>();
            ApplyAuthoredEquippedItems();
            RefreshRuntimeInspectorList();
        }

        private void OnValidate()
        {
            RefreshRuntimeInspectorList();
        }

        public bool CanEquip(ItemComponent item, EquipmentSlotType? preferredSlot = null)
        {
            if (!TryResolveRequiredSlots(item, preferredSlot, out _))
                return false;

            for (int i = 0; i < _slotScratch.Count; i++)
            {
                EquipmentSlotType slot = _slotScratch[i];
                if (_equipped.TryGetValue(slot, out ItemComponent occupant) && occupant != item)
                    return false;
            }

            return true;
        }

        public bool Equip(ItemComponent item, EquipmentSlotType? preferredSlot = null)
        {
            if (!TryResolveRequiredSlots(item, preferredSlot, out EquipmentSlotType primarySlot))
                return false;

            if (IsEquipped(item))
            {
                bool sameSlots = true;
                for (int i = 0; i < _slotScratch.Count; i++)
                {
                    EquipmentSlotType slot = _slotScratch[i];
                    if (!_equipped.TryGetValue(slot, out ItemComponent current) || current != item)
                    {
                        sameSlots = false;
                        break;
                    }
                }

                if (sameSlots)
                    return true;

                UnequipItem(item);
                if (!TryResolveRequiredSlots(item, preferredSlot, out primarySlot))
                    return false;
            }

            for (int i = 0; i < _slotScratch.Count; i++)
            {
                EquipmentSlotType slot = _slotScratch[i];
                if (_equipped.TryGetValue(slot, out ItemComponent occupant) && occupant != null && occupant != item)
                    return false;
            }

            Transform bone = FindBone(item.EquipBone);
            if (bone == null)
            {
                Debug.LogWarning($"[Equipment] Bone '{item.EquipBone}' not found on {name}.");
                return false;
            }

            AttachToBone(item, bone);
            SetEquippedPhysics(item, equipped: true);

            for (int i = 0; i < _slotScratch.Count; i++)
                _equipped[_slotScratch[i]] = item;

            _itemPrimarySlots[item] = primarySlot;
            NotifyChanged();
            return true;
        }

        public ItemComponent Unequip(EquipmentSlotType slot)
        {
            if (!_equipped.TryGetValue(slot, out ItemComponent item) || item == null)
                return null;

            RemoveAllSlotBindingsForItem(item);

            item.transform.SetParent(null);
            SetEquippedPhysics(item, equipped: false);
            item.gameObject.SetActive(false);

            NotifyChanged();
            return item;
        }

        public ItemComponent DetachForDrop(ItemComponent item)
        {
            if (item == null || !IsEquipped(item))
                return null;

            RemoveAllSlotBindingsForItem(item);

            item.transform.SetParent(null);
            SetWorldDropPhysics(item);

            NotifyChanged();
            return item;
        }

        public bool IsSlotOccupied(EquipmentSlotType slot) => _equipped.ContainsKey(slot);

        public bool IsEquipped(ItemComponent item)
        {
            if (item == null)
                return false;

            foreach (KeyValuePair<EquipmentSlotType, ItemComponent> kv in _equipped)
            {
                if (kv.Value == item)
                    return true;
            }

            return false;
        }

        public bool UnequipItem(ItemComponent item)
        {
            if (item == null)
                return false;

            foreach (KeyValuePair<EquipmentSlotType, ItemComponent> kv in _equipped)
            {
                if (kv.Value != item)
                    continue;

                Unequip(kv.Key);
                return true;
            }

            return false;
        }


        public bool TryGetPrimarySlot(ItemComponent item, out EquipmentSlotType slot)
        {
            if (item != null && _itemPrimarySlots.TryGetValue(item, out slot))
                return true;

            if (item != null)
            {
                foreach (KeyValuePair<EquipmentSlotType, ItemComponent> kv in _equipped)
                {
                    if (kv.Value != item)
                        continue;

                    slot = kv.Key;
                    return true;
                }
            }

            slot = default;
            return false;
        }        public IReadOnlyList<EquippedRuntimeEntry> GetEquippedEntries()
        {
            List<EquippedRuntimeEntry> entries = new();
            foreach (KeyValuePair<ItemComponent, EquipmentSlotType> pair in _itemPrimarySlots)
            {
                if (pair.Key == null)
                    continue;

                entries.Add(new EquippedRuntimeEntry(pair.Value, pair.Key));
            }

            return entries;
        }

        private void ApplyAuthoredEquippedItems()
        {
            if (_authoredEquippedItems == null || _authoredEquippedItems.Count == 0)
                return;

            Inventory inventory = GetComponent<Inventory>();
            if (inventory == null)
                return;

            for (int i = 0; i < _authoredEquippedItems.Count; i++)
            {
                AuthoredEquippedItemEntry entry = _authoredEquippedItems[i];
                if (entry == null || !entry.AutoEquipOnStart || string.IsNullOrWhiteSpace(entry.ItemId))
                    continue;

                ItemComponent prefab = ResolveAuthoredItemPrefab(entry.ItemId);
                if (prefab == null)
                    continue;

                ItemComponent runtimeItem = EnsureRuntimeItem(prefab, inventory);
                if (runtimeItem == null)
                    continue;

                Equip(runtimeItem, entry.Slot);
            }
        }

        private static ItemComponent ResolveAuthoredItemPrefab(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            ItemRegistry registry = ItemRegistry.Get();
            if (registry == null)
                return null;

            return registry.GetPrefab(itemId);
        }

        private ItemComponent EnsureRuntimeItem(ItemComponent item, Inventory inventory)
        {
            if (item == null || inventory == null)
                return null;

            ItemComponent runtimeItem = item;
            if (!runtimeItem.gameObject.scene.IsValid())
                runtimeItem = Instantiate(runtimeItem, inventory.transform);

            if (!InventoryContainsItem(inventory, runtimeItem))
            {
                runtimeItem.gameObject.SetActive(false);
                if (!inventory.Add(runtimeItem))
                {
                    if (runtimeItem != item)
                        Destroy(runtimeItem.gameObject);
                    return null;
                }
            }

            return runtimeItem;
        }

        private static bool InventoryContainsItem(Inventory inventory, ItemComponent item)
        {
            if (inventory == null || item == null)
                return false;

            IReadOnlyList<InventorySlot> slots = inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                if (slot == null)
                    continue;

                foreach (ItemComponent candidate in slot.EnumerateItems())
                {
                    if (candidate == item)
                        return true;
                }
            }

            return false;
        }

        private bool TryResolveRequiredSlots(ItemComponent item, EquipmentSlotType? preferredSlot, out EquipmentSlotType primarySlot)
        {
            primarySlot = default;
            if (item == null)
                return false;

            EquipmentSlotType desiredSlot;
            if (preferredSlot.HasValue)
            {
                desiredSlot = preferredSlot.Value;
            }
            else if (!ItemTypeRules.TryResolveDefaultSlot(item, out desiredSlot))
            {
                return false;
            }

            if (!ItemTypeRules.GetRequiredSlotsForEquip(item, desiredSlot, _slotScratch))
                return false;

            primarySlot = desiredSlot;
            return true;
        }

        private void RemoveAllSlotBindingsForItem(ItemComponent item)
        {
            if (item == null)
                return;

            List<EquipmentSlotType> toRemove = new();
            foreach (KeyValuePair<EquipmentSlotType, ItemComponent> kv in _equipped)
            {
                if (kv.Value == item)
                    toRemove.Add(kv.Key);
            }

            for (int i = 0; i < toRemove.Count; i++)
                _equipped.Remove(toRemove[i]);

            _itemPrimarySlots.Remove(item);
        }

        private void AttachToBone(ItemComponent item, Transform bone)
        {
            Transform t = item.transform;
            Vector3 worldScale = t.lossyScale;
            t.SetParent(bone);
            t.localPosition = item.EquipOffset;
            t.localRotation = Quaternion.Euler(item.EquipRotation);

            Vector3 parentScale = bone.lossyScale;
            t.localScale = new Vector3(
                parentScale.x != 0f ? worldScale.x / parentScale.x : 1f,
                parentScale.y != 0f ? worldScale.y / parentScale.y : 1f,
                parentScale.z != 0f ? worldScale.z / parentScale.z : 1f);
            item.gameObject.SetActive(true);
        }

        private void SetEquippedPhysics(ItemComponent item, bool equipped)
        {
            if (item == null)
                return;

            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
                rb.interpolation = equipped ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
                rb.position = item.transform.position;
                rb.rotation = item.transform.rotation;
            }

            Collider col = item.GetComponent<Collider>();
            if (col != null)
                col.enabled = false;

            if (equipped)
            {
                foreach (Light lt in item.GetComponentsInChildren<Light>(includeInactive: true))
                    lt.enabled = false;
            }
        }

        private void SetWorldDropPhysics(ItemComponent item)
        {
            if (item == null)
                return;

            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.position = item.transform.position;
                rb.rotation = item.transform.rotation;
            }

            Collider col = item.GetComponent<Collider>();
            if (col != null)
                col.enabled = true;
        }

        private void NotifyChanged()
        {
            RefreshRuntimeInspectorList();
            RefreshHandIk();
            OnChanged?.Invoke();
        }

        private void RefreshRuntimeInspectorList()
        {
            if (_runtimeEquippedItems == null)
                _runtimeEquippedItems = new List<RuntimeEquippedInspectorEntry>();

            _runtimeEquippedItems.Clear();
            foreach (KeyValuePair<EquipmentSlotType, ItemComponent> kv in _equipped)
            {
                _runtimeEquippedItems.Add(new RuntimeEquippedInspectorEntry
                {
                    Slot = kv.Key,
                    ItemId = kv.Value != null ? kv.Value.ItemId : string.Empty,
                    Item = kv.Value,
                });
            }
        }

        private void RefreshHandIk()
        {
            if (_locoIK == null)
                return;

            ItemComponent ikItem = null;
            if (_equipped.TryGetValue(EquipmentSlotType.RightHand, out ItemComponent right))
                ikItem = right;
            else if (_equipped.TryGetValue(EquipmentSlotType.LeftHand, out ItemComponent left))
                ikItem = left;

            if (ikItem != null)
            {
                _locoIK.SetHandIKTarget(false, ikItem.transform.position, ikItem.transform.rotation);
                return;
            }

            _locoIK.ClearHandIKTarget(false);
        }

        private Transform FindBone(string boneName)
        {
            if (string.IsNullOrEmpty(boneName))
                return null;

            if (_boneCache == null)
            {
                Transform[] bones = GetComponentsInChildren<Transform>();
                _boneCache = new Dictionary<string, Transform>(bones.Length);
                for (int i = 0; i < bones.Length; i++)
                    _boneCache[bones[i].name] = bones[i];
            }

            return _boneCache.TryGetValue(boneName, out Transform bone) ? bone : null;
        }
    }
}

