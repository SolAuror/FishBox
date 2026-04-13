using UnityEngine;
using UnityEngine.AI;
using Sol.Locomotion;

namespace Sol.AI
{
    public partial class AI_NPC
    {
        /// <summary>Tracks how long the rotate-before-move gate has been blocking.</summary>
        private float _rotateGateTimer;

        /// <summary>Current rotate gate timer value (for debug display).</summary>
        public float RotateGateTimer => _rotateGateTimer;

        /// <summary>Threshold for warping the agent when it drifts too far from the transform.</summary>
        private const float AgentWarpThreshold = 3f;

        // --- Initialisation helpers called from Awake ---

        private void InitLocomotion()
        {
            // Mark this controller as NPC-driven so LocomotionInput ignores player input.
            if (locoInput != null)
                locoInput.IsControlledByPlayer = false;

            // Auto-create fakeCam if not assigned — guarantees _camTransform is never null
            // so UpdateBodyRotation() and the intent projection always run.
            if (fakeCam == null)
            {
                var go = new GameObject(gameObject.name + "_FakeCam");
                go.transform.SetParent(transform, worldPositionStays: false);
                go.transform.localPosition = Vector3.zero;
                go.transform.rotation = transform.rotation;
                fakeCam = go.transform;
            }

            // Point the controller's cam reference at fakeCam so UpdateBodyRotation()
            // can Slerp the NPC's body toward its current steeringTarget direction.
            if (locoController != null)
                locoController.CamTransform = fakeCam;
        }

        private void InitNavAgent()
        {
            if (agent == null) return;
            // LocomotionController drives the transform; NavMeshAgent only computes paths.
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.autoBraking    = true;
        }

        /// <summary>Finds a random reachable NavMesh point within <paramref name="radius"/> of <paramref name="origin"/>.</summary>
        public static bool RandomNavPoint(Vector3 origin, float radius, out Vector3 result)
        {
            Vector3 candidate = origin + Random.insideUnitSphere * radius;
            candidate.y = origin.y;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, radius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
            result = origin;
            return false;
        }

        // --- ILocomotionIntentProvider implementation ---

        public Shared.AI.LocomotionIntent GetIntent(Shared.Locomotion.ILocomotionController agent)
        {
            if (CurrentState == State.Dead)
            {
                return new Shared.AI.LocomotionIntent
                {
                    TargetPosition = transform.position,
                    DesiredSpeed = 0f,
                    ActionType = Shared.AI.LocomotionActionType.Idle
                };
            }

            var intent = activeState.GetIntent();

            // Rotate-before-move gate: hold the NPC in place until its body is roughly
            // facing the target direction. Without this, movement input is applied on frame
            // one while the body slerp is still catching up, causing visible sideways sliding.
            // fakeCam / UpdateBodyRotation keep rotating regardless of this gate.
            // A timeout prevents permanent deadlock if rotation stalls for any reason.
            const float MoveAlignDot = 0.5f; // cos(60°) — must face within 60° before walking
            const float GateTimeout  = 1.5f;  // seconds — max time to wait for alignment
            if (intent.ActionType != Shared.AI.LocomotionActionType.Idle)
            {
                Vector3 toTarget = intent.TargetPosition - transform.position;
                toTarget.y = 0f;
                float sqrDist = toTarget.sqrMagnitude;
                if (sqrDist > 0.01f)
                {
                    float dot = Vector3.Dot(transform.forward, toTarget.normalized);
                    if (dot < MoveAlignDot && _rotateGateTimer < GateTimeout)
                    {
                        _rotateGateTimer += Time.deltaTime;
                        return new Shared.AI.LocomotionIntent
                        {
                            TargetPosition = transform.position,
                            DesiredSpeed   = 0f,
                            ActionType     = Shared.AI.LocomotionActionType.Idle
                        };
                    }
                }
                // Gate passed or target too close — allow movement, reset timer.
                _rotateGateTimer = 0f;
            }
            else
            {
                // Idle intent — always reset the gate so it's clean for the next move.
                _rotateGateTimer = 0f;
            }

            return intent;
        }

        public float GetCurrentTargetSpeed()
        {
            if (locoState == null) return locoController.runSpeed;
            return locoState.CurrentMovementState switch
            {
                MovementState.Sprinting => locoController.sprintSpeed,
                MovementState.Walking   => locoController.walkSpeed,
                MovementState.Crouching => locoController.crouchSpeed,
                _                       => locoController.runSpeed
            };
        }

        /// <summary>
        /// Rotates fakeCam to face the NPC's current NavMesh steering target each frame.
        /// LocomotionController's body rotation is driven by _camTransform (= fakeCam),
        /// so this keeps the NPC oriented toward its next path waypoint.
        /// Called from Update() before the intent is consumed in LocomotionController.
        /// </summary>
        private void UpdateFakeCam()
        {
            if (fakeCam == null || locoController == null) return;

            Vector3 steerDir;
            float arrival = config != null ? config.arrivalThreshold : 0.5f;

            if (agent != null && agent.isOnNavMesh && agent.hasPath && agent.remainingDistance > arrival)
            {
                steerDir = agent.steeringTarget - transform.position;
            }
            else
            {
                return; // Nothing to steer toward — keep current orientation.
            }

            steerDir.y = 0f;
            if (steerDir.sqrMagnitude > 0.001f)
                fakeCam.rotation = Quaternion.LookRotation(steerDir.normalized, Vector3.up);
        }

        // --- Swimming Awareness ---

        /// <summary>True when locomotion state is swimming. NavMesh usage should be suspended.</summary>
        public bool IsSwimming => locoState != null && locoState.InSwimmingState();

        private bool _wasSwimming;

        /// <summary>
        /// Call from Update to detect swim enter/exit transitions.
        /// Stops NavMeshAgent while swimming to prevent invalid path queries.
        /// </summary>
        private void UpdateSwimmingAwareness()
        {
            bool swimming = IsSwimming;
            if (swimming && !_wasSwimming)
            {
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                }
            }
            else if (!swimming && _wasSwimming)
            {
                if (agent != null && agent.isOnNavMesh)
                    agent.isStopped = false;
            }
            _wasSwimming = swimming;
        }
    }
}

