using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Sol.Actions;
using Sol.Grab;
using Sol.Fishing;

namespace Sol.HUD
{
    /// <summary>
    /// Lightweight right-click context menu. Dynamically populated from item actions.
    /// ONE global instance.
    /// </summary>
    public class ContextMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private RectTransform _panelRect;
        [SerializeField] private Transform _buttonContainer;
        [SerializeField] private Button _buttonPrefab;

        public static ContextMenuUI Instance { get; private set; }

        private readonly List<GameObject> _spawnedButtons = new();
        private InventorySlot _activeSlot;
        private Inventory _activeInventory;
        private Interactor _activeInteractor;
        private Inventory _lootDestinationInventory;
        private bool _isLootMenu;
        private int _showFrame = -1;

        private Canvas _canvas;

        private PointerEventData _cachedPointerData;
        private readonly List<RaycastResult> _cachedRayResults = new();

        private enum ContextMenuAction
        {
            Take,
            TakeAll,
            AddToRod,
            HookToRod,
            UnhookBait,
            DetachLure,
            Use,
            UseAll,
            EquipToggle,
            Drop,
            DropAll
        }

        private readonly struct MenuEntry
        {
            public readonly string Label;
            public readonly ContextMenuAction Action;

            public MenuEntry(string label, ContextMenuAction action)
            {
                Label = label;
                Action = action;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            AutoResolveReferences();
            _canvas = GetComponentInParent<Canvas>();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void AutoResolveReferences()
        {
            if (_panel == null)
                _panel = gameObject;

            if (_panelRect == null)
                _panelRect = _panel != null
                    ? _panel.GetComponent<RectTransform>()
                    : transform as RectTransform;

            if (_buttonContainer == null && _panel != null)
                _buttonContainer = _panel.transform;

            if (_buttonPrefab == null && _buttonContainer != null)
            {
                Button[] candidates = _buttonContainer.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < candidates.Length; i++)
                {
                    Button candidate = candidates[i];
                    if (candidate == null)
                        continue;

                    if (!candidate.transform.IsChildOf(_buttonContainer))
                        continue;

                    _buttonPrefab = candidate;
                    break;
                }
            }
        }

        public static ContextMenuUI ResolveInstance()
        {
            if (Instance != null)
                return Instance;

            ContextMenuUI[] found = Object.FindObjectsByType<ContextMenuUI>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (found == null || found.Length == 0)
                return null;

            ContextMenuUI resolved = found[0];
            if (resolved == null)
                return null;

            if (!resolved.gameObject.activeSelf)
                resolved.gameObject.SetActive(true);
            if (!resolved.enabled)
                resolved.enabled = true;

            return Instance ?? resolved;
        }

        private void Update()
        {
            if (_panel != null && _panel.activeSelf && Time.frameCount > _showFrame)
            {
                var mouse = Mouse.current;
                if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                {
                    if (!IsPointerOverPanel(mouse.position.ReadValue()))
                        Hide();
                }
            }
        }

        private bool IsPointerOverPanel(Vector2 screenPos)
        {
            if (EventSystem.current == null || _panel == null) return false;

            if (_cachedPointerData == null)
                _cachedPointerData = new PointerEventData(EventSystem.current);
            _cachedPointerData.position = screenPos;
            _cachedRayResults.Clear();
            EventSystem.current.RaycastAll(_cachedPointerData, _cachedRayResults);

            var panelTransform = _panel.transform;
            foreach (var r in _cachedRayResults)
            {
                if (r.gameObject.transform.IsChildOf(panelTransform) || r.gameObject == _panel)
                    return true;
            }
            return false;
        }

        public void Show(InventorySlot slot, Inventory inventory, Interactor interactor, Vector2 screenPos)
        {
            if (slot?.Item == null) return;

            if (_buttonPrefab == null)
            {
                Debug.LogWarning("[ContextMenuUI] _buttonPrefab is not assigned in the Inspector.", this);
                return;
            }

            _showFrame = Time.frameCount;
            _activeSlot = slot;
            _activeInventory = inventory;
            _activeInteractor = interactor;
            _lootDestinationInventory = null;
            _isLootMenu = false;

            ClearButtons();

            foreach (var entry in BuildMenuEntries(slot, interactor))
            {
                var btnGO = Instantiate(_buttonPrefab.gameObject, _buttonContainer);
                btnGO.SetActive(true);

                var label = btnGO.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = entry.Label;

                var btn = btnGO.GetComponent<Button>();
                if (btn != null)
                {
                    var captured = entry.Action;
                    btn.onClick.AddListener(() => OnActionClicked(captured));
                }

                _spawnedButtons.Add(btnGO);
            }

            if (_panelRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);

            float scaler = _canvas != null ? _canvas.scaleFactor : 1f;
            Vector2 size = _panelRect != null
                ? _panelRect.rect.size * scaler
                : Vector2.zero;

            float ox = (screenPos.x + size.x > Screen.width) ? -size.x : 0f;
            float oy = (screenPos.y - size.y < 0f) ? size.y : 0f;
            transform.position = new Vector3(screenPos.x + ox, screenPos.y + oy, 0f);

            if (_panel != null) _panel.SetActive(true);
        }

        /// <summary>
        /// Show a minimal loot context menu for transferring items from a source inventory
        /// into a destination inventory. Uses existing TradeController transfer logic.
        /// </summary>
        public void ShowLoot(InventorySlot slot, Inventory sourceInventory, Inventory destinationInventory, Vector2 screenPos)
        {
            if (slot?.Item == null) return;
            if (sourceInventory == null || destinationInventory == null) return;

            if (_buttonPrefab == null)
            {
                Debug.LogWarning("[ContextMenuUI] _buttonPrefab is not assigned in the Inspector.", this);
                return;
            }

            _showFrame = Time.frameCount;
            _activeSlot = slot;
            _activeInventory = sourceInventory;
            _activeInteractor = null;
            _lootDestinationInventory = destinationInventory;
            _isLootMenu = true;

            ClearButtons();

            var entries = new List<MenuEntry>(2)
            {
                new MenuEntry("Take", ContextMenuAction.Take)
            };

            if (slot.Count > 1)
                entries.Add(new MenuEntry("Take All", ContextMenuAction.TakeAll));

            foreach (var entry in entries)
            {
                var btnGO = Instantiate(_buttonPrefab.gameObject, _buttonContainer);
                btnGO.SetActive(true);

                var label = btnGO.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = entry.Label;

                var btn = btnGO.GetComponent<Button>();
                if (btn != null)
                {
                    var captured = entry.Action;
                    btn.onClick.AddListener(() => OnActionClicked(captured));
                }

                _spawnedButtons.Add(btnGO);
            }

            if (_panelRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);

            float scaler = _canvas != null ? _canvas.scaleFactor : 1f;
            Vector2 size = _panelRect != null
                ? _panelRect.rect.size * scaler
                : Vector2.zero;

            float ox = (screenPos.x + size.x > Screen.width) ? -size.x : 0f;
            float oy = (screenPos.y - size.y < 0f) ? size.y : 0f;
            transform.position = new Vector3(screenPos.x + ox, screenPos.y + oy, 0f);

            if (_panel != null) _panel.SetActive(true);
        }

        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
            ClearButtons();
            _activeSlot = null;
            _activeInventory = null;
            _activeInteractor = null;
            _lootDestinationInventory = null;
            _isLootMenu = false;
        }

