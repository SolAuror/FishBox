using UnityEngine;

namespace Sol.Locomotion
{
    [DefaultExecutionOrder(1)]
    public class LocomotionIK : MonoBehaviour
    {
        #region Inspector Settings
        [Header("Components")]
        [Tooltip("Inspector: tunes animator.")]
        [SerializeField] private Animator _animator;
        [SerializeField] private LocomotionState _state;
        [Tooltip("Inspector: tunes controller.")]
        [SerializeField] private LocomotionController _controller;

        [Header("Foot IK Settings")]
        [Tooltip("Enable/disable foot IK system.")]
        [SerializeField] private bool enableFootIK = true;
        
        [Tooltip("Enable foot locking to prevent sliding when planted (only active when idle/walking).")]
        [SerializeField] private bool enableFootLocking = true;
        
        [Tooltip("Foot velocity below this locks foot in place (only when walking/idle).")]
        [SerializeField] private float lockFootVelocityThreshold = 0.4f;
        
        [Tooltip("Minimum distance between feet to prevent leg crossing.")]
        [SerializeField] private float minFootSeparation = 0.15f;
        
        [Tooltip("Distance to raycast below foot for ground detection.")]
        [SerializeField] private float raycastDistance = 0.5f;
        
        [Tooltip("Extra height offset added to foot position.")]
        [SerializeField] private float footOffset = 0.05f;
        
        [Tooltip("Maximum distance feet can be adjusted down from animated position.")]
        [SerializeField] private float maxFootAdjustmentDown = 0.6f;
        
        [Tooltip("Maximum distance feet can be adjusted up from animated position.")]
        [SerializeField] private float maxFootAdjustmentUp = 0.4f;
        
        [Tooltip("How quickly IK weight transitions on/off.")]
        [SerializeField] private float weightTransitionSpeed = 3f;
        
        [Tooltip("Speed at or above which foot IK weight starts fading (lets fast animations play cleanly).")]
        [SerializeField] private float footIKFadeStartSpeed = 4f;
        
        [Tooltip("Speed at or above which foot IK is fully off.")]
        [SerializeField] private float footIKFadeEndSpeed = 8f;
        
        [Header("Foot Rotation")]
        [Tooltip("Tilt feet to match ground slope using heel + ball raycasts. Gives natural foot angle on slopes.")]
        [SerializeField] private bool enableFootTilt = true;
        
        [Tooltip("How quickly foot rotation aligns to ground normal. Lower = smoother but laggy, higher = responsive but can jitter.")]
        [SerializeField] private float footRotationSpeed = 5f;
        
        [Header("Knee Hints")]
        [Tooltip("Enable knee IK hints to prevent knee popping on slopes.")]
        [SerializeField] private bool enableKneeHints = true;
        
        [Tooltip("Local-space offset for the LEFT knee hint (X=right, Y=up, Z=forward relative to character).")]
        [SerializeField] private Vector3 leftKneeHintOffset = new Vector3(0f, 0.1f, 0.3f);
        
        [Tooltip("Local-space offset for the RIGHT knee hint (X=right, Y=up, Z=forward relative to character).")]
        [SerializeField] private Vector3 rightKneeHintOffset = new Vector3(0f, 0.1f, 0.3f);
        
        [Tooltip("Speed at or below which knee hints are at full weight.")]
        [SerializeField] private float kneeHintFullWeightSpeed = 2f;
        
        [Tooltip("Speed at or above which knee hints are fully faded out.")]
        [SerializeField] private float kneeHintZeroWeightSpeed = 4f;
        
        [Header("Pelvis")]
        [Tooltip("Adjust pelvis height to accommodate foot positions.")]
        [SerializeField] private bool adjustPelvis = true;
        
        [Tooltip("How much to lower pelvis when feet need to reach down (0=none, 1=full).")]
        [Range(0f, 1f)]
        [SerializeField] private float pelvisAdjustmentAmount = 1f;
        
        [Tooltip("Maximum pelvis dip in meters. Prevents excessive squatting.")]
        [SerializeField] private float maxPelvisDip = 0.4f;
        
        [Tooltip("How quickly pelvis offset changes. Higher = snappier, lower = smoother.")]
        [SerializeField] private float pelvisSmoothSpeed = 8f;
        
        [Tooltip("Speed at or below which pelvis is fully adjusted (idle/slow walk).")]
        [SerializeField] private float pelvisFullAdjustSpeed = 1.5f;
        
        [Tooltip("Speed at or above which pelvis adjustment is at its minimum.")]
        [SerializeField] private float pelvisMinAdjustSpeed = 5f;
        
        [Tooltip("Minimum pelvis adjustment multiplier at high speed (0=none, 1=full).")]
        [Range(0f, 1f)]
        [SerializeField] private float pelvisMinSpeedMultiplier = 0.2f;
        
        [Header("Raycast")]
        [Tooltip("Cast rays from hip height for more stable ground detection.")]
        [SerializeField] private bool raycastFromHips = true;
        
        [Tooltip("Layers to detect as ground.")]
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("Head / Upper Body Look IK")]
        [Tooltip("Enable head and upper body look-at IK.")]
        [SerializeField] private bool enableLookIK = true;
        
        [Tooltip("How far ahead of the camera the look target is placed.")]
        [SerializeField] private float lookTargetDistance = 10f;
        
