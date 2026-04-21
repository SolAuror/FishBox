using System.Collections.Generic;
using UnityEngine;
using Sol.Actions;
using Sol.Outline;

namespace Sol.Grab
{
    public enum ItemType
    {
        Consumable = 0,
        Weapon = 1,
        Armor = 2,
        Equipable = 3,
        Material = 4,
        Food = 5,
        Drink = 6,
        Potion = 7,
        Key = 8,
        QuestItem = 9,
        Miscellaneous = 10,
        Gold = 11
    }

    public enum EquipDomain
    {
        Auto = 0,
        Armor = 1,
        WeaponTool = 2
    }

    public enum WeaponHanding
    {
        OneHanded = 0,
        TwoHanded = 1
    }

    [RequireComponent(typeof(GrabbableComponent))]
    [RequireComponent(typeof(Collider))]
    public class ItemComponent : MonoBehaviour, IInteractable
    {
        #region Inspector Settings
        [Header("Item Info")]
        [Tooltip("Inspector: tunes item id.")]
        [SerializeField] private string _itemId = string.Empty;
        [SerializeField] private string _itemName = "Item";
        [Tooltip("Inspector: tunes item type.")]
        [SerializeField] private ItemType _itemType = ItemType.Material;
        [SerializeField] private int _value;
        [Tooltip("Inspector: tunes item owner.")]
        [SerializeField] [HideInInspector] private GameObject _itemOwner;
        [OwnerIdDropdown]
        [SerializeField] private string _itemOwnerId = string.Empty;
        [Tooltip("Inspector: tunes is stolen.")]
        [SerializeField] private bool _isStolen;
        [SerializeField] [TextArea] private string _flavourText = string.Empty;
        [Tooltip("Inspector: tunes icon.")]
        [SerializeField] private Sprite _icon;

        [Header("Properties")]
        [Tooltip("Inspector: tunes is stackable.")]
        [SerializeField] private bool _isStackable;
        [SerializeField] private bool _isConsumable;
        [Tooltip("Inspector: tunes is tradeable.")]
        [SerializeField] private bool _isTradeable = true;
        [Tooltip("Inspector: tunes max stack size.")]
        [SerializeField] private int _maxStackSize = 1;

        [Header("Weapon Stats")]
        [Tooltip("Inspector: tunes damage.")]
        [SerializeField] private float _damage;

        [Header("Armor Stats")]
        [Tooltip("Inspector: tunes defense.")]
        [SerializeField] private float _defense;

        [Header("Equipment")]
        [Tooltip("Bone/socket name to parent this item to when equipped")]
        [SerializeField] private string _equipBone = string.Empty;
        [SerializeField] private Vector3 _equipOffset;
        [SerializeField] private Vector3 _equipRotation;
        [SerializeField] private EquipDomain _equipDomain = EquipDomain.Auto;
        [SerializeField] private WeaponHanding _weaponHanding = WeaponHanding.OneHanded;
        [SerializeField] private List<EquipmentSlotType> _allowedEquipSlots = new();

        [Header("Pickup")]
        [Tooltip("Optional hand target used by the pickup reach. If unassigned, the system falls back to the item's collider or transform.")]
        [SerializeField] private Transform _pickupGrip;
        #endregion

        private GameObject _lastInteractorOwner;

        public string ItemId => _itemId;
        public string ItemName => _itemName;
        public ItemType Type => _itemType;
        public string TypeDisplayName => IsMiscellaneousType(_itemType) ? "Miscellaneous" : _itemType.ToString();
        public string ItemOwnerId
        {
            get
            {
                ResolveOwnerReference();
                return _itemOwnerId;
            }
        }
        public GameObject ItemOwner => ResolveOwnerReference();
        public bool HasOwner => !string.IsNullOrWhiteSpace(ItemOwnerId);
        public bool IsStolen => _isStolen;
        public int Value => _value;
        public string FlavourText => _flavourText;
        public Sprite Icon => _icon;
        public bool IsStackable => _isStackable;
        public bool IsConsumable => _isConsumable || ItemTypeRules.IsConsumableType(_itemType);
        public bool IsTradeable => _isTradeable;
        public int MaxStackSize => _maxStackSize;
        public float Damage => ItemTypeRules.UsesWeaponStats(_itemType) ? _damage : 0f;
        public float Defense => ItemTypeRules.UsesArmorStats(_itemType) ? _defense : 0f;
        public string EquipBone => ItemTypeRules.UsesEquipmentSettings(_itemType) ? _equipBone : string.Empty;
        public Vector3 EquipOffset => ItemTypeRules.UsesEquipmentSettings(_itemType) ? _equipOffset : Vector3.zero;
        public Vector3 EquipRotation => ItemTypeRules.UsesEquipmentSettings(_itemType) ? _equipRotation : Vector3.zero;
        public EquipDomain EquipCategory => _equipDomain;
        public WeaponHanding WeaponHanding => _weaponHanding;
        public IReadOnlyList<EquipmentSlotType> AllowedEquipSlots => _allowedEquipSlots;
        public Transform PickupGrip => _pickupGrip;

