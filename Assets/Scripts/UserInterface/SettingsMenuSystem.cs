using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Sol.Locomotion;
using Sol.Settings;

namespace Sol.HUD
{
    public sealed class SettingsMenuSystem : MenuSystemBase<SettingsMenuSystem>
    {
        #region Inspector Settings
        [Tooltip("Inspector: tunes tab audio.")]
        [SerializeField] private Button _tabAudio;
        [SerializeField] private Button _tabGraphics;
        [Tooltip("Inspector: tunes tab gameplay.")]
        [SerializeField] private Button _tabGameplay;
        [SerializeField] private Button _tabControls;
        [Tooltip("Inspector: tunes tab accessibility.")]
        [SerializeField] private Button _tabAccessibility;
        [SerializeField] private UnityEngine.Object _panelAudio;
        [Tooltip("Inspector: tunes panel graphics.")]
        [SerializeField] private UnityEngine.Object _panelGraphics;
        [SerializeField] private UnityEngine.Object _panelGameplay;
        [Tooltip("Inspector: tunes panel controls.")]
        [SerializeField] private UnityEngine.Object _panelControls;
        [SerializeField] private UnityEngine.Object _panelAccessibility;
        [Tooltip("Inspector: tunes slider master.")]
        [SerializeField] private UnityEngine.Object _sliderMaster;
        [SerializeField] private UnityEngine.Object _sliderMusic;
        [Tooltip("Inspector: tunes slider sfx.")]
        [SerializeField] private UnityEngine.Object _sliderSFX;
        [SerializeField] private UnityEngine.Object _sliderAmbient;
        [Tooltip("Inspector: tunes dropdown resolution.")]
        [SerializeField] private UnityEngine.Object _dropdownResolution;
        [SerializeField] private UnityEngine.Object _dropdownDisplayMode;
        [Tooltip("Inspector: tunes dropdown quality.")]
        [SerializeField] private UnityEngine.Object _dropdownQuality;
        [SerializeField] private UnityEngine.Object _toggleVSync;
        [Tooltip("Inspector: tunes dropdown shadows.")]
        [SerializeField] private UnityEngine.Object _dropdownShadows;
        [SerializeField] private UnityEngine.Object _dropdownAA;
        [Tooltip("Inspector: tunes slider sensitivity.")]
        [SerializeField] private UnityEngine.Object _sliderSensitivity;
        [SerializeField] private UnityEngine.Object _toggleInvertY;
        [Tooltip("Inspector: tunes slider fov.")]
        [SerializeField] private UnityEngine.Object _sliderFOV;
        [SerializeField] private UnityEngine.Object _toggleCrosshair;
        [Tooltip("Inspector: tunes controls scroll rect.")]
        [SerializeField] private ScrollRect _controlsScrollRect;
        [SerializeField] private Transform _bindRowParent;
        [Tooltip("Inspector: tunes slider uiscale.")]
        [SerializeField] private UnityEngine.Object _sliderUIScale;
        [SerializeField] private UnityEngine.Object _dropdownSubtitleSize;
        [Tooltip("Inspector: tunes dropdown colorblind.")]
        [SerializeField] private UnityEngine.Object _dropdownColorblind;
        [SerializeField] private Button _applyButton;
        [Tooltip("Inspector: tunes reset defaults button.")]
        [SerializeField] private Button _resetDefaultsButton;
        [Tooltip("Inspector: tunes back button.")]
        [SerializeField] private Button _backButton;
        #endregion

        public bool ReturnsToPause => _returnToPause;

        private readonly List<Resolution> _filteredResolutions = new();
        private readonly List<GameObject> _spawnedBindRows = new();
        private Resolution[] _availableResolutions;
        private GameObject _bindRowTemplate;
        private InputActionRebindingExtensions.RebindingOperation _activeRebind;
        private SettingsData _workingCopy;
        private bool _returnToPause;

        #region Lifecycle
        protected override void Awake()
        {
            base.Awake();
            if (_instance != this) return;
            AutoWire();
            CacheBindRowTemplate();
            WireUiEvents();
            ApplyPersistedDisplaySettings();
            SetOpen(false);
        }

        protected override void OnDestroy()
        {
            _activeRebind?.Dispose();
            base.OnDestroy();
        }

        protected override void PostResolve()
        {
            AutoWire();
            CacheBindRowTemplate();
            WireUiEvents();
        }
        #endregion

        #region Public API
        public void Open(bool returnToPause = false)
        {
            _returnToPause = returnToPause;
            AutoWire();
            CacheBindRowTemplate();
            // Force settings to be modal so we don't end up with stacked interactive menus.
            UIStateOwnership.CloseConflictingUi(nameof(SettingsMenuSystem));
            _workingCopy = new SettingsData(SettingsPersistence.Current);
            PopulateAllControls();
            SetOpen(true);
            MenuUiUtility.BringToFront(transform.parent);
            MenuUiUtility.BringToFront(transform);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, false);
            ActivateTab("audio");
            UIStateOwnership.SetUiCapture(true);
            MenuUiUtility.SelectButton(_tabAudio != null ? _tabAudio : _backButton);
        }

