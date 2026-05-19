using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sol.Grab;
using Sol.Rpg;

namespace Sol.HUD
{
    public enum InventoryCategoryTabId
    {
        AllItems,
        Weapons,
        Armor,
        FoodDrink,
        Potions,
        MiscItems
    }

    [Serializable]
    public class InventoryCategoryTabDefinition
    {
        public InventoryCategoryTabId Id;
        public string Label;
        public Sprite Icon;
        public bool Enabled = true;
    }

    public class InventoryCategoryTabs : MonoBehaviour
    {
        [SerializeField] private InventoryUI _inventoryUi;
        [SerializeField] private List<InventoryCategoryTabDefinition> _tabs = new();
        [SerializeField] private RectTransform _tabRoot;
        [SerializeField] private Sprite _tabDefaultSprite;
        [SerializeField] private Sprite _tabHoverSprite;
        [SerializeField] private Sprite _tabActiveSprite;
        [SerializeField] private Sprite _tabDisabledSprite;
        [SerializeField] private Color _inactiveIconColor = new(0.58f, 0.56f, 0.50f, 0.72f);
        [SerializeField] private Color _inactiveLabelColor = new(0.64f, 0.58f, 0.46f, 0.78f);
        [SerializeField] private Color _activeIconColor = new(1f, 0.93f, 0.68f, 1f);
        [SerializeField] private Color _activeLabelColor = new(1f, 0.87f, 0.48f, 1f);

        private readonly List<InventoryCategoryTabButton> _buttons = new();
        private InventoryCategoryTabId _selected = InventoryCategoryTabId.AllItems;
        private bool _configured;

        public InventoryCategoryTabId Selected => _selected;

        private void Awake()
        {
            Configure();
        }

        private void OnEnable()
        {
            Configure();
            Select(_selected);
        }

        public void Configure()
        {
            EnsureTabs();
            ResolveAuthoredReferences();
            ConfigureAuthoredButtons();
            _configured = true;
        }

        public void Select(InventoryCategoryTabId id)
        {
            if (!_configured)
                Configure();

            InventoryCategoryTabDefinition selectedDefinition = GetDefinition(id);
            if (selectedDefinition != null && !selectedDefinition.Enabled)
                return;

            _selected = id;
            _inventoryUi?.SetCategoryFilter(id);

            for (int i = 0; i < _buttons.Count; i++)
            {
                InventoryCategoryTabButton button = _buttons[i];
                if (button == null)
                    continue;

                button.SetActive(button.TabId == id);
            }
        }

        public bool ItemMatches(InventoryCategoryTabId id, ItemComponent item)
        {
            if (item == null)
                return false;

            return id switch
            {
                InventoryCategoryTabId.AllItems => true,
                InventoryCategoryTabId.Weapons => IsWeaponCategory(item),
                InventoryCategoryTabId.Armor => IsArmorCategory(item),
                InventoryCategoryTabId.FoodDrink => item.Tags.HasTagOrChild(GameplayCapabilityTags.ItemFood)
                    || item.Tags.HasTagOrChild(GameplayCapabilityTags.ItemDrink),
                InventoryCategoryTabId.Potions => IsPotionCategory(item),
                InventoryCategoryTabId.MiscItems => IsMiscCategory(item),
                _ => true
            };
        }

        public static bool IsWeaponCategory(ItemComponent item)
        {
            if (item == null)
                return false;

            return item.Tags.HasTagOrChild(GameplayCapabilityTags.ItemWeapon)
                || item.Tags.HasTagOrChild(GameplayCapabilityTags.ItemTool)
                || item.Tags.HasTagOrChild(GameplayCapabilityTags.ItemFishingRod)
                || item.Type == ItemType.Equipable && ItemTypeRules.ResolveEquipDomain(item) == EquipDomain.WeaponTool;
        }

        public static bool IsArmorCategory(ItemComponent item)
        {
            if (item == null)
                return false;

            return item.Tags.HasTagOrChild(GameplayCapabilityTags.ItemArmor)
                || item.Type == ItemType.Equipable && ItemTypeRules.ResolveEquipDomain(item) == EquipDomain.Armor;
        }

