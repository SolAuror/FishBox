using UnityEngine;
using Sol.Locomotion;

namespace Sol.HUD
{
    /// <summary>
    /// TAB toggle for the inventory panel. Manages cursor lock and player input state.
    /// Place on the same Canvas as InventoryUI.
    /// </summary>
    public class InventoryToggle : MonoBehaviour
    {
        [SerializeField] private GameObject _inventoryPanel;
        [SerializeField] private TooltipUI _tooltip;
        [SerializeField] private ContextMenuUI _contextMenu;

        public bool IsOpen { get; private set; }

        public static InventoryToggle Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (_inventoryPanel != null)
                _inventoryPanel.SetActive(false);
            else
                Debug.LogWarning("[InventoryToggle] _inventoryPanel is not assigned — inventory will not appear on TAB.", this);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsOpen) return;
            if (_inventoryPanel == null)
            {
                Debug.LogWarning("[InventoryToggle] Cannot open inventory because _inventoryPanel is not assigned.", this);
                return;
            }

            UIStateOwnership.CloseConflictingUi(nameof(InventoryToggle));
            IsOpen = true;

            _inventoryPanel.SetActive(true);

            UIStateOwnership.SetUiCapture(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;

            // Hide sub-UIs.
            if (_tooltip != null) _tooltip.Hide();
            if (_contextMenu != null) _contextMenu.Hide();
            TooltipUI.Instance?.Hide();
            ContextMenuUI.Instance?.Hide();

            if (_inventoryPanel != null)
                _inventoryPanel.SetActive(false);

            UIStateOwnership.SetUiCapture(false);
        }

    }
}

namespace Sol.HUD
{
    /// <summary>
    /// Centralized UI modal ownership helpers to keep cursor/input state consistent
    /// and prevent overlapping modal panels.
    /// </summary>
    internal static class UIStateOwnership
    {
        public static void CloseConflictingUi(string owner)
        {
            if (owner != nameof(InventoryToggle) && InventoryToggle.Instance != null && InventoryToggle.Instance.IsOpen)
                InventoryToggle.Instance.Close();

            if (owner != nameof(TradeUI) && TradeUI.Instance != null && TradeUI.Instance.IsOpen)
                TradeUI.Instance.Close();

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

            if (IsAnyBlockingUiOpen())
                return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (LocomotionInputManager.Instance != null)
                LocomotionInputManager.Instance.UIInputBlocked = false;
        }

        public static bool IsBlockingUiOpen()
        {
            return IsAnyBlockingUiOpen();
        }

        private static bool IsAnyBlockingUiOpen()
        {
            return (InventoryToggle.Instance != null && InventoryToggle.Instance.IsOpen)
                || (TradeUI.Instance != null && TradeUI.Instance.IsOpen);
        }
    }
}
