using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Sol.Actions;
using Sol.Fishing;
using Sol.Grab;

namespace Sol.HUD
{
    public enum InventorySlotDisplayMode
    {
        Inventory,
        Trade
    }

    public enum InventorySlotPresentationMode
    {
        Compact,
        InventoryTable
    }

    /// <summary>
    /// Single row in the inventory list UI. Handles hover, left-click
    /// (primary action), and right-click (context menu).
    /// </summary>
    public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        #region Inspector Settings
        [Tooltip("Inspector: tunes icon.")]
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [Tooltip("Inspector: tunes count text.")]
        [SerializeField] private TextMeshProUGUI _countText;
        [SerializeField] private TextMeshProUGUI _statText;
        [Tooltip("Inspector: tunes stat text1.")]
        [SerializeField] private TextMeshProUGUI _statText1;
        [SerializeField] private TextMeshProUGUI _statText2;
        [Header("Optional Table Columns")]
        [SerializeField] private TextMeshProUGUI _weightText;
        [SerializeField] private TextMeshProUGUI _damageText;
        [SerializeField] private TextMeshProUGUI _armorText;
        [SerializeField] private TextMeshProUGUI _valueText;
        [Tooltip("Inspector: tunes highlight.")]
        [SerializeField] private Image _highlight;
        [Header("Stolen Indicator")]
        [Tooltip("Inspector: tunes stolen icon image.")]
        [SerializeField] private Image _stolenIconImage;
        [Tooltip("Inspector: tunes stolen icon sprite.")]
        [SerializeField] private Sprite _stolenIconSprite;
        #endregion

        private static readonly Color HighlightOff = new(1f, 1f, 1f, 0f);
        private static readonly Color HighlightOn = new(1f, 1f, 1f, 0.12f);
        private static readonly Color EquippedTint = new(0.2f, 0.8f, 0.6f, 0.25f);
        private static readonly Color EquippedHover = new(0.2f, 0.8f, 0.6f, 0.40f);
        private static readonly Color SelectedTint = new(0.95f, 0.75f, 0.2f, 0.25f);
        private static readonly Color SelectedHover = new(0.95f, 0.75f, 0.2f, 0.40f);
        private InventorySlot _slot;
        private Inventory _inventory;
        private Interactor _interactor;
        private Action<InventorySlot> _onClickOverride;
        private Action<InventorySlot> _onRightClickOverride;
        private Action<InventorySlot> _onShiftClickOverride;
        private Action<InventorySlot> _onShiftRightClickOverride;
        private Action<InventorySlot> _onHoverOverride;
        private Action _onHoverExitOverride;
        private Func<InventorySlot, bool> _isSelectedPredicate;
        private Func<InventorySlot, int> _selectedQuantityProvider;
        private Equipment _equipment;
        private InventorySlotDisplayMode _displayMode;
        private InventorySlotPresentationMode _presentationMode;
        private bool _suppressTooltip;
        private bool _isHovered;

        public InventorySlot Slot => _slot;

        public void Bind(
            InventorySlot slot,
            Inventory inventory,
            Interactor interactor,
            Action<InventorySlot> onClickOverride = null,
            Action<InventorySlot> onRightClickOverride = null,
            Action<InventorySlot> onShiftClickOverride = null,
            Action<InventorySlot> onShiftRightClickOverride = null,
            Action<InventorySlot> onHoverOverride = null,
            Action onHoverExitOverride = null,
            InventorySlotDisplayMode displayMode = InventorySlotDisplayMode.Inventory,
            bool suppressTooltip = false,
            Func<InventorySlot, bool> isSelectedPredicate = null,
            Func<InventorySlot, int> selectedQuantityProvider = null,
            InventorySlotPresentationMode presentationMode = InventorySlotPresentationMode.Compact)
        {
            if (_equipment != null) _equipment.OnChanged -= RefreshEquippedTint;

            _slot = slot;
            _inventory = inventory;
            _interactor = interactor;
            _onClickOverride = onClickOverride;
            _onRightClickOverride = onRightClickOverride;
            _onShiftClickOverride = onShiftClickOverride;
            _onShiftRightClickOverride = onShiftRightClickOverride;
            _onHoverOverride = onHoverOverride;
            _onHoverExitOverride = onHoverExitOverride;
            _displayMode = displayMode;
            _presentationMode = displayMode == InventorySlotDisplayMode.Trade
                ? InventorySlotPresentationMode.Compact
                : presentationMode;
            _suppressTooltip = suppressTooltip;
            _isSelectedPredicate = isSelectedPredicate;
            _selectedQuantityProvider = selectedQuantityProvider;
            _isHovered = false;

            _equipment = interactor?.Owner?.GetComponent<Equipment>();
            if (_equipment != null) _equipment.OnChanged += RefreshEquippedTint;

            var cg = GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
                cg.interactable = true;
            }

