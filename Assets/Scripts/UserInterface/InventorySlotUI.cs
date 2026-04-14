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

    /// <summary>
    /// Single row in the inventory list UI. Handles hover, left-click
    /// (primary action), and right-click (context menu).
    /// </summary>
    public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _countText;
        [SerializeField] private TextMeshProUGUI _statText;
        [SerializeField] private TextMeshProUGUI _statText1;
        [SerializeField] private TextMeshProUGUI _statText2;
        [SerializeField] private Image _highlight;
        [Header("Stolen Indicator")]
        [SerializeField] private Image _stolenIconImage;
        [SerializeField] private Sprite _stolenIconSprite;

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
            Func<InventorySlot, int> selectedQuantityProvider = null)
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

            var rootImage = GetComponent<Image>();
            if (rootImage == null)
            {
                rootImage = gameObject.AddComponent<Image>();
                rootImage.color = Color.clear;
            }
            rootImage.raycastTarget = true;

            var item = slot.Item;
            if (_icon != null)
            {
                _icon.sprite = item.Icon;
                _icon.enabled = true;
            }

            if (_nameText != null) _nameText.text = BuildDisplayName(item);
            UpdateStolenIndicator(item);
            if (_countText != null) _countText.text = BuildCountText(slot);

            if (_highlight != null)
            {
                _highlight.enabled = true;
                _highlight.raycastTarget = true;
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
                if (ItemPreviewRenderer.Instance != null) ItemPreviewRenderer.Instance.Show(_slot.Item);
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
                if (ItemPreviewRenderer.Instance != null) ItemPreviewRenderer.Instance.Clear();
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
            string[] values = _displayMode == InventorySlotDisplayMode.Trade
                ? BuildTradeStatTexts(item)
                : BuildInventoryStatTexts(item);

            SetStatText(_statText, values[0]);
            SetStatText(_statText1, values[1]);
            SetStatText(_statText2, values[2]);
        }

        private static string BuildDisplayName(ItemComponent item)
        {
            if (item == null)
                return string.Empty;

            return item.ItemName;
        }

        private void UpdateStolenIndicator(ItemComponent item)
        {
            if (_stolenIconImage == null)
                return;

            if (_stolenIconSprite != null)
                _stolenIconImage.sprite = _stolenIconSprite;

            bool show = item != null && item.IsStolen;
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
            var values = new List<string>(3);

            if (item.Damage > 0f)
                values.Add($"{item.Damage:0.#} dmg");
            if (item.Defense > 0f)
                values.Add($"{item.Defense:0.#} def");

            while (values.Count < 3)
                values.Add(string.Empty);

            return values.ToArray();
        }

        private string[] BuildTradeStatTexts(ItemComponent item)
        {
            return new[]
            {
                string.Empty,
                string.Empty,
                item.Value > 0 ? $"{item.Value} g" : string.Empty
            };
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
