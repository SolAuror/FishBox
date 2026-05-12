using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Sol.Locomotion;
using Sol.Actions;
using Sol.Fishing;
using TMPro;
using Sol.Grab;
using Sol.Settings;

namespace Sol.HUD
{
    public class CrosshairUI : MonoBehaviour, SolControls.IDefaultActions
    {
        private static bool InteractionTraceEnabled => false;

        [Header("Crosshair")]
        [Tooltip("Inspector: tunes crosshair rect.")]
        [SerializeField] private RectTransform _crosshairRect;
        [SerializeField] private Image _crosshairImage;
        [Tooltip("Inspector: tunes fps crosshair.")]
        [SerializeField] private Sprite _fpsCrosshair;
        [Tooltip("Inspector: tunes tps crosshair.")]
        [SerializeField] private Sprite _tpsCrosshair;

        [Header("Interaction")]
        [Tooltip("Inspector: tunes interaction range.")]
        [SerializeField] private float _interactionRange = 3f;
        [SerializeField] private float _interactionRangeTP = 10f;
        [Tooltip("Inspector: tunes raycast layers.")]
        [SerializeField] private LayerMask _raycastLayers = ~0;
        [Tooltip("Inspector: tunes prompt text.")]
        [SerializeField] private TextMeshProUGUI _promptText;

        [Header("PhysGrab (Long Press)")]
        [Tooltip("Seconds the interact key must be held to initiate a physics grab.")]
        [SerializeField] private float _grabHoldThreshold = 0.4f;

