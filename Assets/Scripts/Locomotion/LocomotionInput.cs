using UnityEngine;
using UnityEngine.InputSystem;

namespace Sol.Locomotion
{
    [DefaultExecutionOrder(-2)]
    public class LocomotionInput : MonoBehaviour, SolControls.IDefaultActions
    {
#region Class Variables
        [field: SerializeField] public bool IsControlledByPlayer { get; set; } = true;
        [Tooltip("Inspector: tunes hold to sprint.")]
        [SerializeField] private bool holdToSprint = true;
        [Tooltip("Inspector: tunes is aim hold mode.")]
        [SerializeField] private bool isAimHoldMode = false;

        // Locomotion
        public Vector2 MovementInput { get; set; }
        public Vector2 LookInput { get; set; }
        public bool JumpPressed { get; set; }
        public bool CrouchToggle { get; set; }
        public bool SwimUpHeld { get; set; }
        public bool SwimDownHeld { get; set; }
        public bool SprintPressed { get; set; }
        public bool WalkToggle { get; set; }

        // Actions
        public bool AttackPressed { get; set; }
        public bool AimPressed { get; set; }
        public bool InteractPressed { get; set; }
        public bool ReelHeld { get; set; }
        public bool ReadyTogglePressed { get; set; }

        private LocomotionState _state;
        private bool _callbacksRegistered;
#endregion

#region Initialize
        private void Awake()
        {
            _state = GetComponent<LocomotionState>();

            // NPC rigs own locomotion through an intent provider; default to non-player
            // control before OnEnable callback wiring runs.
            if (GetComponent(typeof(Shared.AI.ILocomotionIntentProvider)) != null)
                IsControlledByPlayer = false;
        }

        private void OnEnable()
        {
            TryRegisterCallbacks();
        }

        private void OnDisable()
        {
            TryUnregisterCallbacks();
        }
#endregion

#region Update
        private void Update()
        {
            // Keep callback wiring aligned with current control ownership.
            if (!IsControlledByPlayer)
            {
                if (_callbacksRegistered)
                    TryUnregisterCallbacks();
            }
            else if (!_callbacksRegistered)
            {
                // Scene/bootstrap order can occasionally enable this component before
                // the input manager singleton is ready; retry registration until it exists.
                TryRegisterCallbacks();
            }

            if (_state != null &&
                (MovementInput != Vector2.zero ||
                 _state.CurrentMovementState == MovementState.Jumping ||
                 _state.CurrentMovementState == MovementState.Falling ||
                 _state.CurrentMovementState == MovementState.Sprinting))
            {
                InteractPressed = false;
            }
        }

        private void LateUpdate()
        {
            JumpPressed = false;
        }
#endregion

#region Public Helpers
        public void SetInteractPressedFalse() => InteractPressed = false;
        public void SetAttackPressedFalse() => AttackPressed = false;
        public void SetReelHeldFalse() => ReelHeld = false;
        public void SetReadyTogglePressedFalse() => ReadyTogglePressed = false;
#endregion

#region SolControls.IDefaultActions - Locomotion
        public void OnMove(InputAction.CallbackContext context)
        {
            if (!IsControlledByPlayer) return;
            MovementInput = context.ReadValue<Vector2>();
        }

        public void OnMouseLook(InputAction.CallbackContext context)
        {
            if (!IsControlledByPlayer) return;
            LookInput = context.ReadValue<Vector2>();
        }

        public void OnSpace(InputAction.CallbackContext context)
        {
            if (!IsControlledByPlayer) return;

            if (context.started || context.performed)
                SwimUpHeld = true;
            else if (context.canceled)
                SwimUpHeld = false;

            if (!context.performed) return;
            if (_state != null && _state.CurrentMovementState == MovementState.Swimming) return;

            JumpPressed = true;
            CrouchToggle = false;
        }

        public void OnLShift(InputAction.CallbackContext context)
        {
            if (!IsControlledByPlayer) return;
            if (context.performed)
                SprintPressed = holdToSprint || !SprintPressed;
            else if (context.canceled)
                SprintPressed = !holdToSprint && SprintPressed;
        }

