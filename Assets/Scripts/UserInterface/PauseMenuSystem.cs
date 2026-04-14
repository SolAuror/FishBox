using UnityEngine;
using UnityEngine.UI;

namespace Sol.HUD
{
    public sealed class PauseMenuSystem : MonoBehaviour
    {
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _saveGameButton;
        [SerializeField] private Button _loadGameButton;

        public static PauseMenuSystem Instance { get; private set; }
        public bool IsOpen => gameObject.activeSelf;

        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _canvasGroup = MenuUiUtility.EnsureCanvasGroup(gameObject);
            AutoWire();
            SetOpen(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static PauseMenuSystem ResolveInstance(bool activateIfInactive = true)
        {
            PauseMenuSystem resolved = Instance ?? UIStateOwnership.Resolve<PauseMenuSystem>(activateIfInactive);
            if (resolved == null)
            {
                PauseMenuSystem[] found = Object.FindObjectsByType<PauseMenuSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (found != null && found.Length > 0)
                    resolved = found[0];
            }

            if (resolved != null)
                resolved.AutoWire();

            return resolved;
        }

        public void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        public void Open()
        {
            if (IsOpen)
                return;

            AutoWire();
            UIStateOwnership.CloseConflictingUi(nameof(PauseMenuSystem));
            SetOpen(true);
            MenuUiUtility.BringToFront(transform.parent);
            MenuUiUtility.BringToFront(transform);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, false);
            UIStateOwnership.SetUiCapture(true);
            MenuUiUtility.SelectButton(_resumeButton);
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            SetOpen(false);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, true);
            UIStateOwnership.SetUiCapture(false);
        }

        private void OpenSettings()
        {
            SettingsMenuSystem.ResolveInstance()?.Open(returnToPause: true);
        }

        private void OpenSaveMenu()
        {
            SaveMenuSystem.ResolveInstance()?.Open(returnToPause: true);
        }

        private void OpenLoadMenu()
        {
            LoadMenuSystem.ResolveInstance()?.Open(returnToPause: true);
        }

        private void AutoWire()
        {
            _resumeButton ??= MenuUiUtility.FindButtonByNames(transform, "Resume", "ResumeButton");
            _settingsButton ??= MenuUiUtility.FindButtonByNames(transform, "Settings", "SettingsButton");
            _saveGameButton ??= MenuUiUtility.FindButtonByNames(transform, "Save Game", "SaveButton");
            _loadGameButton ??= MenuUiUtility.FindButtonByNames(transform, "Load Game", "LoadButton");

            MenuUiUtility.WireButton(_resumeButton, Close);
            MenuUiUtility.WireButton(_settingsButton, OpenSettings);
            MenuUiUtility.WireButton(_saveGameButton, OpenSaveMenu);
            MenuUiUtility.WireButton(_loadGameButton, OpenLoadMenu);
        }

        private void SetOpen(bool open)
        {
            MenuUiUtility.SetVisible(gameObject, open);
            _canvasGroup ??= MenuUiUtility.EnsureCanvasGroup(gameObject);
            MenuUiUtility.SetCanvasGroupVisible(_canvasGroup, open);
        }
    }
}
