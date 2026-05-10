
using System.Collections.Generic;
using Shared.Water;
using Unity.Cinemachine;
using UnityEngine;
using Sol.AI;



namespace Sol.Locomotion
{
    [DefaultExecutionOrder(-1)]
    public partial class LocomotionController : MonoBehaviour, Shared.Locomotion.ILocomotionController
    {
        private Shared.AI.ILocomotionIntentProvider _intentProvider;

        /// <summary>True if this character is currently in a conversation (suppresses rotation).</summary>
        public bool IsConversing { get; set; } = false;
        private IWaterSystem _waterSystem;
        // --- ILocomotionController Implementation ---
        public Shared.Locomotion.LocomotionState GetState()
        {
            return ConvertToSharedLocomotionState(_state.CurrentMovementState);
        }
        // Converts Sol.Locomotion.MovementState to Shared.Locomotion.LocomotionState
        private Shared.Locomotion.LocomotionState ConvertToSharedLocomotionState(MovementState movementState)
        {
            switch (movementState)
            {
                case MovementState.Swimming:
                    return Shared.Locomotion.LocomotionState.Swimming;
                case MovementState.Climbing:
                    return Shared.Locomotion.LocomotionState.Climbing;
                case MovementState.Falling:
                case MovementState.Jumping:
                    return Shared.Locomotion.LocomotionState.Falling;
                default:
                    return Shared.Locomotion.LocomotionState.Grounded;
            }
        }

#region Class Variables
        [Header("Components")]
        [Tooltip("Inspector: tunes character controller.")]
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private Camera _playerCamera;
        [Tooltip("Inspector: tunes cam transform.")]
        [SerializeField] private Transform _camTransform;
        [Tooltip("Inspector: tunes head mesh renderer.")]
        [SerializeField] private Renderer _headMeshRenderer;
        [Header("Camera Modes")]
        [Tooltip("Inspector: tunes default camera mode.")]
        [SerializeField] private CameraMode _defaultCameraMode = CameraMode.FirstPerson;
        [Tooltip("Inspector: tunes camera modes.")]
        [SerializeField] private List<CameraModeBinding> _cameraModes = new();
        public Camera PlayerCamera { get => _playerCamera; set => _playerCamera = value; }
        public Transform CamTransform { get => _camTransform; set => _camTransform = value; }
        public Renderer HeadMeshRenderer => _headMeshRenderer;
        public CameraMode DefaultCameraMode => _defaultCameraMode;
        public float rotationMismatch { get; private set; } = 0f;
        public bool IsRotatingToTarget { get; private set; } = false;
        private LocomotionInput _locomotionInput;
        private LocomotionState _state;

        [Header("Locomotion Settings")]
        public float movementThreshold = 0.01f;
        public float drag = 0.3f;
        [Tooltip("Deceleration applied when grounded with no input. Higher = crisper stops. " +
                 "Increase for NPCs to prevent sliding.")]
        public float stoppingDeceleration = 15f;
        public float gravity = 25f;
        public float terminalVelocity = 50f;

        [Header("Walk")]
        public float walkAcceleration = .15f;
        public float walkSpeed = 3f;

        [Header("Run(Default)")]
        public float runAcceleration = .3f;
        public float runSpeed = 6f;

        [Header("Sprint")]
        public float sprintAcceleration = .5f;
        public float sprintSpeed = 9f;

        [Header("Speed Smoothing")]
        [Tooltip("How fast currentMaxSpeed ramps toward targetSpeed. Higher = faster transitions.")]
        public float speedLerpFactor = 8f;
        [Tooltip("Easing curve for acceleration (0=stopped, 1=at target speed). " +
                 "EaseInOut for Witcher-like player feel, Linear for crisp NPC response.")]
        public AnimationCurve accelerationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        private float currentMaxSpeed = 0f;
        private float _speedBlendTime = 0f;
        private float _prevTargetSpeed = 0f;

        [Header("Crouch")]
        public float crouchAcceleration = .15f;
        public float crouchSpeed = 3f;
        private float _standingHeight;
        private Vector3 _standingCenter;
        [Tooltip("Inspector: tunes crouch height.")]
        [SerializeField] private float crouchHeight = 1.2f;
        [SerializeField] private Vector3 crouchCenter = new Vector3(0, 0.595f, 0);
        [Tooltip("Inspector: tunes crouch transition speed.")]
        [SerializeField] private float crouchTransitionSpeed = 5f;

