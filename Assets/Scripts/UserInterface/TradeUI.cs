using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sol.HUD
{
    public class TradeUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject _tradePanel;
        [SerializeField] private InventoryUI _playerInventoryUI;
        [SerializeField] private InventoryUI _npcInventoryUI;
        [SerializeField] private TextMeshProUGUI _playerInventoryTitleText;
        [SerializeField] private TextMeshProUGUI _npcInventoryTitleText;

        [Header("Cart")]
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TextMeshProUGUI _cancelButtonText;
        [SerializeField] private TextMeshProUGUI _buyText;
        [SerializeField] private TextMeshProUGUI _sellText;
        [SerializeField] private TextMeshProUGUI _netDirectionText;
        [SerializeField] private TextMeshProUGUI _totalCostText;

        [Header("Fixed Item Info")]
        [SerializeField] private TextMeshProUGUI _itemNameText;
        [SerializeField] private TextMeshProUGUI _itemTypeText;
        [SerializeField] private TextMeshProUGUI _itemFlavourText;
        [SerializeField] private TextMeshProUGUI _itemStatsText;
        [Header("Reference Codes")]
        [SerializeField] private bool _showReferenceCodes = true;
        [SerializeField] private RawImage _previewImage;
        [Header("Stolen Indicator")]
        [SerializeField] private Image _itemNameStolenIcon;
        [SerializeField] private Sprite _stolenIconSprite;

        public bool IsOpen { get; private set; }
        public static TradeUI Instance { get; private set; }

        private readonly Dictionary<InventorySlot, int> _selectedPlayerQuantities = new();
        private readonly Dictionary<InventorySlot, int> _selectedNpcQuantities = new();
        private Inventory _playerInventory;
        private Inventory _npcInventory;
        private bool _isLootMode;
        private bool _isFreeTrade;
        private string _defaultCancelLabel = "Cancel";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            UIStateOwnership.Register<TradeUI>(this);
            TryResolveInventoryPanelReferences();
            TryResolveFixedInfoReferences();
        }

        private void OnDestroy()
        {
            UIStateOwnership.Unregister<TradeUI>();
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            TryResolveInventoryPanelReferences();
            TryResolveFixedInfoReferences();
            if (_tradePanel != null && !IsOpen) _tradePanel.SetActive(false);
            if (_confirmButton != null) _confirmButton.onClick.AddListener(ConfirmTrade);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(CancelTrade);
            CacheCancelButtonLabel();
            RefreshCartSummary();
            ClearInspectedItem();
        }

        public static TradeUI ResolveInstance(bool activateIfInactive = true)
        {
            TradeUI resolved = Instance ?? UIStateOwnership.Resolve<TradeUI>(activateIfInactive);
            if (resolved == null && activateIfInactive)
            {
                TradeUI[] found = UnityEngine.Object.FindObjectsByType<TradeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (found != null && found.Length > 0) resolved = found[0];
            }
            resolved?.TryResolveInventoryPanelReferences();
            return resolved;
        }

        public void OpenTrade(Inventory playerInventory, Inventory npcInventory, bool freeTrade = false)
        {
            OpenInternal(playerInventory, npcInventory, lootMode: false, freeTrade);
        }

        public void OpenLoot(Inventory playerInventory, Inventory lootInventory)
        {
            OpenInternal(playerInventory, lootInventory, lootMode: true, freeTrade: false);
        }

        public void Open(Inventory playerInventory, Inventory npcInventory, bool lootMode = false, bool freeTrade = false)
        {
            OpenInternal(playerInventory, npcInventory, lootMode, freeTrade);
        }

        private void OpenInternal(Inventory playerInventory, Inventory npcInventory, bool lootMode, bool freeTrade)
        {
            if (IsOpen || playerInventory == null || npcInventory == null) return;
            if (_playerInventoryUI == null || _npcInventoryUI == null) TryResolveInventoryPanelReferences();
            TryResolveFixedInfoReferences();
            if (_playerInventoryUI == null || _npcInventoryUI == null)
            {
                Debug.LogWarning("[TradeUI] Inventory panel references are not assigned.", this);
                return;
            }

            if (InventoryToggle.Instance != null && InventoryToggle.Instance.IsOpen) InventoryToggle.Instance.Close();
            UIStateOwnership.CloseConflictingUi(nameof(TradeUI));

            _playerInventory = playerInventory;
            _npcInventory = npcInventory;
            _isLootMode = lootMode;
            _isFreeTrade = !_isLootMode && freeTrade;
            ContextMenuUI.Instance?.Hide();
            bool hideNonTradeableItems = !_isLootMode && !_isFreeTrade;

            ClearCart();

            _playerInventoryUI.SetBinding(playerInventory, new Interactor(playerInventory.gameObject, true),
                OnPlayerSlotLeftClicked, OnPlayerSlotRightClicked, OnPlayerSlotShiftLeftClicked, OnPlayerSlotShiftRightClicked,
                OnSlotHovered, null, InventorySlotDisplayMode.Trade, true, true, false, hideNonTradeableItems,
                _isLootMode ? null : slot => GetSelectedQuantity(_selectedPlayerQuantities, slot) > 0,
                _isLootMode ? null : slot => GetSelectedQuantity(_selectedPlayerQuantities, slot));

            _npcInventoryUI.SetBinding(npcInventory, new Interactor(npcInventory.gameObject, false),
                OnNpcSlotLeftClicked, OnNpcSlotRightClicked, OnNpcSlotShiftLeftClicked, OnNpcSlotShiftRightClicked,
                OnSlotHovered, null, InventorySlotDisplayMode.Trade, true, !_isLootMode, _isLootMode, hideNonTradeableItems,
                _isLootMode ? null : slot => GetSelectedQuantity(_selectedNpcQuantities, slot) > 0,
                _isLootMode ? null : slot => GetSelectedQuantity(_selectedNpcQuantities, slot));

            UpdateInventoryPanelTitles();
            IsOpen = true;
            if (_tradePanel != null) _tradePanel.SetActive(true);
            UIStateOwnership.SetUiCapture(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            ContextMenuUI.Instance?.Hide();
            if (_tradePanel != null) _tradePanel.SetActive(false);
            UIStateOwnership.SetUiCapture(false);
            _playerInventoryUI?.ClearBinding();
            _npcInventoryUI?.ClearBinding();
            _playerInventory = null;
            _npcInventory = null;
            _isLootMode = false;
            _isFreeTrade = false;
            ClearCart();
            ClearInspectedItem();
        }

        private void OnPlayerSlotLeftClicked(InventorySlot slot) { if (_isLootMode) { if (CanDepositIntoLootTarget()) TransferLootFromPlayer(slot, false); return; } ChangeSelection(slot, _selectedPlayerQuantities, 1); InspectSlot(slot); }
        private void OnPlayerSlotRightClicked(InventorySlot slot) { if (_isLootMode) return; ChangeSelection(slot, _selectedPlayerQuantities, -1); InspectSlot(slot); }
        private void OnPlayerSlotShiftLeftClicked(InventorySlot slot) { if (_isLootMode) { if (CanDepositIntoLootTarget()) TransferLootFromPlayer(slot, true); return; } SelectWholeStack(slot, _selectedPlayerQuantities); InspectSlot(slot); }
        private void OnPlayerSlotShiftRightClicked(InventorySlot slot) { if (_isLootMode) return; ClearSelection(slot, _selectedPlayerQuantities); InspectSlot(slot); }
        private void OnNpcSlotLeftClicked(InventorySlot slot) { if (_isLootMode) { TransferLoot(slot, false); return; } ChangeSelection(slot, _selectedNpcQuantities, 1); InspectSlot(slot); }
        private void OnNpcSlotRightClicked(InventorySlot slot) { if (_isLootMode) { ShowLootContext(slot); return; } ChangeSelection(slot, _selectedNpcQuantities, -1); InspectSlot(slot); }
        private void OnNpcSlotShiftLeftClicked(InventorySlot slot) { if (_isLootMode) { TransferLoot(slot, true); return; } SelectWholeStack(slot, _selectedNpcQuantities); InspectSlot(slot); }
        private void OnNpcSlotShiftRightClicked(InventorySlot slot) { if (_isLootMode) { ShowLootContext(slot); return; } ClearSelection(slot, _selectedNpcQuantities); InspectSlot(slot); }
        private void OnSlotHovered(InventorySlot slot) { InspectSlot(slot); }

        private void ChangeSelection(InventorySlot slot, Dictionary<InventorySlot, int> selection, int delta)
        {
            if (slot == null || slot.Item == null || delta == 0) return;
            int next = Mathf.Clamp(GetSelectedQuantity(selection, slot) + delta, 0, Mathf.Max(1, slot.Count));
            if (next <= 0) selection.Remove(slot); else selection[slot] = next;
            RefreshTradePanels();
            RefreshCartSummary();
        }

        private void SelectWholeStack(InventorySlot slot, Dictionary<InventorySlot, int> selection)
        {
            if (slot == null || slot.Item == null) return;
            selection[slot] = Mathf.Max(1, slot.Count);
            RefreshTradePanels();
            RefreshCartSummary();
        }

        private void ClearSelection(InventorySlot slot, Dictionary<InventorySlot, int> selection)
        {
            if (slot == null) return;
            selection.Remove(slot);
            RefreshTradePanels();
            RefreshCartSummary();
        }

        private void ConfirmTrade()
        {
            if (_playerInventory == null || _npcInventory == null || _isLootMode) return;
            List<KeyValuePair<InventorySlot, int>> purchases = new(_selectedNpcQuantities);
            List<KeyValuePair<InventorySlot, int>> sales = new(_selectedPlayerQuantities);

            if (_isFreeTrade)
            {
                foreach (KeyValuePair<InventorySlot, int> entry in sales) ExecuteQuantity(entry.Key, entry.Value, () => Sol.TradeController.TransferItem(entry.Key, _playerInventory, _npcInventory));
                foreach (KeyValuePair<InventorySlot, int> entry in purchases) ExecuteQuantity(entry.Key, entry.Value, () => Sol.TradeController.TransferItem(entry.Key, _npcInventory, _playerInventory));
            }
            else
            {
                foreach (KeyValuePair<InventorySlot, int> entry in sales) ExecuteQuantity(entry.Key, entry.Value, () => Sol.TradeController.SellItem(entry.Key, _playerInventory, _npcInventory));
                foreach (KeyValuePair<InventorySlot, int> entry in purchases) ExecuteQuantity(entry.Key, entry.Value, () => Sol.TradeController.BuyItem(entry.Key, _playerInventory, _npcInventory));
            }

            ClearCart();
            RefreshTradePanels();
        }

        private void ExecuteQuantity(InventorySlot slot, int quantity, Func<bool> transfer)
        {
            if (slot == null || transfer == null || quantity <= 0) return;
            for (int i = 0; i < quantity; i++) { if (slot.Item == null || slot.Count <= 0 || !transfer()) break; }
        }

        private void ClearCart()
        {
            _selectedPlayerQuantities.Clear();
            _selectedNpcQuantities.Clear();
            RefreshCartSummary();
            RefreshTradePanels();
        }

        private void CancelTrade() { ClearCart(); Close(); }

        private void RefreshCartSummary()
        {
            if (_isLootMode)
            {
                if (_buyText != null) { _buyText.gameObject.SetActive(true); _buyText.text = "Loot"; }
                if (_sellText != null) { _sellText.gameObject.SetActive(true); _sellText.text = "Left Click: Take"; }
                if (_netDirectionText != null) { _netDirectionText.gameObject.SetActive(true); _netDirectionText.text = "Right Click: Context"; }
                if (_totalCostText != null) { _totalCostText.text = string.Empty; _totalCostText.gameObject.SetActive(false); }
                if (_confirmButton != null) { _confirmButton.interactable = false; _confirmButton.gameObject.SetActive(false); }
                if (_cancelButtonText != null) _cancelButtonText.text = "Close";
                return;
            }

            if (_totalCostText != null) _totalCostText.gameObject.SetActive(true);
            if (_confirmButton != null) _confirmButton.gameObject.SetActive(true);

            int buying = CalculateSelectionTotal(_selectedNpcQuantities);
            int selling = CalculateSelectionTotal(_selectedPlayerQuantities);
            int netCost = buying - selling;

            if (_isFreeTrade)
            {
                if (_buyText != null) _buyText.text = "Receiving: Free";
                if (_sellText != null) _sellText.text = "Giving: Free";
                if (_netDirectionText != null) _netDirectionText.text = "Shared Exchange";
                if (_totalCostText != null) _totalCostText.text = "0 g";
                if (_confirmButton != null) _confirmButton.interactable = CanConfirmTrade(buying, selling);
                if (_cancelButtonText != null) _cancelButtonText.text = _defaultCancelLabel;
                return;
            }

            if (_buyText != null) _buyText.text = $"Buying: {buying} g";
            if (_sellText != null) _sellText.text = $"Selling: {selling} g";
            if (_netDirectionText != null) _netDirectionText.text = netCost > 0 ? "Spending..." : netCost < 0 ? "Gaining..." : "Fair Trade";
            if (_totalCostText != null) _totalCostText.text = $"{Mathf.Abs(netCost)} g";
            if (_confirmButton != null) _confirmButton.interactable = CanConfirmTrade(buying, selling);
            if (_cancelButtonText != null) _cancelButtonText.text = _defaultCancelLabel;
        }

        private void CacheCancelButtonLabel()
        {
            if (_cancelButtonText == null && _cancelButton != null) _cancelButtonText = _cancelButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (_cancelButtonText != null && !string.IsNullOrWhiteSpace(_cancelButtonText.text)) _defaultCancelLabel = _cancelButtonText.text;
        }

        private void TransferLoot(InventorySlot slot, bool transferAll)
        {
            if (!_isLootMode || _npcInventory == null || _playerInventory == null || slot?.Item == null) return;
            if (transferAll) Sol.TradeController.TransferStack(slot, _npcInventory, _playerInventory, slot.Count);
            else ExecuteQuantity(slot, 1, () => Sol.TradeController.TransferItem(slot, _npcInventory, _playerInventory));
            if (ContainsSlot(_npcInventory, slot)) InspectSlot(slot); else ClearInspectedItem();
        }

        private void TransferLootFromPlayer(InventorySlot slot, bool transferAll)
        {
            if (!_isLootMode || !CanDepositIntoLootTarget() || slot?.Item == null) return;
            if (transferAll) Sol.TradeController.TransferStack(slot, _playerInventory, _npcInventory, slot.Count);
            else Sol.TradeController.TransferItem(slot, _playerInventory, _npcInventory);
            if (ContainsSlot(_playerInventory, slot)) InspectSlot(slot); else ClearInspectedItem();
        }

        private bool CanDepositIntoLootTarget() => _isLootMode && _playerInventory != null && _npcInventory != null && _npcInventory.IsContainer;

        private void ShowLootContext(InventorySlot slot)
        {
            if (!_isLootMode || slot?.Item == null || _npcInventory == null || _playerInventory == null) return;
            InspectSlot(slot);
            ContextMenuUI contextMenu = ContextMenuUI.Instance ?? ContextMenuUI.ResolveInstance();
            if (contextMenu == null || Mouse.current == null) return;
            contextMenu.ShowLoot(slot, _npcInventory, _playerInventory, Mouse.current.position.ReadValue());
        }

        private static bool ContainsSlot(Inventory inventory, InventorySlot slot)
        {
            if (inventory == null || slot == null) return false;
            foreach (InventorySlot current in inventory.Slots) if (current == slot) return true;
            return false;
        }

        private int CalculateSelectionTotal(IEnumerable<KeyValuePair<InventorySlot, int>> slots)
        {
            int total = 0;
            foreach (KeyValuePair<InventorySlot, int> entry in slots) if (entry.Key?.Item != null) total += entry.Key.Item.Value * Mathf.Max(0, entry.Value);
            return total;
        }

        private static int GetSelectedQuantity(Dictionary<InventorySlot, int> selection, InventorySlot slot) => selection != null && slot != null && selection.TryGetValue(slot, out int quantity) ? quantity : 0;

        private static int GetSelectedQuantityTotal(Dictionary<InventorySlot, int> selection)
        {
            if (selection == null || selection.Count == 0) return 0;
            int total = 0;
            foreach (KeyValuePair<InventorySlot, int> entry in selection) if (entry.Key?.Item != null) total += Mathf.Max(0, entry.Value);
            return total;
        }

        private void InspectSlot(InventorySlot slot)
        {
            if (slot?.Item == null) { ClearInspectedItem(); return; }
            TryResolveFixedInfoReferences();
            Sol.Grab.ItemComponent item = slot.Item;
            if (_itemNameText != null) _itemNameText.text = BuildDisplayName(item);
            UpdateStolenIndicator(item);
            if (_itemTypeText != null) _itemTypeText.text = BuildTypeDisplayText(item);
            if (_itemFlavourText != null) { _itemFlavourText.text = item.FlavourText; _itemFlavourText.gameObject.SetActive(!string.IsNullOrEmpty(item.FlavourText)); }
            SetInfoText(_itemStatsText, BuildInspectStatsText(item));
            if (_previewImage != null && ItemPreviewRenderer.Instance != null)
            {
                _previewImage.texture = ItemPreviewRenderer.Instance.RenderTexture;
                _previewImage.gameObject.SetActive(true);
                ItemPreviewRenderer.Instance.Show(item);
            }
        }

        private void ClearInspectedItem()
        {
            SetInfoText(_itemNameText, string.Empty);
            SetInfoText(_itemTypeText, string.Empty);
            SetInfoText(_itemFlavourText, string.Empty);
            SetInfoText(_itemStatsText, string.Empty);
            UpdateStolenIndicator(null);
            if (_previewImage != null) { _previewImage.texture = null; _previewImage.gameObject.SetActive(false); }
            ItemPreviewRenderer.Instance?.Clear();
        }

        private bool CanConfirmTrade(int buying, int selling)
        {
            if (_playerInventory == null || _npcInventory == null) return false;
            if (_isFreeTrade) return GetSelectedQuantityTotal(_selectedNpcQuantities) + GetSelectedQuantityTotal(_selectedPlayerQuantities) > 0;
            if (buying <= 0 && selling <= 0) return false;
            if (_npcInventory.Gold < selling) return false;
            return _playerInventory.Gold + selling >= buying;
        }

        private string BuildInspectStatsText(Sol.Grab.ItemComponent item)
        {
            List<string> stats = new(3);
            if (item.Damage > 0f) stats.Add($"Damage: {item.Damage:0.#}");
            if (item.Defense > 0f) stats.Add($"Defense: {item.Defense:0.#}");
            if (item.Value > 0 && stats.Count < 3) stats.Add($"Value: {item.Value} g");
            if (_showReferenceCodes && !string.IsNullOrWhiteSpace(item.ItemOwnerId)) stats.Add($"Owner ID: {item.ItemOwnerId}");
            return string.Join("\n", stats);
        }

        private void SetInfoText(TextMeshProUGUI label, string value) { if (label == null) return; label.text = value; label.gameObject.SetActive(!string.IsNullOrEmpty(value)); }
        private string BuildDisplayName(Sol.Grab.ItemComponent item) => item == null ? string.Empty : _showReferenceCodes && !string.IsNullOrWhiteSpace(item.ItemId) ? $"{item.ItemName} [{item.ItemId}]" : item.ItemName;
        private string BuildTypeDisplayText(Sol.Grab.ItemComponent item) => item == null ? string.Empty : item.TypeDisplayName;

        private void UpdateStolenIndicator(Sol.Grab.ItemComponent item)
        {
            if (_itemNameStolenIcon == null) return;
            if (_stolenIconSprite != null) _itemNameStolenIcon.sprite = _stolenIconSprite;
            _itemNameStolenIcon.gameObject.SetActive(item != null && item.IsStolen);
        }

        private void RefreshTradePanels()
        {
            if (_playerInventoryUI != null && _playerInventoryUI.Inventory != null) _playerInventoryUI.Refresh();
            if (_npcInventoryUI != null && _npcInventoryUI.Inventory != null) _npcInventoryUI.Refresh();
            UpdateInventoryPanelTitles();
        }

        private void UpdateInventoryPanelTitles()
        {
            SetInventoryTitle(_playerInventoryTitleText, _playerInventory, "Player Inventory");
            SetInventoryTitle(_npcInventoryTitleText, _npcInventory, "NPC Inventory");
        }

        private void TryResolveInventoryPanelReferences()
        {
            if (_playerInventoryUI != null && _npcInventoryUI != null) return;
            InventoryUI[] foundPanels = GetComponentsInChildren<InventoryUI>(true);
            if (foundPanels == null || foundPanels.Length == 0) return;

            for (int i = 0; i < foundPanels.Length; i++)
            {
                InventoryUI panel = foundPanels[i];
                if (panel == null) continue;
                string panelName = panel.gameObject.name;
                if (_playerInventoryUI == null && (panelName.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0 || panelName.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0)) { _playerInventoryUI = panel; continue; }
                if (_npcInventoryUI == null && (panelName.IndexOf("npc", StringComparison.OrdinalIgnoreCase) >= 0 || panelName.IndexOf("trader", StringComparison.OrdinalIgnoreCase) >= 0 || panelName.IndexOf("loot", StringComparison.OrdinalIgnoreCase) >= 0 || panelName.IndexOf("right", StringComparison.OrdinalIgnoreCase) >= 0)) _npcInventoryUI = panel;
            }

            if (_playerInventoryUI == null && foundPanels.Length > 0) _playerInventoryUI = foundPanels[0];
            if (_npcInventoryUI == null) for (int i = 0; i < foundPanels.Length; i++) if (foundPanels[i] != null && foundPanels[i] != _playerInventoryUI) { _npcInventoryUI = foundPanels[i]; break; }
        }

        private void TryResolveFixedInfoReferences()
        {
            Transform root = _tradePanel != null ? _tradePanel.transform : transform;
            Transform fixedTooltipRoot = MenuUiUtility.FindDeep(root, "FixedTooltip") ?? root;

            if (_itemNameText == null)
                _itemNameText = MenuUiUtility.FindTextByNames(fixedTooltipRoot, "NameText", "ItemNameText", "ItemName") as TextMeshProUGUI;

            if (_itemTypeText == null)
                _itemTypeText = MenuUiUtility.FindTextByNames(fixedTooltipRoot, "TypeText", "ItemTypeText", "ItemType") as TextMeshProUGUI;

            if (_itemFlavourText == null)
                _itemFlavourText = MenuUiUtility.FindTextByNames(fixedTooltipRoot, "FlavourText", "FlavorText", "ItemFlavourText", "ItemFlavorText") as TextMeshProUGUI;

            if (_itemStatsText == null)
                _itemStatsText = MenuUiUtility.FindTextByNames(fixedTooltipRoot, "StatsText", "ItemStatsText", "Stats") as TextMeshProUGUI;

            _previewImage ??= MenuUiUtility.FindDeepComponent<RawImage>(fixedTooltipRoot, "PreviewImage")
                ?? MenuUiUtility.FindDeepComponent<RawImage>(fixedTooltipRoot, "ItemPreview");

            if (_itemNameStolenIcon == null)
            {
                Transform stolenIcon = MenuUiUtility.FindDeep(fixedTooltipRoot, "ItemNameStolenIcon")
                    ?? MenuUiUtility.FindDeep(fixedTooltipRoot, "StolenIcon");
                if (stolenIcon != null)
                    _itemNameStolenIcon = stolenIcon.GetComponent<Image>();
            }
        }

        private void SetInventoryTitle(TextMeshProUGUI label, Inventory inventory, string fallback)
        {
            if (label == null) return;
            string title = BuildInventoryTitle(inventory);
            label.text = string.IsNullOrWhiteSpace(title) ? fallback : title;
        }

        private string BuildInventoryTitle(Inventory inventory)
        {
            if (inventory == null) return string.Empty;
            if (inventory.IsContainer) return BuildContainerTitle(inventory);
            string ownerName = GetDisplayNameForOwner(inventory.gameObject);
            return string.IsNullOrWhiteSpace(ownerName) ? "Inventory" : $"{ownerName}'s Inventory";
        }

        private string BuildContainerTitle(Inventory inventory)
        {
            string containerName = GetDisplayNameForObject(inventory.gameObject, "Container");
            string containerId = GetContainerId(inventory.gameObject);
            if (string.IsNullOrWhiteSpace(containerName) || !_showReferenceCodes || string.IsNullOrWhiteSpace(containerId)) return containerName;
            return $"{containerName} [{containerId}]";
        }

        private static string GetContainerId(GameObject containerObject)
        {
            if (containerObject == null) return string.Empty;
            ContainerInteractable interactable = containerObject.GetComponent<ContainerInteractable>();
            return interactable != null ? interactable.ContainerId : string.Empty;
        }

        private static string GetDisplayNameForOwner(GameObject ownerObject)
        {
            if (ownerObject == null) return string.Empty;
            if (ownerObject.TryGetComponent<Sol.AI.NPCSoul>(out Sol.AI.NPCSoul soul) && !string.IsNullOrWhiteSpace(soul.CharacterName)) return soul.CharacterName.Trim();
            return GetDisplayNameForObject(ownerObject, string.Empty);
        }

        private static string GetDisplayNameForObject(GameObject source, string fallback)
        {
            if (source == null) return fallback;
            string displayName = source.name?.Trim() ?? string.Empty;
            const string CloneSuffix = "(Clone)";
            if (displayName.EndsWith(CloneSuffix, StringComparison.Ordinal)) displayName = displayName.Substring(0, displayName.Length - CloneSuffix.Length).TrimEnd();
            return string.IsNullOrWhiteSpace(displayName) ? fallback : displayName;
        }
    }
}