            if (_nameText == null)
                Debug.LogWarning($"[InventorySlotUI] _nameText is null on '{name}'. Wire NameText in the prefab Inspector.", this);
            if (_icon == null)
                Debug.LogWarning($"[InventorySlotUI] _icon is null on '{name}'. Wire Icon in the prefab Inspector.", this);

            var item = slot.Item;
            if (_icon != null)
            {
                _icon.sprite = item.Icon;
                _icon.enabled = true;
            }

            if (_nameText != null) _nameText.text = ItemPresentationUtility.BuildDisplayName(item, showReferenceCodes: false);
            UpdateStolenIndicator(item);
            if (_countText != null) _countText.text = BuildCountText(slot);

            if (_highlight != null)
            {
                _highlight.enabled = true;
                _highlight.color = HighlightOff;
            }

            ApplyStatTexts(item);
            RefreshEquippedTint();
        }

        private void OnDestroy()
        {
            if (_equipment != null) _equipment.OnChanged -= RefreshEquippedTint;
        }

        private void RefreshEquippedTint()
        {
            if (_highlight == null) return;
            _highlight.color = ResolveHighlightColor(_isHovered);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            if (_highlight != null) _highlight.color = ResolveHighlightColor(true);

            if (_slot?.Item == null) return;

            _onHoverOverride?.Invoke(_slot);

            if (!_suppressTooltip)
            {
                TooltipUI tooltip = TooltipUI.Instance ?? TooltipUI.ResolveInstance();
                if (tooltip != null) tooltip.Show(_slot.Item);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            if (_highlight != null) _highlight.color = ResolveHighlightColor(false);

            _onHoverExitOverride?.Invoke();

            if (!_suppressTooltip)
            {
                TooltipUI tooltip = TooltipUI.Instance ?? TooltipUI.ResolveInstance();
                if (tooltip != null) tooltip.Hide();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_slot?.Item == null) return;

            if (_displayMode == InventorySlotDisplayMode.Trade)
            {
                bool shiftHeld = Keyboard.current != null &&
                    (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

                if (eventData.button == PointerEventData.InputButton.Left)
                {
                    if (shiftHeld && _onShiftClickOverride != null)
                        _onShiftClickOverride(_slot);
                    else if (_onClickOverride != null)
                        _onClickOverride(_slot);
                }
                else if (eventData.button == PointerEventData.InputButton.Right)
                {
                    if (shiftHeld && _onShiftRightClickOverride != null)
                        _onShiftRightClickOverride(_slot);
                    else if (_onRightClickOverride != null)
                        _onRightClickOverride(_slot);
                }
                return;
            }

            if (_onClickOverride != null && eventData.button == PointerEventData.InputButton.Left)
            {
                _onClickOverride(_slot);
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (ActionSystem.Instance != null)
                {
                    var action = CreatePrimaryAction();
                    if (action != null)
                        ActionSystem.Instance.Dispatch(action, _interactor.Owner);
                }
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                _onClickOverride?.Invoke(_slot);

                ContextMenuUI contextMenu = ContextMenuUI.Instance ?? ContextMenuUI.ResolveInstance();
                if (contextMenu != null)
                    contextMenu.Show(_slot, _inventory, _interactor, eventData.position);
            }
        }

        private GameAction CreateActionForType(ItemActionType type)
        {
            return type switch
            {
                ItemActionType.Use => new ConsumeAction(_slot, _interactor),
                ItemActionType.Equip => new EquipItemAction(_slot.Item),
                ItemActionType.Drop => new DropAction(_slot, _interactor),
                _ => null
            };
        }

        private GameAction CreatePrimaryAction()
        {
            if (FishingInventoryLoadAction.CanHandle(_slot?.Item))
                return new FishingInventoryLoadAction(_slot, _inventory, _interactor);

            return CreateActionForType(_slot.Item.GetPrimaryAction());
        }

        private void ApplyStatTexts(ItemComponent item)
        {
            if (_presentationMode == InventorySlotPresentationMode.InventoryTable && HasAuthoredTableColumns())
            {
                ApplyInventoryTableTexts(item);
                return;
            }

            SetTableColumnsVisible(false);
            string[] values = _displayMode == InventorySlotDisplayMode.Trade
                ? BuildTradeStatTexts(item)
                : BuildInventoryStatTexts(item);

            SetStatText(_statText, values[0]);
            SetStatText(_statText1, values[1]);
            SetStatText(_statText2, values[2]);
        }

        private void ApplyInventoryTableTexts(ItemComponent item)
        {
            SetTableColumnsVisible(true);

            SetStatText(_weightText != null ? _weightText : _statText, ItemPresentationUtility.BuildWeightText(item));

            bool hasDedicatedCombatColumns = _damageText != null || _armorText != null;
            if (hasDedicatedCombatColumns)
            {
                SetStatText(_damageText, ItemPresentationUtility.BuildDamageText(item));
                SetStatText(_armorText, ItemPresentationUtility.BuildArmorText(item));
                SetStatText(_statText1, string.Empty);
            }
            else
            {
                string damage = ItemPresentationUtility.BuildDamageText(item);
                string armor = ItemPresentationUtility.BuildArmorText(item);
                SetStatText(_statText1, $"{damage} / {armor}");
            }

            SetStatText(_valueText != null ? _valueText : _statText2, ItemPresentationUtility.BuildValueText(item));

            if (_weightText != null && _weightText != _statText)
                SetStatText(_statText, string.Empty);
            if (_valueText != null && _valueText != _statText2)
                SetStatText(_statText2, string.Empty);
        }

        private bool HasAuthoredTableColumns()
        {
            return _weightText != null
                && _damageText != null
                && _armorText != null
                && _valueText != null;
        }

        private void SetTableColumnsVisible(bool visible)
        {
            SetColumnVisible(_weightText, visible);
            SetColumnVisible(_damageText, visible);
            SetColumnVisible(_armorText, visible);
            SetColumnVisible(_valueText, visible);
        }

        private static void SetColumnVisible(TextMeshProUGUI label, bool visible)
        {
            if (label != null)
                label.gameObject.SetActive(visible);
        }

        private void UpdateStolenIndicator(ItemComponent item)
        {
            if (_stolenIconImage == null)
                return;

            if (_stolenIconSprite != null)
                _stolenIconImage.sprite = _stolenIconSprite;

            bool show = ItemPresentationUtility.ShouldShowStolenIndicator(item);
            _stolenIconImage.gameObject.SetActive(show);
        }

        private string BuildCountText(InventorySlot slot)
        {
            if (slot == null)
                return string.Empty;

            int total = Mathf.Max(1, slot.Count);
            int selected = _selectedQuantityProvider?.Invoke(slot) ?? 0;

            if (_displayMode == InventorySlotDisplayMode.Trade && selected > 0 && total > 1)
                return $"{selected}/{total}";

            return total > 1 ? $"x{total}" : string.Empty;
        }

        private string[] BuildInventoryStatTexts(ItemComponent item)
        {
            string[] values = ItemPresentationUtility.BuildCompactInventoryStatTexts(item);
            if (_equipment == null || item == null || !_equipment.IsEquipped(item))
                return values;

            if (_equipment.TryGetPrimarySlot(item, out EquipmentSlotType equippedSlot))
            {
                string equippedText = $"Equipped: {equippedSlot}";
                for (int i = 0; i < values.Length; i++)
                {
                    if (!string.IsNullOrEmpty(values[i]))
                        continue;

                    values[i] = equippedText;
                    return values;
                }

                values[values.Length - 1] = equippedText;
            }

            return values;
        }

        private string[] BuildTradeStatTexts(ItemComponent item)
        {
            return ItemPresentationUtility.BuildCompactTradeStatTexts(item);
        }

        private void SetStatText(TextMeshProUGUI label, string value)
        {
            if (label == null) return;
            label.text = value;
            label.gameObject.SetActive(!string.IsNullOrEmpty(value));
        }

        private Color ResolveHighlightColor(bool hovered)
        {
            bool selected = _isSelectedPredicate != null && _slot != null && _isSelectedPredicate(_slot);
            if (selected)
                return hovered ? SelectedHover : SelectedTint;

            bool equipped = _equipment != null && _slot?.Item != null && _equipment.IsEquipped(_slot.Item);
            if (equipped)
                return hovered ? EquippedHover : EquippedTint;

            return hovered ? HighlightOn : HighlightOff;
        }
    }
}