        [Header("Jumping & In-Air")]
        public float jumpSpeed = 1.0f;
        public float LedgeJumpCoyoteTime = 0.1f;
        public float inAirAcceleration = 0.15f;
        public float inAirDrag = 0.1f;
        [Tooltip("Allow jumping while crouched (crouch-jump).")]
        public bool allowCrouchJump = true;
        [Tooltip("Crouch-jump gives reduced jump height (multiplier).")]
        [Range(0.5f, 1f)] public float crouchJumpMultiplier = 0.75f;

        [Header("Swimming")]
        [Tooltip("Set true by external trigger (e.g. water volume).")]
        public bool IsSwimming { get; set; }
        public float swimSpeed = 4f;
        public float swimAcceleration = 0.2f;
        public float swimVerticalSpeed = 3f;
        [Tooltip("Fraction of character height that sits at the water surface when floating. " +
                 "0.5 = waist, 0.6 = chest / spine-2. Increase to expose less of the body.")]
        [Range(0.3f, 0.8f)]
        public float swimWaterLine = 0.6f;

        [Header("Flying")]
        [Tooltip("Whether this character is allowed to fly.")]
        public bool CanFly { get; set; }
        [Tooltip("Set true to enter flight (only if CanFly is true).")]
        public bool IsFlying { get; set; }
        public float flySpeed = 8f;
        public float flyAcceleration = 0.3f;
        public float flyVerticalSpeed = 5f;

        [Header("Stat Multipliers")]
        [Tooltip("Fallback value if no delegate is assigned.")]
        public float SpeedMultiplier = 1f;
        [Tooltip("Fallback value if no delegate is assigned.")]
        public float JumpMultiplier = 1f;
        [Tooltip("Swim-specific speed multiplier (skill scaling). Fallback if no delegate is assigned.")]
        public float SwimSpeedMultiplier = 1f;

        [Header("Stat Delegates (set by external systems)")]
        public System.Func<float> GetSpeedMultiplier;
        public System.Func<float> GetJumpMultiplier;
        public System.Func<float> GetSwimSpeedMultiplier;

        [Header("Landing Impact")]
        [Tooltip("Minimum fall distance to trigger landing impact.")]
        [SerializeField] private float landingImpactThreshold = 3f;
        [Tooltip("Fall distance for maximum (hard) landing.")]
        [SerializeField] private float hardLandingDistance = 8f;
        [Tooltip("Duration of hard landing movement lock.")]
        [SerializeField] private float hardLandingRecoveryTime = 0.4f;
        public float LastLandingImpact { get; private set; }
        /// <summary>Fired on landing with normalized impact (0-1). Subscribe from FootstepPlayer.</summary>
        public event System.Action<float> OnLanded;
        private float _fallStartY;
        private bool _trackingFall;
        private float _landingRecoveryTimer;
        private float _externalMovementLockTimer;
        
        [Header("Animation")]
        public float playerModelRotationSpeed = 10f;
        public float rotateToTargetTime = 0.67f;
        [Tooltip("Seconds the camera must be still before idle turn-in-place activates (third person).")]
        [SerializeField] private float turnInPlaceDelay = 5f;

        [Header("Camera Settings")]
        public float lookSenseH = 0.1f;
        public float lookSenseV = 0.1f;
        public float lookLimitV = 70f;

        [Header("Environment Details")]
        [SerializeField] private LayerMask _groundLayers;
        public LayerMask GroundLayers { get => _groundLayers; set => _groundLayers = value; }
        public PhysicsMaterial CurrentGroundMaterial { get; private set; }
        public MovementState CurrentMovementState => _state.CurrentMovementState;
        public Vector3 CurrentVelocity => _characterController.velocity;
        public float CurrentSpeed => new Vector3(_characterController.velocity.x, 0f, _characterController.velocity.z).magnitude;
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public bool IsMovementLocked => _landingRecoveryTimer > 0f || _externalMovementLockTimer > 0f;

        [Header("Slope/Wall handling")]
        [Tooltip("Inspector: tunes steep check distance.")]
        [SerializeField] private float steepCheckDistance = 0.25f;
        [Tooltip("Inspector: tunes steep check disable duration.")]
        [SerializeField] private float steepCheckDisableDuration = 0.12f;
        private float _steepWallDisableTimer = 0f;
        private bool _isNearWallCached = false;