        public void Close() => Close(false);

        public void Close(bool reopenPause)
        {
            if (!IsOpen) return;

            bool wasPauseSubmenu = _returnToPause;
            _returnToPause = false;

            _activeRebind?.Cancel();
            SetOpen(false);
            MenuUiUtility.SetBackgroundUiRaycasts(transform, true);

            if (reopenPause)
            {
                PauseMenuSystem.ResolveInstance()?.Show();
                return;
            }

            if (wasPauseSubmenu)
            {
                PauseMenuSystem pauseMenu = PauseMenuSystem.ResolveInstance(activateIfInactive: false);
                if (pauseMenu != null)
                {
                    // Keep pause flow consistent when settings was opened as a pause submenu.
                    pauseMenu.EndPauseSessionFromSubmenu();
                    return;
                }
            }

            UIStateOwnership.SetUiCapture(false);
        }

        public static void ApplyPersistedInputAndGameplaySettings()
        {
            SettingsData data = SettingsPersistence.Current;
            ApplyGameplaySettings(data);
            ApplyControlOverrides(data);
        }
        #endregion

        #region Static Apply Helpers
        public static void ApplyGameplaySettings(SettingsData data)
        {
            if (data == null) return;
            if (Camera.main != null) Camera.main.fieldOfView = data.FieldOfView;
            if (TryResolvePlayerRoot(out GameObject playerRoot))
            {
                LocomotionController locomotion = playerRoot.GetComponent<LocomotionController>();
                if (locomotion != null)
                {
                    float invert = data.InvertYAxis ? -1f : 1f;
                    locomotion.lookSenseH = data.MouseSensitivity;
                    locomotion.lookSenseV = data.MouseSensitivity * invert;
                }
            }
            FindFirstObjectByType<CrosshairUI>(FindObjectsInactive.Include)?.SetCrosshairVisible(data.ShowCrosshair);
        }

