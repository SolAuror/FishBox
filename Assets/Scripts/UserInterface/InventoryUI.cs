using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sol.HUD
{
    public enum InventorySortKey
    {
        Name,
        Weight,
        Damage,
        Armor,
        Value
    }

    public enum InventorySortDirection
    {
        Ascending,
        Descending
    }

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

        [Header("Search And Sorting")]
        [SerializeField] private bool _enableSearchAndSorting;
        [SerializeField] private InventorySlotPresentationMode _presentationMode = InventorySlotPresentationMode.Compact;
        [SerializeField] private TMP_InputField _searchInput;
        [SerializeField] private Button _sortNameButton;
        [SerializeField] private Button _sortWeightButton;
        [SerializeField] private Button _sortDamageButton;
        [SerializeField] private Button _sortArmorButton;
        [SerializeField] private Button _sortValueButton;
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
        private InventorySortKey _sortKey = InventorySortKey.Name;
        private InventorySortDirection _sortDirection = InventorySortDirection.Ascending;
        private string _searchQuery = string.Empty;
        private bool _searchSortUiCreated;
        private bool _sortButtonsWired;
        private readonly List<InventorySlot> _visibleSlots = new();

        public Inventory Inventory => _inventory;
        public Interactor Interactor => _interactor;
        public InventorySlot SelectedSlot => _selectedSlot;

        private void OnEnable()
        {
            EnsureSearchSortUi();
            EnsureSortButtonsWired();
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
            if (_displayMode == InventorySlotDisplayMode.Trade)
                _presentationMode = InventorySlotPresentationMode.Compact;
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
            _presentationMode = _enableSearchAndSorting
                ? InventorySlotPresentationMode.InventoryTable
                : InventorySlotPresentationMode.Compact;
            _suppressTooltip = false;
            _showGold = true;
            _showGoldItemsInList = false;
            _hideNonTradeableInTradeMode = false;
            _isSelectedPredicate = null;
            _selectedQuantityProvider = null;
            _selectedSlot = null;
        }

        public void UsePlayerMenuPresentation()
        {
            _enableSearchAndSorting = true;
            if (_displayMode != InventorySlotDisplayMode.Trade)
                _presentationMode = InventorySlotPresentationMode.InventoryTable;

            EnsureSearchSortUi();
            EnsureSortButtonsWired();

            if (isActiveAndEnabled)
                Refresh();
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
            BuildVisibleSlots();
            foreach (var slot in _visibleSlots)
            {
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
                    _selectedQuantityProvider,
                    _presentationMode);

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
 // The immediate call is intentionally omitted - one rebuild per frame is enough.
            CancelPendingRebuild();
            _rebuildScheduled = true;
            _pendingRebuild = PersistentCoroutineRunner.Run(DeferredRebuildRoutine());
        }

        private void BuildVisibleSlots()
        {
            _visibleSlots.Clear();
            if (_inventory == null)
                return;

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

                if (_enableSearchAndSorting && !MatchesSearch(slot))
                    continue;

                _visibleSlots.Add(slot);
            }

            if (_enableSearchAndSorting && _visibleSlots.Count > 1)
                _visibleSlots.Sort(CompareSlots);
        }

        private bool MatchesSearch(InventorySlot slot)
        {
            if (string.IsNullOrWhiteSpace(_searchQuery))
                return true;

            string query = _searchQuery.Trim();
            var item = slot?.Item;
            if (item == null)
                return false;

            return ContainsIgnoreCase(ItemPresentationUtility.BuildDisplayName(item, showReferenceCodes: false), query)
                || ContainsIgnoreCase(ItemPresentationUtility.BuildTypeDisplayText(item), query)
                || ContainsIgnoreCase(ItemPresentationUtility.GetItemIdCode(item), query);
        }

        private static bool ContainsIgnoreCase(string value, string query)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private int CompareSlots(InventorySlot a, InventorySlot b)
        {
            int result = _sortKey == InventorySortKey.Name
                ? string.Compare(
                    ItemPresentationUtility.BuildDisplayName(a?.Item, showReferenceCodes: false),
                    ItemPresentationUtility.BuildDisplayName(b?.Item, showReferenceCodes: false),
                    StringComparison.CurrentCultureIgnoreCase)
                : GetNumericSortValue(a).CompareTo(GetNumericSortValue(b));

            if (result == 0 && _sortKey != InventorySortKey.Name)
            {
                result = string.Compare(
                    ItemPresentationUtility.BuildDisplayName(a?.Item, showReferenceCodes: false),
                    ItemPresentationUtility.BuildDisplayName(b?.Item, showReferenceCodes: false),
                    StringComparison.CurrentCultureIgnoreCase);
            }

            return _sortDirection == InventorySortDirection.Ascending ? result : -result;
        }

        private float GetNumericSortValue(InventorySlot slot)
        {
            var item = slot?.Item;
            if (item == null)
                return 0f;

            return _sortKey switch
            {
                InventorySortKey.Weight => item.Weight,
                InventorySortKey.Damage => item.Damage,
                InventorySortKey.Armor => item.Defense,
                InventorySortKey.Value => item.Value,
                _ => 0f
            };
        }

        private void SetSort(InventorySortKey key)
        {
            if (_sortKey == key)
            {
                _sortDirection = _sortDirection == InventorySortDirection.Ascending
                    ? InventorySortDirection.Descending
                    : InventorySortDirection.Ascending;
            }
            else
            {
                _sortKey = key;
                _sortDirection = key == InventorySortKey.Name
                    ? InventorySortDirection.Ascending
                    : InventorySortDirection.Descending;
            }

            UpdateSortButtonLabels();
            Refresh();
        }

        private void EnsureSearchSortUi()
        {
            if (!_enableSearchAndSorting || _searchSortUiCreated)
                return;

            _searchSortUiCreated = true;

            RectTransform root = transform as RectTransform;
            if (root == null)
                return;

            RectTransform bar = CreateRect("InventorySearchSortBar", root);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -92f);
            bar.sizeDelta = new Vector2(-28f, 74f);

            VerticalLayoutGroup vertical = bar.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(10, 10, 0, 0);
            vertical.spacing = 4f;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            _searchInput = CreateSearchInput(bar);
            RectTransform header = CreateRect("SortHeader", bar);
            LayoutElement headerLayout = header.gameObject.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 28f;

            HorizontalLayoutGroup row = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 4f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;

            _sortNameButton = CreateSortButton(header, "Name", 220f);
            _sortWeightButton = CreateSortButton(header, "Weight", 72f);
            _sortDamageButton = CreateSortButton(header, "Damage", 72f);
            _sortArmorButton = CreateSortButton(header, "Armor", 72f);
            _sortValueButton = CreateSortButton(header, "Value", 72f);

            UpdateSortButtonLabels();
        }

        private void EnsureSortButtonsWired()
        {
            if (!_enableSearchAndSorting || _sortButtonsWired)
                return;

            EnsureSearchSortUi();

            if (_searchInput != null)
                _searchInput.onValueChanged.AddListener(OnSearchChanged);
            if (_sortNameButton != null)
                _sortNameButton.onClick.AddListener(() => SetSort(InventorySortKey.Name));
            if (_sortWeightButton != null)
                _sortWeightButton.onClick.AddListener(() => SetSort(InventorySortKey.Weight));
            if (_sortDamageButton != null)
                _sortDamageButton.onClick.AddListener(() => SetSort(InventorySortKey.Damage));
            if (_sortArmorButton != null)
                _sortArmorButton.onClick.AddListener(() => SetSort(InventorySortKey.Armor));
            if (_sortValueButton != null)
                _sortValueButton.onClick.AddListener(() => SetSort(InventorySortKey.Value));

            _sortButtonsWired = true;
        }

        private void OnSearchChanged(string value)
        {
            _searchQuery = value ?? string.Empty;
            Refresh();
        }

        private void UpdateSortButtonLabels()
        {
            SetSortButtonLabel(_sortNameButton, "Name", InventorySortKey.Name);
            SetSortButtonLabel(_sortWeightButton, "Weight", InventorySortKey.Weight);
            SetSortButtonLabel(_sortDamageButton, "Damage", InventorySortKey.Damage);
            SetSortButtonLabel(_sortArmorButton, "Armor", InventorySortKey.Armor);
            SetSortButtonLabel(_sortValueButton, "Value", InventorySortKey.Value);
        }

        private void SetSortButtonLabel(Button button, string label, InventorySortKey key)
        {
            if (button == null)
                return;

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                string marker = _sortKey == key
                    ? _sortDirection == InventorySortDirection.Ascending ? " ^" : " v"
                    : string.Empty;
                text.text = label + marker;
            }
        }

        private TMP_InputField CreateSearchInput(RectTransform parent)
        {
            RectTransform root = CreateRect("SearchInput", parent);
            LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 34f;

            Image background = root.gameObject.AddComponent<Image>();
            background.color = new Color(0.18f, 0.15f, 0.10f, 0.82f);

            TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.textViewport = CreateRect("TextViewport", root);
            input.textViewport.anchorMin = Vector2.zero;
            input.textViewport.anchorMax = Vector2.one;
            input.textViewport.offsetMin = new Vector2(10f, 0f);
            input.textViewport.offsetMax = new Vector2(-10f, 0f);
            input.textViewport.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI placeholder = CreateText("Placeholder", input.textViewport, "Search", TextAlignmentOptions.MidlineLeft, 0.55f);
            TextMeshProUGUI text = CreateText("Text", input.textViewport, string.Empty, TextAlignmentOptions.MidlineLeft, 0.92f);
            input.placeholder = placeholder;
            input.textComponent = text;
            return input;
        }

        private Button CreateSortButton(RectTransform parent, string label, float width)
        {
            RectTransform root = CreateRect(label + "SortButton", parent);
            LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = 28f;

            Image image = root.gameObject.AddComponent<Image>();
            image.color = new Color(0.30f, 0.24f, 0.14f, 0.88f);

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            CreateText("Label", root, label, TextAlignmentOptions.Center, 0.88f);
            return button;
        }

        private RectTransform CreateRect(string childName, Transform parent)
        {
            GameObject go = new(childName, typeof(RectTransform));
            go.layer = gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private TextMeshProUGUI CreateText(string childName, Transform parent, string value, TextAlignmentOptions alignment, float alpha)
        {
            RectTransform rect = CreateRect(childName, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = _capacityText != null ? _capacityText.font : text.font;
            text.fontSize = 15f;
            text.color = new Color(0.91f, 0.86f, 0.72f, alpha);
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
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
