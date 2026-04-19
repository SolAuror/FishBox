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

    [RequireComponent(typeof(GrabbableComponent))]
    [RequireComponent(typeof(Collider))]
    public class ItemComponent : MonoBehaviour, IInteractable
    {
        [Header("Item Info")]
        [SerializeField] private string _itemId = string.Empty;
        [SerializeField] private string _itemName = "Item";
        [SerializeField] private ItemType _itemType = ItemType.Material;
        [SerializeField] private int _value;
        [SerializeField] [HideInInspector] private GameObject _itemOwner;
        [SerializeField] private string _itemOwnerId = string.Empty;
        [SerializeField] private bool _isStolen;
        [SerializeField] [TextArea] private string _flavourText = string.Empty;
        [SerializeField] private Sprite _icon;

        [Header("Properties")]
        [SerializeField] private bool _isStackable;
        [SerializeField] private bool _isConsumable;
        [SerializeField] private bool _isTradeable = true;
        [SerializeField] private int _maxStackSize = 1;

        [Header("Weapon Stats")]
        [SerializeField] private float _damage;

        [Header("Armor Stats")]
        [SerializeField] private float _defense;

        [Header("Equipment")]
        [Tooltip("Bone/socket name to parent this item to when equipped")]
        [SerializeField] private string _equipBone = string.Empty;
        [SerializeField] private Vector3 _equipOffset;
        [SerializeField] private Vector3 _equipRotation;

        [Header("Pickup")]
        [Tooltip("Optional hand target used by the pickup reach. If unassigned, the system falls back to the item's collider or transform.")]
        [SerializeField] private Transform _pickupGrip;

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
        public bool IsConsumable => _isConsumable || IsConsumableType(_itemType);
        public bool IsTradeable => _isTradeable;
        public int MaxStackSize => _maxStackSize;
        public float Damage => _damage;
        public float Defense => _defense;
        public string EquipBone => _equipBone;
        public Vector3 EquipOffset => _equipOffset;
        public Vector3 EquipRotation => _equipRotation;
        public Transform PickupGrip => _pickupGrip;

        public List<ItemActionType> GetAvailableActions()
        {
            List<ItemActionType> actions = new(3);
            if (IsConsumable)
                actions.Add(ItemActionType.Use);

            if (_itemType == ItemType.Weapon || _itemType == ItemType.Armor || _itemType == ItemType.Equipable)
                actions.Add(ItemActionType.Equip);

            actions.Add(ItemActionType.Drop);
            return actions;
        }

        public ItemActionType GetPrimaryAction()
        {
            if (IsConsumable)
                return ItemActionType.Use;
            if (_itemType == ItemType.Weapon || _itemType == ItemType.Armor || _itemType == ItemType.Equipable)
                return ItemActionType.Equip;
            return ItemActionType.Drop;
        }

        private static bool IsConsumableType(ItemType itemType)
        {
            return itemType == ItemType.Consumable
                || itemType == ItemType.Food
                || itemType == ItemType.Drink
                || itemType == ItemType.Potion;
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
            if (!HasOwner)
                return true;

            string actorOwnerId = OwnerIdentity.ResolveOwnerId(actor);
            return !string.IsNullOrWhiteSpace(actorOwnerId)
                && string.Equals(ItemOwnerId, actorOwnerId, System.StringComparison.OrdinalIgnoreCase);
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
            _itemOwnerId = OwnerIdentity.ResolveOwnerId(owner);
        }

        public void SetOwnerId(string ownerId)
        {
            _itemOwnerId = NormalizeOwnerId(ownerId);
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
            ResolveOwnerReference();
        }

        private void OnValidate()
        {
            if (_itemType == ItemType.Gold)
                _value = 1;

            _itemId = Sol.EntityCodeUtility.NormalizeOrEmpty(_itemId, Sol.EntityCodeUtility.ItemPrefix);
#if UNITY_EDITOR
            if (!Sol.ItemRegistry.IsEditorSyncInProgress)
                Sol.ItemRegistry.ScheduleEditorSync();
#endif
            _itemOwnerId = NormalizeOwnerId(_itemOwnerId);
            if (_itemOwner != null)
                _itemOwnerId = OwnerIdentity.ResolveOwnerId(_itemOwner);
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
                string resolvedOwnerId = OwnerIdentity.ResolveOwnerId(_itemOwner);
                if (!string.IsNullOrWhiteSpace(resolvedOwnerId))
                    _itemOwnerId = resolvedOwnerId;

                return _itemOwner;
            }

            if (string.IsNullOrWhiteSpace(_itemOwnerId))
                return null;

            _itemOwner = OwnerRegistry.Resolve(_itemOwnerId);
            return _itemOwner;
        }

        private static string NormalizeOwnerId(string rawOwnerId)
        {
            if (string.IsNullOrWhiteSpace(rawOwnerId))
                return string.Empty;

            string trimmed = rawOwnerId.Trim();
            if (string.Equals(trimmed, Sol.EntityCodeUtility.DefaultPlayerOwnerId, System.StringComparison.OrdinalIgnoreCase))
                return Sol.EntityCodeUtility.DefaultPlayerOwnerId;

            return Sol.EntityCodeUtility.NormalizeOrEmpty(trimmed, Sol.EntityCodeUtility.OwnerPrefix);
        }
    }
}