        public static void ApplyAccessibilitySettings(SettingsData data)
        {
            if (data == null) return;
            CanvasScaler[] scalers = FindObjectsByType<CanvasScaler>(FindObjectsSortMode.None);
            for (int i = 0; i < scalers.Length; i++)
                if (scalers[i] != null && scalers[i].uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
                    scalers[i].scaleFactor = data.UIScale;
        }

        public static void ApplyGraphicsSettings(SettingsData data)
        {
            if (data == null) return;
            FullScreenMode fullscreenMode = NormalizeFullscreenMode(data.FullscreenMode);
            int width = Mathf.Max(640, data.ResolutionWidth);
            int height = Mathf.Max(360, data.ResolutionHeight);
            RefreshRate refreshRate = new RefreshRate
            {
                numerator = (uint)Mathf.Max(1, Mathf.RoundToInt((float)data.RefreshRate)),
                denominator = 1
            };

            // In borderless fullscreen we should follow the desktop mode.
            if (fullscreenMode == FullScreenMode.FullScreenWindow || fullscreenMode == FullScreenMode.MaximizedWindow)
            {
                fullscreenMode = FullScreenMode.FullScreenWindow;
                Resolution native = Screen.currentResolution;
                width = Mathf.Max(640, native.width);
                height = Mathf.Max(360, native.height);
                Screen.SetResolution(width, height, fullscreenMode);
            }
            else if (fullscreenMode == FullScreenMode.ExclusiveFullScreen)
            {
                // Only request exclusive fullscreen at explicit resolutions.
                Screen.SetResolution(width, height, fullscreenMode, refreshRate);
            }
            else
            {
                // Windowed mode should use the selected size and avoid forcing a monitor refresh.
                Screen.SetResolution(width, height, FullScreenMode.Windowed);
            }

            if (data.QualityLevel >= 0)
                QualitySettings.SetQualityLevel(data.QualityLevel, true);
            QualitySettings.vSyncCount = Mathf.Max(0, data.VSyncCount);
            switch (Mathf.Clamp(data.ShadowQuality, 0, 3))
            {
                case 0: QualitySettings.shadows = ShadowQuality.Disable; break;
                case 1: QualitySettings.shadows = ShadowQuality.HardOnly; QualitySettings.shadowResolution = ShadowResolution.Low; break;
                case 2: QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowResolution = ShadowResolution.Medium; break;
                case 3: QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowResolution = ShadowResolution.High; break;
            }
            QualitySettings.antiAliasing = Mathf.Clamp(data.AntiAliasing, 0, 8);
        }

        public static void ApplyControlOverrides(SettingsData data)
        {
            SolControls controls = LocomotionInputManager.Instance?.Controls;
            if (data == null || controls == null) return;
            controls.asset.RemoveAllBindingOverrides();
            if (string.IsNullOrWhiteSpace(data.ControlOverridesJson)) return;
            try { controls.asset.LoadBindingOverridesFromJson(data.ControlOverridesJson); }
            catch (Exception ex) { Debug.LogWarning($"[SettingsMenuSystem] Failed to load binding overrides: {ex.Message}"); }
        }
        #endregion

        #region UI Population
        private void PopulateAllControls()
        {
            PopulateAudio();
            PopulateGraphics();
            PopulateGameplay();
            PopulateControls();
            PopulateAccessibility();
        }

        private void PopulateAudio()
        {
            Slider master = SliderRef(ref _sliderMaster, "Slider_Master");
            Slider music = SliderRef(ref _sliderMusic, "Slider_Music");
            Slider sfx = SliderRef(ref _sliderSFX, "Slider_SFX");
            Slider ambient = SliderRef(ref _sliderAmbient, "Slider_Ambient");
            if (master != null) { master.minValue = 0f; master.maxValue = 100f; master.value = _workingCopy.MasterVolume; }
            if (music != null) { music.minValue = 0f; music.maxValue = 100f; music.value = _workingCopy.MusicVolume; }
            if (sfx != null) { sfx.minValue = 0f; sfx.maxValue = 100f; sfx.value = _workingCopy.SFXVolume; }
            if (ambient != null) { ambient.minValue = 0f; ambient.maxValue = 100f; ambient.value = _workingCopy.AmbientVolume; }
        }

        private void PopulateGraphics()
        {
            TMP_Dropdown resolution = DropdownRef(ref _dropdownResolution, "Dropdown_Resolution");
            TMP_Dropdown displayMode = DropdownRef(ref _dropdownDisplayMode, "Dropdown_DisplayMode");
            TMP_Dropdown quality = DropdownRef(ref _dropdownQuality, "Dropdown_Quality");
            Toggle vSync = ToggleRef(ref _toggleVSync, "Toggle_VSync");
            TMP_Dropdown shadows = DropdownRef(ref _dropdownShadows, "Dropdown_Shadows");
            TMP_Dropdown antiAliasing = DropdownRef(ref _dropdownAA, "Dropdown_AA");

            if (resolution != null)
            {
                _filteredResolutions.Clear();
                _availableResolutions = Screen.resolutions;
                HashSet<string> seen = new();
                foreach (Resolution res in _availableResolutions)
                {
                    // Collapse refresh-rate variants so the dropdown stays readable.
                    string key = $"{res.width}x{res.height}";
                    if (seen.Add(key)) _filteredResolutions.Add(res);
                }
                resolution.ClearOptions();
                int selected = 0;
                List<string> options = new();
                for (int i = 0; i < _filteredResolutions.Count; i++)
                {
                    Resolution res = _filteredResolutions[i];
                    options.Add($"{res.width} x {res.height}");
                    if (res.width == _workingCopy.ResolutionWidth && res.height == _workingCopy.ResolutionHeight) selected = i;
                }
                resolution.AddOptions(options);
                resolution.value = selected;
            }

            if (displayMode != null)
            {
                displayMode.ClearOptions();
                displayMode.AddOptions(new List<string> { "Fullscreen", "Borderless Window", "Windowed" });
                displayMode.value = _workingCopy.FullscreenMode switch
                {
                    (int)FullScreenMode.ExclusiveFullScreen => 0,
                    (int)FullScreenMode.FullScreenWindow => 1,
                    (int)FullScreenMode.Windowed => 2,
                    _ => 1
                };
            }

            if (quality != null)
            {
                quality.ClearOptions();
                quality.AddOptions(new List<string>(QualitySettings.names));
                quality.value = _workingCopy.QualityLevel >= 0 ? Mathf.Clamp(_workingCopy.QualityLevel, 0, QualitySettings.names.Length - 1) : QualitySettings.GetQualityLevel();
            }
            if (vSync != null) vSync.isOn = _workingCopy.VSyncCount > 0;
            if (shadows != null)
            {
                shadows.ClearOptions();
                shadows.AddOptions(new List<string> { "Off", "Low", "Medium", "High" });
                shadows.value = Mathf.Clamp(_workingCopy.ShadowQuality, 0, 3);
            }
            if (antiAliasing != null)
            {
                antiAliasing.ClearOptions();
                antiAliasing.AddOptions(new List<string> { "Off", "2x", "4x", "8x" });
                antiAliasing.value = _workingCopy.AntiAliasing switch { 0 => 0, 2 => 1, 4 => 2, 8 => 3, _ => 1 };
            }
        }

        private void PopulateGameplay()
        {
            Slider sensitivity = SliderRef(ref _sliderSensitivity, "Slider_Sensitivity");
            Toggle invertY = ToggleRef(ref _toggleInvertY, "Toggle_InvertY");
            Slider fov = SliderRef(ref _sliderFOV, "Slider_FOV");
            Toggle crosshair = ToggleRef(ref _toggleCrosshair, "Toggle_Crosshair");
            if (sensitivity != null) { sensitivity.minValue = 0.01f; sensitivity.maxValue = 2f; sensitivity.value = _workingCopy.MouseSensitivity; }
            if (invertY != null) invertY.isOn = _workingCopy.InvertYAxis;
            if (fov != null) { fov.minValue = 60f; fov.maxValue = 120f; fov.value = _workingCopy.FieldOfView; }
            if (crosshair != null) crosshair.isOn = _workingCopy.ShowCrosshair;
        }

        private void PopulateControls()
        {
            if (_bindRowParent == null)
            {
                Debug.LogWarning("[SettingsMenuSystem] PopulateControls skipped: _bindRowParent is null.", this);
                return;
            }
            if (_bindRowTemplate == null)
            {
                Debug.LogWarning("[SettingsMenuSystem] PopulateControls skipped: bind row template not cached.", this);
                return;
            }
            if (LocomotionInputManager.Instance?.Controls == null)
            {
 Debug.LogWarning("[SettingsMenuSystem] PopulateControls skipped: LocomotionInputManager.Instance or Controls is null - is the player scene loaded?", this);
                return;
            }
            // Re-apply in-memory overrides first so the binding list mirrors the current working copy.
            ApplyControlOverrides(_workingCopy);
            InputActionMap map = LocomotionInputManager.Instance.Controls.Default.Get();
            int rowIndex = 0;
            rowIndex = PopulateBindingSection(map, "MousenKeyboard", "Keyboard & Mouse", rowIndex);
            rowIndex = PopulateBindingSection(map, "Controller", "Controller", rowIndex);
            for (int i = rowIndex; i < _spawnedBindRows.Count; i++) _spawnedBindRows[i].SetActive(false);
            if (_controlsScrollRect != null && _controlsScrollRect.content != null) _controlsScrollRect.verticalNormalizedPosition = 1f;
        }

        private void PopulateAccessibility()
        {
            Slider uiScale = SliderRef(ref _sliderUIScale, "Slider_UIScale");
            TMP_Dropdown subtitle = DropdownRef(ref _dropdownSubtitleSize, "Dropdown_SubtitleSize");
            TMP_Dropdown colorblind = DropdownRef(ref _dropdownColorblind, "Dropdown_Colorblind");
            if (uiScale != null) { uiScale.minValue = 0.75f; uiScale.maxValue = 1.5f; uiScale.value = _workingCopy.UIScale; }
            if (subtitle != null)
            {
                subtitle.ClearOptions();
                subtitle.AddOptions(new List<string> { "Small", "Medium", "Large" });
                subtitle.value = Mathf.Clamp(_workingCopy.SubtitleSize, 0, 2);
            }
            if (colorblind != null)
            {
                colorblind.ClearOptions();
                colorblind.AddOptions(new List<string> { "None", "Protanopia", "Deuteranopia", "Tritanopia" });
                colorblind.value = Mathf.Clamp(_workingCopy.ColorblindMode, 0, 3);
            }
        }

        private void ActivateTab(string key)
        {
            SetPanelVisible(_panelAudio, key == "audio");
            SetPanelVisible(_panelGraphics, key == "graphics");
            SetPanelVisible(_panelGameplay, key == "gameplay");
            SetPanelVisible(_panelControls, key == "controls");
            SetPanelVisible(_panelAccessibility, key == "accessibility");
        }
        #endregion

        #region Mutations & Rebinding
        private void ApplyCurrentSettings()
        {
            _workingCopy ??= new SettingsData(SettingsPersistence.Current);
            ReadControlsIntoWorkingCopy();
            SaveControlOverrides();
            ApplyAudioSettings(_workingCopy);
            ApplyGraphicsSettings(_workingCopy);
            ApplyGameplaySettings(_workingCopy);
            ApplyAccessibilitySettings(_workingCopy);
            SettingsPersistence.Save(_workingCopy);
        }

        private void ResetDefaults()
        {
            _activeRebind?.Cancel();
            LocomotionInputManager.Instance?.Controls?.asset.RemoveAllBindingOverrides();
            _workingCopy = new SettingsData();
            PopulateAllControls();
            ActivateTab("audio");
        }

        private void ReadControlsIntoWorkingCopy()
        {
            Slider master = SliderRef(ref _sliderMaster, "Slider_Master");
            Slider music = SliderRef(ref _sliderMusic, "Slider_Music");
            Slider sfx = SliderRef(ref _sliderSFX, "Slider_SFX");
            Slider ambient = SliderRef(ref _sliderAmbient, "Slider_Ambient");
            TMP_Dropdown resolution = DropdownRef(ref _dropdownResolution, "Dropdown_Resolution");
            TMP_Dropdown displayMode = DropdownRef(ref _dropdownDisplayMode, "Dropdown_DisplayMode");
            TMP_Dropdown quality = DropdownRef(ref _dropdownQuality, "Dropdown_Quality");
            Toggle vSync = ToggleRef(ref _toggleVSync, "Toggle_VSync");
            TMP_Dropdown shadows = DropdownRef(ref _dropdownShadows, "Dropdown_Shadows");
            TMP_Dropdown antiAliasing = DropdownRef(ref _dropdownAA, "Dropdown_AA");
            Slider sensitivity = SliderRef(ref _sliderSensitivity, "Slider_Sensitivity");
            Toggle invertY = ToggleRef(ref _toggleInvertY, "Toggle_InvertY");
            Slider fov = SliderRef(ref _sliderFOV, "Slider_FOV");
            Toggle crosshair = ToggleRef(ref _toggleCrosshair, "Toggle_Crosshair");
            Slider uiScale = SliderRef(ref _sliderUIScale, "Slider_UIScale");
            TMP_Dropdown subtitle = DropdownRef(ref _dropdownSubtitleSize, "Dropdown_SubtitleSize");
            TMP_Dropdown colorblind = DropdownRef(ref _dropdownColorblind, "Dropdown_Colorblind");

            if (master != null) _workingCopy.MasterVolume = master.value;
            if (music != null) _workingCopy.MusicVolume = music.value;
            if (sfx != null) _workingCopy.SFXVolume = sfx.value;
            if (ambient != null) _workingCopy.AmbientVolume = ambient.value;

            if (resolution != null && _filteredResolutions.Count > 0)
            {
                Resolution selected = _filteredResolutions[Mathf.Clamp(resolution.value, 0, _filteredResolutions.Count - 1)];
                _workingCopy.ResolutionWidth = selected.width;
                _workingCopy.ResolutionHeight = selected.height;
                _workingCopy.RefreshRate = selected.refreshRateRatio.value;
            }

            if (displayMode != null)
            {
                _workingCopy.FullscreenMode = displayMode.value switch
                {
                    0 => (int)FullScreenMode.ExclusiveFullScreen,
                    1 => (int)FullScreenMode.FullScreenWindow,
                    2 => (int)FullScreenMode.Windowed,
                    _ => (int)FullScreenMode.FullScreenWindow
                };
            }

            if (quality != null) _workingCopy.QualityLevel = quality.value;
            if (vSync != null) _workingCopy.VSyncCount = vSync.isOn ? 1 : 0;
            if (shadows != null) _workingCopy.ShadowQuality = shadows.value;
            if (antiAliasing != null) _workingCopy.AntiAliasing = antiAliasing.value switch { 0 => 0, 1 => 2, 2 => 4, 3 => 8, _ => 2 };
            if (sensitivity != null) _workingCopy.MouseSensitivity = sensitivity.value;
            if (invertY != null) _workingCopy.InvertYAxis = invertY.isOn;
            if (fov != null) _workingCopy.FieldOfView = fov.value;
            if (crosshair != null) _workingCopy.ShowCrosshair = crosshair.isOn;
            if (uiScale != null) _workingCopy.UIScale = uiScale.value;
            if (subtitle != null) _workingCopy.SubtitleSize = subtitle.value;
            if (colorblind != null) _workingCopy.ColorblindMode = colorblind.value;
        }

        private void SaveControlOverrides()
        {
            SolControls controls = LocomotionInputManager.Instance?.Controls;
            if (controls != null)
                _workingCopy.ControlOverridesJson = controls.asset.SaveBindingOverridesAsJson();
        }

        private void StartRebind(InputAction action, int bindingIndex, TMP_Text label, Button button)
        {
            _activeRebind?.Cancel();
            if (label != null) label.text = "Press any key...";
            if (button != null) button.interactable = false;
            action.Disable();
            // We ignore mouse here on purpose; this menu is meant for keyboard/gamepad rebinding.
            _activeRebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("Mouse")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(op =>
                {
                    action.Enable();
                    op.Dispose();
                    _activeRebind = null;
                    if (label != null) label.text = InputControlPath.ToHumanReadableString(action.bindings[bindingIndex].effectivePath, InputControlPath.HumanReadableStringOptions.OmitDevice);
                    if (button != null) button.interactable = true;
                })
                .OnCancel(op =>
                {
                    action.Enable();
                    op.Dispose();
                    _activeRebind = null;
                    if (label != null) label.text = InputControlPath.ToHumanReadableString(action.bindings[bindingIndex].effectivePath, InputControlPath.HumanReadableStringOptions.OmitDevice);
                    if (button != null) button.interactable = true;
                })
                .Start();
        }
        #endregion

        #region Internal Wiring
        private void CacheBindRowTemplate()
        {
            if (_bindRowTemplate != null) return;

            if (_bindRowParent == null)
            {
                Transform fallbackParent = MenuUiUtility.FindDeep(transform, "BindRows");
                if (fallbackParent != null) _bindRowParent = fallbackParent;
            }

            if (_bindRowParent == null)
            {
                Debug.LogWarning("[SettingsMenuSystem] _bindRowParent is not wired and no 'BindRows' child found.", this);
                return;
            }

            Transform template = _bindRowParent.childCount > 0 ? _bindRowParent.GetChild(0) : MenuUiUtility.FindDeep(transform, "BindRowTemplate");
            if (template == null)
            {
                Debug.LogWarning($"[SettingsMenuSystem] No bind row template found under '{_bindRowParent.name}'.", this);
                return;
            }

            _bindRowTemplate = template.gameObject;
            if (_bindRowTemplate.transform.parent != _bindRowParent)
                _bindRowTemplate.transform.SetParent(_bindRowParent, false);
            _bindRowTemplate.SetActive(false);
        }

        private GameObject GetOrCreateBindRow(int index)
        {
            while (_spawnedBindRows.Count <= index)
            {
                if (_bindRowTemplate == null) return null;
                // Pool rows once and reuse them to avoid alloc spikes when reopening settings.
                GameObject row = Instantiate(_bindRowTemplate, _bindRowParent);
                row.SetActive(true);
                _spawnedBindRows.Add(row);
            }
            _spawnedBindRows[index].SetActive(true);
            return _spawnedBindRows[index];
        }

        private int PopulateBindingSection(InputActionMap map, string group, string headerText, int rowIndex)
        {
            bool hasAny = false;
            foreach (InputAction action in map.actions)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (BindingMatchesGroup(action.bindings[i], group)) { hasAny = true; break; }
                }
                if (hasAny) break;
            }
            if (!hasAny) return rowIndex;