        [Header("Pickup")]
        [Tooltip("How strongly the left hand is pulled toward the pickup target.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("_pickupPreviewIKWeight")]
        [SerializeField] private float _pickupIKWeight = 0.55f;
        [Tooltip("How strongly the left hand rotation follows the pickup target.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("_pickupPreviewRotationWeight")]
        [SerializeField] private float _pickupRotationWeight = 0.15f;
        [Tooltip("How long to keep the cached pickup IK target active after pressing interact on an item.")]
        [SerializeField] private float _pickupIKDuration = 0.18f;

        [Header("Player")]
        [Tooltip("Assign the player root GameObject (the one with Inventory and player locomotion components). " +
                 "If left empty, CrosshairUI resolves it at runtime from the active player actor.")]
        [SerializeField] private GameObject _playerRoot;

        public RaycastHit? CurrentHit { get; private set; }
        public IInteractable CurrentInteractable { get; private set; }

        private bool _interactPressed;
        private Interactor _playerInteractor;
        private bool _callbacksRegistered;
        private bool _actionTraceSubscribed;
        private bool _hasLoggedMissingPlayerWarning;
        private LocomotionIK _playerLocomotionIK;
        private LocomotionAnimation _playerLocomotionAnimation;
        private FishingState _playerFishingState;
        private ItemComponent _pendingPickupItem;
        private Pose _pickupIKPose;
        private bool _pickupIKUsesGripRotation;
        private float _pickupIKUntil;

        // Long-press E state
        private bool _eHeld;
        private float _eHoldTime;
        private bool _grabInitiated;
        private bool _dropCatchPressed;
        private bool _isFirstPerson = true;

        private void Start()
        {
            ConfigurePromptText();

            EnsurePlayerInteractorBound(logWarningIfMissing: true);
            TryRegisterCallbacks();
            SetCrosshairVisible(SettingsPersistence.Current.ShowCrosshair);
        }

        private void OnEnable()
        {
            _callbacksRegistered = false;
            EnsurePlayerInteractorBound(logWarningIfMissing: false);
            TryRegisterCallbacks();
            if (InteractionTraceEnabled)
                TryRegisterActionTrace();
        }

        private void OnDisable()
        {
            ClearPendingPickup();
            if (LocomotionInputManager.Instance != null)
            {
                if (LocomotionInputManager.Instance.Controls != null)
                    LocomotionInputManager.Instance.Controls.Default.RemoveCallbacks(this);
                LocomotionInputManager.Instance.CameraContextChanged -= HandleCameraContextChanged;
            }
            if (InteractionTraceEnabled)
                TryUnregisterActionTrace();
            _callbacksRegistered = false;
        }

        private void TryRegisterCallbacks()
        {
            if (_callbacksRegistered) return;
            if (LocomotionInputManager.Instance?.Controls == null) return;
            LocomotionInputManager.Instance.Controls.Default.AddCallbacks(this);
            LocomotionInputManager.Instance.CameraContextChanged -= HandleCameraContextChanged;
            LocomotionInputManager.Instance.CameraContextChanged += HandleCameraContextChanged;
            RefreshCameraContext();
            _callbacksRegistered = true;
        }

        private void BuildInteractor(GameObject owner)
        {
            if (owner == null)
            {
                _playerInteractor = null;
                _playerLocomotionIK = null;
                _playerLocomotionAnimation = null;
                _playerFishingState = null;
                return;
            }

            _playerInteractor = new Interactor(owner, true);
            _playerLocomotionIK = owner.GetComponent<LocomotionIK>();
            _playerLocomotionAnimation = owner.GetComponent<LocomotionAnimation>();
            _playerFishingState = owner.GetComponent<FishingState>();
        }

        private void TryRegisterActionTrace()
        {
            if (!InteractionTraceEnabled)
                return;

            if (_actionTraceSubscribed || ActionSystem.Instance == null)
                return;

            ActionSystem.Instance.OnActionStarted += HandleActionStarted;
            ActionSystem.Instance.OnActionCompleted += HandleActionCompleted;
            ActionSystem.Instance.OnActionCancelled += HandleActionCancelled;
            _actionTraceSubscribed = true;
        }

        private void TryUnregisterActionTrace()
        {
            if (!InteractionTraceEnabled)
                return;

            if (!_actionTraceSubscribed || ActionSystem.Instance == null)
                return;

            ActionSystem.Instance.OnActionStarted -= HandleActionStarted;
            ActionSystem.Instance.OnActionCompleted -= HandleActionCompleted;
            ActionSystem.Instance.OnActionCancelled -= HandleActionCancelled;
            _actionTraceSubscribed = false;
        }

        private void HandleActionStarted(GameObject actor, GameAction action)
        {
            if (!InteractionTraceEnabled)
                return;

            if (!IsPlayerActor(actor))
                return;

            Debug.Log($"[InteractionTrace][ActionFlow][f{Time.frameCount}] START {action.GetType().Name} [{action.Priority}]");
        }

        private void HandleActionCompleted(GameObject actor, GameAction action)
        {
            if (!InteractionTraceEnabled)
                return;

            if (!IsPlayerActor(actor))
                return;

            Debug.Log($"[InteractionTrace][ActionFlow][f{Time.frameCount}] COMPLETE {action.GetType().Name} [{action.Priority}]");
        }

        private void HandleActionCancelled(GameObject actor, GameAction action)
        {
            if (!InteractionTraceEnabled)
                return;

            if (!IsPlayerActor(actor))
                return;

            Debug.Log($"[InteractionTrace][ActionFlow][f{Time.frameCount}] CANCEL {action.GetType().Name} [{action.Priority}]");
        }

        private bool IsPlayerActor(GameObject actor)
        {
            if (actor == null)
                return false;

            if (_playerInteractor != null && _playerInteractor.Owner == actor)
                return true;

            return actor.CompareTag("Player");
        }

        private bool EnsurePlayerInteractorBound(bool logWarningIfMissing)
        {
            if (_playerInteractor != null && IsValidPlayerRoot(_playerInteractor.Owner))
            {
                if (_playerRoot != _playerInteractor.Owner)
                    _playerRoot = _playerInteractor.Owner;
                return true;
            }

            if (TryResolvePlayerRoot(out GameObject resolvedRoot))
            {
                _playerRoot = resolvedRoot;
                BuildInteractor(resolvedRoot);
                _hasLoggedMissingPlayerWarning = false;
                return true;
            }

            BuildInteractor(null);
            if (logWarningIfMissing && !_hasLoggedMissingPlayerWarning)
            {
                Debug.LogWarning("[CrosshairUI] Could not resolve player root (tagged Player + Inventory). Interactions are temporarily disabled.", this);
                _hasLoggedMissingPlayerWarning = true;
            }

            return false;
        }

        private bool TryResolvePlayerRoot(out GameObject resolvedRoot)
        {
            resolvedRoot = _playerRoot;
            if (IsValidPlayerRoot(resolvedRoot))
                return true;

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (IsValidPlayerRoot(taggedPlayer))
            {
                resolvedRoot = taggedPlayer;
                return true;
            }

            LocomotionController[] controllers = FindObjectsByType<LocomotionController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                LocomotionController controller = controllers[i];
                if (controller == null || !controller.IsPlayerControlled())
                    continue;

                if (IsValidPlayerRoot(controller.gameObject))
                {
                    resolvedRoot = controller.gameObject;
                    return true;
                }
            }

            return false;
        }

        private static bool IsValidPlayerRoot(GameObject root)
        {
            return root != null && root.GetComponent<Inventory>() != null;
        }

        private void Update()
        {
            if (!_callbacksRegistered)
                TryRegisterCallbacks();
            if (InteractionTraceEnabled && !_actionTraceSubscribed)
                TryRegisterActionTrace();

            if (!EnsurePlayerInteractorBound(logWarningIfMissing: false))
            {
                ResetInteractionInputState();
                ClearPendingPickup();
                CurrentHit = null;
                CurrentInteractable = null;
                if (_promptText != null) _promptText.enabled = false;
                return;
            }

            if (IsUiInputBlocked())
            {
                ResetInteractionInputState();
                ClearPendingPickup();
                CurrentHit = null;
                CurrentInteractable = null;
                if (_promptText != null) _promptText.enabled = false;
                return;
            }

            if (_crosshairImage != null)
                _crosshairImage.sprite = _isFirstPerson ? _fpsCrosshair : _tpsCrosshair;

            var cam = GetActiveGameplayCamera();
            if (cam == null) return;

            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            float range = _isFirstPerson ? _interactionRange : _interactionRangeTP;

            if (Physics.Raycast(ray, out var hit, range, _raycastLayers, QueryTriggerInteraction.Ignore))
            {
                var interactable = hit.collider.GetComponent<IInteractable>()
                                ?? hit.collider.GetComponentInParent<IInteractable>();

                if (interactable != null)
                {
                    CurrentHit = hit;
                    CurrentInteractable = interactable;
                }
                else
                {
                    CurrentHit = null;
                    CurrentInteractable = null;
                }
            }
            else
            {
                CurrentHit = null;
                CurrentInteractable = null;
            }

            UpdatePickupIK();

            if (_promptText != null)
            {
                string prompt = GetActivePromptText();
                bool show = !string.IsNullOrWhiteSpace(prompt);
                _promptText.enabled = show;
                if (show) _promptText.text = prompt;
            }

            if (_eHeld)
            {
                _eHoldTime += Time.deltaTime;
                if (!_grabInitiated && _eHoldTime >= _grabHoldThreshold)
                {
                    _grabInitiated = true;
                    _pendingPickupItem = null;
                    if (!TryGrabDisplayedCatch())
                        TryGrabLookedAtObject(ray, range);
                }
            }

            if (_dropCatchPressed)
            {
                _dropCatchPressed = false;
                TryDropDisplayedCatch();
            }

            if (_interactPressed)
            {
                _interactPressed = false;
                if (!_grabInitiated && TryTakeDisplayedCatch())
                {
                    _pendingPickupItem = null;
                    return;
                }

                IInteractable interactableToUse = _pendingPickupItem != null ? _pendingPickupItem : CurrentInteractable;
                if (!_grabInitiated && interactableToUse != null
                    && _playerInteractor != null
                    && interactableToUse is not GrabInteractable
                    && interactableToUse.CanInteract(_playerInteractor)
                    && ActionSystem.Instance != null)
                {
                    string interactableName = (interactableToUse as Component) != null
                        ? (interactableToUse as Component).name
                        : interactableToUse.GetType().Name;
                    if (InteractionTraceEnabled)
                        Debug.Log($"[InteractionTrace][CrosshairUI][f{Time.frameCount}] Dispatch InteractAction | actor='{_playerInteractor.Owner?.name ?? "null"}' interactable='{interactableName}' pendingPickup={(_pendingPickupItem != null)}");

                    bool queued = ActionSystem.Instance.Dispatch(
                        new InteractAction(interactableToUse, _playerInteractor),
                        _playerInteractor.Owner);
                    if (InteractionTraceEnabled)
                        Debug.Log($"[InteractionTrace][CrosshairUI][f{Time.frameCount}] Dispatch result | queued={queued}");
                }
                else if (!_grabInitiated)
                {
                    if (InteractionTraceEnabled)
                        Debug.Log($"[InteractionTrace][CrosshairUI][f{Time.frameCount}] Interact blocked | interactable={(interactableToUse != null)} interactor={(_playerInteractor != null)} canInteract={(interactableToUse != null && _playerInteractor != null && interactableToUse.CanInteract(_playerInteractor))} actionSystem={(ActionSystem.Instance != null)}");
                }

                _pendingPickupItem = null;
            }
        }

        private void TryGrabLookedAtObject(Ray ray, float range)
        {
            if (ActionSystem.Instance == null || _playerInteractor == null || _playerInteractor.Owner == null) return;
            if (!Physics.Raycast(ray, out var grabHit, range, _raycastLayers, QueryTriggerInteraction.Ignore)) return;

            var grabbable = grabHit.collider.GetComponent<Sol.Grab.GrabbableComponent>()
                         ?? grabHit.collider.GetComponentInParent<Sol.Grab.GrabbableComponent>();

            if (grabbable != null && !grabbable.IsGrabbed)
            {
                var owner = _playerInteractor.Owner;
                ActionSystem.Instance.Dispatch(new StartGrabAction(), owner, grabbable.gameObject);
            }
        }

        private void RefreshCameraContext()
        {
            if (LocomotionInputManager.Instance != null &&
                LocomotionInputManager.Instance.TryGetCameraContext(out CameraContext context))
            {
                _isFirstPerson = !context.IsThirdPerson;
                return;
            }

            _isFirstPerson = LocomotionInputManager.Instance == null
                || !LocomotionInputManager.Instance.IsThirdPerson;
        }

        private void HandleCameraContextChanged(CameraContext context)
        {
            _isFirstPerson = !context.IsThirdPerson;

            if (_crosshairImage != null)
                _crosshairImage.sprite = _isFirstPerson ? _fpsCrosshair : _tpsCrosshair;
        }

        public void SetCrosshairVisible(bool visible)
        {
            if (_crosshairImage != null)
                _crosshairImage.enabled = visible;
        }

        private Camera GetActiveGameplayCamera()
        {
            if (LocomotionInputManager.Instance != null &&
                LocomotionInputManager.Instance.TryGetCameraContext(out CameraContext context) &&
                context.UnityCamera != null)
            {
                return context.UnityCamera;
            }

            return Camera.main;
        }

        #region SolControls.IDefaultActions
        public void OnE(InputAction.CallbackContext context)
        {
            if (!EnsurePlayerInteractorBound(logWarningIfMissing: true))
            {
                ResetInteractionInputState();
                return;
            }

            if (IsUiInputBlocked())
            {
                ResetInteractionInputState();
                return;
            }

            if (context.started)
            {
                if (InteractionTraceEnabled)
                    Debug.Log($"[InteractionTrace][CrosshairUI][f{Time.frameCount}] E started | uiBlocked={IsUiInputBlocked()}");
                _eHeld = true;
                _eHoldTime = 0f;
                _grabInitiated = false;
                StartPendingPickupIfItem();
            }
            else if (context.canceled)
            {
                if (InteractionTraceEnabled)
                    Debug.Log($"[InteractionTrace][CrosshairUI][f{Time.frameCount}] E canceled | hold={_eHoldTime:F3} grabInitiated={_grabInitiated}");
                _eHeld = false;
                if (!_grabInitiated)
                {
                    _interactPressed = true;
                    if (_pendingPickupItem != null)
                        _pickupIKUntil = Time.time + _pickupIKDuration;
                }
                else if (ActionSystem.Instance != null)
                {
                    if (_playerInteractor != null && _playerInteractor.Owner != null)
                        ActionSystem.Instance.Dispatch(new StopGrabAction(), _playerInteractor.Owner);
                    ClearPendingPickup();
                }
            }
        }

        public void OnMouseLook(InputAction.CallbackContext context) { }
        public void OnScroll(InputAction.CallbackContext context) { }
        public void OnMove(InputAction.CallbackContext context) { }
        public void OnLClick(InputAction.CallbackContext context) { }
        public void OnRClick(InputAction.CallbackContext context) { }
        public void OnMClick(InputAction.CallbackContext context) { }
        public void OnQ(InputAction.CallbackContext context)
        {
            if (!context.performed || IsUiInputBlocked())
                return;

            _dropCatchPressed = true;
        }
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
        public void OnDown(InputAction.CallbackContext context) { }
        public void OnUp(InputAction.CallbackContext context) { }
        public void OnRight(InputAction.CallbackContext context) { }
        public void OnLeft(InputAction.CallbackContext context) { }
        public void OnTilde(InputAction.CallbackContext context) { }
        public void OnEsc(InputAction.CallbackContext context) { }
        public void OnTab(InputAction.CallbackContext context) { }
        public void OnCapsLock(InputAction.CallbackContext context) { }
        public void OnP(InputAction.CallbackContext context) { }
        public void OnM(InputAction.CallbackContext context) { }
        #endregion

        private bool IsUiInputBlocked()
        {
            if (LocomotionInputManager.Instance != null && LocomotionInputManager.Instance.UIInputBlocked)
                return true;

            return UIStateOwnership.IsBlockingUiOpen();
        }

        private void ResetInteractionInputState()
        {
            _eHeld = false;
            _eHoldTime = 0f;
            _grabInitiated = false;
            _interactPressed = false;
            _dropCatchPressed = false;
        }

        private void StartPendingPickupIfItem()
        {
            if (_playerInteractor == null)
            {
                ClearPendingPickup();
                return;
            }

            if (CurrentInteractable is not ItemComponent item)
            {
                ClearPendingPickup();
                return;
            }

            if (!item.CanInteract(_playerInteractor))
            {
                ClearPendingPickup();
                return;
            }

            Pose pickupPose = item.GetPickupPose();
            _pendingPickupItem = item;
            _pickupIKPose = pickupPose;
            _pickupIKUsesGripRotation = item.PickupGrip != null;
            _pickupIKUntil = Time.time + _pickupIKDuration;

            _playerLocomotionAnimation?.TriggerPickupAnimation();
        }

        private void UpdatePickupIK()
        {
            if (_playerLocomotionIK == null)
                return;

            if (Time.time >= _pickupIKUntil)
            {
                _playerLocomotionIK.ClearHandIKTarget(true);
                return;
            }

            float rotationWeight = _pickupIKUsesGripRotation ? _pickupRotationWeight : 0f;
            _playerLocomotionIK.SetHandIKTarget(
                true,
                _pickupIKPose.position,
                _pickupIKPose.rotation,
                _pickupIKWeight,
                rotationWeight);
        }

        private void ClearPickupIK()
        {
            _pickupIKUntil = 0f;
            _pickupIKUsesGripRotation = false;

            if (_playerLocomotionIK != null)
                _playerLocomotionIK.ClearHandIKTarget(true);
        }

        private void ClearPendingPickup()
        {
            _pendingPickupItem = null;
            ClearPickupIK();
        }

        private string GetActivePromptText()
        {
            if (_playerFishingState != null && _playerFishingState.HasDisplayedCatch)
                return _playerFishingState.DisplayedCatchPrompt;

            if (_playerFishingState != null && _playerFishingState.HasFishingStatus)
                return _playerFishingState.FishingStatusText;

            if (CurrentInteractable != null
                && _playerInteractor != null
                && (CurrentInteractable.CanInteract(_playerInteractor)
                    || ShouldShowBlockedOwnedInteractionPrompt(CurrentInteractable, _playerInteractor)))
            {
                return FormatInteractionPrompt(CurrentInteractable.InteractionPrompt);
            }

            return string.Empty;
        }

        private static bool ShouldShowBlockedOwnedInteractionPrompt(IInteractable interactable, Interactor interactor)
        {
            if (interactable == null || interactor == null)
                return false;

            if (interactable is InteractionPoint point)
                return point.IsOwned && !point.CanOwnerUse(interactor);

            if (interactable is SleepInteractable bed && bed.InteractionPoint != null)
                return bed.InteractionPoint.IsOwned && !bed.InteractionPoint.CanOwnerUse(interactor);

            return false;
        }

        private void ConfigurePromptText()
        {
            if (_promptText == null)
                return;

            _promptText.raycastTarget = false;
            _promptText.alignment = TextAlignmentOptions.Center;
            _promptText.textWrappingMode = TextWrappingModes.Normal;
        }

        private string FormatInteractionPrompt(string actionText)
        {
            if (string.IsNullOrWhiteSpace(actionText))
                return string.Empty;

            return $"Press {GetInteractInputDisplayName()}\n{actionText.Trim()}";
        }

        private string GetInteractInputDisplayName()
        {
            InputAction interactAction = LocomotionInputManager.Instance?.Controls?.Default.E;
            if (interactAction == null)
                return "E";

            string keyboardBinding = GetFirstBindingDisplayName(interactAction, preferKeyboard: true);
            if (!string.IsNullOrWhiteSpace(keyboardBinding))
                return keyboardBinding;

            string fallbackBinding = GetFirstBindingDisplayName(interactAction, preferKeyboard: false);
            return string.IsNullOrWhiteSpace(fallbackBinding) ? "E" : fallbackBinding;
        }

        private static string GetFirstBindingDisplayName(InputAction action, bool preferKeyboard)
        {
            if (action == null)
                return string.Empty;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite)
                    continue;

                string path = !string.IsNullOrWhiteSpace(binding.effectivePath)
                    ? binding.effectivePath
                    : binding.path;
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                bool isKeyboardBinding = path.IndexOf("Keyboard", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (preferKeyboard != isKeyboardBinding)
                    continue;

                string display = InputControlPath.ToHumanReadableString(
                    path,
                    InputControlPath.HumanReadableStringOptions.OmitDevice);
                if (!string.IsNullOrWhiteSpace(display))
                    return display;
            }

            return string.Empty;
        }

        private bool TryTakeDisplayedCatch()
        {
            return _playerFishingState != null
                && _playerInteractor != null
                && _playerFishingState.TryTakeDisplayedCatch(_playerInteractor);
        }

        private bool TryDropDisplayedCatch()
        {
            return _playerFishingState != null
                && _playerFishingState.TryDropDisplayedCatch();
        }

        private bool TryGrabDisplayedCatch()
        {
            return _playerFishingState != null
                && _playerInteractor?.Owner != null
                && _playerFishingState.TryGrabDisplayedCatch(_playerInteractor.Owner);
        }
    }
}
