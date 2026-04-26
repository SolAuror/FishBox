using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Sol.HUD
{
    public sealed class PauseMenuSystem : MenuSystemBase<PauseMenuSystem>
    {
        #region Inspector Settings
        [Tooltip("Inspector: tunes panel rect.")]
        [SerializeField] private RectTransform _panelRect;
        [SerializeField] private Button _resumeButton;
        [Tooltip("Inspector: tunes settings button.")]
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _saveGameButton;
        [Tooltip("Inspector: tunes load game button.")]
        [SerializeField] private Button _loadGameButton;
        [SerializeField] private Button _quitButton;
        [Tooltip("Inspector: tunes pause time on show.")]
        [SerializeField] private bool _pauseTimeOnShow = true;
        #endregion

        public event Action OnResume;
        public event Action OnSettings;
        public event Action OnSaveGame;
        public event Action OnLoadGame;
        public event Action OnQuit;

        public bool IsVisible => IsOpen;

        private float _previousTimeScale;
        private bool _pauseSessionActive;
        private bool _isInitialized;

        protected override void Awake()
        {
            base.Awake();
            if (_instance != this) return;
            EnsureInitialized();
            SetOpen(false);
        }

        protected override void PostResolve()
        {
            EnsureInitialized();
        }

        public new static PauseMenuSystem ResolveInstance(bool activateIfInactive = true)
        {
            PauseMenuSystem resolved = Instance ?? UIStateOwnership.Resolve<PauseMenuSystem>(activateIfInactive);
            if (resolved != null)
                resolved.EnsureInitialized();

            return resolved;
        }

        public void Show()
        {
            if (IsOpen)
                return;

            EnsureUiEventSystem();
            PrepareForInteraction();
            UIStateOwnership.CloseConflictingUi(nameof(PauseMenuSystem));

            if (_pauseTimeOnShow && !_pauseSessionActive)
            {
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                _pauseSessionActive = true;
            }

            SetOpen(true);
            MenuUiUtility.BringToFront(transform.parent);
            MenuUiUtility.BringToFront(transform);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, false);
            UIStateOwnership.SetUiCapture(true);
            MenuUiUtility.SelectButton(_resumeButton);
        }

        public void Hide()
        {
            if (!IsOpen)
                return;

            SetOpen(false);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, true);
            EndPauseSession();
            UIStateOwnership.SetUiCapture(false);
        }

        public void HideForSubmenu()
        {
            if (!IsOpen)
                return;

            SetOpen(false);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, true);
        }

        public void EndPauseSessionFromSubmenu()
        {
            EndPauseSession();
            UIStateOwnership.SetUiCapture(false);
        }

        public void Resume()
        {
            if (!IsOpen)
                return;

            DoResume();
        }

        public void Toggle()
        {
            if (IsOpen)
                Resume();
            else
                Show();
        }

        public void Open()
        {
            Show();
        }

        public void Close()
        {
            Hide();
        }

        private void DoResume()
        {
            Hide();
            OnResume?.Invoke();
        }

        private void DoSettings()
        {
            OnSettings?.Invoke();

            if (SettingsMenuSystem.Instance == null || !SettingsMenuSystem.Instance.IsOpen)
            {
                HideForSubmenu();
                SettingsMenuSystem settingsMenu = SettingsMenuSystem.ResolveInstance(activateIfInactive: true);
                if (settingsMenu != null)
                    settingsMenu.Open(returnToPause: true);
                else
                    Show();
            }
        }

        private void DoSaveGame()
        {
            OnSaveGame?.Invoke();

            if (SaveLoadMenuSystem.Instance == null || !SaveLoadMenuSystem.Instance.IsOpen)
            {
                HideForSubmenu();
                SaveLoadMenuSystem saveLoadMenu = SaveLoadMenuSystem.ResolveInstance(activateIfInactive: true);
                if (saveLoadMenu != null)
                    saveLoadMenu.Open(SaveLoadMode.Save, returnToPause: true);
                else
                    Show();
            }
        }

        private void DoLoadGame()
        {
            OnLoadGame?.Invoke();

            if (SaveLoadMenuSystem.Instance == null || !SaveLoadMenuSystem.Instance.IsOpen)
            {
                HideForSubmenu();
                SaveLoadMenuSystem saveLoadMenu = SaveLoadMenuSystem.ResolveInstance(activateIfInactive: true);
                if (saveLoadMenu != null)
                    saveLoadMenu.Open(SaveLoadMode.Load, returnToPause: true);
                else
                    Show();
            }
        }

        private void DoQuit()
        {
            OnQuit?.Invoke();
        }

        private void EndPauseSession()
        {
            if (!_pauseTimeOnShow || !_pauseSessionActive)
                return;

            Time.timeScale = _previousTimeScale;
            _pauseSessionActive = false;
        }

        private void AutoWire()
        {
            _panelRect ??= MenuUiUtility.FindRectByNames(transform, "Panel");
            _resumeButton ??= MenuUiUtility.FindButtonByNames(transform, "Resume", "ResumeButton");
            _settingsButton ??= MenuUiUtility.FindButtonByNames(transform, "Settings", "SettingsButton");
            _saveGameButton ??= MenuUiUtility.FindButtonByNames(transform, "Save Game", "SaveButton");
            _loadGameButton ??= MenuUiUtility.FindButtonByNames(transform, "Load Game", "LoadButton");
            _quitButton ??= MenuUiUtility.FindButtonByNames(transform, "Quit to Menu", "Quit", "QuitButton");
        }

        private void WireButtons()
        {
            MenuUiUtility.WireButton(_resumeButton, DoResume);
            MenuUiUtility.WireButton(_settingsButton, DoSettings);
            MenuUiUtility.WireButton(_saveGameButton, DoSaveGame);
            MenuUiUtility.WireButton(_loadGameButton, DoLoadGame);
            MenuUiUtility.WireButton(_quitButton, DoQuit);
        }

        private void SetupNavigation()
        {
            Button[] buttons = { _resumeButton, _settingsButton, _saveGameButton, _loadGameButton, _quitButton };

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null)
                    continue;

                Navigation navigation = buttons[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = FindPrevious(buttons, i);
                navigation.selectOnDown = FindNext(buttons, i);
                buttons[i].navigation = navigation;
            }
        }

        private void PrepareForInteraction()
        {
            EnsureRootCanvasScale();
            HideTransientUiBlockers();
            EnsureButtonsInteractable();
        }

        private void EnsureInitialized()
        {
            if (_isInitialized)
                return;

            EnsureUiEventSystem();
            AutoWire();
            WireButtons();
            SetupNavigation();
            _isInitialized = true;
        }

        private void EnsureRootCanvasScale()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform root = canvas != null && canvas.rootCanvas != null
                ? canvas.rootCanvas.transform
                : canvas != null
                    ? canvas.transform
                    : null;

            if (root == null)
                return;

            Vector3 scale = root.localScale;
            if (Mathf.Approximately(scale.x, 0f) || Mathf.Approximately(scale.y, 0f) || Mathf.Approximately(scale.z, 0f))
                root.localScale = Vector3.one;
        }

        private void HideTransientUiBlockers()
        {
            TooltipUI.Instance?.Hide();
            ContextMenuUI.Instance?.Hide();
        }

        private void EnsureButtonsInteractable()
        {
            Button[] buttons = { _resumeButton, _settingsButton, _saveGameButton, _loadGameButton, _quitButton };
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null)
                    continue;

                button.interactable = true;
                MenuUiUtility.MakeButtonClickable(button);
            }
        }

        private static Selectable FindPrevious(Button[] buttons, int current)
        {
            for (int i = current - 1; i >= 0; i--)
            {
                if (buttons[i] != null)
                    return buttons[i];
            }

            for (int i = buttons.Length - 1; i > current; i--)
            {
                if (buttons[i] != null)
                    return buttons[i];
            }

            return null;
        }

        private static Selectable FindNext(Button[] buttons, int current)
        {
            for (int i = current + 1; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                    return buttons[i];
            }

            for (int i = 0; i < current; i++)
            {
                if (buttons[i] != null)
                    return buttons[i];
            }

            return null;
        }

        private static void EnsureUiEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystem = eventSystemGO.AddComponent<EventSystem>();
            }
            else if (!eventSystem.enabled)
            {
                eventSystem.enabled = true;
            }

            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
            else if (!inputModule.enabled)
            {
                inputModule.enabled = true;
            }

            StandaloneInputModule legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
                legacyModule.enabled = false;

            UIInputModuleFix fix = eventSystem.GetComponent<UIInputModuleFix>();
            if (fix == null)
                fix = eventSystem.gameObject.AddComponent<UIInputModuleFix>();
        }
    }
}
