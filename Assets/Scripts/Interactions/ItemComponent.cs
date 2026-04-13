using System.Collections.Generic;
using UnityEngine;
using Sol.Actions;
using Sol.Outline;

namespace Sol.Grab
{
    public enum ItemType
    {
        // Keep legacy numeric values stable for existing serialized prefabs/items.
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

    /// Attach to any world object that represents a pick-up-able item.
    /// Requires a GrabbableComponent (physics grab) and a Collider (raycasting).
    /// Automatically tags the GameObject "Item" when first added in the editor.
    [RequireComponent(typeof(GrabbableComponent))]
    [RequireComponent(typeof(Collider))]
    public class ItemComponent : MonoBehaviour, IInteractable
    {
        [Header("Item Info")]
        [SerializeField] private string _itemId = "";
        [SerializeField] private string _itemName = "Item";
        [SerializeField] private ItemType _itemType = ItemType.Material;
        [SerializeField] private int _value;
        [SerializeField] [TextArea] private string _flavourText = "";
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
        [SerializeField] private string _equipBone = "";
        [SerializeField] private Vector3 _equipOffset;
        [SerializeField] private Vector3 _equipRotation;

        [Header("Pickup")]
        [Tooltip("Optional hand target used by the pickup reach. If unassigned, the system falls back to the item's collider or transform.")]
        [SerializeField] private Transform _pickupGrip;

        public string ItemId       => _itemId;
        public string ItemName     => _itemName;
        public ItemType Type       => _itemType;
        public string TypeDisplayName => IsMiscellaneousType(_itemType) ? "Miscellaneous" : _itemType.ToString();
        public string ItemOwnerId  => string.Empty;
        public GameObject ItemOwner => null;
        public bool IsStolen       => false;
        public int Value           => _value;
        public string FlavourText  => _flavourText;
        public Sprite Icon         => _icon;
        public bool IsStackable    => _isStackable;
        public bool IsConsumable   => _isConsumable || IsConsumableType(_itemType);
        public bool IsTradeable    => _isTradeable;
        public int MaxStackSize    => _maxStackSize;

        // Weapon
        public float Damage => _damage;

        // Armor
        public float Defense => _defense;

        // Equipment
        public string EquipBone    => _equipBone;
        public Vector3 EquipOffset   => _equipOffset;
        public Vector3 EquipRotation => _equipRotation;
        public Transform PickupGrip => _pickupGrip;

        // --- Item Actions ---

        public List<ItemActionType> GetAvailableActions()
        {
            var actions = new List<ItemActionType>(3);

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

        // --- IInteractable ---

        public string InteractionPrompt => $"Pick up {_itemName}";

        public bool CanInteract(Interactor interactor)
        {
            return true;
        }

        public GameAction GetInteraction(Interactor interactor)
        {
            return new Actions.PickupItemAction(this);
        }

        public bool IsOwnedBy(GameObject actor)
        {
            return true;
        }

        public bool WouldBeStealing(GameObject actor)
        {
            return false;
        }

        public void SetStolen(bool stolen)
        {
        }

        public void SetOwner(GameObject owner)
        {
        }

        public void SetOwnerId(string ownerId)
        {
        }

        public void ConfigureRuntimeItem(
            string itemName,
            int value,
            string flavourText = null,
            Sprite icon = null)
        {
            if (!string.IsNullOrWhiteSpace(itemName))
                _itemName = itemName.Trim();

            _value = Mathf.Max(0, value);

            if (flavourText != null)
                _flavourText = flavourText;

            if (icon != null)
                _icon = icon;
        }

        /// <summary>
        /// Returns the best available pickup target for hand IK / preview.
        /// Prefers the authored PickupGrip transform, then falls back to the collider,
        /// and finally to the item transform itself.
        /// </summary>
        public Pose GetPickupPose()
        {
            if (_pickupGrip != null)
                return new Pose(_pickupGrip.position, _pickupGrip.rotation);

            var itemCollider = GetComponent<Collider>();
            if (itemCollider != null)
            {
                Bounds bounds = itemCollider.bounds;
                Vector3 targetPosition = bounds.center;

                // Bias the fallback grip slightly upward so the hand aims toward the visible body
                // of the item rather than clipping toward the support surface.
                targetPosition.y = bounds.min.y + bounds.extents.y * 1.25f;

                return new Pose(targetPosition, transform.rotation);
            }

            return new Pose(transform.position, transform.rotation);
        }

        // --- Editor helpers ---

        /// Auto-tag when the component is first added in the editor.
        private void Reset()
        {
            gameObject.tag = "Item";
        }

        private void Awake()
        {
        }

        private void OnValidate()
        {
            if (_itemType == ItemType.Gold)
                _value = 1;

            _itemId = Sol.EntityCodeUtility.NormalizeOrEmpty(_itemId, Sol.EntityCodeUtility.ItemPrefix);
#if UNITY_EDITOR
            _itemId = Sol.EntityCodeUtility.EnsureAssignedCode(
                this,
                _itemId,
                Sol.EntityCodeUtility.ItemPrefix,
                static item => item._itemId);
#endif
        }
    }
}