            GameObject header = GetOrCreateBindRow(rowIndex++);
            if (header != null) ConfigureHeaderRow(header, headerText);

            foreach (InputAction action in map.actions)
            {
                for (int bindingIndex = 0; bindingIndex < action.bindings.Count; bindingIndex++)
                {
                    InputBinding binding = action.bindings[bindingIndex];
                    if (binding.isComposite) continue;
                    if (!BindingMatchesGroup(binding, group)) continue;

                    GameObject row = GetOrCreateBindRow(rowIndex++);
                    if (row == null) continue;
                    ConfigureBindingRow(row, action, bindingIndex, binding);
                }
            }
            return rowIndex;
        }

        private static bool BindingMatchesGroup(InputBinding binding, string group)
        {
            if (binding.isComposite) return false;
            string groups = binding.groups;
            if (string.IsNullOrEmpty(groups)) return false;
            foreach (string g in groups.Split(InputBinding.Separator))
            {
                if (g == group) return true;
            }
            return false;
        }

        private static void ConfigureHeaderRow(GameObject row, string headerText)
        {
            TMP_Text actionName = MenuUiUtility.FindTextByNames(row.transform, "ActionName");
            TMP_Text bindingLabel = MenuUiUtility.FindTextByNames(row.transform, "BindingLabel");
            Button rebindButton = MenuUiUtility.FindButtonByNames(row.transform, "RebindButton");
            if (actionName != null)
            {
                actionName.text = headerText;
                actionName.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            }
            if (bindingLabel != null) bindingLabel.gameObject.SetActive(false);
            if (rebindButton != null)
            {
                rebindButton.onClick.RemoveAllListeners();
                rebindButton.gameObject.SetActive(false);
            }
            row.SetActive(true);
        }

        private void ConfigureBindingRow(GameObject row, InputAction action, int bindingIndex, InputBinding binding)
        {
            TMP_Text actionName = MenuUiUtility.FindTextByNames(row.transform, "ActionName");
            TMP_Text bindingLabel = MenuUiUtility.FindTextByNames(row.transform, "BindingLabel");
            Button rebindButton = MenuUiUtility.FindButtonByNames(row.transform, "RebindButton");
            if (actionName != null)
            {
                actionName.text = binding.isPartOfComposite ? $"{action.name} ({binding.name})" : action.name;
                actionName.fontStyle = FontStyles.Normal;
            }
            if (bindingLabel != null)
            {
                bindingLabel.gameObject.SetActive(true);
                bindingLabel.text = InputControlPath.ToHumanReadableString(action.bindings[bindingIndex].effectivePath, InputControlPath.HumanReadableStringOptions.OmitDevice);
            }
            if (rebindButton != null)
            {
                rebindButton.gameObject.SetActive(true);
                rebindButton.onClick.RemoveAllListeners();
                int capturedIndex = bindingIndex;
                InputAction capturedAction = action;
                rebindButton.onClick.AddListener(() => StartRebind(capturedAction, capturedIndex, bindingLabel, rebindButton));
            }
            row.SetActive(true);
        }

        private void WireUiEvents()
        {
            MenuUiUtility.WireButton(_tabAudio, () => ActivateTab("audio"));
            MenuUiUtility.WireButton(_tabGraphics, () => ActivateTab("graphics"));
            MenuUiUtility.WireButton(_tabGameplay, () => ActivateTab("gameplay"));
            MenuUiUtility.WireButton(_tabControls, () => ActivateTab("controls"));
            MenuUiUtility.WireButton(_tabAccessibility, () => ActivateTab("accessibility"));
            MenuUiUtility.WireButton(_applyButton, ApplyCurrentSettings);
            MenuUiUtility.WireButton(_resetDefaultsButton, ResetDefaults);
            MenuUiUtility.WireButton(_backButton, () => Close(_returnToPause));
            WireSlider(SliderRef(ref _sliderMaster, "Slider_Master"), PreviewAudio);
            WireSlider(SliderRef(ref _sliderMusic, "Slider_Music"), PreviewAudio);
            WireSlider(SliderRef(ref _sliderSFX, "Slider_SFX"), PreviewAudio);
            WireSlider(SliderRef(ref _sliderAmbient, "Slider_Ambient"), PreviewAudio);
        }
        #endregion

        #region Utility Helpers
        private void PreviewAudio(float _)
        {
            _workingCopy ??= new SettingsData(SettingsPersistence.Current);
            Slider master = SliderRef(ref _sliderMaster, "Slider_Master");
            Slider music = SliderRef(ref _sliderMusic, "Slider_Music");
            Slider sfx = SliderRef(ref _sliderSFX, "Slider_SFX");
            Slider ambient = SliderRef(ref _sliderAmbient, "Slider_Ambient");
            if (master != null) _workingCopy.MasterVolume = master.value;
            if (music != null) _workingCopy.MusicVolume = music.value;
            if (sfx != null) _workingCopy.SFXVolume = sfx.value;
            if (ambient != null) _workingCopy.AmbientVolume = ambient.value;
            ApplyAudioSettings(_workingCopy);
        }

        private void ApplyPersistedDisplaySettings()
        {
            SettingsData data = SettingsPersistence.Current;
            ApplyAudioSettings(data);
            ApplyGraphicsSettings(data);
            ApplyAccessibilitySettings(data);
        }

        private static void ApplyAudioSettings(SettingsData data)
        {
            if (data != null) AudioListener.volume = Mathf.Clamp01(data.MasterVolume / 100f);
        }

        private void AutoWire()
        {
            _tabAudio ??= MenuUiUtility.FindButtonByNames(transform, "Tab_Audio", "Audio", "TabAudio");
            _tabGraphics ??= MenuUiUtility.FindButtonByNames(transform, "Tab_Graphics", "Graphics", "TabGraphics");
            _tabGameplay ??= MenuUiUtility.FindButtonByNames(transform, "Tab_Gameplay", "Gameplay", "TabGameplay");
            _tabControls ??= MenuUiUtility.FindButtonByNames(transform, "Tab_Controls", "Controls", "TabControls");
            _tabAccessibility ??= MenuUiUtility.FindButtonByNames(transform, "Tab_Accessibility", "Accessibility", "TabAccessibility");
            _backButton ??= MenuUiUtility.FindButtonByNames(transform, "Back", "CloseButton");
            _applyButton ??= MenuUiUtility.FindButtonByNames(transform, "Apply", "ApplyButton");
            _resetDefaultsButton ??= MenuUiUtility.FindButtonByNames(transform, "ResetDefaults", "Reset", "ResetDefaultsButton");

            PanelRef(ref _panelAudio, "Panel_Audio");
            PanelRef(ref _panelGraphics, "Panel_Graphics");
            PanelRef(ref _panelGameplay, "Panel_Gameplay");
            PanelRef(ref _panelControls, "Panel_Controls");
            PanelRef(ref _panelAccessibility, "Panel_Accessibility");

            if (_controlsScrollRect == null)
            {
                GameObject controlsPanel = GameObjectRef(_panelControls);
                if (controlsPanel != null) _controlsScrollRect = controlsPanel.GetComponentInChildren<ScrollRect>(true);
            }
            if (_controlsScrollRect != null && _bindRowParent == null)
                _bindRowParent = _controlsScrollRect.content;
        }

        private static void WireSlider(Slider slider, UnityEngine.Events.UnityAction<float> action)
        {
            if (slider == null) return;
            slider.onValueChanged.RemoveListener(action);
            slider.onValueChanged.AddListener(action);
        }

        private Slider SliderRef(ref UnityEngine.Object reference, string name) => ComponentRef<Slider>(ref reference, name);
        private TMP_Dropdown DropdownRef(ref UnityEngine.Object reference, string name) => ComponentRef<TMP_Dropdown>(ref reference, name);
        private Toggle ToggleRef(ref UnityEngine.Object reference, string name) => ComponentRef<Toggle>(ref reference, name);

        private T ComponentRef<T>(ref UnityEngine.Object reference, string name) where T : Component
        {
            T component = reference switch
            {
                T typed => typed,
                GameObject go => go.GetComponent<T>(),
                _ => null
            };
            if (component != null) return component;
            component = MenuUiUtility.FindDeepComponent<T>(transform, name);
            if (component != null) reference = component;
            return component;
        }

        private void PanelRef(ref UnityEngine.Object reference, string name)
        {
            if (GameObjectRef(reference) != null) return;
            Transform found = MenuUiUtility.FindDeep(transform, name);
            if (found != null) reference = found.gameObject;
        }

        private static GameObject GameObjectRef(UnityEngine.Object reference)
        {
            return reference switch
            {
                GameObject go => go,
                Component component => component.gameObject,
                _ => null
            };
        }

        private static void SetPanelVisible(UnityEngine.Object panelReference, bool visible)
        {
            GameObject panel = GameObjectRef(panelReference);
            if (panel != null) panel.SetActive(visible);
        }

        private static bool TryResolvePlayerRoot(out GameObject playerRoot)
        {
            playerRoot = GameObject.FindGameObjectWithTag("Player");
            if (playerRoot != null) return true;
            LocomotionController controller = FindFirstObjectByType<LocomotionController>();
            if (controller != null && controller.IsPlayerControlled())
            {
                playerRoot = controller.gameObject;
                return true;
            }
            return false;
        }

        private static FullScreenMode NormalizeFullscreenMode(int fullscreenMode)
        {
            return fullscreenMode switch
            {
                (int)FullScreenMode.ExclusiveFullScreen => FullScreenMode.ExclusiveFullScreen,
                (int)FullScreenMode.FullScreenWindow => FullScreenMode.FullScreenWindow,
                (int)FullScreenMode.MaximizedWindow => FullScreenMode.MaximizedWindow,
                (int)FullScreenMode.Windowed => FullScreenMode.Windowed,
                _ => FullScreenMode.FullScreenWindow
            };
        }
        #endregion
    }
}

