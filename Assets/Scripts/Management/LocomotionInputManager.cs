using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using Sol.HUD;

namespace Sol.Locomotion
{
    [DefaultExecutionOrder(-3)]
    public class LocomotionInputManager : MonoBehaviour, SolControls.IDefaultActions
    {
        private static readonly Rect FullViewportRect = new Rect(0f, 0f, 1f, 1f);
        public static LocomotionInputManager Instance { get; private set; }
        public SolControls Controls { get; private set; }
        public event System.Action<CameraContext> CameraContextChanged;

        [Header("Camera Setup")]
        [Tooltip("Inspector: tunes cinemachine brain.")]
        [SerializeField] private CinemachineBrain _cinemachineBrain;
        [SerializeField] private CinemachineCamera _thirdPersonCamera;
        [Tooltip("Inspector: tunes first person camera.")]
        [SerializeField] private CinemachineCamera _firstPersonCamera;
        [Tooltip("Inspector: tunes perspective swap delay.")]
        [SerializeField] private float _perspectiveSwapDelay = -1f;

        [Header("Third Person Zoom")]
        [Tooltip("Inspector: tunes zoom speed.")]
        [SerializeField] private float _zoomSpeed = 2f;
        [SerializeField] private float _zoomLerpSpeed = 10f;
        [Tooltip("Inspector: tunes camera min zoom.")]
        [SerializeField] private float _cameraMinZoom = 1f;
        [Tooltip("Inspector: tunes camera max zoom.")]
        [SerializeField] private float _cameraMaxZoom = 5f;

        [Header("Player Reference")]
        [Tooltip("Inspector: tunes controller.")]
        [SerializeField] private LocomotionController _controller;
        [Tooltip("Inspector: tunes head mesh renderer.")]
        [SerializeField] private Renderer _headMeshRenderer;

        [field: SerializeField] public bool IsThirdPerson { get; private set; } = false;
        public CameraMode CurrentMode => IsThirdPerson ? CameraMode.ThirdPerson : CameraMode.FirstPerson;

        private bool _isTransitioning = false;
        private bool _hasAppliedInitialPerspective = false;
        private bool _hasLoggedBindingWarning = false;
        private int _transitionToken = 0;
        private CinemachineOrbitalFollow _orbital;
        private CinemachinePanTilt _firstPersonPanTilt;
        private float _targetZoom;
        private float _currentZoom;
        private Vector2 _scrollDelta;
        private float _dpadDelta;
        private Vector2 _lookDelta;

        /// <summary>Seconds since the last camera look input. Used by turn-in-place logic.</summary>
        public float CameraStillDuration { get; private set; }

        /// <summary>Set true by UI systems (inventory, menus) to suppress camera look input.</summary>
        public bool UIInputBlocked { get; set; }

        public bool TryGetCameraContext(out CameraContext context)
        {
            CinemachineCamera activeRig = GetCameraForMode(CurrentMode);
            Camera unityCamera = _controller != null ? _controller.PlayerCamera : null;
            Transform cameraTransform = activeRig != null ? activeRig.transform
                : _controller != null ? _controller.CamTransform : null;

            if (unityCamera == null && cameraTransform == null)
            {
                context = default;
                return false;
            }

            context = new CameraContext(CurrentMode, unityCamera, cameraTransform, activeRig);
            return true;
        }

        private void Awake()
        {
            Controls = new SolControls();
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent != null)
                transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            TryResolveRuntimeBindings(applyPerspectiveIfNeeded: true);
            SettingsMenuSystem.ApplyPersistedInputAndGameplaySettings();
        }

        private void OnEnable()
        {
            Controls.Enable();
            Controls.Default.AddCallbacks(this);
        }

        private void OnDisable()
        {
            if (Controls == null) return;
            Controls.Default.RemoveCallbacks(this);
            Controls.Disable();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Controls?.Dispose();
            Controls = null;
        }