        public void OnLCntrl(InputAction.CallbackContext context)
        {
            if (!IsControlledByPlayer) return;

            if (context.started || context.performed)
                SwimDownHeld = true;
            else if (context.canceled)
                SwimDownHeld = false;

            if (!context.performed) return;
            if (_state != null && _state.CurrentMovementState == MovementState.Swimming) return;

            CrouchToggle = !CrouchToggle;
        }

        public void OnCapsLock(InputAction.CallbackContext context)
        {
            if (!IsControlledByPlayer || !context.performed) return;
            WalkToggle = !WalkToggle;
        }
#endregion

#region SolControls.IDefaultActions - Actions
        public void OnLClick(InputAction.CallbackContext context)
        {
            if (!IsControlledByPlayer || !context.performed) return;
            // Don't register attacks while UI is open.
            if (LocomotionInputManager.Instance != null && LocomotionInputManager.Instance.UIInputBlocked) return;
            AttackPressed = true;
        }

        public void OnRClick(InputAction.CallbackContext context)
        {
            if (!IsControlledByPlayer) return;
            // Don't toggle aim while UI is open.
            if (LocomotionInputManager.Instance != null && LocomotionInputManager.Instance.UIInputBlocked) return;
            if (context.performed)
                AimPressed = isAimHoldMode || !AimPressed;
            else if (context.canceled)
                AimPressed = !isAimHoldMode && AimPressed;
        }

        public void OnE(InputAction.CallbackContext context)
        {
            if (!IsControlledByPlayer || !context.performed) return;
            // Keep interaction input consistent with the rest of the UI block policy.
            if (LocomotionInputManager.Instance != null && LocomotionInputManager.Instance.UIInputBlocked) return;
            InteractPressed = true;
        }
#endregion

#region SolControls.IDefaultActions - Unused Stubs
        public void OnScroll(InputAction.CallbackContext context) { }
        public void OnMClick(InputAction.CallbackContext context) { }
        public void OnQ(InputAction.CallbackContext context) { }
        public void OnR(InputAction.CallbackContext context)
        {
            if (!IsControlledByPlayer) return;
            if (LocomotionInputManager.Instance != null && LocomotionInputManager.Instance.UIInputBlocked) return;

            if (context.performed)
            {
                ReelHeld = true;
                ReadyTogglePressed = true;
            }
            else if (context.canceled)
            {
                ReelHeld = false;
            }
        }
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
        public void OnLAlt(InputAction.CallbackContext context) { }
        public void OnDown(InputAction.CallbackContext context)
        {
            if (!IsControlledByPlayer) return;
            if (context.started || context.performed)
                SwimDownHeld = true;
            else if (context.canceled)
                SwimDownHeld = false;
        }
        public void OnUp(InputAction.CallbackContext context)
        {
            if (!IsControlledByPlayer) return;
            if (context.started || context.performed)
                SwimUpHeld = true;
            else if (context.canceled)
                SwimUpHeld = false;
        }
        public void OnRight(InputAction.CallbackContext context) { }
        public void OnLeft(InputAction.CallbackContext context) { }
        public void OnTilde(InputAction.CallbackContext context) { }
        public void OnEsc(InputAction.CallbackContext context) { }
        public void OnTab(InputAction.CallbackContext context) { }
        public void OnP(InputAction.CallbackContext context) { }
        public void OnM(InputAction.CallbackContext context) { }
#endregion

        private void TryRegisterCallbacks()
        {
            if (!IsControlledByPlayer || _callbacksRegistered) return;
            var controls = LocomotionInputManager.Instance?.Controls;
            if (controls == null) return;

            controls.Default.AddCallbacks(this);
            _callbacksRegistered = true;
        }

        private void TryUnregisterCallbacks()
        {
            if (!_callbacksRegistered) return;
            var controls = LocomotionInputManager.Instance?.Controls;
            if (controls == null)
            {
                _callbacksRegistered = false;
                return;
            }

            controls.Default.RemoveCallbacks(this);
            _callbacksRegistered = false;
        }
    }
}
