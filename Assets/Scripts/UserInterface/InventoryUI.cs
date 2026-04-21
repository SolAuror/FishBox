using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sol.HUD
{
    /// <summary>
    /// Simple list-view inventory UI. Refreshes only when inventory contents change.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        #region Inspector Settings
        [Header("References")]
        [Tooltip("Inspector: tunes inventory.")]
        [SerializeField] private Inventory _inventory;
        [SerializeField] private Transform _slotContainer;
        [Tooltip("Inspector: tunes slot prefab.")]
        [SerializeField] private InventorySlotUI _slotPrefab;

        [Header("Display")]
        [Tooltip("Inspector: tunes capacity text.")]
        [SerializeField] private TextMeshProUGUI _capacityText;
        [Tooltip("Inspector: tunes gold text.")]
        [SerializeField] private TextMeshProUGUI _goldText;
        #endregion

        private Interactor _interactor;
        private Coroutine _pendingRebuild;
        private bool _rebuildScheduled;
        private readonly List<InventorySlotUI> _slotPool = new();
        private Action<InventorySlot> _slotClickOverride;
        private Action<InventorySlot> _slotRightClickOverride;
        private Action<InventorySlot> _slotShiftClickOverride;
        private Action<InventorySlot> _slotShiftRightClickOverride;
        private Action<InventorySlot> _slotHoverOverride;
        private Action _slotHoverExitOverride;
        private Func<InventorySlot, bool> _isSelectedPredicate;
        private Func<InventorySlot, int> _selectedQuantityProvider;
        private InventorySlotDisplayMode _displayMode = InventorySlotDisplayMode.Inventory;
        private bool _suppressTooltip;
        private bool _showGold = true;
        private bool _showGoldItemsInList;
        private bool _hideNonTradeableInTradeMode;
        private InventorySlot _selectedSlot;

        public Inventory Inventory => _inventory;
        public Interactor Interactor => _interactor;
        public InventorySlot SelectedSlot => _selectedSlot;

        private void OnEnable()
        {
            SubscribeAndRefresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
            CancelPendingRebuild();
        }

        public void SetBinding(
            Inventory inventory,
            Interactor interactor = null,
            Action<InventorySlot> slotClickOverride = null,
            Action<InventorySlot> slotRightClickOverride = null,
            Action<InventorySlot> slotShiftClickOverride = null,
            Action<InventorySlot> slotShiftRightClickOverride = null,
            Action<InventorySlot> slotHoverOverride = null,
            Action slotHoverExitOverride = null,
            InventorySlotDisplayMode displayMode = InventorySlotDisplayMode.Inventory,
            bool suppressTooltip = false,
            bool showGold = true,
            bool showGoldItemsInList = false,
            bool hideNonTradeableInTradeMode = false,
            Func<InventorySlot, bool> isSelectedPredicate = null,
            Func<InventorySlot, int> selectedQuantityProvider = null)
        {
            bool wasActive = isActiveAndEnabled;
            if (wasActive)
                Unsubscribe();

            _inventory = inventory;
            _interactor = interactor;
            _slotClickOverride = slotClickOverride;
            _slotRightClickOverride = slotRightClickOverride;
            _slotShiftClickOverride = slotShiftClickOverride;
            _slotShiftRightClickOverride = slotShiftRightClickOverride;
            _slotHoverOverride = slotHoverOverride;
            _slotHoverExitOverride = slotHoverExitOverride;
            _displayMode = displayMode;
            _suppressTooltip = suppressTooltip;
            _showGold = showGold;
            _showGoldItemsInList = showGoldItemsInList;
            _hideNonTradeableInTradeMode = hideNonTradeableInTradeMode;
            _isSelectedPredicate = isSelectedPredicate;
            _selectedQuantityProvider = selectedQuantityProvider;

            if (wasActive)
                SubscribeAndRefresh();
        }

        public void ClearBinding()
        {
            if (isActiveAndEnabled)
                Unsubscribe();
            CancelPendingRebuild();

            _inventory = null;
            _interactor = null;
            _slotClickOverride = null;
            _slotRightClickOverride = null;
            _slotShiftClickOverride = null;
            _slotShiftRightClickOverride = null;
            _slotHoverOverride = null;
            _slotHoverExitOverride = null;
            _displayMode = InventorySlotDisplayMode.Inventory;
            _suppressTooltip = false;
            _showGold = true;
            _showGoldItemsInList = false;
            _hideNonTradeableInTradeMode = false;
            _isSelectedPredicate = null;
            _selectedQuantityProvider = null;
            _selectedSlot = null;
        }

        /// <summary>Rebuild the list from scratch. Called only when inventory changes.</summary>
        public void Refresh()
        {
            if (_inventory == null || _slotPrefab == null)
            {
                Debug.LogWarning("[InventoryUI] _inventory or _slotPrefab is not assigned.", this);
                return;
            }

            if (_slotContainer == null)
            {
                Debug.LogWarning("[InventoryUI] _slotContainer is not assigned.", this);
                return;
            }

            bool isSceneTemplate = _slotPrefab.gameObject.scene.IsValid();

            if (isSceneTemplate && _slotPrefab.transform.parent == _slotContainer)
                _slotPrefab.gameObject.SetActive(false);

            if (isSceneTemplate &&
                _slotPrefab.gameObject.activeSelf &&
                _slotPrefab.transform.parent != _slotContainer)
            {
                Debug.LogError("[InventoryUI] _slotPrefab is active but not inside _slotContainer. " +
                    "It will render as a ghost slot. Deactivate it or move it under the container.", _slotPrefab);
            }

            if (_selectedSlot != null && (_inventory == null || !HasSlot(_selectedSlot)))
                _selectedSlot = null;

            float slotHeight = 48f;
            var templateLE = _slotPrefab.GetComponent<LayoutElement>();
            if (templateLE != null && templateLE.preferredHeight > 0f)
                slotHeight = templateLE.preferredHeight;

            Action<InventorySlot> effectiveClickOverride = _slotClickOverride;
            Func<InventorySlot, bool> effectiveSelectedPredicate = _isSelectedPredicate;

            if (_displayMode == InventorySlotDisplayMode.Inventory && _slotClickOverride == null)
                effectiveSelectedPredicate = null;

            int slotIndex = 0;
            foreach (var slot in _inventory.Slots)
            {
                if (slot?.Item == null)
                    continue;

                if (!_showGoldItemsInList && slot.Item.Type == Sol.Grab.ItemType.Gold)
                    continue;

                if (_hideNonTradeableInTradeMode
                    && _displayMode == InventorySlotDisplayMode.Trade
                    && !slot.Item.IsTradeable)
                    continue;

                if (slotIndex >= _slotPool.Count)
                {
                    var newRow = Instantiate(_slotPrefab, _slotContainer);
                    _slotPool.Add(newRow);
                }

                var row = _slotPool[slotIndex];
                row.gameObject.SetActive(true);

                var le = row.GetComponent<LayoutElement>();
                if (le != null)
                    le.preferredHeight = slotHeight;

                row.Bind(
                    slot,
                    _inventory,
                    _interactor,
                    effectiveClickOverride,
                    _slotRightClickOverride,
                    _slotShiftClickOverride,
                    _slotShiftRightClickOverride,
                    _slotHoverOverride,
                    _slotHoverExitOverride,
                    _displayMode,
                    _suppressTooltip,
                    effectiveSelectedPredicate,
                    _selectedQuantityProvider);

                slotIndex++;
            }

            for (int i = slotIndex; i < _slotPool.Count; i++)
                _slotPool[i].gameObject.SetActive(false);

            if (_capacityText != null)
                _capacityText.text = $"{_inventory.Count} / {_inventory.Capacity}";

            if (_goldText != null)
            {
                _goldText.gameObject.SetActive(_showGold);
                if (_showGold)
                    _goldText.text = $"{_inventory.Gold} g";
            }

            // Deferred rebuild lets TMP auto-size settle before measuring layout.
            // The immediate call is intentionally omitted ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â one rebuild per frame is enough.
            CancelPendingRebuild();
            _rebuildScheduled = true;
            _pendingRebuild = PersistentCoroutineRunner.Run(DeferredRebuildRoutine());
        }

        private void RebuildLayout()
        {
            if (_slotContainer is RectTransform rt)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
        }

        private IEnumerator DeferredRebuildRoutine()
        {
            yield return null;
            if (!_rebuildScheduled) yield break;
            _rebuildScheduled = false;
            RebuildLayout();
            _pendingRebuild = null;
        }

        private void SubscribeAndRefresh()
        {
            if (_inventory == null) return;

            _interactor ??= new Interactor(_inventory.gameObject, true);
            _inventory.OnChanged += Refresh;
            Refresh();
        }

        private void Unsubscribe()
        {
            if (_inventory != null)
                _inventory.OnChanged -= Refresh;
        }

        private void CancelPendingRebuild()
        {
            _rebuildScheduled = false;
            if (_pendingRebuild != null)
            {
                PersistentCoroutineRunner.Stop(_pendingRebuild);
                _pendingRebuild = null;
            }
        }

        private bool HasSlot(InventorySlot slot)
        {
            if (_inventory == null || slot == null)
                return false;

            foreach (var current in _inventory.Slots)
            {
                if (current == slot)
                    return true;
            }

            return false;
        }
    }
}
