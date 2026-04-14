using UnityEngine;
using UnityEngine.UI;

namespace Sol.HUD
{
    public sealed class SettingsMenuSystem : MonoBehaviour
    {
        [SerializeField] private Button _tabAudio;
        [SerializeField] private Button _tabGraphics;
        [SerializeField] private Button _tabGameplay;
        [SerializeField] private Button _tabControls;
        [SerializeField] private Button _tabAccessibility;
        [SerializeField] private Object _panelAudio;
        [SerializeField] private Object _panelGraphics;
        [SerializeField] private Object _panelGameplay;
        [SerializeField] private Object _panelControls;
        [SerializeField] private Object _panelAccessibility;
        [SerializeField] private Object _sliderMaster;
        [SerializeField] private Object _sliderMusic;
        [SerializeField] private Object _sliderSFX;
        [SerializeField] private Object _sliderAmbient;
        [SerializeField] private Object _dropdownResolution;
        [SerializeField] private Object _dropdownDisplayMode;
        [SerializeField] private Object _dropdownQuality;
        [SerializeField] private Object _toggleVSync;
        [SerializeField] private Object _dropdownShadows;
        [SerializeField] private Object _dropdownAA;
        [SerializeField] private Object _sliderSensitivity;
        [SerializeField] private Object _toggleInvertY;
        [SerializeField] private Object _sliderFOV;
        [SerializeField] private Object _toggleCrosshair;
        [SerializeField] private ScrollRect _controlsScrollRect;
        [SerializeField] private Transform _bindRowParent;
        [SerializeField] private Object _sliderUIScale;
        [SerializeField] private Object _dropdownSubtitleSize;
        [SerializeField] private Object _dropdownColorblind;
        [SerializeField] private Button _applyButton;
        [SerializeField] private Button _resetDefaultsButton;
        [SerializeField] private Button _backButton;

        public static SettingsMenuSystem Instance { get; private set; }
        public bool IsOpen => gameObject.activeSelf;

        private CanvasGroup _canvasGroup;
        private bool _returnToPause;

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

        public static SettingsMenuSystem ResolveInstance(bool activateIfInactive = true)
        {
            SettingsMenuSystem resolved = Instance ?? UIStateOwnership.Resolve<SettingsMenuSystem>(activateIfInactive);
            if (resolved != null)
                resolved.AutoWire();
            return resolved;
        }

        public void Open(bool returnToPause = false)
        {
            _returnToPause = returnToPause;
            AutoWire();
            UIStateOwnership.CloseConflictingUi(nameof(SettingsMenuSystem));
            SetOpen(true);
            MenuUiUtility.BringToFront(transform.parent);
            MenuUiUtility.BringToFront(transform);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, false);
            ActivateTab("audio");
            UIStateOwnership.SetUiCapture(true);
            MenuUiUtility.SelectButton(_tabAudio != null ? _tabAudio : _backButton);
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            SetOpen(false);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, true);
            UIStateOwnership.SetUiCapture(false);

            if (_returnToPause)
                PauseMenuSystem.ResolveInstance()?.Open();
        }

        private void AutoWire()
        {
            _tabAudio ??= MenuUiUtility.FindButtonByNames(transform, "Audio", "TabAudio");
            _tabGraphics ??= MenuUiUtility.FindButtonByNames(transform, "Graphics", "TabGraphics");
            _tabGameplay ??= MenuUiUtility.FindButtonByNames(transform, "Gameplay", "TabGameplay");
            _tabControls ??= MenuUiUtility.FindButtonByNames(transform, "Controls", "TabControls");
            _tabAccessibility ??= MenuUiUtility.FindButtonByNames(transform, "Accessibility", "TabAccessibility");
            _backButton ??= MenuUiUtility.FindButtonByNames(transform, "Back", "CloseButton");
            _applyButton ??= MenuUiUtility.FindButtonByNames(transform, "Apply", "ApplyButton");
            _resetDefaultsButton ??= MenuUiUtility.FindButtonByNames(transform, "Reset", "ResetDefaultsButton");

            MenuUiUtility.WireButton(_tabAudio, () => ActivateTab("audio"));
            MenuUiUtility.WireButton(_tabGraphics, () => ActivateTab("graphics"));
            MenuUiUtility.WireButton(_tabGameplay, () => ActivateTab("gameplay"));
            MenuUiUtility.WireButton(_tabControls, () => ActivateTab("controls"));
            MenuUiUtility.WireButton(_tabAccessibility, () => ActivateTab("accessibility"));
            MenuUiUtility.WireButton(_applyButton, ApplyCurrentSettings);
            MenuUiUtility.WireButton(_resetDefaultsButton, ResetDefaults);
            MenuUiUtility.WireButton(_backButton, Close);
        }

        private void ActivateTab(string key)
        {
            SetPanelVisible(_panelAudio, key == "audio");
            SetPanelVisible(_panelGraphics, key == "graphics");
            SetPanelVisible(_panelGameplay, key == "gameplay");
            SetPanelVisible(_panelControls, key == "controls");
            SetPanelVisible(_panelAccessibility, key == "accessibility");
        }

        private void ApplyCurrentSettings()
        {
            Debug.Log("[SettingsMenuSystem] Sister settings menu wired. Settings persistence can layer in next.");
        }

        private void ResetDefaults()
        {
            ActivateTab("audio");
        }

        private void SetOpen(bool open)
        {
            MenuUiUtility.SetVisible(gameObject, open);
            _canvasGroup ??= MenuUiUtility.EnsureCanvasGroup(gameObject);
            MenuUiUtility.SetCanvasGroupVisible(_canvasGroup, open);
        }

        private static void SetPanelVisible(Object panelReference, bool visible)
        {
            GameObject panel = panelReference switch
            {
                GameObject gameObject => gameObject,
                Component component => component.gameObject,
                _ => null
            };

            if (panel != null)
                panel.SetActive(visible);
        }
    }
}
