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
        [Tooltip("Item id (registry-backed) added to this inventory at startup.")]
        [ItemIdDropdown]
        [SerializeField] private string _itemId = string.Empty;

        [Min(1)]
        public int Quantity = 1;

        public string ItemId
        {
            get => _itemId;
            set => _itemId = value;
        }
    }

    public enum InventoryContainerType
    {
        Inventory = 0,
        Container = 1
    }

    public enum InventoryAddOwnershipMode
    {
        ClaimInventoryOwner = 0,
        PreserveExistingOwner = 1
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
    public partial class Inventory : MonoBehaviour
    {
        private const string SkeletonKeyItemId = "ITM00006";
        private const string LockpickItemId = "ITM00007";
#region Inspector Settings

        [Header("Inventory")]
        [Tooltip("Inspector: tunes capacity.")]
        [SerializeField] private int _capacity = 30;
        [SerializeField] private int _gold = 0;
        [Tooltip("Inspector: tunes container type.")]
        [SerializeField] private InventoryContainerType _containerType = InventoryContainerType.Inventory;
        [Tooltip("Inspector: tunes inspector contents.")]
        [SerializeField] private List<InventorySeedEntry> _inspectorContents = new();

        [Header("World Container Security")]
        [Tooltip("Inspector: tunes owner.")]
        [SerializeField] private GameObject _owner;
        [SerializeField] private string _ownerId = string.Empty;
        [Tooltip("Inspector: tunes is locked.")]
        [SerializeField] private bool _isLocked = false;
        [SerializeField] private bool _isLockpickable = true;
        [Tooltip("Required lock skill level when opening without a key.")]
        [SerializeField] private int _lockLevel = 0;
        [Tooltip("Optional key item name in the interactor inventory that can open this while locked.")]
        [SerializeField] private string _requiredKeyItemName = string.Empty;
        [Tooltip("Optional key item id in the interactor inventory that can open this while locked.")]
        [ItemIdDropdown]
        [SerializeField] private string _requiredKeyItemId = string.Empty;
#endregion

        private readonly List<InventorySlot> _slots = new();
        private int _deferChangedDepth;
        private bool _changedDuringDefer;

        /// <summary>Read-only view of all slots.</summary>
        public IReadOnlyList<InventorySlot> Slots => _slots;
        public int Capacity => _capacity;
        public int Count => GetCapacityUsedSlotCount();
        public InventoryContainerType ContainerType => _containerType;
        public bool IsContainer => _containerType == InventoryContainerType.Container;
        public bool IsWorldContainer => IsContainer;
        public GameObject Owner
        {
            get
            {
                ResolveOwnerIdentity();
                return IsWorldContainer ? _owner : gameObject;
            }
        }
        public string OwnerId
        {
            get
            {
                ResolveOwnerIdentity();
                return IsWorldContainer
                    ? _ownerId
                    : OwnerRegistry.ResolveOwnerId(gameObject);
            }
        }
        public bool HasOwner => !string.IsNullOrWhiteSpace(OwnerId);
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

#if UNITY_EDITOR
#endif
    }
}