        private void Update()
        {
            EnsureRuntimeBindings();

            // Track how long the camera has been still (for turn-in-place)
            if (_lookDelta.sqrMagnitude > 0.001f)
                CameraStillDuration = 0f;
            else
                CameraStillDuration += Time.deltaTime;

            DriveCameraLook();

            if (!IsThirdPerson || _orbital == null) return;

            if (_scrollDelta.y != 0)
                _targetZoom = Mathf.Clamp(_orbital.Radius - _scrollDelta.y * _zoomSpeed, _cameraMinZoom, _cameraMaxZoom);

            if (_dpadDelta != 0)
                _targetZoom = Mathf.Clamp(_orbital.Radius - _dpadDelta * _zoomSpeed, _cameraMinZoom, _cameraMaxZoom);

            _currentZoom = Mathf.Lerp(_currentZoom, _targetZoom, Time.deltaTime * _zoomLerpSpeed);
            _orbital.Radius = _currentZoom;
        }

        private void DriveCameraLook()
        {
            if (UIInputBlocked || _lookDelta == Vector2.zero || _controller == null) return;

            float h = _controller.lookSenseH;
            float v = _controller.lookSenseV;
            float vLimit = _controller.lookLimitV;

            if (!IsThirdPerson && _firstPersonPanTilt != null)
            {
                _firstPersonPanTilt.PanAxis.Value += h * _lookDelta.x;
                _firstPersonPanTilt.TiltAxis.Value = Mathf.Clamp(
                    _firstPersonPanTilt.TiltAxis.Value - v * _lookDelta.y, -vLimit, vLimit);
            }
            else if (IsThirdPerson && _orbital != null)
            {
                _orbital.HorizontalAxis.Value += h * _lookDelta.x;
                _orbital.VerticalAxis.Value = Mathf.Clamp(
                    _orbital.VerticalAxis.Value - v * _lookDelta.y, -vLimit, vLimit);
            }

            _lookDelta = Vector2.zero;
        }

        private void LateUpdate()
        {
            _scrollDelta = Vector2.zero;
            _dpadDelta = 0;
        }

        public bool RegisterRuntimeRig(LocomotionController controller)
        {
            if (controller == null || !controller.IsPlayerControlled())
                return false;

            return TryResolveRuntimeBindings(controller, applyPerspectiveIfNeeded: true);
        }

        private void EnsureRuntimeBindings()
        {
            if (HasRequiredBindings()) return;
            TryResolveRuntimeBindings(applyPerspectiveIfNeeded: !_hasAppliedInitialPerspective);
        }

        private bool TryResolveRuntimeBindings(bool applyPerspectiveIfNeeded)
        {
            return TryResolveRuntimeBindings(ResolveControllerCandidate(), applyPerspectiveIfNeeded);
        }

        private bool TryResolveRuntimeBindings(LocomotionController controller, bool applyPerspectiveIfNeeded)
        {
            if (controller == null) return false;

            bool controllerChanged = controller != _controller;
            if (controllerChanged)
                ResetResolvedBindings();

            _controller = controller;

            ResolveBrainBinding(controller);
            ResolveCameraBindings(controller);
            ResolveHeadBinding(controller);
            CacheCameraComponents();

            if (!ValidateBindings())
                return false;

            if (controllerChanged || !_hasAppliedInitialPerspective)
            {
                ApplyConfiguredPerspective(GetConfiguredDefaultMode(), useTransition: applyPerspectiveIfNeeded);
                _hasAppliedInitialPerspective = true;
            }
            else
            {
                RefreshActivePerspectiveState();
            }

            return true;
        }

        private LocomotionController ResolveControllerCandidate()
        {
            if (_controller != null && _controller.IsPlayerControlled())
                return _controller;

            // Find any player-controlled controller in the scene
            LocomotionController[] controllers = FindObjectsByType<LocomotionController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                LocomotionController candidate = controllers[i];
                if (candidate != null && candidate.IsPlayerControlled())
                    return candidate;
            }

