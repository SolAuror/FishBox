using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Splines;
using Sol.Locomotion;
using Sol.Grab;
using Sol.Player;

namespace Sol.AI
{
    public partial class AI_NPC : MonoBehaviour, Shared.AI.ILocomotionIntentProvider
    {
        #region Inspector Settings
        [Header("Configuration")]
        [Tooltip("Inspector: tunes config.")]
        [SerializeField] private AIConfig config;

        [Header("References")]
        [Tooltip("Inspector: tunes soul.")]
        [SerializeField] private NPCSoul soul;
        [SerializeField] private Animator animator;
        [Tooltip("Inspector: tunes fake cam.")]
        [SerializeField] private Transform fakeCam;

        [Header("Death")]
        [SerializeField] private string deathTrigger = "Death";
        [Tooltip("Optional full animator state path to force on death, e.g. 'Base Layer.Death_1'.")]
        [SerializeField] private string deathStateName = "Base Layer.Death_1";
        [Tooltip("How long (seconds) the death animation plays before the Animator is frozen. Set this to match your death clip length.")]
        [SerializeField] private float deathAnimDuration = 2f;
        [Tooltip("Normalized time within the death state to hold on. Use this to pin the corpse pose before the controller can transition out.")]
        [Range(0.5f, 1f)]
        [SerializeField] private float deathHoldNormalizedTime = 0.82f;
        [Tooltip("If child ragdoll rigidbodies are already authored on the prefab, switch to ragdoll after the death pose is reached.")]
        [SerializeField] private bool ragdollAfterDeath = false;
        #endregion

        public event Action<State, State> OnStateChanged;

        public AIConfig Config => config;
        public NavMeshAgent Agent => agent;
        public NPCSoul Soul => soul;
        public Animator Animator => animator;
        public State CurrentState { get; private set; } = State.Idle;
        public State PreviousState { get; private set; } = State.Idle;

        /// <summary>Read-only context populated once per Update before Tick().</summary>
        public IntentContext Context { get; private set; }

        private LocomotionInput locoInput;
        private LocomotionController locoController;
        private LocomotionState locoState;
        private LocomotionAnimation locoAnim;
        private CharacterController characterController;
        private NavMeshAgent agent;
        private Transform playerTarget;
        private float nextPlayerResolveTime;
        private readonly Dictionary<State, AIStateBase> states = new();
        private AIStateBase activeState;

        private Coroutine deathFreezeRoutine;
        private Rigidbody[] ragdollBodies = Array.Empty<Rigidbody>();
        private Collider[] ragdollColliders = Array.Empty<Collider>();
        private bool hasRagdoll;
        private GrabbableComponent corpseGrabbable;

        private void Awake()
        {
            if (!ValidateSetup())
            {
                enabled = false;
                return;
            }
            locoInput      = GetComponent<LocomotionInput>();
            locoController = GetComponent<LocomotionController>();
            locoState      = GetComponent<LocomotionState>();
            locoAnim       = GetComponent<LocomotionAnimation>();
            characterController = GetComponent<CharacterController>();
            agent          = GetComponent<NavMeshAgent>();
            if (soul == null) soul = GetComponent<NPCSoul>();
            if (animator == null) animator = GetComponent<Animator>();
            if (soul != null) soul.OnDeath += HandleDeath;
            CacheRagdollParts();
            if (hasRagdoll)
            {
                ragdollAfterDeath = true;
                SetRagdollEnabled(false);
            }
            InitInventory();
            InitLocomotion();
            InitNavAgent();
            InitStateMachine();
        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
        }

        private void OnDestroy()
        {
            if (soul != null) soul.OnDeath -= HandleDeath;
        }

