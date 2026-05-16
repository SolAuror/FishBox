using System.Collections.Generic;
using UnityEngine;
using Sol.Actions;
using Sol.Outline;
using Sol.Rpg;

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

    public enum ItemUseOccasion
    {
        Always = 0,
        InventoryOnly = 1,
        Never = 2
    }

    public enum ItemUseEffectType
    {
        HealHealthFlat = 0,
        HealHealthPercent = 1,
        RestoreStaminaFlat = 2,
        RestoreStaminaPercent = 3
    }

    public enum ItemAuthoringTemplate
    {
        None = 0,
        Consumable = 1,
        KeyItem = 2,
        Equipment = 3,
        FishingBait = 4,
        FishingLure = 5,
        Currency = 6
    }

    [System.Serializable]
    public class ItemUseEffect
    {
        [SerializeField] private ItemUseEffectType _effectType = ItemUseEffectType.HealHealthFlat;
        [SerializeField] [Min(0f)] private float _amount = 10f;

        public ItemUseEffectType EffectType => _effectType;
        public float Amount => Mathf.Max(0f, _amount);

        public bool Apply(Interactor interactor)
        {
            if (interactor == null || Amount <= 0f)
                return false;

            if (interactor.PlayerSoul != null)
                return ApplyToPlayer(interactor.PlayerSoul);

            if (interactor.NpcSoul != null)
                return ApplyToNpc(interactor.NpcSoul);

            return false;
        }

        private bool ApplyToPlayer(Player.PlayerSoul soul)
        {
            switch (_effectType)
            {
                case ItemUseEffectType.HealHealthFlat:
                    soul.Heal(Amount);
                    return true;
                case ItemUseEffectType.HealHealthPercent:
                    soul.Heal(soul.MaxHealth * Amount * 0.01f);
                    return true;
                case ItemUseEffectType.RestoreStaminaFlat:
                    soul.RestoreStamina(Amount);
                    return true;
                case ItemUseEffectType.RestoreStaminaPercent:
                    soul.RestoreStamina(soul.MaxStamina * Amount * 0.01f);
                    return true;
                default:
                    return false;
            }
        }

        private bool ApplyToNpc(AI.NPCSoul soul)
        {
            switch (_effectType)
            {
                case ItemUseEffectType.HealHealthFlat:
                    soul.Heal(Amount);
                    return true;
                case ItemUseEffectType.HealHealthPercent:
                    soul.Heal(soul.MaxHealth * Amount * 0.01f);
                    return true;
                case ItemUseEffectType.RestoreStaminaFlat:
                    soul.RestoreStamina(Amount);
                    return true;
                case ItemUseEffectType.RestoreStaminaPercent:
                    soul.RestoreStamina(soul.MaxStamina * Amount * 0.01f);
                    return true;
                default:
                    return false;
            }
        }
    }

    [RequireComponent(typeof(GrabbableComponent))]
    [RequireComponent(typeof(Collider))]
    public class ItemComponent : MonoBehaviour, IInteractable, IGameplayTagProvider, IGameplayStatModifierProvider
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
        [Tooltip("Inspector: tunes weight.")]
        [SerializeField] [Min(0f)] private float _weight;
        [Tooltip("Inspector: tunes is stackable.")]
        [SerializeField] private bool _isStackable;
        [SerializeField] private bool _isConsumable;
        [Tooltip("Inspector: tunes is tradeable.")]
        [SerializeField] private bool _isTradeable = true;
        [Tooltip("Inspector: tunes max stack size.")]
        [SerializeField] private int _maxStackSize = 1;

        [Header("Use")]
        [SerializeField] private ItemUseOccasion _useOccasion = ItemUseOccasion.InventoryOnly;
        [SerializeField] private List<ItemUseEffect> _useEffects = new();

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

        [Header("Authoring")]
        [SerializeField] private ItemAuthoringTemplate _authoringTemplate = ItemAuthoringTemplate.None;
        [SerializeField] [TextArea] private string _itemAuthoringNotes = string.Empty;
        [SerializeField] [HideInInspector] private bool _legacyDesignMigratedToRegistry;
        #endregion

        private GameObject _lastInteractorOwner;
        private bool _missingDefinitionWarned;

        public string ItemId => _itemId;
        public string ItemName => Definition != null ? Definition.DisplayName : _itemName;
        public ItemType Type => Definition != null ? Definition.ItemType : _itemType;
        public string TypeDisplayName => IsMiscellaneousType(Type) ? "Miscellaneous" : Type.ToString();
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
        public int Value => Definition != null ? Definition.Value : _value;
        public float Weight => Definition != null ? Definition.Weight : _weight;
        public string FlavourText => Definition != null ? Definition.FlavourText : _flavourText;
        public Sprite Icon => Definition != null ? Definition.Icon : _icon;
        public bool IsStackable => Definition != null ? Definition.IsStackable : _isStackable;
        public bool IsConsumable => Definition != null ? Definition.IsConsumable || ItemTypeRules.IsConsumableType(Definition.ItemType) : _isConsumable || ItemTypeRules.IsConsumableType(_itemType);
        public bool IsTradeable => Definition != null ? Definition.IsTradeable : _isTradeable;
        public int MaxStackSize => Definition != null ? Definition.MaxStackSize : _maxStackSize;
        public ItemUseOccasion UseOccasion => Definition != null ? Definition.UseOccasion : _useOccasion;
        public IReadOnlyList<ItemUseEffect> UseEffects => Definition != null ? Definition.UseEffects : _useEffects;
        public GameplayTagSet Tags => Definition != null ? Definition.Tags : GameplayTagSet.Empty;
        public GameplayStatModifierSet StatModifiers => Definition != null ? Definition.StatModifiers : null;
        public bool CanUseFromInventory => IsConsumable && UseOccasion != ItemUseOccasion.Never;
        public float Damage => ItemTypeRules.UsesWeaponStats(Type) ? (Definition != null ? Definition.Damage : _damage) : 0f;
        public float Defense => ItemTypeRules.UsesArmorStats(Type) ? (Definition != null ? Definition.Defense : _defense) : 0f;
        public string EquipBone => ItemTypeRules.UsesEquipmentSettings(Type) ? (Definition != null ? Definition.EquipBone : _equipBone) : string.Empty;
        public Vector3 EquipOffset => ItemTypeRules.UsesEquipmentSettings(Type) ? (Definition != null ? Definition.EquipOffset : _equipOffset) : Vector3.zero;
        public Vector3 EquipRotation => ItemTypeRules.UsesEquipmentSettings(Type) ? (Definition != null ? Definition.EquipRotation : _equipRotation) : Vector3.zero;
        public EquipDomain EquipCategory => Definition != null ? Definition.EquipDomain : _equipDomain;
        public WeaponHanding WeaponHanding => Definition != null ? Definition.WeaponHanding : _weaponHanding;
        public IReadOnlyList<EquipmentSlotType> AllowedEquipSlots => Definition != null ? Definition.AllowedEquipSlots : _allowedEquipSlots;
        public Transform PickupGrip => _pickupGrip;
        public ItemAuthoringTemplate AuthoringTemplate => Definition != null ? Definition.AuthoringTemplate : _authoringTemplate;
        public string ItemAuthoringNotes => Definition != null ? Definition.AuthoringNotes : _itemAuthoringNotes;

        public string LegacyItemId => _itemId;
        public string LegacyItemName => _itemName;
        public ItemType LegacyType => _itemType;
        public int LegacyValue => _value;
        public float LegacyWeight => _weight;
        public string LegacyFlavourText => _flavourText;
        public Sprite LegacyIcon => _icon;
        public bool LegacyIsStackable => _isStackable;
        public bool LegacyIsConsumable => _isConsumable;
        public bool LegacyIsTradeable => _isTradeable;
        public int LegacyMaxStackSize => _maxStackSize;
        public ItemUseOccasion LegacyUseOccasion => _useOccasion;
        public float LegacyDamage => _damage;
        public float LegacyDefense => _defense;
        public string LegacyEquipBone => _equipBone;
        public Vector3 LegacyEquipOffset => _equipOffset;
        public Vector3 LegacyEquipRotation => _equipRotation;
        public EquipDomain LegacyEquipDomain => _equipDomain;
        public WeaponHanding LegacyWeaponHanding => _weaponHanding;
        public ItemAuthoringTemplate LegacyAuthoringTemplate => _authoringTemplate;
        public string LegacyAuthoringNotes => _itemAuthoringNotes;
        public bool LegacyDesignMigratedToRegistry => _legacyDesignMigratedToRegistry;

        private Sol.ItemRegistry.Entry Definition => ResolveDefinition();

        public List<ItemUseEffect> CloneLegacyUseEffects()
        {
            return _useEffects != null ? new List<ItemUseEffect>(_useEffects) : new List<ItemUseEffect>();
        }

        public List<EquipmentSlotType> CloneLegacyAllowedEquipSlots()
        {
            return _allowedEquipSlots != null ? new List<EquipmentSlotType>(_allowedEquipSlots) : new List<EquipmentSlotType>();
        }

        public List<ItemActionType> GetAvailableActions()
        {
            List<ItemActionType> actions = new(3);
            if (CanUseFromInventory && ItemTypeRules.SupportsAction(Type, IsConsumable, ItemActionType.Use))
                actions.Add(ItemActionType.Use);

            if (ItemTypeRules.SupportsAction(Type, IsConsumable, ItemActionType.Equip))
                actions.Add(ItemActionType.Equip);

            actions.Add(ItemActionType.Drop);
            return actions;
        }

        public ItemActionType GetPrimaryAction()
        {
            if (CanUseFromInventory && ItemTypeRules.SupportsAction(Type, IsConsumable, ItemActionType.Use))
                return ItemActionType.Use;
            if (ItemTypeRules.SupportsAction(Type, IsConsumable, ItemActionType.Equip))
                return ItemActionType.Equip;
            return ItemActionType.Drop;
        }

        private static bool IsMiscellaneousType(ItemType itemType)
        {
            return itemType == ItemType.Miscellaneous;
        }

        public string InteractionPrompt => ShouldShowStealPrompt()
            ? $"Steal {ItemName}"
            : $"Pick up {ItemName}";

        public bool CanInteract(Interactor interactor)
        {
            _lastInteractorOwner = interactor != null ? interactor.Owner : null;
            return true;
        }

        public GameAction GetInteraction(Interactor interactor)
        {
            return new PickupItemAction(this);
        }

        public bool ApplyUseEffects(Interactor interactor)
        {
            if (_useEffects == null || _useEffects.Count == 0)
            {
                IReadOnlyList<ItemUseEffect> definitionEffects = UseEffects;
                if (definitionEffects == null || definitionEffects.Count == 0)
                    return false;
            }

            IReadOnlyList<ItemUseEffect> effects = UseEffects;
            if (effects == null || effects.Count == 0)
                return false;

            bool appliedAny = false;
            for (int i = 0; i < effects.Count; i++)
            {
                ItemUseEffect effect = effects[i];
                if (effect == null)
                    continue;

                appliedAny |= effect.Apply(interactor);
            }

            return appliedAny;
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

        private Sol.ItemRegistry.Entry ResolveDefinition()
        {
            if (string.IsNullOrWhiteSpace(_itemId))
                return null;

            Sol.ItemRegistry registry = Sol.ItemRegistry.Get();
            Sol.ItemRegistry.Entry definition = registry != null ? registry.GetDefinition(_itemId) : null;
            if (definition != null)
                return definition;

            if (!_missingDefinitionWarned)
            {
                _missingDefinitionWarned = true;
                Debug.LogWarning($"[ItemComponent] '{name}' has item id '{_itemId}' but no ItemRegistry definition. Falling back to legacy prefab fields.", this);
            }

            return null;
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
            _weight = Mathf.Max(0f, _weight);
            _maxStackSize = Mathf.Max(1, _maxStackSize);
            _itemId = Sol.EntityCodeUtility.NormalizeOrEmpty(_itemId, Sol.EntityCodeUtility.ItemPrefix);
            _itemOwnerId = ItemOwnershipUtility.NormalizeOwnerIdOrEmpty(_itemOwnerId);
            _itemAuthoringNotes = _itemAuthoringNotes?.Trim() ?? string.Empty;
            if (_itemOwner != null)
                _itemOwnerId = ItemOwnershipUtility.NormalizeOwnerIdOrEmpty(OwnerRegistry.ResolveOwnerId(_itemOwner));
        }
    }
}