        private void OnActionClicked(ContextMenuAction action)
        {
            if (_activeSlot != null)
                ExecuteMenuAction(action);

            Hide();
        }

        private List<MenuEntry> BuildMenuEntries(InventorySlot slot, Interactor interactor)
        {
            var entries = new List<MenuEntry>(5);
            var item = slot?.Item;
            if (item == null) return entries;

            bool canUse = item.IsConsumable;
            bool canEquip = item.Type == ItemType.Weapon || item.Type == ItemType.Armor || item.Type == ItemType.Equipable;
            bool canDrop = interactor != null && interactor.IsPlayer;
            bool canAddToRod = false;
            bool canHookToRod = false;
            bool canUnhookBait = false;
            bool canDetachLure = false;
            if (interactor?.Owner != null && interactor.Owner.TryGetComponent<FishingRodState>(out var fishingRodState))
            {
                canAddToRod = item.GetComponent<FishingTackleItem>() != null && fishingRodState.CanLoadTackle(item);
                canHookToRod = fishingRodState.CanLoadBait(item);

                bool isEquippedRod = item.GetComponent<FishingRodItem>() != null
                    && interactor.Owner.TryGetComponent<Equipment>(out var equipment)
                    && equipment.IsEquipped(item);

                if (isEquippedRod)
                {
                    canUnhookBait = fishingRodState.CanDetachBait();
                    canDetachLure = fishingRodState.CanDetachTackle();
                }
            }
            bool isStack = slot.Count > 1;

            if (canAddToRod)
                entries.Add(new MenuEntry("Add to Rod", ContextMenuAction.AddToRod));

            if (canHookToRod)
                entries.Add(new MenuEntry("Hook to Rod", ContextMenuAction.HookToRod));

            if (canUnhookBait)
                entries.Add(new MenuEntry("Unhook Bait", ContextMenuAction.UnhookBait));

            if (canDetachLure)
                entries.Add(new MenuEntry("Detach Lure", ContextMenuAction.DetachLure));

            if (canUse)
            {
                entries.Add(new MenuEntry("Use", ContextMenuAction.Use));
                if (isStack)
                    entries.Add(new MenuEntry("Use All", ContextMenuAction.UseAll));
            }

            if (canEquip)
            {
                bool equipped = interactor?.Owner != null
                    && interactor.Owner.TryGetComponent<Equipment>(out var equipment)
                    && equipment.IsEquipped(item);

                entries.Add(new MenuEntry(equipped ? "Unequip" : "Equip", ContextMenuAction.EquipToggle));
            }

            if (canDrop)
            {
                entries.Add(new MenuEntry("Drop", ContextMenuAction.Drop));
                if (isStack)
                    entries.Add(new MenuEntry("Drop All", ContextMenuAction.DropAll));
            }

            return entries;
        }