namespace Sol.Settings
{
    [Serializable]
    public class SettingsData
    {
        public float MasterVolume = 80f;
        public float MusicVolume = 80f;
        public float SFXVolume = 80f;
        public float AmbientVolume = 80f;
        public int ResolutionWidth = 1920;
        public int ResolutionHeight = 1080;
        public double RefreshRate = 60d;
        public int FullscreenMode = (int)FullScreenMode.FullScreenWindow;
        public int QualityLevel = -1;
        public int VSyncCount = 1;
        public int ShadowQuality = 2;
        public int AntiAliasing = 2;
        public float MouseSensitivity = 0.15f;
        public bool InvertYAxis;
        public float FieldOfView = 70f;
        public bool ShowCrosshair = true;
        public string ControlOverridesJson = string.Empty;
        public float UIScale = 1f;
        public int SubtitleSize = 1;
        public int ColorblindMode;

        public SettingsData() { }

        public SettingsData(SettingsData other)
        {
            if (other == null) return;
            MasterVolume = other.MasterVolume;
            MusicVolume = other.MusicVolume;
            SFXVolume = other.SFXVolume;
            AmbientVolume = other.AmbientVolume;
            ResolutionWidth = other.ResolutionWidth;
            ResolutionHeight = other.ResolutionHeight;
            RefreshRate = other.RefreshRate;
            FullscreenMode = other.FullscreenMode;
            QualityLevel = other.QualityLevel;
            VSyncCount = other.VSyncCount;
            ShadowQuality = other.ShadowQuality;
            AntiAliasing = other.AntiAliasing;
            MouseSensitivity = other.MouseSensitivity;
            InvertYAxis = other.InvertYAxis;
            FieldOfView = other.FieldOfView;
            ShowCrosshair = other.ShowCrosshair;
            ControlOverridesJson = other.ControlOverridesJson;
            UIScale = other.UIScale;
            SubtitleSize = other.SubtitleSize;
            ColorblindMode = other.ColorblindMode;
        }
    }

    public static class SettingsPersistence
    {
        private const string FileName = "settings.json";
        private static string FilePath => System.IO.Path.Combine(Application.persistentDataPath, FileName);
        private static SettingsData _current;

        public static SettingsData Current
        {
            get
            {
                _current ??= Load();
                return _current;
            }
        }

        public static void Save(SettingsData data)
        {
            if (data == null) return;
            _current = data;
            try
            {
                string json = JsonUtility.ToJson(data, true);
                System.IO.File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SettingsPersistence] Failed to save settings: {ex.Message}");
            }
        }

        public static SettingsData Load()
        {
            try
            {
                if (System.IO.File.Exists(FilePath))
                {
                    string json = System.IO.File.ReadAllText(FilePath);
                    SettingsData data = JsonUtility.FromJson<SettingsData>(json);
                    if (data != null)
                    {
                        _current = data;
                        return data;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SettingsPersistence] Failed to load settings, using defaults: {ex.Message}");
            }

            _current = new SettingsData();
            return _current;
        }
    }
}