        [Header("Physics Pushing")]
        [Tooltip("Enable pushing rigidbodies on contact.")]
        [SerializeField] private bool enablePushing = true;
        [Tooltip("Base force applied when pushing objects.")]
        [SerializeField] private float pushPower = 2f;
        [Tooltip("Layer mask for pushable objects (leave default for all).")]
        [SerializeField] private LayerMask pushLayers = ~0;

        private bool _jumpedLastFrame = false;
        private bool _isRotatingClockwise = false;
        private float _rotatingToTargetTimer = 0f;
        private float _verticalVelocity = 0f;
        private Vector3 _swimVelocity = Vector3.zero;
        private bool _wasSwimmingLastFrame = false;
        private float _antiBump;
        private float _stepOffset;
        
        /// <summary>The CharacterController's original step offset (read-only, for IK to match foot reach).</summary>
        public float StepOffset => _stepOffset;
        private float _ledgeJumpCoyoteTimer = 0f;

        private MovementState _lastMovementState = MovementState.Falling;
#endregion



#region Initialize
        private void Awake()
        {
            _locomotionInput = GetComponent<LocomotionInput>();
            _state = GetComponent<LocomotionState>();
            _waterSystem = GetComponent<IWaterSystem>() ?? FindFirstObjectByType<WaterSystemAdapter>();
            // Try to get an AI intent provider if present (for NPCs)
            _intentProvider = GetComponent<Shared.AI.ILocomotionIntentProvider>();

            _antiBump = sprintSpeed;
            _stepOffset = _characterController.stepOffset;

            _standingHeight = _characterController.height;
            _standingCenter = _characterController.center;
            currentMaxSpeed = walkSpeed;
        }
#endregion



#region Update Logic
        private void Start()
        {
            if (IsPlayerControlled())
                LocomotionInputManager.Instance?.RegisterRuntimeRig(this);
        }

        private void Update()
        {
            // If this controller has an AI intent provider, convert its intent to
            // camera-relative MovementInput (x = right, y = forward in _camTransform space),
            // which is the coordinate frame HandleLateralMovement expects.
            // AI_NPC.UpdateFakeCam() keeps _camTransform (fakeCam) oriented toward
            // agent.steeringTarget each frame so the projection produces correct directions.
            if (_intentProvider != null)
            {
                var intent = _intentProvider.GetIntent((Shared.Locomotion.ILocomotionController)this);
                Vector3 toTarget = intent.TargetPosition - transform.position;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                Vector2 moveInput = Vector2.zero;
                if (distance > 0.1f)
                {
                    Vector3 dir = toTarget / distance;
                    if (_camTransform != null)
                    {
                        // Project the world-space direction onto the camera's XZ axes.
                        Vector3 camFwd   = new Vector3(_camTransform.forward.x, 0f, _camTransform.forward.z).normalized;
                        Vector3 camRight = new Vector3(_camTransform.right.x,   0f, _camTransform.right.z).normalized;
                        if (camFwd.sqrMagnitude > 0.0001f)
                            moveInput = new Vector2(Vector3.Dot(dir, camRight), Vector3.Dot(dir, camFwd));
                    }
                    else
                    {
                        // No cam reference: world XZ fallback (entity has no orientated body).
                        moveInput = new Vector2(dir.x, dir.z);
                    }
                    // Clamp to unit disc - player hardware input is naturally bounded,
                    // but the dot projection can exceed 1.0 at diagonal angles.
                    if (moveInput.sqrMagnitude > 1f)
                        moveInput = moveInput.normalized;
                }
                _locomotionInput.MovementInput = moveInput;
                _locomotionInput.WalkToggle    = intent.ActionType == Shared.AI.LocomotionActionType.Walk;
                _locomotionInput.SprintPressed = intent.ActionType == Shared.AI.LocomotionActionType.Sprint;
            }

            // Swimming and flying use their own movement pipeline
            if (IsSwimming && !IsFlying)
            {
                _verticalVelocity = 0f; // Cancel gravity carried from falling
                _state.SetMovementState(MovementState.Swimming);

                // Uncrouch when entering water so the character stands at full height.
                if (!_wasSwimmingLastFrame && _locomotionInput.CrouchToggle)
                    _locomotionInput.CrouchToggle = false;

                // Cancel any in-progress fall tracking so landing into water
                // doesn't produce a landing-impact animation on swim exit.
                _trackingFall = false;
                _landingRecoveryTimer = 0f;
                LastLandingImpact = 0f;

                _wasSwimmingLastFrame = true;
                HandleSwimMovement();
                return;
            }

            _wasSwimmingLastFrame = false;

            // Clear swim momentum the first frame after leaving the water so it
            // doesn't bleed into the ground/air movement pipeline.
            if (_swimVelocity != Vector3.zero)
            {
                // Seed vertical velocity to the normal grounded value so the
                // player doesn't flicker through a Falling state on exit.
                if (_characterController.isGrounded)
                    _verticalVelocity = -_antiBump;

                _swimVelocity = Vector3.zero;
            }

            if (IsFlying && CanFly && !IsSwimming)
            {
                _state.SetMovementState(MovementState.Flying);
                HandleFlyMovement();
                return;
            }

            // Normal ground/air pipeline
            UpdateLandingImpact();
            if (_externalMovementLockTimer > 0f)
            {
                _externalMovementLockTimer = Mathf.Max(0f, _externalMovementLockTimer - Time.deltaTime);
                _locomotionInput.JumpPressed = false;
                HandleVerticalMovement();
                UpdateMovementState();
                UpdateCrouchShape();
                UpdateGroundMaterial();
                return;
            }

            if (_landingRecoveryTimer > 0f)
            {
                _landingRecoveryTimer -= Time.deltaTime;
                HandleVerticalMovement();
                UpdateMovementState();
                UpdateCrouchShape();
                // Movement locked during hard landing recovery
                return;
            }

            HandleVerticalMovement();
            UpdateMovementState();
            UpdateCrouchShape();
            HandleLateralMovement();
            UpdateGroundMaterial();
        }