            return null;
        }

        private void ResetResolvedBindings()
        {
            _cinemachineBrain = null;
            _thirdPersonCamera = null;
            _firstPersonCamera = null;
            _controller = null;
            _headMeshRenderer = null;
            _orbital = null;
            _firstPersonPanTilt = null;
            _hasLoggedBindingWarning = false;
            _transitionToken++;
            _isTransitioning = false;
        }

        private void ResolveBrainBinding(LocomotionController controller)
        {
            if (_cinemachineBrain != null) return;
            if (controller.PlayerCamera != null)
                _cinemachineBrain = controller.PlayerCamera.GetComponent<CinemachineBrain>();
        }

        private void ResolveCameraBindings(LocomotionController controller)
        {
            if (_firstPersonCamera == null && controller.TryGetCameraForMode(CameraMode.FirstPerson, out CinemachineCamera firstPersonCamera))
                _firstPersonCamera = firstPersonCamera;

            if (_thirdPersonCamera == null && controller.TryGetCameraForMode(CameraMode.ThirdPerson, out CinemachineCamera thirdPersonCamera))
                _thirdPersonCamera = thirdPersonCamera;
        }

        private void ResolveHeadBinding(LocomotionController controller)
        {
            if (_headMeshRenderer == null && controller.TryGetHeadMeshRenderer(out Renderer headRenderer))
                _headMeshRenderer = headRenderer;
        }

        private void CacheCameraComponents()
        {
            _orbital = _thirdPersonCamera != null ? _thirdPersonCamera.GetComponent<CinemachineOrbitalFollow>() : null;
            _firstPersonPanTilt = _firstPersonCamera != null ? _firstPersonCamera.GetComponent<CinemachinePanTilt>() : null;

            DisableBuiltInAxisController(_thirdPersonCamera);
            DisableBuiltInAxisController(_firstPersonCamera);

            if (_orbital != null)
                _targetZoom = _currentZoom = _orbital.Radius;
        }

        private void DisableBuiltInAxisController(CinemachineCamera camera)
        {
            if (camera == null) return;

            var axisController = camera.GetComponent<CinemachineInputAxisController>();
            if (axisController != null)
                axisController.enabled = false;
        }

        private bool ValidateBindings()
        {
            List<string> missing = null;

            if (_controller == null)
                AddMissing("player controller", ref missing);
            if (_cinemachineBrain == null)
                AddMissing("Cinemachine brain", ref missing);
            if (_firstPersonCamera == null)
                AddMissing("FirstPerson camera", ref missing);
            if (_thirdPersonCamera == null)
                AddMissing("ThirdPerson camera", ref missing);

            if (missing == null)
                return true;

            if (!_hasLoggedBindingWarning)
            {
                Debug.LogWarning($"[Camera] Runtime binding incomplete: {string.Join(", ", missing)}.", this);
                _hasLoggedBindingWarning = true;
            }

            return false;
        }

        private void AddMissing(string label, ref List<string> missing)
        {
            missing ??= new List<string>(4);
            missing.Add(label);
        }

        private bool HasRequiredBindings()
        {
            return _controller != null
                && _cinemachineBrain != null
                && _firstPersonCamera != null
                && _thirdPersonCamera != null;
        }

        private CameraMode GetConfiguredDefaultMode()
        {
            return _controller != null ? _controller.DefaultCameraMode : CameraMode.FirstPerson;
        }

        #region Perspective
        private void SwapPerspective()
        {
            IsThirdPerson = !IsThirdPerson;
            ApplyConfiguredPerspective(CurrentMode, useTransition: true);
        }

        private void ApplyConfiguredPerspective(CameraMode mode, bool useTransition)
        {
            IsThirdPerson = mode == CameraMode.ThirdPerson;
            CinemachineCamera activeCamera = GetCameraForMode(mode);
            CinemachineCamera inactiveCamera = GetInactiveCameraForMode(mode);

            if (activeCamera == null)
                return;

            if (inactiveCamera != null) inactiveCamera.Priority = 0;
            if (activeCamera != null) activeCamera.Priority = 10;

            if (_controller != null && activeCamera != null)
                _controller.CamTransform = activeCamera.transform;

            if (!useTransition)
            {
                _isTransitioning = false;
                ApplyHeadVisibility(mode, transitionComplete: true);
                NotifyCameraContextChanged();
                return;
            }

            _isTransitioning = true;

            if (mode == CameraMode.ThirdPerson)
                ApplyHeadVisibility(mode, transitionComplete: false);

            NotifyCameraContextChanged();
            StartCoroutine(HandleTransitionEnd(mode, ++_transitionToken));
        }

        private void RefreshActivePerspectiveState()
        {
            ApplyConfiguredPerspective(CurrentMode, useTransition: false);
        }

        private CinemachineCamera GetCameraForMode(CameraMode mode)
        {
            return mode == CameraMode.FirstPerson ? _firstPersonCamera : _thirdPersonCamera;
        }

        private CinemachineCamera GetInactiveCameraForMode(CameraMode mode)
        {
            return mode == CameraMode.FirstPerson ? _thirdPersonCamera : _firstPersonCamera;
        }

        private void ApplyHeadVisibility(CameraMode mode, bool transitionComplete)
        {
            if (_headMeshRenderer == null) return;

            _headMeshRenderer.shadowCastingMode = mode == CameraMode.ThirdPerson || !transitionComplete
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        private void NotifyCameraContextChanged()
        {
            EnsurePlayerCameraViewport();
            if (TryGetCameraContext(out CameraContext context))
                CameraContextChanged?.Invoke(context);
        }

        private void EnsurePlayerCameraViewport()
        {
            Camera gameplayCamera = _controller != null ? _controller.PlayerCamera : null;
            if (gameplayCamera == null || gameplayCamera.targetTexture != null)
                return;

            if (gameplayCamera.rect != FullViewportRect)
                gameplayCamera.rect = FullViewportRect;
        }

        private System.Collections.IEnumerator HandleTransitionEnd(CameraMode mode, int transitionToken)
        {
            float waitTime;
            if (_perspectiveSwapDelay >= 0f)
                waitTime = _perspectiveSwapDelay;
            else
                waitTime = _cinemachineBrain != null ? _cinemachineBrain.DefaultBlend.Time : 0.5f;
            yield return new WaitForSeconds(waitTime);

            if (transitionToken != _transitionToken)
                yield break;

            ApplyHeadVisibility(mode, transitionComplete: true);
            _isTransitioning = false;
        }
        #endregion

        #region SolControls.IDefaultActions
        public void OnP(InputAction.CallbackContext context)
        {
            if (context.performed && !_isTransitioning)
                SwapPerspective();
        }

        public void OnScroll(InputAction.CallbackContext context)
        {
            _scrollDelta = context.ReadValue<Vector2>();
        }

        public void OnUp(InputAction.CallbackContext context)
        {
            if (context.performed) _dpadDelta = 1f;
            else if (context.canceled) _dpadDelta = 0f;
        }

        public void OnDown(InputAction.CallbackContext context)
        {
            if (context.performed) _dpadDelta = -1f;
            else if (context.canceled) _dpadDelta = 0f;
        }

        public void OnMouseLook(InputAction.CallbackContext context)
        {
            _lookDelta = context.ReadValue<Vector2>();
        }
        public void OnMove(InputAction.CallbackContext context) { }
        public void OnLClick(InputAction.CallbackContext context) { }
        public void OnRClick(InputAction.CallbackContext context) { }
        public void OnMClick(InputAction.CallbackContext context) { }
        public void OnQ(InputAction.CallbackContext context) { }
        public void OnE(InputAction.CallbackContext context) { }
        public void OnR(InputAction.CallbackContext context) { }
        public void OnT(InputAction.CallbackContext context) { }
        public void OnF(InputAction.CallbackContext context) { }
        public void OnG(InputAction.CallbackContext context) { }
        public void OnZ(InputAction.CallbackContext context) { }
        public void OnX(InputAction.CallbackContext context) { }
        public void OnC(InputAction.CallbackContext context) { }
        public void OnV(InputAction.CallbackContext context) { }
        public void On_1(InputAction.CallbackContext context) { }
        public void On_2(InputAction.CallbackContext context) { }
        public void On_3(InputAction.CallbackContext context) { }
        public void On_4(InputAction.CallbackContext context) { }
        public void On_5(InputAction.CallbackContext context) { }
        public void On_6(InputAction.CallbackContext context) { }
        public void On_7(InputAction.CallbackContext context) { }
        public void On_8(InputAction.CallbackContext context) { }
        public void On_9(InputAction.CallbackContext context) { }
        public void On_0(InputAction.CallbackContext context) { }
        public void OnSpace(InputAction.CallbackContext context) { }
        public void OnLAlt(InputAction.CallbackContext context) { }
        public void OnLShift(InputAction.CallbackContext context) { }
        public void OnLCntrl(InputAction.CallbackContext context) { }
        public void OnRight(InputAction.CallbackContext context) { }
        public void OnLeft(InputAction.CallbackContext context) { }
        public void OnTilde(InputAction.CallbackContext context) { }
        public void OnEsc(InputAction.CallbackContext context) { }
        public void OnTab(InputAction.CallbackContext context) { }
        public void OnCapsLock(InputAction.CallbackContext context) { }
        public void OnM(InputAction.CallbackContext context) { }
        #endregion
    }
}