        [Tooltip("Overall look-at weight (0 = off, 1 = full).")]
        [Range(0f, 1f)]
        [SerializeField] private float lookWeight = 1f;
        
        [Tooltip("How much the body rotates toward the look target.")]
        [Range(0f, 1f)]
        [SerializeField] private float lookBodyWeight = 0.3f;
        
        [Tooltip("How much the head rotates toward the look target.")]
        [Range(0f, 1f)]
        [SerializeField] private float lookHeadWeight = 0.8f;
        
        [Tooltip("How much the eyes rotate toward the look target.")]
        [Range(0f, 1f)]
        [SerializeField] private float lookEyesWeight = 1f;
        
 [Tooltip("Clamp weight - 0 = no limit, 1 = completely clamped. Controls how far the look can deviate from forward.")]
        [Range(0f, 1f)]
        [SerializeField] private float lookClampWeight = 0.3f;
        
        [Tooltip("How quickly look IK blends in/out.")]
        [SerializeField] private float lookTransitionSpeed = 5f;
        
        [Tooltip("Maximum angle from body forward before look IK weight reduces. Prevents unnatural head twist.")]
        [SerializeField] private float lookMaxAngle = 80f;
        
        [Tooltip("Reduce body weight to zero when idle in third person (avoids upper body twisting while orbiting).")]
        [SerializeField] private bool reduceBodyWeightWhenIdle = true;

        [Header("Hand / Arm IK")]
        [Tooltip("How quickly hand IK weight blends in/out when activated or deactivated.")]
        [SerializeField] private float handIKTransitionSpeed = 8f;
        [Tooltip("How quickly the hand IK target position follows changes. Higher = snappier, lower = smoother.")]
        [SerializeField] private float handIKTargetPositionSmoothSpeed = 18f;
        [Tooltip("How quickly the hand IK target rotation follows changes. Higher = snappier, lower = smoother.")]
        [SerializeField] private float handIKTargetRotationSmoothSpeed = 18f;

        [Tooltip("Local-space offset for the LEFT elbow hint (X=right, Y=up, Z=forward relative to character).")]
        [SerializeField] private Vector3 leftElbowHintOffset = new Vector3(-0.2f, -0.1f, -0.3f);

        [Tooltip("Local-space offset for the RIGHT elbow hint (X=right, Y=up, Z=forward relative to character).")]
        [SerializeField] private Vector3 rightElbowHintOffset = new Vector3(0.2f, -0.1f, -0.3f);

        [Header("Animation Curve IK Weights")]
        [Tooltip("Use per-foot IK weight curves from animation clips (LeftFootIK / RightFootIK). " +
                 "When enabled, foot IK weight is multiplied by the clip's curve value so IK only applies when the foot is planted.")]
        [SerializeField] private bool useAnimationCurves = true;
        
        [Tooltip("Name of the animator float parameter for left foot IK weight (0 = swing, 1 = planted).")]
        [SerializeField] private string leftFootCurveParam = "LeftFootIK";
        
        [Tooltip("Name of the animator float parameter for right foot IK weight (0 = swing, 1 = planted).")]
        [SerializeField] private string rightFootCurveParam = "RightFootIK";

        [Header("Debug")]
        [SerializeField] private bool showDebugRays = false;
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool showDebugSpheres = false;
        #endregion

        // IK weights
        private float _leftFootWeight = 0f;
        private float _rightFootWeight = 0f;
        private float _leftFootRotWeight = 0f;
        private float _rightFootRotWeight = 0f;
        
        // Foot positions
        private Vector3 _leftFootPosition;
        private Vector3 _rightFootPosition;
        private Quaternion _leftFootRotation;
        private Quaternion _rightFootRotation;

        // Pelvis adjustment
        private float _leftFootOffset = 0f;
        private float _rightFootOffset = 0f;
        private float _pelvisOffset = 0f;

        // Foot locking
        private bool _leftFootLocked = false;
        private bool _rightFootLocked = false;
        private Vector3 _leftFootLockedPosition;
        private Vector3 _rightFootLockedPosition;
        private Quaternion _leftFootLockedRotation;
        private Quaternion _rightFootLockedRotation;
        private Vector3 _lastLeftFootAnimPos;
        private Vector3 _lastRightFootAnimPos;

        // Smoothed foot rotations
        private Quaternion _smoothLeftFootRot;
        private Quaternion _smoothRightFootRot;
        private bool _rotationsInitialized;

        // Last valid IK positions
        private Vector3 _lastLeftFootPos;
        private Vector3 _lastRightFootPos;

        // Hip transform cache
        private Transform _hipTransform;

        // Foot bone caches for two-point sole tilt
        private Transform _leftFootBone, _rightFootBone;
        private Transform _leftToesBone, _rightToesBone;

        // Look IK
        private float _currentLookWeight = 0f;
        private Vector3 _smoothLookTarget;