        private void UpdateMovementState() 
        {
            _lastMovementState = _state.CurrentMovementState;

            bool canRun = CanRun();
            bool isMovementInput = _locomotionInput.MovementInput != Vector2.zero;
            bool isMovingLaterally = IsMovingLaterally();
            bool isSprinting = _locomotionInput.SprintPressed && isMovingLaterally;
            bool isWalking = isMovingLaterally && (!canRun || _locomotionInput.WalkToggle);

            bool isGrounded = IsGrounded();
            bool isTrulyGrounded = _state.InGroundedState() && isGrounded;

            bool wantsToCrouch = _locomotionInput.CrouchToggle && isTrulyGrounded;

            if (_steepWallDisableTimer > 0f)
                _steepWallDisableTimer -= Time.deltaTime;

            _isNearWallCached = LocomotionUtil.DetectNearbyWall(_characterController, transform, _groundLayers, steepCheckDistance);

            if (_isNearWallCached)
                _steepWallDisableTimer = steepCheckDisableDuration;

            MovementState lateralState = wantsToCrouch ? MovementState.Crouching :
                                         isWalking ? MovementState.Walking :   
                                         isSprinting ? MovementState.Sprinting :
                                         isMovingLaterally || isMovementInput ? MovementState.Running : MovementState.Idle;

            if (_state.CurrentMovementState == MovementState.Crouching && !wantsToCrouch)
            {
                if (CanStandUp()) _state.SetMovementState(lateralState);
                else _state.SetMovementState(MovementState.Crouching);
            }
            else
            {
                _state.SetMovementState(lateralState);
            }
            
            //control Airborne
            if ((!isGrounded || _jumpedLastFrame) && _characterController.velocity.y > 0f)
            {
                _state.SetMovementState(MovementState.Jumping);
                _jumpedLastFrame = false;
                _characterController.stepOffset = 0f;
            }

            else if ((!isGrounded || _jumpedLastFrame) && _characterController.velocity.y <= 0f)
            {
                _state.SetMovementState(MovementState.Falling);
                _jumpedLastFrame = false;
                _characterController.stepOffset = 0f;
            }
            else
            {
                if (_steepWallDisableTimer > 0f)
                    _characterController.stepOffset = 0f;
                else
                    _characterController.stepOffset = _stepOffset;
            }
        }

