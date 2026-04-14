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
            if (IsTabBlockedByModal())
                return;

            ConversationWindowSystem conversationUi = UIStateOwnership.Resolve<ConversationWindowSystem>(activateIfInactive: false);
            if (conversationUi != null && conversationUi.IsVisible)
            {
                conversationUi.Hide();
                return;
            }

            TradeUI tradeUi = UIStateOwnership.Resolve<TradeUI>(activateIfInactive: true);
            if (tradeUi != null && tradeUi.IsOpen)
            {
                tradeUi.Close();
                return;
            }

            InventoryToggle inventoryToggle = UIStateOwnership.Resolve<InventoryToggle>(activateIfInactive: true);
            if (inventoryToggle != null)
                inventoryToggle.Toggle();
        }

        private void HandleEscPerformed(InputAction.CallbackContext _)
        {
            if (TryCloseTopModal())
                return;

            PauseMenuSystem pauseMenu = UIStateOwnership.Resolve<PauseMenuSystem>(activateIfInactive: true);
            if (pauseMenu != null)
            {
                pauseMenu.Toggle();
                return;
            }
        }

        private bool TryCloseTopModal()
        {
            DialoguePromptSystem promptUi = UIStateOwnership.Resolve<DialoguePromptSystem>(activateIfInactive: false);
            if (promptUi != null && promptUi.IsOpen)
            {
                promptUi.Close();
                return true;
            }

            ConversationWindowSystem conversationUi = UIStateOwnership.Resolve<ConversationWindowSystem>(activateIfInactive: false);
            if (conversationUi != null && conversationUi.IsVisible)
            {
                conversationUi.Hide();
                return true;
            }

            RadialMenuSystem radialMenu = UIStateOwnership.Resolve<RadialMenuSystem>(activateIfInactive: false);
            if (radialMenu != null && radialMenu.IsOpen)
            {
                radialMenu.Close();
                return true;
            }

            LoadMenuSystem loadMenu = UIStateOwnership.Resolve<LoadMenuSystem>(activateIfInactive: false);
            if (loadMenu != null && loadMenu.IsOpen)
            {
                loadMenu.Close(false);
                return true;
            }

            SaveMenuSystem saveMenu = UIStateOwnership.Resolve<SaveMenuSystem>(activateIfInactive: false);
            if (saveMenu != null && saveMenu.IsOpen)
            {
                saveMenu.Close(false);
                return true;
            }

            SettingsMenuSystem settingsMenu = UIStateOwnership.Resolve<SettingsMenuSystem>(activateIfInactive: false);
            if (settingsMenu != null && settingsMenu.IsOpen)
            {
                settingsMenu.Close();
                return true;
            }

            CharacterMenuSystem characterMenu = UIStateOwnership.Resolve<CharacterMenuSystem>(activateIfInactive: false);
            if (characterMenu != null && characterMenu.IsOpen)
            {
                characterMenu.Close();
                return true;
            }

            SkillMenuSystem skillMenu = UIStateOwnership.Resolve<SkillMenuSystem>(activateIfInactive: false);
            if (skillMenu != null && skillMenu.IsOpen)
            {
                skillMenu.Close();
                return true;
            }

            TradeUI tradeUi = UIStateOwnership.Resolve<TradeUI>(activateIfInactive: true);
            if (tradeUi != null && tradeUi.IsOpen)
            {
                tradeUi.Close();
                return true;
            }

            InventoryToggle inventoryToggle = UIStateOwnership.Resolve<InventoryToggle>(activateIfInactive: true);
            if (inventoryToggle != null && inventoryToggle.IsOpen)
            {
                inventoryToggle.Close();
                return true;
            }

            return false;
        }

        private bool IsTabBlockedByModal()
        {
            PauseMenuSystem pauseMenu = UIStateOwnership.Resolve<PauseMenuSystem>(activateIfInactive: false);
            if (pauseMenu != null && pauseMenu.IsOpen)
                return true;

            SaveMenuSystem saveMenu = UIStateOwnership.Resolve<SaveMenuSystem>(activateIfInactive: false);
            if (saveMenu != null && saveMenu.IsOpen)
                return true;

            LoadMenuSystem loadMenu = UIStateOwnership.Resolve<LoadMenuSystem>(activateIfInactive: false);
            if (loadMenu != null && loadMenu.IsOpen)
                return true;

            SettingsMenuSystem settingsMenu = UIStateOwnership.Resolve<SettingsMenuSystem>(activateIfInactive: false);
            if (settingsMenu != null && settingsMenu.IsOpen)
                return true;

            CharacterMenuSystem characterMenu = UIStateOwnership.Resolve<CharacterMenuSystem>(activateIfInactive: false);
            if (characterMenu != null && characterMenu.IsOpen)
                return true;

            SkillMenuSystem skillMenu = UIStateOwnership.Resolve<SkillMenuSystem>(activateIfInactive: false);
            if (skillMenu != null && skillMenu.IsOpen)
                return true;

            DialoguePromptSystem promptUi = UIStateOwnership.Resolve<DialoguePromptSystem>(activateIfInactive: false);
            if (promptUi != null && promptUi.IsOpen)
                return true;

            RadialMenuSystem radialMenu = UIStateOwnership.Resolve<RadialMenuSystem>(activateIfInactive: false);
            return radialMenu != null && radialMenu.IsOpen;
        }
    }
}

