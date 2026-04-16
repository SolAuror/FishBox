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
        [SerializeField] private RectTransform _panelRect;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _saveGameButton;
        [SerializeField] private Button _loadGameButton;
        [SerializeField] private Button _quitButton;
        [SerializeField] private bool _pauseTimeOnShow = true;

        public event Action OnResume;
        public event Action OnSettings;
        public event Action OnSaveGame;
        public event Action OnLoadGame;
        public event Action OnQuit;

        public bool IsVisible => IsOpen;

        private float _previousTimeScale;

        protected override void Awake()
        {
            base.Awake();
            if (_instance != this) return;
            EnsureUiEventSystem();
            AutoWire();
            WireButtons();
            SetupNavigation();
            SetOpen(false);
        }

        public new static PauseMenuSystem ResolveInstance(bool activateIfInactive = true)
        {
            PauseMenuSystem resolved = Instance ?? UIStateOwnership.Resolve<PauseMenuSystem>(activateIfInactive);
            if (resolved == null)
            {
                PauseMenuSystem[] found = UnityEngine.Object.FindObjectsByType<PauseMenuSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (found != null && found.Length > 0)
                    resolved = found[0];
            }

            if (resolved != null)
            {
                EnsureUiEventSystem();
                resolved.AutoWire();
                resolved.WireButtons();
                resolved.SetupNavigation();
            }

            return resolved;
        }

        public void Show()
        {
            if (IsOpen)
                return;

            EnsureUiEventSystem();
            PrepareForInteraction();
            UIStateOwnership.CloseConflictingUi(nameof(PauseMenuSystem));

            if (_pauseTimeOnShow)
            {
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
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

            if (_pauseTimeOnShow)
                Time.timeScale = _previousTimeScale;

            MenuUiUtility.SetBackgroundUiRaycasts(transform, true);
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
                Hide();
                SettingsMenuSystem settingsMenu = SettingsMenuSystem.ResolveInstance(activateIfInactive: true);
                settingsMenu?.Open(returnToPause: true);
            }
        }

        private void DoSaveGame()
        {
            OnSaveGame?.Invoke();

            if (SaveMenuSystem.Instance == null || !SaveMenuSystem.Instance.IsOpen)
            {
                Hide();
                SaveMenuSystem saveMenu = SaveMenuSystem.ResolveInstance(activateIfInactive: true);
                saveMenu?.Open(returnToPause: true);
            }
        }

        private void DoLoadGame()
        {
            OnLoadGame?.Invoke();

            if (LoadMenuSystem.Instance == null || !LoadMenuSystem.Instance.IsOpen)
            {
                Hide();
                LoadMenuSystem loadMenu = LoadMenuSystem.ResolveInstance(activateIfInactive: true);
                loadMenu?.Open(returnToPause: true);
            }
        }

        private void DoQuit()
        {
            if (_pauseTimeOnShow)
                Time.timeScale = _previousTimeScale;

            OnQuit?.Invoke();
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