        private void HandleVerticalMovement()
        {
            bool isGrounded = _state.InGroundedState();

            _verticalVelocity -= gravity * Time.deltaTime;

            if (isGrounded && _verticalVelocity <= 0)
            {
                _verticalVelocity = -_antiBump;
                _ledgeJumpCoyoteTimer = LedgeJumpCoyoteTime;
            }
            else
            {
                _ledgeJumpCoyoteTimer -= Time.deltaTime;
            }


            bool isCrouching = _state.CurrentMovementState == MovementState.Crouching;
            bool canJump = isGrounded || _ledgeJumpCoyoteTimer > 0f;
            bool crouchJump = isCrouching && allowCrouchJump;

            float effectiveJumpMult = GetJumpMultiplier?.Invoke() ?? JumpMultiplier;

            if (_locomotionInput.JumpPressed && canJump && (!isCrouching || crouchJump))
            {
                float jumpMult = effectiveJumpMult * (crouchJump ? crouchJumpMultiplier : 1f);
                _verticalVelocity += Mathf.Sqrt(jumpSpeed * jumpMult * 3 * gravity);
                _jumpedLastFrame = true;
                _ledgeJumpCoyoteTimer = 0f;

                // Uncrouch on crouch-jump
                if (crouchJump && CanStandUp())
                    _locomotionInput.CrouchToggle = false;
            }

            if (_state.IsStateGrounded(_lastMovementState) && !isGrounded)
            {
                _verticalVelocity += _antiBump;
            }

            if (Mathf.Abs(_verticalVelocity) > Mathf.Abs(terminalVelocity))
            {
                _verticalVelocity = -1f * Mathf.Abs(terminalVelocity);
            }

        }

        private void HandleLateralMovement()
        {

            bool isGrounded = _state.InGroundedState();
            bool isCrouching = _state.CurrentMovementState == MovementState.Crouching;
            bool isWalking = _state.CurrentMovementState == MovementState.Walking;
            bool isSprinting = _state.CurrentMovementState == MovementState.Sprinting;

            //state dependant accel
            bool hasInput = _locomotionInput.MovementInput.sqrMagnitude > 0.001f;
            float lateralAcceleration = !isGrounded ? inAirAcceleration :
                                          isWalking ? walkAcceleration :
                                          isCrouching ? crouchAcceleration :
                                          isSprinting ? sprintAcceleration : runAcceleration;
            float effectiveSpeedMult = GetSpeedMultiplier?.Invoke() ?? SpeedMultiplier;
            float rawTargetSpeed = !isGrounded ? sprintSpeed :
                                    isWalking ? walkSpeed :
                                    isCrouching ? crouchSpeed :
                                    isSprinting ? sprintSpeed : runSpeed;
            float targetSpeed = hasInput ? rawTargetSpeed * effectiveSpeedMult : 0f;

            // Reset blend timer when target speed changes significantly
            // (e.g. start moving, stop moving, or change speed tier)
            if (Mathf.Abs(targetSpeed - _prevTargetSpeed) > 1f)
                _speedBlendTime = 0f;
            _prevTargetSpeed = targetSpeed;

            // speed smoothing via animation curve
            if (Mathf.Abs(targetSpeed - currentMaxSpeed) > 0.01f)
            {
                _speedBlendTime += Time.deltaTime * speedLerpFactor / Mathf.Max(1f, Mathf.Abs(targetSpeed - currentMaxSpeed));
                _speedBlendTime = Mathf.Clamp01(_speedBlendTime);
                float curveValue = accelerationCurve.Evaluate(_speedBlendTime);
                currentMaxSpeed = Mathf.Lerp(currentMaxSpeed, targetSpeed, curveValue);
            }
            else
            {
                _speedBlendTime = 0f;
                currentMaxSpeed = targetSpeed;
            }
            float clampLateralMagnitude = currentMaxSpeed;

            // Sets movement to camera direction (falls back to world axes for NPCs without a camera)
            Transform camRef = _camTransform != null ? _camTransform : null;
            Vector3 forward = camRef != null ? new Vector3(camRef.forward.x, 0f, camRef.forward.z).normalized : Vector3.forward;
            Vector3 right   = camRef != null ? new Vector3(camRef.right.x,   0f, camRef.right.z).normalized   : Vector3.right;
            Vector3 movementDirection = right * _locomotionInput.MovementInput.x + forward * _locomotionInput.MovementInput.y;

            Vector3 movementDelta = movementDirection * lateralAcceleration * Time.deltaTime;
            Vector3 combinedVelocity = _characterController.velocity + movementDelta;

            //Apply drag to character, if new drag is greater than current, subtract current or set to 0
            float dragMagnitude = isGrounded ? drag : inAirDrag;
            Vector3 currentDrag = combinedVelocity.normalized * dragMagnitude * Time.deltaTime;
            combinedVelocity = (combinedVelocity.magnitude > dragMagnitude * Time.deltaTime) ? combinedVelocity - currentDrag : Vector3.zero;

            // Active stopping - when grounded with no input, decelerate quickly
            // so the character doesn't slide past the animation blend-down.
            if (!hasInput && isGrounded)
            {
                Vector3 lateralVel = new Vector3(combinedVelocity.x, 0f, combinedVelocity.z);
                lateralVel = Vector3.MoveTowards(lateralVel, Vector3.zero, stoppingDeceleration * Time.deltaTime);
                combinedVelocity = new Vector3(lateralVel.x, combinedVelocity.y, lateralVel.z);
            }

            combinedVelocity = Vector3.ClampMagnitude(new Vector3(combinedVelocity.x, 0f, combinedVelocity.z), clampLateralMagnitude);
            combinedVelocity.y += _verticalVelocity;
            if (!isGrounded)
            {
                combinedVelocity = LocomotionUtil.AdjustVelocityForSteepGround(_characterController, combinedVelocity, _groundLayers, 0.5f);
            }

            _characterController.Move(combinedVelocity * Time.deltaTime);
        }