        public List<ItemActionType> GetAvailableActions()
        {
            List<ItemActionType> actions = new(3);
            if (ItemTypeRules.SupportsAction(_itemType, IsConsumable, ItemActionType.Use))
                actions.Add(ItemActionType.Use);

            if (ItemTypeRules.SupportsAction(_itemType, IsConsumable, ItemActionType.Equip))
                actions.Add(ItemActionType.Equip);

            actions.Add(ItemActionType.Drop);
            return actions;
        }

        public ItemActionType GetPrimaryAction()
        {
            if (ItemTypeRules.SupportsAction(_itemType, IsConsumable, ItemActionType.Use))
                return ItemActionType.Use;
            if (ItemTypeRules.SupportsAction(_itemType, IsConsumable, ItemActionType.Equip))
                return ItemActionType.Equip;
            return ItemActionType.Drop;
        }

        private static bool IsMiscellaneousType(ItemType itemType)
        {
            return itemType == ItemType.Miscellaneous;
        }

        public string InteractionPrompt => ShouldShowStealPrompt()
            ? $"Steal {_itemName}"
            : $"Pick up {_itemName}";

        public bool CanInteract(Interactor interactor)
        {
            _lastInteractorOwner = interactor != null ? interactor.Owner : null;
            return true;
        }

        public GameAction GetInteraction(Interactor interactor)
        {
            return new PickupItemAction(this);
        }

        public bool IsOwnedBy(GameObject actor)
        {
            return ItemOwnershipUtility.IsOwnedBy(ItemOwnerId, actor);
        }

        public bool WouldBeStealing(GameObject actor)
        {
            if (actor == null || !actor.CompareTag("Player"))
                return false;

            if (!HasOwner)
                return false;

            return !IsOwnedBy(actor);
        }

        public void SetStolen(bool stolen)
        {
            _isStolen = stolen;
        }

        public void SetOwner(GameObject owner)
        {
            _itemOwner = owner;
            _itemOwnerId = ItemOwnershipUtility.NormalizeOwnerIdOrEmpty(OwnerRegistry.ResolveOwnerId(owner));
        }

        public void SetOwnerId(string ownerId)
        {
            _itemOwnerId = ItemOwnershipUtility.NormalizeOwnerIdOrEmpty(ownerId);
            _itemOwner = null;
            ResolveOwnerReference();
        }

        public void ConfigureRuntimeItem(string itemName, int value, string flavourText = null, Sprite icon = null)
        {
            if (!string.IsNullOrWhiteSpace(itemName))
                _itemName = itemName.Trim();

            _value = Mathf.Max(0, value);

            if (flavourText != null)
                _flavourText = flavourText;

            if (icon != null)
                _icon = icon;
        }

        public Pose GetPickupPose()
        {
            if (_pickupGrip != null)
                return new Pose(_pickupGrip.position, _pickupGrip.rotation);

            Collider itemCollider = GetComponent<Collider>();
            if (itemCollider != null)
            {
                Bounds bounds = itemCollider.bounds;
                Vector3 targetPosition = bounds.center;
                targetPosition.y = bounds.min.y + bounds.extents.y * 1.25f;
                return new Pose(targetPosition, transform.rotation);
            }

            return new Pose(transform.position, transform.rotation);
        }

        private void Reset()
        {
            gameObject.tag = "Item";
        }

        private void Awake()
        {
            ApplyRuntimeValidation();
            ResolveOwnerReference();
        }

        private void OnValidate()
        {
            ApplyRuntimeValidation();

#if UNITY_EDITOR
            if (!Sol.ItemRegistry.IsEditorSyncInProgress)
                Sol.ItemRegistry.ScheduleEditorSync();
#endif
        }

        private bool ShouldShowStealPrompt()
        {
            if (_lastInteractorOwner == null || !_lastInteractorOwner.CompareTag("Player"))
                return false;

            if (!HasOwner)
                return false;

            return !IsOwnedBy(_lastInteractorOwner);
        }

        private GameObject ResolveOwnerReference()
        {
            if (_itemOwner != null)
            {
                string resolvedOwnerId = OwnerRegistry.ResolveOwnerId(_itemOwner);
                if (!string.IsNullOrWhiteSpace(resolvedOwnerId))
                    _itemOwnerId = ItemOwnershipUtility.NormalizeOwnerIdOrEmpty(resolvedOwnerId);

                return _itemOwner;
            }

            if (string.IsNullOrWhiteSpace(_itemOwnerId))
                return null;

            _itemOwner = OwnerRegistry.Resolve(_itemOwnerId);
            return _itemOwner;
        }

        private void ApplyRuntimeValidation()
        {
            _itemName = string.IsNullOrWhiteSpace(_itemName) ? "Item" : _itemName.Trim();
            _value = _itemType == ItemType.Gold ? 1 : Mathf.Max(0, _value);
            _maxStackSize = Mathf.Max(1, _maxStackSize);
            _itemId = Sol.EntityCodeUtility.NormalizeOrEmpty(_itemId, Sol.EntityCodeUtility.ItemPrefix);
            _itemOwnerId = ItemOwnershipUtility.NormalizeOwnerIdOrEmpty(_itemOwnerId);
            if (_itemOwner != null)
                _itemOwnerId = ItemOwnershipUtility.NormalizeOwnerIdOrEmpty(OwnerRegistry.ResolveOwnerId(_itemOwner));
        }
    }
}
