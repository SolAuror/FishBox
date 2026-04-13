using UnityEngine;
using UnityEngine.InputSystem;
using Sol.Locomotion;

namespace Sol.HUD
{
    /// <summary>
    /// Centralized owner for UI hotkeys routed through SolControls UI action map.
    /// </summary>
    [DefaultExecutionOrder(-2)]
    public sealed class UIInputManager : MonoBehaviour
    {
        public static UIInputManager Instance { get; private set; }

        private bool _callbacksRegistered;
        private InputAction _uiTab;
        private InputAction _uiEsc;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                bool thisIsManagerHosted = GetComponent<LocomotionInputManager>() != null;
                bool existingIsManagerHosted = Instance.GetComponent<LocomotionInputManager>() != null;

                // If a manager-hosted instance appears, promote it as the runtime owner.
                if (thisIsManagerHosted && !existingIsManagerHosted)
                {
                    Debug.LogWarning("[UIInputManager] Promoting manager-hosted instance and removing previous duplicate.", this);
                    var previous = Instance;
                    Instance = this;
                    Destroy(previous);
                    return;
                }

                // Only remove the duplicate component; never destroy the host object
                // (e.g., GameManager), which can take core systems down with it.
                Debug.LogWarning("[UIInputManager] Duplicate instance detected. Removing duplicate component.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            TryRegisterCallbacks();
        }

        private void OnEnable()
        {
            _callbacksRegistered = false;
            TryRegisterCallbacks();
        }

        private void Update()
        {
            if (!_callbacksRegistered)
                TryRegisterCallbacks();
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
        }

        private void OnDestroy()
        {
            UnregisterCallbacks();
            if (Instance == this) Instance = null;
        }

        private void TryRegisterCallbacks()
        {
            if (_callbacksRegistered)
                return;

            var controls = LocomotionInputManager.Instance?.Controls;
            if (controls == null)
                return;

            InputActionMap uiMap = controls.UI.Get();
            _uiTab = uiMap.FindAction("Tab", throwIfNotFound: false);
            _uiEsc = uiMap.FindAction("Esc", throwIfNotFound: false);

            if (_uiTab == null || _uiEsc == null)
            {
                Debug.LogWarning("[UIInputManager] Missing one or more UI actions (Tab/Esc).", this);
                return;
            }

            _uiTab.performed += HandleTabPerformed;
            _uiEsc.performed += HandleEscPerformed;
            _callbacksRegistered = true;
        }

        private void UnregisterCallbacks()
        {
            if (!_callbacksRegistered)
                return;

            if (_uiTab != null) _uiTab.performed -= HandleTabPerformed;
            if (_uiEsc != null) _uiEsc.performed -= HandleEscPerformed;

            _uiTab = null;
            _uiEsc = null;
            _callbacksRegistered = false;
        }

        private void HandleTabPerformed(InputAction.CallbackContext _)
        {
            if (TradeUI.Instance != null && TradeUI.Instance.IsOpen)
            {
                TradeUI.Instance.Close();
                return;
            }

            if (InventoryToggle.Instance != null)
                InventoryToggle.Instance.Toggle();
        }

        private void HandleEscPerformed(InputAction.CallbackContext _)
        {
            if (TradeUI.Instance != null && TradeUI.Instance.IsOpen)
            {
                TradeUI.Instance.Close();
                return;
            }

            if (InventoryToggle.Instance != null && InventoryToggle.Instance.IsOpen)
            {
                InventoryToggle.Instance.Close();
                return;
            }
        }
    }
}