        private void UpdateCrouchShape()
        {
            float targetHeight = _state.CurrentMovementState == MovementState.Crouching ? crouchHeight : _standingHeight;
            Vector3 targetCenter = _state.CurrentMovementState == MovementState.Crouching ? crouchCenter : _standingCenter;

            _characterController.height = Mathf.Lerp(_characterController.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            _characterController.center = Vector3.Lerp(_characterController.center, targetCenter, Time.deltaTime * crouchTransitionSpeed);
        }
#endregion



#region Late Update Logic
        private void LateUpdate()
        {
            UpdateBodyRotation();
        }
        private void UpdateBodyRotation()
        {

            if (_camTransform == null) return;
            if (IsConversing) return; // Suppress rotation during conversation

            // NPCs always rotate toward fakeCam.forward unconditionally.
            // The player-specific third-person idle-orbit / turn-in-place logic depends
            // on LocomotionInputManager (player singleton) which NPCs don't own, and
            // would deadlock idle NPCs waiting for a CameraStillDuration that never comes.
            if (_intentProvider != null)
            {
                RotatePlayerToTarget();
            }
            else
            {
                bool isIdle = _state.CurrentMovementState == MovementState.Idle;
                bool isThirdPerson = LocomotionInputManager.Instance != null && LocomotionInputManager.Instance.IsThirdPerson;

                if (_state.InSpecialLocomotionState() && isThirdPerson) return;

                if (_rotatingToTargetTimer > 0f)
                    _rotatingToTargetTimer -= Time.deltaTime;

                IsRotatingToTarget = _rotatingToTargetTimer > 0;

                if (!isThirdPerson)
                {
                    _rotatingToTargetTimer = 0f;
                    IsRotatingToTarget = false;
                    RotatePlayerToTarget();
                }
                else if (!isIdle)
                {
                    _rotatingToTargetTimer = 0f;
                    IsRotatingToTarget = false;
                    RotatePlayerToTarget();
                }
                else
                {
                    float cameraStillTime = LocomotionInputManager.Instance != null
                        ? LocomotionInputManager.Instance.CameraStillDuration : 0f;

                    if (cameraStillTime >= turnInPlaceDelay)
                    {
                        if (Mathf.Abs(rotationMismatch) > 90f || IsRotatingToTarget)
                            UpdateIdleRotation(90f);
                    }
                    else
                    {
                        _rotatingToTargetTimer = 0f;
                        IsRotatingToTarget = false;
                    }
                }
            }

            // Update rotation mismatch for animation system
            Vector3 camForwardProjectedXZ = new Vector3(_camTransform.forward.x, 0f, _camTransform.forward.z).normalized;
            Vector3 crossProduct = Vector3.Cross(transform.forward, camForwardProjectedXZ);
            float sign = Mathf.Sign(Vector3.Dot(crossProduct, transform.up));
            rotationMismatch = sign * Vector3.Angle(transform.forward, camForwardProjectedXZ);
        }

        private void UpdateIdleRotation(float rotationTolerance)
        {
            if (Mathf.Abs(rotationMismatch) > rotationTolerance)
            {
                _rotatingToTargetTimer = rotateToTargetTime;
                _isRotatingClockwise = rotationMismatch > rotationTolerance;
            }

            if (_isRotatingClockwise && rotationMismatch > 0f ||
                    !_isRotatingClockwise && rotationMismatch < 0f)
            {
                RotatePlayerToTarget();
            }

        }
        
        private void RotatePlayerToTarget()
        { 
            Vector3 camForwardProjectedXZ = new Vector3(_camTransform.forward.x, 0f, _camTransform.forward.z).normalized;

            if (camForwardProjectedXZ.sqrMagnitude < 0.0001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(camForwardProjectedXZ, Vector3.up);

            float regularRotSpeed;
            if (_rotatingToTargetTimer > 0f && rotateToTargetTime > 0f)
            {
                regularRotSpeed = Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.0001f, _rotatingToTargetTimer));
            }
            else
            {
                regularRotSpeed = playerModelRotationSpeed * Time.deltaTime;
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Mathf.Clamp01(regularRotSpeed));

            if (Quaternion.Angle(transform.rotation, targetRotation) < 0.1f)
                transform.rotation = targetRotation;
        }
#endregion



#region State Checks
        private bool IsMovingLaterally()
        {
            Vector3 lateralVelocity = new Vector3(_characterController.velocity.x, 0f, _characterController.velocity.z);

            return lateralVelocity.magnitude > movementThreshold;
        }