        private void Update()
        {
            if (CurrentState == State.Dead)
            {
                // Dead: skip perception, fakeCam, look updates. Only sync animation.
                SyncAnimationWithLocomotion();
                return;
            }

            bool canUseNavAgent = agent != null && agent.enabled && agent.isOnNavMesh;
            if (!canUseNavAgent && agent != null && agent.enabled)
                canUseNavAgent = EnsureAgentOnNavMesh(logIfUnavailable: false);

            if (canUseNavAgent)
            {
                // Safety: warp agent if it drifts too far from the transform.
                // Compare BEFORE syncing nextPosition so the delta is meaningful.
                if ((agent.transform.position - transform.position).sqrMagnitude > AgentWarpThreshold * AgentWarpThreshold)
                    agent.Warp(transform.position);

                agent.nextPosition = transform.position;
            }

            // Build per-frame context once so states don't recalculate.
            Context = new IntentContext(soul);
            ResolvePlayerTarget();

            if (canUseNavAgent)
                agent.speed = GetCurrentTargetSpeed();
            UpdateFakeCam();

            // Detect a failed path and notify listeners so external systems can react.
            // Only check when the agent is actively trying to reach something.
            if (canUseNavAgent && agent.hasPath && agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid)
                agent.ResetPath();

            State desired = activeState != null ? activeState.Tick() : State.Idle;
            if (desired != CurrentState)
                SetState(desired);
            SyncAnimationWithLocomotion();
        }

        private bool ValidateSetup()
        {
            bool valid = true;
            if (config == null)
                Debug.LogWarning($"[AI_NPC] '{name}' has no AIConfig assigned.", this);
            if (GetComponent<CharacterController>() == null)
            {
                Debug.LogError($"[AI_NPC] '{name}' is missing CharacterController.", this);
                valid = false;
            }
            if (GetComponent<NavMeshAgent>() == null)
            {
                Debug.LogError($"[AI_NPC] '{name}' is missing NavMeshAgent.", this);
                valid = false;
            }
            return valid;
        }

        public bool CanChasePlayer()
        {
            return config != null
                && config.autoChasePlayer
                && config.chaseRadius > 0f
                && TryGetPlayerPosition(out Vector3 playerPosition)
                && (playerPosition - transform.position).sqrMagnitude <= config.chaseRadius * config.chaseRadius;
        }

        public bool TryGetPlayerPosition(out Vector3 playerPosition)
        {
            ResolvePlayerTarget();
            if (playerTarget == null)
            {
                playerPosition = default;
                return false;
            }

            playerPosition = playerTarget.position;
            return true;
        }

        private void ResolvePlayerTarget()
        {
            if (playerTarget != null && playerTarget.gameObject.activeInHierarchy)
            {
                if (Time.time < nextPlayerResolveTime)
                    return;
            }

            if (Time.time < nextPlayerResolveTime && playerTarget != null)
                return;

            PlayerSoul playerSoul = FindFirstObjectByType<PlayerSoul>();
            if (playerSoul != null)
            {
                playerTarget = playerSoul.transform;
                nextPlayerResolveTime = Time.time + 1f;
                return;
            }

            try
            {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
                playerTarget = taggedPlayer != null ? taggedPlayer.transform : null;
            }
            catch (UnityException)
            {
                playerTarget = null;
            }

            nextPlayerResolveTime = Time.time + 1f;
        }

        private void InitStateMachine()
        {
            states[State.Idle]     = new AIState_Idle(this);
            states[State.Patrol]   = new AIState_Patrol(this);
            states[State.Chase]    = new AIState_Chase(this);
            states[State.Dead]     = new AIState_Dead(this);
            activeState = states[State.Idle];
            activeState.Enter();
        }

        public void RegisterState(State key, AIStateBase handler)
        {
            states[key] = handler;
            if (CurrentState == key)
            {
                activeState.Exit();
                activeState = handler;
                activeState.Enter();
            }
        }

        public T GetState<T>(State key) where T : AIStateBase
        {
            return states.TryGetValue(key, out AIStateBase s) ? s as T : null;
        }