 // Hand IK - inactive by default, driven by external grab/interaction systems
        private float _leftHandTargetWeight = 0f;
        private float _rightHandTargetWeight = 0f;
        private float _leftHandTargetRotationWeight = 1f;
        private float _rightHandTargetRotationWeight = 1f;
        private float _leftHandCurrentWeight = 0f;
        private float _rightHandCurrentWeight = 0f;
        private Vector3 _leftHandTargetPos;
        private Vector3 _rightHandTargetPos;
        private Quaternion _leftHandTargetRot = Quaternion.identity;
        private Quaternion _rightHandTargetRot = Quaternion.identity;
        private Vector3 _leftHandSmoothedTargetPos;
        private Vector3 _rightHandSmoothedTargetPos;
        private Quaternion _leftHandSmoothedTargetRot = Quaternion.identity;
        private Quaternion _rightHandSmoothedTargetRot = Quaternion.identity;
        private bool _leftHandActive = false;
        private bool _rightHandActive = false;

        // Deferred init flag
        private bool _initialized;

        // Whether the animator has the foot curve parameters
        private bool _hasLeftFootCurve;
        private bool _hasRightFootCurve;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_state == null) _state = GetComponent<LocomotionState>();
            if (_controller == null) _controller = GetComponent<LocomotionController>();
        }

        private void Start()
        {
            if (_animator == null) return;

            _hipTransform = _animator.GetBoneTransform(HumanBodyBones.Hips);

            // Cache foot bones for two-point sole tilt
            _leftFootBone  = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFootBone = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _leftToesBone  = _animator.GetBoneTransform(HumanBodyBones.LeftToes);
            _rightToesBone = _animator.GetBoneTransform(HumanBodyBones.RightToes);

            // Initialize look target ahead of camera
            if (_controller != null && _controller.CamTransform != null)
                _smoothLookTarget = _controller.CamTransform.position + _controller.CamTransform.forward * lookTargetDistance;

            // Check whether the Animator Controller exposes the foot curve parameters.
            // Clips with baked curves feed these automatically; clips without them
            // would return 0 from GetFloat, incorrectly zeroing IK weight.
            _hasLeftFootCurve = HasAnimatorParameter(leftFootCurveParam);
            _hasRightFootCurve = HasAnimatorParameter(rightFootCurveParam);
        }

        private bool HasAnimatorParameter(string paramName)
        {
            foreach (var p in _animator.parameters)
            {
                if (p.name == paramName)
                    return true;
            }
            return false;
        }

        private void InitializeFootPositions()
        {
            _lastLeftFootPos = _animator.GetIKPosition(AvatarIKGoal.LeftFoot);
            _lastRightFootPos = _animator.GetIKPosition(AvatarIKGoal.RightFoot);
            _lastLeftFootAnimPos = _lastLeftFootPos;
            _lastRightFootAnimPos = _lastRightFootPos;
            _initialized = true;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null) return;
            
 // Only process IK on the base layer - additional layers with IK Pass enabled
            // would double-process foot positions each frame, causing pelvis oscillation and sliding.
            if (layerIndex != 0) return;

            if (!_initialized)
                InitializeFootPositions();

            // --- Look / Aim IK ---
            ProcessLookIK();

            // --- Hand / Arm IK ---
            ProcessHandIK(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow,
                _leftHandActive, _leftHandTargetPos, _leftHandTargetRot,
                _leftHandTargetWeight, _leftHandTargetRotationWeight, ref _leftHandCurrentWeight,
                ref _leftHandSmoothedTargetPos, ref _leftHandSmoothedTargetRot, leftElbowHintOffset);
            ProcessHandIK(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow,
                _rightHandActive, _rightHandTargetPos, _rightHandTargetRot,
                _rightHandTargetWeight, _rightHandTargetRotationWeight, ref _rightHandCurrentWeight,
                ref _rightHandSmoothedTargetPos, ref _rightHandSmoothedTargetRot, rightElbowHintOffset);

            // --- Foot IK ---
            if (!enableFootIK) return;

            bool isGrounded = _state != null && _state.InGroundedState();
            
            // Base target: 1 when grounded, 0 when airborne
            float targetWeight = isGrounded ? 1f : 0f;
            
 // Fade foot IK out at higher speeds - fast animations (sprint) are designed
            // to look correct without IK. Forcing IK at speed causes the solver's knee-bend
            // to shift foot XZ, producing visible sliding.
            if (targetWeight > 0f && _controller != null)
            {
                float speed = _controller.CurrentSpeed;
                float speedRange = footIKFadeEndSpeed - footIKFadeStartSpeed;
                if (speedRange > 0f)
                    targetWeight *= Mathf.Clamp01(1f - (speed - footIKFadeStartSpeed) / speedRange);
            }

            // Smooth weight transition
            float leftCurveWeight = 1f;
            float rightCurveWeight = 1f;
            if (useAnimationCurves)
            {
                // Only read the curve value when the parameter actually exists in the
                // Animator Controller. Clips without the curve return 0 from GetFloat,
                // which would disable IK entirely - we default to 1 (full IK) instead.
                if (_hasLeftFootCurve)
                    leftCurveWeight = _animator.GetFloat(leftFootCurveParam);
                if (_hasRightFootCurve)
                    rightCurveWeight = _animator.GetFloat(rightFootCurveParam);
            }
            
            _leftFootWeight = Mathf.Lerp(_leftFootWeight, targetWeight * leftCurveWeight, Time.deltaTime * weightTransitionSpeed);
            _rightFootWeight = Mathf.Lerp(_rightFootWeight, targetWeight * rightCurveWeight, Time.deltaTime * weightTransitionSpeed);
            
            // Rotation weight fades at half the speed so foot angle blends out more gradually
            _leftFootRotWeight = Mathf.Lerp(_leftFootRotWeight, targetWeight * leftCurveWeight, Time.deltaTime * weightTransitionSpeed * 0.5f);
            _rightFootRotWeight = Mathf.Lerp(_rightFootRotWeight, targetWeight * rightCurveWeight, Time.deltaTime * weightTransitionSpeed * 0.5f);

            // Reset offsets
            _leftFootOffset = 0f;
            _rightFootOffset = 0f;

            // Disable foot locking when moving fast or during active turn-in-place rotation
            bool allowFootLocking = enableFootLocking;
            bool isRotating = _controller != null && _controller.IsRotatingToTarget;
            
            if (_controller != null && _controller.CurrentSpeed > 3f)
            {
                allowFootLocking = false;
                _leftFootLocked = false;
                _rightFootLocked = false;
            }
            
            // Release locks only when body is actually rotating (not just mismatched)
            if (isRotating)
            {
                allowFootLocking = false;
                _leftFootLocked = false;
                _rightFootLocked = false;
            }
            
 // Suppress foot locking during lateral movement - strafe animations need feet to cycle freely
            if (allowFootLocking && _controller != null)
            {
                float lateralSpeed = Mathf.Abs(Vector3.Dot(_controller.CurrentVelocity, transform.right));
                if (lateralSpeed > 0.5f)
                {
                    allowFootLocking = false;
                    _leftFootLocked = false;
                    _rightFootLocked = false;
                }
            }

            if (_leftFootWeight > 0.01f)
            {
                ProcessFootIK(AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, _leftFootWeight, ref _leftFootPosition, ref _leftFootRotation, 
                    ref _lastLeftFootPos, ref _leftFootOffset, ref _leftFootLocked, ref _leftFootLockedPosition, 
                    ref _leftFootLockedRotation, ref _lastLeftFootAnimPos, ref _smoothLeftFootRot, allowFootLocking);
            }

            if (_rightFootWeight > 0.01f)
            {
                ProcessFootIK(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, _rightFootWeight, ref _rightFootPosition, ref _rightFootRotation, 
                    ref _lastRightFootPos, ref _rightFootOffset, ref _rightFootLocked, ref _rightFootLockedPosition, 
                    ref _rightFootLockedRotation, ref _lastRightFootAnimPos, ref _smoothRightFootRot, allowFootLocking);
            }

 // Prevent leg crossing - only when feet are locked (IK overrides XZ).
            // During animation-driven movement (especially strafes), trust the animation's foot placement.
            if (_leftFootLocked || _rightFootLocked)
                EnforceFootSeparation();

            // Adjust pelvis to lower body when feet need to reach down
            if (adjustPelvis && isGrounded)
            {
                // Scale each foot's offset by its current IK weight so the swing foot
                // (curve weight - 0) doesn't inflate the pelvis dip and push feet underground
                float weightedLeftOffset  = _leftFootOffset  * _leftFootWeight;
                float weightedRightOffset = _rightFootOffset * _rightFootWeight;
                float pelvisInput = Mathf.Max(weightedLeftOffset, weightedRightOffset);
                
                // Reduce pelvis compensation at speed - animation handles stride height,
                // pelvis adjustment is mainly for idle/slow poses on uneven terrain
                float speed = _controller != null ? _controller.CurrentSpeed : 0f;
                float speedRange = pelvisMinAdjustSpeed - pelvisFullAdjustSpeed;
                float speedFactor = speedRange > 0f
                    ? Mathf.Lerp(1f, pelvisMinSpeedMultiplier, Mathf.Clamp01((speed - pelvisFullAdjustSpeed) / speedRange))
                    : 1f;
                
                float rawPelvisOffset = pelvisInput * pelvisAdjustmentAmount * speedFactor;
                float targetPelvisOffset = pelvisInput > 0.01f ? -Mathf.Min(rawPelvisOffset, maxPelvisDip) : 0f;

                // Smooth pelvis movement - use asymmetric speed: slow to dip (filters noise), fast to recover
                float smoothFactor = targetPelvisOffset < _pelvisOffset ? pelvisSmoothSpeed : pelvisSmoothSpeed * 2f;
                _pelvisOffset = Mathf.Lerp(_pelvisOffset, targetPelvisOffset, Time.deltaTime * smoothFactor);

                Vector3 bodyPos = _animator.bodyPosition;
                bodyPos.y += _pelvisOffset;
                _animator.bodyPosition = bodyPos;
                
                if (showDebugInfo && Mathf.Abs(_pelvisOffset) > 0.005f)
                {
                    Debug.Log($"PELVIS - LeftOffset: {_leftFootOffset:F3}, RightOffset: {_rightFootOffset:F3}, PelvisInput: {pelvisInput:F3}, CharSpeed: {speed:F2}, SpeedFactor: {speedFactor:F2}, PelvisOffset: {_pelvisOffset:F3}");
                }
            }
        }

        private void ProcessFootIK(AvatarIKGoal foot, AvatarIKHint kneeHint, float weight, ref Vector3 footPosition, ref Quaternion footRotation, 
            ref Vector3 lastValidPos, ref float footHeightOffset, ref bool footLocked, ref Vector3 lockedPosition, 
            ref Quaternion lockedRotation, ref Vector3 lastAnimPos, ref Quaternion smoothedRotation, bool allowFootLocking)
        {
            // Get the foot's animated position and rotation (before IK is applied)
            Vector3 footAnimPos = _animator.GetIKPosition(foot);
            Quaternion footAnimRot = _animator.GetIKRotation(foot);
            
            // Calculate foot velocity in animation
            float footVelocity = (footAnimPos - lastAnimPos).magnitude / Time.deltaTime;
            lastAnimPos = footAnimPos;
            
            // Raycast origin: from hip height for stability, or from foot bone
            Vector3 rayOrigin;
            float rayLength;
            if (raycastFromHips && _hipTransform != null)
            {
                // Cast from directly above the foot at hip height
                rayOrigin = new Vector3(footAnimPos.x, _hipTransform.position.y, footAnimPos.z);
                rayLength = _hipTransform.position.y - footAnimPos.y + raycastDistance;
            }
            else
            {
                HumanBodyBones footBone = foot == AvatarIKGoal.LeftFoot ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
                Transform footTransform = _animator.GetBoneTransform(footBone);
                rayOrigin = footTransform != null ? footTransform.position : footAnimPos;
                rayOrigin.y += 0.5f;
                rayLength = raycastDistance + 0.5f;
            }

            RaycastHit hit;
            bool hitGround = Physics.Raycast(rayOrigin, Vector3.down, out hit, rayLength, groundLayers, QueryTriggerInteraction.Ignore);

            if (showDebugSpheres)
            {
                // Show animated foot position in yellow
                Debug.DrawLine(footAnimPos + Vector3.up * 0.1f, footAnimPos - Vector3.up * 0.1f, Color.yellow);
                Debug.DrawLine(footAnimPos + Vector3.right * 0.1f, footAnimPos - Vector3.right * 0.1f, Color.yellow);
            }

            // Foot locking logic (only when allowed - not when running fast)
            if (allowFootLocking && hitGround)
            {
                if (!footLocked && footVelocity < lockFootVelocityThreshold)
                {
                    // Lock the foot
                    footLocked = true;
                    lockedPosition = hit.point + Vector3.up * footOffset;
                    lockedRotation = ComputeFootRotation(foot, hit.normal, hit.point);
                    
                    if (showDebugInfo)
                    {
                        string footName = foot == AvatarIKGoal.LeftFoot ? "LEFT" : "RIGHT";
                        Debug.Log($"{footName} LOCKED - Velocity: {footVelocity:F3}");
                    }
                }
                else if (footLocked && footVelocity > lockFootVelocityThreshold * 2f)
                {
                    // Unlock the foot when it starts moving again
                    footLocked = false;
                    
                    if (showDebugInfo)
                    {
                        string footName = foot == AvatarIKGoal.LeftFoot ? "LEFT" : "RIGHT";
                        Debug.Log($"{footName} UNLOCKED - Velocity: {footVelocity:F3}");
                    }
                }
            }
            else
            {
                footLocked = false;
            }

            // Use locked position if foot is locked
            if (footLocked && allowFootLocking)
            {
                footPosition = lockedPosition;
                footRotation = lockedRotation;
                footHeightOffset = footAnimPos.y - lockedPosition.y;
                
                if (showDebugSpheres)
                {
                    Debug.DrawLine(lockedPosition + Vector3.up * 0.2f, lockedPosition - Vector3.up * 0.2f, Color.red);
                    Debug.DrawLine(lockedPosition + Vector3.forward * 0.2f, lockedPosition - Vector3.forward * 0.2f, Color.red);
                }
            }
            else if (hitGround)
            {
                // Calculate how far the ground is from animated position
                float distanceToGround = footAnimPos.y - hit.point.y;
                
                // Apply limits to prevent excessive adjustment
                float clampedDistance = distanceToGround;
                
                if (distanceToGround > 0f) // Ground is below foot
                    clampedDistance = Mathf.Min(distanceToGround, maxFootAdjustmentDown);
                else // Ground is above foot
                    clampedDistance = Mathf.Max(distanceToGround, -maxFootAdjustmentUp);
                
                // CRITICAL: Only adjust Y (height), preserve XZ from animation to avoid lag
                footPosition.x = footAnimPos.x;
                footPosition.z = footAnimPos.z;
                footPosition.y = footAnimPos.y - clampedDistance + footOffset;
                
                // Store offset for pelvis adjustment (positive = foot needs to move down to reach ground)
                footHeightOffset = clampedDistance;

 // Smoothed foot rotation - either two-point sole sampling or single-normal fallback
                Quaternion targetRot = ComputeFootRotation(foot, hit.normal, hit.point);
                if (!_rotationsInitialized)
                {
                    smoothedRotation = targetRot;
                    _rotationsInitialized = true;
                }
                smoothedRotation = Quaternion.Slerp(smoothedRotation, targetRot, Time.deltaTime * footRotationSpeed);
                footRotation = smoothedRotation;

                lastValidPos = footPosition;

                if (showDebugRays)
                {
                    Debug.DrawRay(rayOrigin, Vector3.down * rayLength, Color.green);
                    Debug.DrawRay(hit.point, hit.normal * 0.3f, Color.cyan);
                    Debug.DrawLine(hit.point, footPosition, Color.magenta);
                }
                
                if (showDebugSpheres)
                {
                    Debug.DrawLine(footPosition + Vector3.up * 0.1f, footPosition - Vector3.up * 0.1f, Color.green);
                    Debug.DrawLine(footPosition + Vector3.forward * 0.1f, footPosition - Vector3.forward * 0.1f, Color.green);
                }

                if (showDebugInfo)
                {
                    string footName = foot == AvatarIKGoal.LeftFoot ? "LEFT" : "RIGHT";
                    Debug.Log($"{footName} - AnimY: {footAnimPos.y:F3}, GroundY: {hit.point.y:F3}, Velocity: {footVelocity:F3}, Distance: {distanceToGround:F3}, Clamped: {clampedDistance:F3}, Offset: {footHeightOffset:F3}");
                }
            }
            else
            {
                // No ground found, use animated position
                footPosition = footAnimPos;
                footRotation = footAnimRot;
                footHeightOffset = 0f;
                footLocked = false;

                if (showDebugRays)
                    Debug.DrawRay(rayOrigin, Vector3.down * rayLength, Color.red);
            }

 // Apply foot IK - rotation weight fades separately for smoother blend-out
            float rotWeight = foot == AvatarIKGoal.LeftFoot ? _leftFootRotWeight : _rightFootRotWeight;
            _animator.SetIKPositionWeight(foot, weight);
            _animator.SetIKRotationWeight(foot, rotWeight);
            _animator.SetIKPosition(foot, footPosition);
            _animator.SetIKRotation(foot, footRotation);

            // Knee hint to prevent popping (fade out at speed so sprint animation drives knees)
            if (enableKneeHints)
            {
                float speed = _controller != null ? _controller.CurrentSpeed : 0f;
                float speedRange = kneeHintZeroWeightSpeed - kneeHintFullWeightSpeed;
                float kneeWeight = speedRange > 0f
                    ? weight * Mathf.Clamp01(1f - (speed - kneeHintFullWeightSpeed) / speedRange)
                    : (speed <= kneeHintFullWeightSpeed ? weight : 0f);

                HumanBodyBones kneeBone = foot == AvatarIKGoal.LeftFoot ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg;
                Transform kneeTransform = _animator.GetBoneTransform(kneeBone);
                if (kneeTransform != null && kneeWeight > 0.01f)
                {
                    Vector3 offset = foot == AvatarIKGoal.LeftFoot ? leftKneeHintOffset : rightKneeHintOffset;
                    Vector3 kneeHintPos = kneeTransform.position
                        + transform.right   * offset.x
                        + Vector3.up        * offset.y
                        + transform.forward * offset.z;
                    _animator.SetIKHintPositionWeight(kneeHint, kneeWeight);
                    _animator.SetIKHintPosition(kneeHint, kneeHintPos);

                    if (showDebugSpheres)
                    {
                        Debug.DrawLine(kneeHintPos + Vector3.up * 0.05f, kneeHintPos - Vector3.up * 0.05f, Color.yellow);
                        Debug.DrawLine(kneeHintPos + Vector3.right * 0.05f, kneeHintPos - Vector3.right * 0.05f, Color.yellow);
                    }
                }
            }
        }

        private void ProcessHandIK(AvatarIKGoal hand, AvatarIKHint elbowHint,
            bool active, Vector3 targetPos, Quaternion targetRot,
            float targetWeight, float targetRotationWeight, ref float currentWeight,
            ref Vector3 smoothedTargetPos, ref Quaternion smoothedTargetRot, Vector3 elbowOffset)
        {
            float goal = active ? targetWeight : 0f;
            currentWeight = Mathf.Lerp(currentWeight, goal, Time.deltaTime * handIKTransitionSpeed);

            if (active)
            {
                smoothedTargetPos = Vector3.Lerp(
                    smoothedTargetPos,
                    targetPos,
                    Time.deltaTime * handIKTargetPositionSmoothSpeed);
                smoothedTargetRot = Quaternion.Slerp(
                    smoothedTargetRot,
                    targetRot,
                    Time.deltaTime * handIKTargetRotationSmoothSpeed);
            }

            if (currentWeight < 0.01f)
            {
                _animator.SetIKPositionWeight(hand, 0f);
                _animator.SetIKRotationWeight(hand, 0f);
                _animator.SetIKHintPositionWeight(elbowHint, 0f);
                return;
            }

            _animator.SetIKPositionWeight(hand, currentWeight);
            _animator.SetIKRotationWeight(hand, currentWeight * Mathf.Clamp01(targetRotationWeight));
            _animator.SetIKPosition(hand, smoothedTargetPos);
            _animator.SetIKRotation(hand, smoothedTargetRot);

            // Elbow hint
            HumanBodyBones elbowBone = hand == AvatarIKGoal.LeftHand
                ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
            Transform elbowTransform = _animator.GetBoneTransform(elbowBone);
            if (elbowTransform != null)
            {
                Vector3 elbowHintPos = elbowTransform.position
                    + transform.right   * elbowOffset.x
                    + Vector3.up        * elbowOffset.y
                    + transform.forward * elbowOffset.z;
                _animator.SetIKHintPositionWeight(elbowHint, currentWeight);
                _animator.SetIKHintPosition(elbowHint, elbowHintPos);

                if (showDebugSpheres)
                {
                    Debug.DrawLine(elbowHintPos + Vector3.up * 0.05f, elbowHintPos - Vector3.up * 0.05f, Color.blue);
                    Debug.DrawLine(elbowHintPos + Vector3.right * 0.05f, elbowHintPos - Vector3.right * 0.05f, Color.blue);
                }
            }

            if (showDebugSpheres)
            {
                Debug.DrawLine(smoothedTargetPos + Vector3.up * 0.05f, smoothedTargetPos - Vector3.up * 0.05f, Color.white);
                Debug.DrawLine(smoothedTargetPos + Vector3.right * 0.05f, smoothedTargetPos - Vector3.right * 0.05f, Color.white);
            }
        }

        /// <summary>
        /// Activate hand IK for grabbing/interaction. Call each frame or once to set.
        /// Weight blends in smoothly via handIKTransitionSpeed.
        /// </summary>
        public void SetHandIKTarget(bool leftHand, Vector3 position, Quaternion rotation, float weight = 1f, float rotationWeight = 1f)
        {
            if (leftHand)
            {
                if (!_leftHandActive)
                {
                    _leftHandSmoothedTargetPos = position;
                    _leftHandSmoothedTargetRot = rotation;
                }
                _leftHandActive = true;
                _leftHandTargetPos = position;
                _leftHandTargetRot = rotation;
                _leftHandTargetWeight = Mathf.Clamp01(weight);
                _leftHandTargetRotationWeight = Mathf.Clamp01(rotationWeight);
            }
            else
            {
                if (!_rightHandActive)
                {
                    _rightHandSmoothedTargetPos = position;
                    _rightHandSmoothedTargetRot = rotation;
                }
                _rightHandActive = true;
                _rightHandTargetPos = position;
                _rightHandTargetRot = rotation;
                _rightHandTargetWeight = Mathf.Clamp01(weight);
                _rightHandTargetRotationWeight = Mathf.Clamp01(rotationWeight);
            }
        }

        /// <summary>
 /// Deactivate hand IK - weight blends out smoothly.
        /// </summary>
        public void ClearHandIKTarget(bool leftHand)
        {
            if (leftHand)
            {
                _leftHandActive = false;
                _leftHandTargetRotationWeight = 1f;
            }
            else
            {
                _rightHandActive = false;
                _rightHandTargetRotationWeight = 1f;
            }
        }

        /// <summary>
        /// Returns the current blended hand IK weight (0 when inactive).
        /// </summary>
        public float GetHandIKWeight(bool leftHand)
        {
            return leftHand ? _leftHandCurrentWeight : _rightHandCurrentWeight;
        }

        // Public method to force disable IK (useful for custom animations)
        public void SetIKEnabled(bool enabled)
        {
            enableFootIK = enabled;
        }

        public void SetLookIKEnabled(bool enabled)
        {
            enableLookIK = enabled;
        }

        // Public method to get current IK weight (useful for blending)
        public float GetFootIKWeight(bool leftFoot)
        {
            return leftFoot ? _leftFootWeight : _rightFootWeight;
        }

        private void ProcessLookIK()
        {
            if (!enableLookIK || _controller == null || _controller.CamTransform == null)
            {
                _currentLookWeight = Mathf.Lerp(_currentLookWeight, 0f, Time.deltaTime * lookTransitionSpeed);
                _animator.SetLookAtWeight(_currentLookWeight);
                return;
            }

            Transform camTransform = _controller.CamTransform;

            // Calculate raw look target from camera
            Vector3 rawTarget = camTransform.position + camTransform.forward * lookTargetDistance;

            // Smooth the look target to avoid jerky head movement
            _smoothLookTarget = Vector3.Lerp(_smoothLookTarget, rawTarget, Time.deltaTime * lookTransitionSpeed * 2f);

            // Calculate angle between body forward and look direction
            Vector3 headPos = _animator.GetBoneTransform(HumanBodyBones.Head) != null
                ? _animator.GetBoneTransform(HumanBodyBones.Head).position
                : transform.position + Vector3.up * 1.5f;
            Vector3 lookDir = (_smoothLookTarget - headPos).normalized;
            Vector3 bodyForwardXZ = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            Vector3 lookDirXZ = new Vector3(lookDir.x, 0f, lookDir.z).normalized;
            float lookAngle = Vector3.Angle(bodyForwardXZ, lookDirXZ);

 // Fade weight based on angle - full weight within limit, fades to zero past max
            float angleFactor = lookAngle <= lookMaxAngle ? 1f : Mathf.Clamp01(1f - (lookAngle - lookMaxAngle) / 30f);

            // Determine if look IK should be active
            bool isSpecialState = _state != null && _state.InSpecialLocomotionState();
            float targetLookWeight = (!isSpecialState) ? lookWeight * angleFactor : 0f;

            _currentLookWeight = Mathf.Lerp(_currentLookWeight, targetLookWeight, Time.deltaTime * lookTransitionSpeed);

            // Determine effective body weight
            float effectiveBodyWeight = lookBodyWeight;
            if (reduceBodyWeightWhenIdle && _state != null && _state.CurrentMovementState == MovementState.Idle)
            {
                bool isThirdPerson = LocomotionInputManager.Instance != null && LocomotionInputManager.Instance.IsThirdPerson;
                if (isThirdPerson)
                    effectiveBodyWeight = 0f;
            }

            _animator.SetLookAtWeight(_currentLookWeight, effectiveBodyWeight, lookHeadWeight, lookEyesWeight, lookClampWeight);
            _animator.SetLookAtPosition(_smoothLookTarget);

            if (showDebugRays && _currentLookWeight > 0.01f)
            {
                Debug.DrawLine(headPos, _smoothLookTarget, Color.yellow);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_animator == null) return;

            if (enableKneeHints)
            {
                DrawHintGizmo(HumanBodyBones.LeftLowerLeg, leftKneeHintOffset, Color.cyan);
                DrawHintGizmo(HumanBodyBones.RightLowerLeg, rightKneeHintOffset, Color.magenta);
            }

            // Elbow hints (show when hands are active or always in editor for tuning)
            DrawHintGizmo(HumanBodyBones.LeftLowerArm, leftElbowHintOffset, new Color(0.3f, 0.6f, 1f));
            DrawHintGizmo(HumanBodyBones.RightLowerArm, rightElbowHintOffset, new Color(1f, 0.4f, 0.7f));
        }

        private void DrawHintGizmo(HumanBodyBones bone, Vector3 offset, Color color)
        {
            Transform boneTransform = _animator.GetBoneTransform(bone);
            if (boneTransform == null) return;

            Vector3 hintPos = boneTransform.position
                + transform.right   * offset.x
                + Vector3.up        * offset.y
                + transform.forward * offset.z;

            Gizmos.color = color;
            Gizmos.DrawWireSphere(hintPos, 0.04f);
            Gizmos.DrawLine(boneTransform.position, hintPos);
        }