        private void ExecuteMenuAction(ContextMenuAction action)
        {
            switch (action)
            {
                case ContextMenuAction.Take:
                    ExecuteLootTransfers(1);
                    break;
                case ContextMenuAction.TakeAll:
                    ExecuteLootTransfers(_activeSlot?.Count ?? 0);
                    break;
                case ContextMenuAction.AddToRod:
                    if (_activeInteractor?.Owner == null || _activeSlot == null || _activeInventory == null)
                        return;
                    if (_activeInteractor.Owner.TryGetComponent<FishingRodState>(out var fishingRodState))
                        fishingRodState.TryLoadTackle(_activeSlot, _activeInventory);
                    break;
                case ContextMenuAction.HookToRod:
                    if (_activeInteractor?.Owner == null || _activeSlot == null || _activeInventory == null)
                        return;
                    if (_activeInteractor.Owner.TryGetComponent<FishingRodState>(out fishingRodState))
                        fishingRodState.TryLoadBait(_activeSlot, _activeInventory);
                    break;
                case ContextMenuAction.UnhookBait:
                    if (_activeInteractor?.Owner == null || _activeInventory == null)
                        return;
                    if (_activeInteractor.Owner.TryGetComponent<FishingRodState>(out fishingRodState))
                        fishingRodState.TryDetachBait(_activeInventory);
                    break;
                case ContextMenuAction.DetachLure:
                    if (_activeInteractor?.Owner == null || _activeInventory == null)
                        return;
                    if (_activeInteractor.Owner.TryGetComponent<FishingRodState>(out fishingRodState))
                        fishingRodState.TryDetachTackle(_activeInventory);
                    break;
                case ContextMenuAction.Use:
                    if (_activeInteractor == null) return;
                    DispatchItemAction(ItemActionType.Use);
                    break;
                case ContextMenuAction.UseAll:
                    if (_activeInteractor == null) return;
                    DispatchRepeated(ItemActionType.Use, _activeSlot?.Count ?? 0);
                    break;
                case ContextMenuAction.EquipToggle:
                    if (_activeInteractor == null) return;
                    DispatchItemAction(ItemActionType.Equip);
                    break;
                case ContextMenuAction.Drop:
                    if (_activeInteractor == null) return;
                    DispatchItemAction(ItemActionType.Drop);
                    break;
                case ContextMenuAction.DropAll:
                    if (_activeInteractor == null) return;
                    DispatchRepeated(ItemActionType.Drop, _activeSlot?.Count ?? 0);
                    break;
            }
        }

        private void ExecuteLootTransfers(int count)
        {
            if (!_isLootMenu || _activeSlot == null || _activeInventory == null || _lootDestinationInventory == null || count <= 0)
                return;

            if (count > 1)
            {
                TradeController.TransferStack(_activeSlot, _activeInventory, _lootDestinationInventory, count);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                if (_activeSlot.Item == null || _activeSlot.Count <= 0)
                    break;

                if (!TradeController.TransferItem(_activeSlot, _activeInventory, _lootDestinationInventory))
                    break;
            }
        }

        private void DispatchItemAction(ItemActionType action)
        {
            if (_activeSlot == null || _activeInteractor == null || ActionSystem.Instance == null)
                return;

            GameAction gameAction = CreateItemAction(action);

            if (gameAction != null)
                ActionSystem.Instance.Dispatch(gameAction, _activeInteractor.Owner);
        }

        private void DispatchRepeated(ItemActionType action, int count)
        {
            if (_activeSlot == null || _activeInteractor == null || ActionSystem.Instance == null || count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                if (_activeSlot.Item == null)
                    break;

                GameAction gameAction = CreateItemAction(action);
                if (gameAction == null)
                    break;

                ActionSystem.Instance.Dispatch(gameAction, _activeInteractor.Owner);
            }
        }

        private GameAction CreateItemAction(ItemActionType action)
        {
            if (_activeSlot == null || _activeInteractor == null)
                return null;

            return action switch
            {
                ItemActionType.Use => new ConsumeAction(_activeSlot, _activeInteractor),
                ItemActionType.Equip => new EquipItemAction(_activeSlot.Item),
                ItemActionType.Drop => new DropAction(_activeSlot, _activeInteractor),
                _ => null
            };
        }

        private void ClearButtons()
        {
            for (int i = _spawnedButtons.Count - 1; i >= 0; i--)
                Destroy(_spawnedButtons[i]);
            _spawnedButtons.Clear();
        }
    }
}