        /// <summary>Returns a priority value for state preemption checks. Higher = more important.</summary>
        private static int GetPriority(State s)
        {
            return s switch
            {
                State.Dead           => 100,
                State.Chase          => 30,
                State.Patrol         => 20,
                State.Idle           => 10,
                _                    => 0
            };
        }

        /// <summary>Transition with priority check. Same-or-higher priority required (Dead always wins).
        /// Use for external state requests. Tick-initiated transitions use SetState directly.</summary>
        public void RequestStateChange(State newState)
        {
            if (newState != State.Dead && GetPriority(newState) < GetPriority(CurrentState))
                return;
            SetState(newState);
        }

        private void SetState(State newState)
        {
            if (!states.TryGetValue(newState, out AIStateBase next))
            {
                Debug.LogWarning($"[AI_NPC] No handler registered for state '{newState}'.", this);
                return;
            }
            State old = CurrentState;
            activeState.Exit();
            PreviousState = old;
            CurrentState = newState;
            activeState = next;
            activeState.Enter();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (old != newState)
                Debug.Log($"[AI] {name}: {old} \u2192 {newState}");
#endif
            OnStateChanged?.Invoke(old, newState);
        }

        private void HandleDeath()
        {
            if (CurrentState == State.Dead && deathFreezeRoutine != null)
                return;

            SetState(State.Dead);
            ShutdownSystemsOnDeath();
            EnsureCorpseGrabbable();

            // Freeze the Animator after the death animation finishes.
            // The Animator controller has an exit transition at ~87.5% that returns to the
            // blend tree - disabling the Animator prevents this and holds the final dead pose.
            if (animator != null)
            {
                PrepareAnimatorForDeath();
                animator.speed = 1f;
                animator.SetTrigger(deathTrigger);

                if (!string.IsNullOrWhiteSpace(deathStateName))
                    animator.CrossFadeInFixedTime(deathStateName, 0.05f, 0, 0f);
            }

            if (deathFreezeRoutine != null)
                StopCoroutine(deathFreezeRoutine);

            deathFreezeRoutine = StartCoroutine(FreezeAnimatorAfterDeath());
        }

        private void ShutdownSystemsOnDeath()
        {
            if (locoInput != null)
            {
                locoInput.IsControlledByPlayer = false;
                locoInput.enabled = false;
            }

            if (locoController != null)
                locoController.enabled = false;

            if (locoState != null)
                locoState.enabled = false;

            if (locoAnim != null)
                locoAnim.enabled = false;

            if (agent != null)
                agent.enabled = false;

            SetRagdollEnabled(false);
            SetChildCollidersEnabled(false);
            playerTarget = null;

            if (fakeCam != null)
                fakeCam.gameObject.SetActive(false);
        }

        private IEnumerator FreezeAnimatorAfterDeath()
        {
            if (animator == null)
            {
                deathFreezeRoutine = null;
                yield break;
            }

            yield return new WaitForSeconds(Mathf.Max(0.01f, deathAnimDuration));

            int deathStateHash = string.IsNullOrWhiteSpace(deathStateName)
                ? 0
                : Animator.StringToHash(deathStateName);

            if (animator != null)
            {
                if (deathStateHash != 0)
                {
                    animator.Play(deathStateHash, 0, deathHoldNormalizedTime);
                    animator.Update(0f);
                }

                animator.speed = 0f;
            }

            if (ragdollAfterDeath && hasRagdoll)
                ActivateRagdollFromDeathPose();

            deathFreezeRoutine = null;
        }

        private void CacheRagdollParts()
        {
            List<Rigidbody> bodyList = new();
            List<Collider> colliderList = new();

            Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody body = bodies[i];
                if (body == null || body.gameObject == gameObject)
                    continue;

                bodyList.Add(body);
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || collider.gameObject == gameObject)
                    continue;

                colliderList.Add(collider);
            }