#endif

        /// <summary>
        /// Computes foot rotation from ground. Uses two-point heel+ball raycasts when
        /// enableFootTilt is on and bones are available, otherwise single-normal projection.
        /// </summary>
        private Quaternion ComputeFootRotation(AvatarIKGoal foot, Vector3 fallbackNormal, Vector3 fallbackHitPoint)
        {
            if (enableFootTilt)
            {
                Transform heelBone = foot == AvatarIKGoal.LeftFoot ? _leftFootBone : _rightFootBone;
                Transform toeBone  = foot == AvatarIKGoal.LeftFoot ? _leftToesBone : _rightToesBone;

                if (heelBone != null && toeBone != null)
                {
                    float soleRayLen = raycastDistance + 0.3f;
                    Vector3 heelOrigin = heelBone.position + Vector3.up * 0.2f;
                    Vector3 toeOrigin  = toeBone.position  + Vector3.up * 0.2f;

                    bool heelHit = Physics.Raycast(heelOrigin, Vector3.down, out RaycastHit hHit, soleRayLen, groundLayers, QueryTriggerInteraction.Ignore);
                    bool toeHit  = Physics.Raycast(toeOrigin,  Vector3.down, out RaycastHit tHit, soleRayLen, groundLayers, QueryTriggerInteraction.Ignore);

                    if (heelHit && toeHit)
                    {
                        Vector3 soleForward = (tHit.point - hHit.point).normalized;
                        Vector3 soleUp = ((hHit.normal + tHit.normal) * 0.5f).normalized;
                        soleForward = Vector3.ProjectOnPlane(soleForward, soleUp).normalized;

                        if (soleForward.sqrMagnitude > 0.001f)
                        {
                            if (showDebugRays)
                            {
                                Debug.DrawLine(hHit.point, tHit.point, Color.blue);
                                Debug.DrawRay(hHit.point, hHit.normal * 0.15f, new Color(0f, 0.5f, 1f));
                                Debug.DrawRay(tHit.point, tHit.normal * 0.15f, new Color(0f, 0.5f, 1f));
                            }
                            return Quaternion.LookRotation(soleForward, soleUp);
                        }
                    }
                    else if (heelHit)
                    {
                        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, hHit.normal).normalized;
                        if (fwd.sqrMagnitude > 0.001f)
                            return Quaternion.LookRotation(fwd, hHit.normal);
                    }
                    else if (toeHit)
                    {
                        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, tHit.normal).normalized;
                        if (fwd.sqrMagnitude > 0.001f)
                            return Quaternion.LookRotation(fwd, tHit.normal);
                    }
                }
            }

            // Fallback: single normal
            Vector3 footForward = Vector3.ProjectOnPlane(transform.forward, fallbackNormal).normalized;
            return Quaternion.LookRotation(footForward, fallbackNormal);
        }

        private void EnforceFootSeparation()
        {
            if (minFootSeparation <= 0f) return;

            // Project both feet to local XZ (relative to character) to check lateral separation
            Vector3 localLeft = transform.InverseTransformPoint(_leftFootPosition);
            Vector3 localRight = transform.InverseTransformPoint(_rightFootPosition);

 // Check lateral (X) separation - left foot should be on the left, right on the right
            float separation = localLeft.x - localRight.x;

            // If feet have crossed (left foot is to the right of right foot, or too close)
            if (separation > -minFootSeparation)
            {
                float correction = (-minFootSeparation - separation) * 0.5f;
                localLeft.x += correction;
                localRight.x -= correction;

                _leftFootPosition = transform.TransformPoint(localLeft);
                _rightFootPosition = transform.TransformPoint(localRight);

                // Re-apply corrected positions
                _animator.SetIKPosition(AvatarIKGoal.LeftFoot, _leftFootPosition);
                _animator.SetIKPosition(AvatarIKGoal.RightFoot, _rightFootPosition);

                // Release locks since positions were corrected
                _leftFootLocked = false;
                _rightFootLocked = false;
            }
        }
    }
}