        public static bool IsPotionCategory(ItemComponent item)
        {
            if (item == null)
                return false;

            if (item.Tags.HasTagOrChild(GameplayCapabilityTags.ItemPotion))
                return true;

            return item.Type == ItemType.Consumable
                && item.CanUseFromInventory
                && item.UseEffects != null
                && item.UseEffects.Count > 0;
        }

        public static bool IsMiscCategory(ItemComponent item)
        {
            if (item == null)
                return false;

            return !IsWeaponCategory(item)
                && !IsArmorCategory(item)
                && !item.Tags.HasTagOrChild(GameplayCapabilityTags.ItemFood)
                && !item.Tags.HasTagOrChild(GameplayCapabilityTags.ItemDrink)
                && !IsPotionCategory(item);
        }

        private void EnsureTabs()
        {
            if (HasCategoryDefinitions())
                return;

            _tabs.Clear();
            _tabs.Add(new InventoryCategoryTabDefinition { Id = InventoryCategoryTabId.AllItems, Label = "All Items" });
            _tabs.Add(new InventoryCategoryTabDefinition { Id = InventoryCategoryTabId.Weapons, Label = "Weapons" });
            _tabs.Add(new InventoryCategoryTabDefinition { Id = InventoryCategoryTabId.Armor, Label = "Armor" });
            _tabs.Add(new InventoryCategoryTabDefinition { Id = InventoryCategoryTabId.FoodDrink, Label = "Food/Drink" });
            _tabs.Add(new InventoryCategoryTabDefinition { Id = InventoryCategoryTabId.Potions, Label = "Potions" });
            _tabs.Add(new InventoryCategoryTabDefinition { Id = InventoryCategoryTabId.MiscItems, Label = "Misc Items" });
        }

        private bool HasCategoryDefinitions()
        {
            if (_tabs == null || _tabs.Count != 6)
                return false;

            for (int i = 0; i < _tabs.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < _tabs.Count; j++)
                {
                    if (_tabs[j] != null && (int)_tabs[j].Id == i)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    return false;
            }

            return true;
        }

        private void ResolveAuthoredReferences()
        {
            _inventoryUi ??= GetComponent<InventoryUI>() ?? GetComponentInChildren<InventoryUI>(true);
            _tabRoot ??= MenuUiUtility.FindRectByNames(transform, "InventoryTabs", "InventoryTabStrip", "TabStrip");
        }

        private void ConfigureAuthoredButtons()
        {
            _buttons.Clear();

            if (_tabRoot == null)
            {
                Debug.LogWarning("[InventoryCategoryTabs] No authored InventoryTabs root assigned.", this);
                return;
            }

            InventoryCategoryTabButton[] tabButtons = _tabRoot.GetComponentsInChildren<InventoryCategoryTabButton>(true);
            for (int i = 0; i < tabButtons.Length; i++)
            {
                InventoryCategoryTabButton button = tabButtons[i];
                if (button == null)
                    continue;

                InventoryCategoryTabDefinition definition = GetDefinition(button.TabId);
                button.Configure(
                    this,
                    definition,
                    _tabDefaultSprite,
                    _tabHoverSprite,
                    _tabActiveSprite,
                    _tabDisabledSprite,
                    _inactiveIconColor,
                    _inactiveLabelColor,
                    _activeIconColor,
                    _activeLabelColor);
                _buttons.Add(button);
            }

            if (_buttons.Count == 0)
                Debug.LogWarning("[InventoryCategoryTabs] InventoryTabs has no recognized authored tab buttons.", _tabRoot);
        }

        private InventoryCategoryTabDefinition GetDefinition(InventoryCategoryTabId id)
        {
            EnsureTabs();
            for (int i = 0; i < _tabs.Count; i++)
            {
                InventoryCategoryTabDefinition tab = _tabs[i];
                if (tab != null && tab.Id == id)
                    return tab;
            }

            return new InventoryCategoryTabDefinition { Id = id, Label = id.ToString(), Enabled = true };
        }
    }
}
