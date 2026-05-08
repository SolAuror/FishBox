using System.Collections.Generic;
using UnityEngine;
using Sol.Fishing;
using Sol.Combat;
using Sol.Actions;

namespace Sol.Locomotion
{
    public class LocomotionAnimation : MonoBehaviour
    {
#region Class Variables
        [Tooltip("Inspector: tunes animator.")]
        [SerializeField] private Animator _animator;
        [Tooltip("Inspector: tunes locomotion blend speed.")]
        [SerializeField] private float locomotionBlendSpeed = 8f;
        private LocomotionState _state;
        private LocomotionInput _locomotionInput;
        private LocomotionController _controller;
        private FishingState _fishingState;
        private Sol.AI.AI_NPC _aiNpc;
        private BasicMeleeAttack _meleeAttack;

        //Locomotion Hashes
        private static int inputXHash = Animator.StringToHash("inputX");
        private static int inputYHash = Animator.StringToHash("inputY");
        private static int inputMagnitudeHash = Animator.StringToHash("inputMagnitude");
        private static int isIdleHash = Animator.StringToHash("isIdle");
        private static int isGroundedHash = Animator.StringToHash("isGrounded");
        private static int isFallingHash = Animator.StringToHash("isFalling");
        private static int isJumpingHash = Animator.StringToHash("isJumping");
        private static int isCrouchingHash = Animator.StringToHash("isCrouching");
        private static int isSwimmingHash = Animator.StringToHash("isSwimming");
        private static int isFlyingHash = Animator.StringToHash("isFlying");
        private static int landingImpactHash = Animator.StringToHash("landingImpact");
        private static int speedHash = Animator.StringToHash("speed");
        private static int swimSpeedHash = Animator.StringToHash("swimSpeed");

        //Action Hashes
        private static int attackTriggerHash = Animator.StringToHash("isAttacking");
        private static int isAimingHash = Animator.StringToHash("isAiming");
        private static int interactTriggerHash = Animator.StringToHash("isInteracting");
        private static int isPlayingActionHash = Animator.StringToHash("isPlayingAction");
        private static int interactionActiveHash = Animator.StringToHash("interactionActive");
        private static int interactionTypeHash = Animator.StringToHash("interactionType");
        private const string ActionStateTag = "Action";
        
        //Camera Rotation Hashes
        private static int isRotatingToTargetHash = Animator.StringToHash("isRotatingToTarget");
        private static int rotationMismatchHash = Animator.StringToHash("rotationMismatch");

        //AI Hashes (NPC-only, optional on controllers)
        private static int aiStateHash = Animator.StringToHash("aiState");
        private static int isClimbingHash = Animator.StringToHash("isClimbing");
        private static int swimVerticalHash = Animator.StringToHash("SwimVertical");
        private static int swimHorizontalHash = Animator.StringToHash("SwimHorizontal");

        private Vector3 _currentBlendInput = Vector3.zero;
        private HashSet<int> _validParams;
        private int _upperBodyLayerIndex = -1;
        public bool IsUpperBodyActionActive => IsCurrentUpperBodyActionState();
        public bool IsUpperBodyActionPlaying => IsUpperBodyActionPlayingInternal();

        //Blend Tree state values
        private float _crouchMaxBlendValue = 0.5f;
        private float _walkMaxBlendValue = 0.75f;
        private float _runMaxBlendValue = 1f;
        private float _sprintMaxBlendValue = 1.5f;
        #endregion

#region Initialize
        private void Awake()
        {
            _locomotionInput = GetComponent<LocomotionInput>();
            _state = GetComponent<LocomotionState>();
            _controller = GetComponent<LocomotionController>();
            _fishingState = GetComponent<FishingState>();
            _aiNpc = GetComponent<Sol.AI.AI_NPC>();
            _meleeAttack = GetComponent<BasicMeleeAttack>();

            // Cache which parameters actually exist in the Animator Controller
            _validParams = new HashSet<int>();
            foreach (var p in _animator.parameters)
                _validParams.Add(p.nameHash);

            _upperBodyLayerIndex = _animator.GetLayerIndex("Upper Body");
        }

        private void Update()
        {
            UpdateAnimationState();
        }
#endregion

#region Update