            ragdollBodies = bodyList.ToArray();
            ragdollColliders = colliderList.ToArray();
            hasRagdoll = ragdollBodies.Length > 0;
        }

        private void SetChildCollidersEnabled(bool enabled)
        {
            for (int i = 0; i < ragdollColliders.Length; i++)
                ragdollColliders[i].enabled = enabled;
        }

        private void SetRagdollEnabled(bool enabled)
        {
            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                Rigidbody body = ragdollBodies[i];
                body.isKinematic = !enabled;
                body.useGravity = enabled;

                if (enabled)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.WakeUp();
                }
                else
                {
                    body.Sleep();
                }
            }

            SetChildCollidersEnabled(enabled);
        }

        private void ActivateRagdollFromDeathPose()
        {
            if (characterController != null)
                characterController.enabled = false;

            if (animator != null)
                animator.enabled = false;

            SetRagdollEnabled(true);
        }

        private void EnsureCorpseGrabbable()
        {
            if (corpseGrabbable != null)
                return;

            Transform anchor = ResolveCorpseGrabAnchor();
            corpseGrabbable = anchor.GetComponent<GrabbableComponent>();
            if (corpseGrabbable == null)
            {
                corpseGrabbable = anchor.gameObject.AddComponent<GrabbableComponent>();
                corpseGrabbable.holdDistance = 2.2f;
                corpseGrabbable.followSpeed = 12f;
            }
        }

        private Transform ResolveCorpseGrabAnchor()
        {
            if (!hasRagdoll || ragdollBodies.Length == 0)
                return transform;

            HashSet<Rigidbody> ragdollBodySet = new(ragdollBodies);
            Transform firstWithCollider = null;

            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                Rigidbody body = ragdollBodies[i];
                if (body == null)
                    continue;

                if (firstWithCollider == null && body.GetComponent<Collider>() != null)
                    firstWithCollider = body.transform;

                Transform parent = body.transform.parent;
                bool hasParentRagdollBody = false;
                while (parent != null && parent != transform)
                {
                    if (parent.TryGetComponent(out Rigidbody parentBody) && ragdollBodySet.Contains(parentBody))
                    {
                        hasParentRagdollBody = true;
                        break;
                    }
                    parent = parent.parent;
                }

                if (!hasParentRagdollBody && body.GetComponent<Collider>() != null)
                    return body.transform;
            }

            return firstWithCollider != null ? firstWithCollider : transform;
        }

        private void PrepareAnimatorForDeath()
        {
            animator.SetBool("isIdle", false);
            animator.SetBool("isJumping", false);
            animator.SetBool("isFalling", false);
            animator.SetBool("isCrouching", false);
            animator.SetBool("isSwimming", false);
            animator.SetBool("isFlying", false);
            animator.SetBool("isPlayingAction", false);
            animator.SetBool("isRotatingToTarget", false);
            animator.SetBool("isGrounded", true);
            animator.SetFloat("inputX", 0f);
            animator.SetFloat("inputY", 0f);
            animator.SetFloat("inputMagnitude", 0f);
            animator.SetFloat("speed", 0f);
            animator.SetFloat("rotationMismatch", 0f);
        }

        /// <summary>Debug/editor helper: force a state transition (bypasses priority).</summary>
        public void ForceState(State state) => SetState(state);

        private void OnDrawGizmosSelected()
        {
            float patrol = config != null ? config.patrolRadius : 15f;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, patrol);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.2f,
                $"[{CurrentState}]" +
                (inventory != null ? $" Inv:{inventory.Count}" : "") +
                (!string.IsNullOrEmpty(LastItemAction) ? $" | {LastItemAction}" : ""));

            // Steering direction (where the agent is headed)
            if (agent != null && agent.hasPath)
            {
                Gizmos.color = Color.blue;
                Vector3 steerDir = (agent.steeringTarget - transform.position).normalized;
                Gizmos.DrawRay(transform.position + Vector3.up, steerDir * 2f);
            }

            // Forward vector
            Gizmos.color = Color.white;
            Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 1.5f);

#endif
        }
    }
}
