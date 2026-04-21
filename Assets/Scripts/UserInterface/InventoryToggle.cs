using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sol.Locomotion;

namespace Sol.HUD
{
    /// <summary>
    /// TAB toggle for the inventory panel. Manages cursor lock and player input state.
    /// Place on the same Canvas as InventoryUI.
    /// </summary>
    public class InventoryToggle : MonoBehaviour
    {
        #region Inspector Settings
        [Tooltip("Inspector: tunes inventory panel.")]
        [SerializeField] private GameObject _inventoryPanel;
        [SerializeField] private TooltipUI _tooltip;
        [Tooltip("Inspector: tunes context menu.")]
        [SerializeField] private ContextMenuUI _contextMenu;
        [Tooltip("Inspector: tunes close button.")]
        [SerializeField] private Button _closeButton;
        #endregion

        public bool IsOpen { get; private set; }

        public static InventoryToggle Instance { get; private set; }

        private bool _closeButtonWired;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            UIStateOwnership.Register<InventoryToggle>(this);

            TryResolveReferences();
            EnsureCloseButtonWired();

            if (UIInputManager.Instance == null)
                gameObject.AddComponent<UIInputManager>();
        }

        private void OnDestroy()
        {
            if (_closeButton != null && _closeButtonWired)
                _closeButton.onClick.RemoveListener(Close);

            UIStateOwnership.Unregister<InventoryToggle>();
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            TryResolveReferences();
            EnsureCloseButtonWired();

            if (_inventoryPanel != null)
                _inventoryPanel.SetActive(false);
            else
                Debug.LogWarning("[InventoryToggle] _inventoryPanel is not assigned - inventory will not appear on TAB.", this);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsOpen)
                return;

            if (_inventoryPanel == null)
            {
                TryResolveReferences();
                if (_inventoryPanel == null)
                {
                    Debug.LogWarning("[InventoryToggle] Cannot open inventory because _inventoryPanel is not assigned.", this);
                    return;
                }
            }

            UIStateOwnership.CloseConflictingUi(nameof(InventoryToggle));
            IsOpen = true;

            _inventoryPanel.SetActive(true);

            UIStateOwnership.SetUiCapture(true);
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;

            if (_tooltip != null)
                _tooltip.Hide();
            if (_contextMenu != null)
                _contextMenu.Hide();

            TooltipUI.Instance?.Hide();
            ContextMenuUI.Instance?.Hide();

            if (_inventoryPanel != null)
                _inventoryPanel.SetActive(false);

            UIStateOwnership.SetUiCapture(false);
        }

        private void TryResolveReferences()
        {
            if (_inventoryPanel == null)
            {
                InventoryUI localInventoryUi = GetComponentInChildren<InventoryUI>(true);
                if (localInventoryUi != null)
                    _inventoryPanel = localInventoryUi.gameObject;
            }

            if (_tooltip == null)
                _tooltip = TooltipUI.Instance ?? FindAny<TooltipUI>();

            if (_contextMenu == null)
                _contextMenu = ContextMenuUI.Instance ?? FindAny<ContextMenuUI>();

            TryResolveCloseButton();
        }

        private void TryResolveCloseButton()
        {
            if (_closeButton != null)
                return;

            Transform searchRoot = _inventoryPanel != null ? _inventoryPanel.transform : transform;
            Transform closeTransform = MenuUiUtility.FindDeep(searchRoot, "CloseButton");
            if (closeTransform == null)
                closeTransform = MenuUiUtility.FindDeep(searchRoot, "Close");

            if (closeTransform != null)
                _closeButton = closeTransform.GetComponent<Button>();
        }

        private void EnsureCloseButtonWired()
        {
            if (_closeButton == null || _closeButtonWired)
                return;

            _closeButton.onClick.AddListener(Close);
            _closeButtonWired = true;
        }

        private static T FindAny<T>() where T : Component
        {
            T[] found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found == null || found.Length == 0)
                return null;

            return found[0];
        }
    }
}

namespace Sol.HUD
{
    /// <summary>
    /// Centralized UI modal ownership helpers to keep cursor/input state
    /// consistent and prevent overlapping modal panels.
    /// </summary>
    internal static class UIStateOwnership
    {
        private static readonly Dictionary<Type, MonoBehaviour> _registry = new();