        private void UpdateAnimationState()
        {
            if (_fishingState == null)
                _fishingState = GetComponent<FishingState>();

            bool isIdle = _state.CurrentMovementState == MovementState.Idle;
            bool isWalking = _state.CurrentMovementState == MovementState.Walking;
            bool isCrouching = _state.CurrentMovementState == MovementState.Crouching;
            bool isRunning = _state.CurrentMovementState == MovementState.Running;
            bool isSprinting = _state.CurrentMovementState == MovementState.Sprinting;
            bool isJumping = _state.CurrentMovementState == MovementState.Jumping;
            bool isFalling = _state.CurrentMovementState == MovementState.Falling;
            bool isSwimming = _state.CurrentMovementState == MovementState.Swimming;
            bool isFlying = _state.CurrentMovementState == MovementState.Flying;
            bool isGrounded = _state.InGroundedState();
            bool interactionActive = IsInteractionAnimationActive();
            bool isPlayingAction = IsUpperBodyActionPlayingInternal() || interactionActive;

            bool isRunBlendValue = isRunning || isJumping || isFalling;

            Vector2 inputTarget = isSprinting ? _locomotionInput.MovementInput * _sprintMaxBlendValue :
                                  isCrouching ? _locomotionInput.MovementInput * _crouchMaxBlendValue :  
                                  isRunBlendValue ? _locomotionInput.MovementInput * _runMaxBlendValue : 
                                                    _locomotionInput.MovementInput * _walkMaxBlendValue;


            _currentBlendInput = Vector3.Lerp(_currentBlendInput, inputTarget, locomotionBlendSpeed * Time.deltaTime);

            //movement
            _animator.SetBool(isGroundedHash, isGrounded);
            _animator.SetBool(isIdleHash, isIdle);
            _animator.SetBool(isJumpingHash, isJumping);
            _animator.SetBool(isFallingHash, isFalling);
            _animator.SetBool(isCrouchingHash, isCrouching);
            if (_validParams.Contains(isSwimmingHash)) _animator.SetBool(isSwimmingHash, isSwimming);
            if (_validParams.Contains(isFlyingHash)) _animator.SetBool(isFlyingHash, isFlying);
            _animator.SetFloat(inputXHash, _currentBlendInput.x);
            _animator.SetFloat(inputYHash, _currentBlendInput.y);
            _animator.SetFloat(inputMagnitudeHash, _currentBlendInput.magnitude);
            if (_validParams.Contains(landingImpactHash)) _animator.SetFloat(landingImpactHash, _controller.LastLandingImpact);
            if (_validParams.Contains(speedHash)) _animator.SetFloat(speedHash, _controller.CurrentSpeed);
            if (_validParams.Contains(swimSpeedHash))
            {
                float lateralSpeed = _controller.CurrentSpeed;
                float maxSwim = _controller.swimSpeed * (_controller.SwimSpeedMultiplier);
                float swimBlend = maxSwim > 0.01f ? Mathf.Clamp01(lateralSpeed / maxSwim) : 0f;
                _animator.SetFloat(swimSpeedHash, swimBlend);
            }

            //actions
            _animator.SetBool(isAimingHash, _locomotionInput.AimPressed);

            bool shouldBlockDefaultAttack = _fishingState != null && _fishingState.ShouldBlockDefaultAttack;
            if (!shouldBlockDefaultAttack && _locomotionInput.AttackPressed && _validParams.Contains(attackTriggerHash))
            {
                if (_meleeAttack == null || _meleeAttack.CanStartAttack())
                {
                    _animator.SetTrigger(attackTriggerHash);
                    if (_meleeAttack != null)
                    {
                        bool dispatched = ActionSystem.Instance != null
                            && ActionSystem.Instance.Dispatch(new MeleeAttackAction(), gameObject);
                        if (!dispatched)
                            _meleeAttack.BeginAttack();
                    }
                }
                _locomotionInput.SetAttackPressedFalse();
            }

            _animator.SetBool(isPlayingActionHash, isPlayingAction);
            
            //camera rotation
            _animator.SetBool(isRotatingToTargetHash, _controller.IsRotatingToTarget);
            // Only pass a non-zero mismatch when a turn is actually in progress - passing the
            // raw value at all times lets the animator blend tree partially activate turn-in-place
            // animations during the idle wait period, producing the leg jitter.
            _animator.SetFloat(rotationMismatchHash, _controller.IsRotatingToTarget ? _controller.rotationMismatch : 0f);

            UpdateAiSpecificAnimationChannels(isSwimming);
        }

        private bool IsInteractionAnimationActive()
        {
            bool interactionActive = _validParams.Contains(interactionActiveHash)
                && _animator.GetBool(interactionActiveHash);
            if (interactionActive)
                return true;

            // Compatibility fallback: some animator variants may omit interactionActive,
            // but still expose interactionType routing. Non-zero means an interaction is active.
            return _validParams.Contains(interactionTypeHash)
                && _animator.GetInteger(interactionTypeHash) != 0;
        }

        private void UpdateAiSpecificAnimationChannels(bool isSwimming)
        {
            if (_aiNpc == null)
                return;

            if (_validParams.Contains(isClimbingHash))
                _animator.SetBool(isClimbingHash, _state.CurrentMovementState == MovementState.Climbing);

            if (_validParams.Contains(swimVerticalHash))
                _animator.SetFloat(swimVerticalHash, isSwimming ? _locomotionInput.MovementInput.y : 0f);

            if (_validParams.Contains(swimHorizontalHash))
                _animator.SetFloat(swimHorizontalHash, isSwimming ? _locomotionInput.MovementInput.x : 0f);

            if (_validParams.Contains(aiStateHash))
                _animator.SetInteger(aiStateHash, (int)_aiNpc.CurrentState);
        }

        private bool IsUpperBodyActionPlayingInternal()
        {
            if (_upperBodyLayerIndex < 0) return false;

            if (IsCurrentUpperBodyActionState())
                return true;

            if (_animator.IsInTransition(_upperBodyLayerIndex))
            {
                AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(_upperBodyLayerIndex);
                if (next.fullPathHash != 0 && next.IsTag(ActionStateTag))
                    return true;
            }

            return false;
        }

        private bool IsCurrentUpperBodyActionState()
        {
            if (_upperBodyLayerIndex < 0) return false;
            AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(_upperBodyLayerIndex);
            return current.IsTag(ActionStateTag);
        }

        public void TriggerPickupAnimation()
        {
            if (!_validParams.Contains(interactTriggerHash)) return;
            _animator.SetTrigger(interactTriggerHash);
        }

        // Animation event receiver retained so existing clips don't break if the event is still assigned.
        public void OnPickupContext()
        {
        }

        public void OnPickupContact()
        {
            OnPickupContext();
        }

        // Shared punch animation event hook.
        public void OnMeleeHitFrame()
        {
            _meleeAttack?.ResolveHitFrame();
        }

        // Compatibility aliases in case clips already reference alternative names.
        public void OnAttackHit() => OnMeleeHitFrame();
        public void OnPunchHit() => OnMeleeHitFrame();
#endregion
    }
}