        private bool IsGrounded() 
        {
            bool grounded = _state.InGroundedState() ? IsGroundedWhileGrounded() : IsGroundedWhileAirborne();

            return grounded;
        }

        private bool IsGroundedWhileGrounded()
        {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - _characterController.radius, transform.position.z);

            bool grounded = Physics.CheckSphere(spherePosition, _characterController.radius, _groundLayers, QueryTriggerInteraction.Ignore);

            return grounded;
        }

        private bool IsGroundedWhileAirborne()
        {
            Vector3 normal = LocomotionUtil.GetNormalWithSphereCast(_characterController, _groundLayers);
            float angle = Vector3.Angle(normal, Vector3.up);
            bool validAngle = angle <= _characterController.slopeLimit;

            return _characterController.isGrounded && validAngle;
        }

        private bool CanRun()
        {
            // NPCs have their direction projected by the intent pipeline, so y-dominance is meaningless.
            // An NPC pathing diagonally would always walk instead of run without this bypass.
            if (_intentProvider != null) return true;
            return _locomotionInput.MovementInput.y >= Mathf.Abs(_locomotionInput.MovementInput.x);
        }

        private bool IsGroundedWhileGroundedDetailed(out RaycastHit hitInfo)
        {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - _characterController.radius, transform.position.z);
            bool grounded = Physics.SphereCast(spherePosition, _characterController.radius, Vector3.down, out hitInfo, 0.1f, _groundLayers, QueryTriggerInteraction.Ignore);
            return grounded;
        }

        private void UpdateGroundMaterial()
        {
            if (_state.InGroundedState() && IsGroundedWhileGroundedDetailed(out var hit))
            {
                CurrentGroundMaterial = hit.collider.sharedMaterial;
                GroundNormal = hit.normal;
            }
            else
            {
                CurrentGroundMaterial = null;
                GroundNormal = Vector3.up;
            }
        }

        private void UpdateLandingImpact()
        {
            bool isFalling = _state.CurrentMovementState == MovementState.Falling;
            bool isGrounded = _state.InGroundedState();

            if (isFalling && !_trackingFall)
            {
                _fallStartY = transform.position.y;
                _trackingFall = true;
            }

            if (_trackingFall && isGrounded)
            {
                float fallDistance = _fallStartY - transform.position.y;
                _trackingFall = false;

                if (fallDistance >= landingImpactThreshold)
                {
                    LastLandingImpact = Mathf.Clamp01(fallDistance / hardLandingDistance);
                    OnLanded?.Invoke(LastLandingImpact);

                    if (fallDistance >= hardLandingDistance)
                        _landingRecoveryTimer = hardLandingRecoveryTime;
                }
                else
                {
                    LastLandingImpact = 0f;
                }
            }
        }

#endregion
    }
}