        public static void Register<T>(T instance) where T : MonoBehaviour
        {
            if (instance != null)
                _registry[typeof(T)] = instance;
        }

        public static void Unregister<T>() where T : MonoBehaviour
        {
            _registry.Remove(typeof(T));
        }

        public static T Resolve<T>(bool activateIfInactive) where T : MonoBehaviour
        {
            T instance = GetKnownInstance<T>();
            if (instance != null)
                return instance;

            T[] found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found == null || found.Length == 0)
                return null;

            T resolved = found[0];
            if (resolved != null)
            {
                if (activateIfInactive && !resolved.gameObject.activeSelf)
                    resolved.gameObject.SetActive(true);

                if (!resolved.enabled)
                    resolved.enabled = true;
            }

            return GetKnownInstance<T>() ?? resolved;
        }

        public static T GetKnownInstance<T>() where T : MonoBehaviour
        {
            if (_registry.TryGetValue(typeof(T), out MonoBehaviour instance))
                return instance as T;
            return null;
        }

        public static void CloseConflictingUi(string owner)
        {
            bool openingPauseSubmenu = owner == nameof(SettingsMenuSystem)
                || owner == nameof(SaveLoadMenuSystem);

            if (owner != nameof(InventoryToggle) && InventoryToggle.Instance != null && InventoryToggle.Instance.IsOpen)
                InventoryToggle.Instance.Close();

            if (owner != nameof(TradeUI) && TradeUI.Instance != null && TradeUI.Instance.IsOpen)
                TradeUI.Instance.Close();

            if (owner != nameof(ConversationWindowSystem)
                && ConversationWindowSystem.Instance != null
                && ConversationWindowSystem.Instance.IsVisible)
            {
                ConversationWindowSystem.Instance.Hide();
            }

            if (owner != nameof(DialoguePromptSystem) && DialoguePromptSystem.Instance != null && DialoguePromptSystem.Instance.IsOpen)
                DialoguePromptSystem.Instance.Close();

            if (owner != nameof(SaveLoadMenuSystem) && SaveLoadMenuSystem.Instance != null && SaveLoadMenuSystem.Instance.IsOpen)
                SaveLoadMenuSystem.Instance.Close(false);

            if (owner != nameof(SettingsMenuSystem) && SettingsMenuSystem.Instance != null && SettingsMenuSystem.Instance.IsOpen)
                SettingsMenuSystem.Instance.Close(false);

            if (owner != nameof(RadialMenuSystem) && RadialMenuSystem.Instance != null && RadialMenuSystem.Instance.IsOpen)
                RadialMenuSystem.Instance.Close();

            if (owner != nameof(PauseMenuSystem) && PauseMenuSystem.Instance != null && PauseMenuSystem.Instance.IsOpen)
            {
                if (openingPauseSubmenu)
                    PauseMenuSystem.Instance.HideForSubmenu();
                else
                    PauseMenuSystem.Instance.Close();
            }
        }

        public static void SetUiCapture(bool enabled)
        {
            if (enabled)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (LocomotionInputManager.Instance != null)
                    LocomotionInputManager.Instance.UIInputBlocked = true;
                return;
            }

            if (IsBlockingUiOpen())
                return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (LocomotionInputManager.Instance != null)
                LocomotionInputManager.Instance.UIInputBlocked = false;
        }

        public static bool IsBlockingUiOpen()
        {
            return (InventoryToggle.Instance != null && InventoryToggle.Instance.IsOpen)
                || (TradeUI.Instance != null && TradeUI.Instance.IsOpen)
                || (ConversationWindowSystem.Instance != null && ConversationWindowSystem.Instance.IsVisible)
                || (PauseMenuSystem.Instance != null && PauseMenuSystem.Instance.IsOpen)
                || (SaveLoadMenuSystem.Instance != null && SaveLoadMenuSystem.Instance.IsOpen)
                || (SettingsMenuSystem.Instance != null && SettingsMenuSystem.Instance.IsOpen)
                || (DialoguePromptSystem.Instance != null && DialoguePromptSystem.Instance.IsOpen)
                || (RadialMenuSystem.Instance != null && RadialMenuSystem.Instance.IsOpen);
        }
    }
}

