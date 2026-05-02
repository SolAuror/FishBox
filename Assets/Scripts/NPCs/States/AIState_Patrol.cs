using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Splines;
using Sol.Player;

namespace Sol.AI
{
    public class AIState_Patrol : AIStateBase
    {
        private NavMeshAgent agent => npc.Agent;
        private AIConfig config => npc.Config;
        private Vector3 patrolTarget;
        private bool hasPatrolTarget;
        private int waypointIndex;
        private int waypointDirection = 1;
        private float targetWaitSeconds;
        private float waitTimer;
        private bool waitingAtPoint;

        public AIState_Patrol(AI_NPC npc) : base(npc) { }

        public override void Enter()
        {
            waitingAtPoint = false;
            waitTimer = 0f;

            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = false;

            SelectNewPatrolPoint();
        }

        public override void Exit()
        {
            hasPatrolTarget = false;
            waitingAtPoint = false;
            waitTimer = 0f;
        }

        public override AI_NPC.State Tick()
        {
            if (npc.CanChasePlayer())
                return AI_NPC.State.Chase;

            if (waitingAtPoint)
            {
                waitTimer -= Time.deltaTime;
                if (waitTimer > 0f)
                    return AI_NPC.State.Patrol;

                waitingAtPoint = false;
                if (agent != null && agent.isOnNavMesh)
                    agent.isStopped = false;

                SelectNewPatrolPoint();
                return AI_NPC.State.Patrol;
            }

            if (!hasPatrolTarget)
            {
                SelectNewPatrolPoint();
                return AI_NPC.State.Patrol;
            }

            if (HasReachedPatrolTarget())
            {
                BeginWaypointWaitOrAdvance();
            }

            return AI_NPC.State.Patrol;
        }

        public override Shared.AI.LocomotionIntent GetIntent()
        {
            if (waitingAtPoint)
            {
                return new Shared.AI.LocomotionIntent
                {
                    TargetPosition = npc.transform.position,
                    DesiredSpeed = 0f,
                    ActionType = Shared.AI.LocomotionActionType.Idle
                };
            }

            Vector3 targetPos = (hasPatrolTarget && npc.Agent.hasPath)
                ? npc.Agent.steeringTarget
                : npc.transform.position;
            return new Shared.AI.LocomotionIntent
            {
                TargetPosition = targetPos,
                DesiredSpeed = npc.GetCurrentTargetSpeed(),
                ActionType = Shared.AI.LocomotionActionType.Walk
            };
        }

        private void SelectNewPatrolPoint()
        {
            targetWaitSeconds = 0f;

            int authoredPointCount = npc.GetAuthoredPatrolPointCount();
            if (authoredPointCount > 0)
            {
                if (waypointIndex < 0 || waypointIndex >= authoredPointCount)
                    waypointIndex = 0;

                if (!npc.TryGetAuthoredPatrolPoint(waypointIndex, out patrolTarget, out targetWaitSeconds))
                {
                    hasPatrolTarget = false;
                    return;
                }

                if (agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(patrolTarget);
                    hasPatrolTarget = true;
                }
                else
                {
                    hasPatrolTarget = false;
                }

                AdvanceWaypointIndex(authoredPointCount);
                return;
            }

            // Spline patrol if a spline is assigned.
            SplineContainer patrolSpline = npc.PatrolSpline;
            if (patrolSpline != null && patrolSpline.Spline != null && patrolSpline.Spline.Count > 0)
            {
                int sampleCount = Mathf.Max(2, npc.PatrolSampleCount);
                float t = (sampleCount <= 1) ? 0f : (float)waypointIndex / (sampleCount - 1);
                patrolTarget = patrolSpline.EvaluatePosition(Mathf.Clamp01(t));

                if (agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(patrolTarget);
                    hasPatrolTarget = true;
                }
                else
                {
                    hasPatrolTarget = false;
                }

                AdvanceWaypointIndex(sampleCount);
                return;
            }

            // Fallback: random NavMesh wandering.
            float radius = config?.patrolRadius ?? 10f;
            if (AI_NPC.RandomNavPoint(npc.transform.position, radius, out patrolTarget))
            {
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(patrolTarget);
                    hasPatrolTarget = true;
                }
                else
                {
                    hasPatrolTarget = false;
                }
            }
            else
            {
                hasPatrolTarget = false;
            }
        }

        private bool HasReachedPatrolTarget()
        {
            if (agent == null || !agent.isOnNavMesh || agent.pathPending)
                return false;

            float arrival = config?.arrivalThreshold ?? 0.5f;
            return agent.hasPath && agent.remainingDistance <= arrival;
        }

        private void BeginWaypointWaitOrAdvance()
        {
            hasPatrolTarget = false;

            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }

            waitTimer = Mathf.Max(0f, targetWaitSeconds);
            if (waitTimer <= 0f)
            {
                waitingAtPoint = false;
                if (agent != null && agent.isOnNavMesh)
                    agent.isStopped = false;

                SelectNewPatrolPoint();
                return;
            }

            waitingAtPoint = true;
        }

        private void AdvanceWaypointIndex(int pointCount)
        {
            if (pointCount <= 1)
            {
                waypointIndex = 0;
                waypointDirection = 1;
                return;
            }

            if (npc.LoopPatrol)
            {
                waypointIndex = (waypointIndex + 1) % pointCount;
                waypointDirection = 1;
                return;
            }

            int next = waypointIndex + waypointDirection;
            if (next >= pointCount)
            {
                waypointDirection = -1;
                waypointIndex = pointCount - 2;
                return;
            }

            if (next < 0)
            {
                waypointDirection = 1;
                waypointIndex = 1;
                return;
            }

            waypointIndex = next;
        }
    }

    public class AIState_Chase : AIStateBase
    {
        private NavMeshAgent agent => npc.Agent;
        private AIConfig config => npc.Config;

        public AIState_Chase(AI_NPC npc) : base(npc) { }

        public override void Enter()
        {
            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = false;
        }

        public override void Exit()
        {
        }

        public override AI_NPC.State Tick()
        {
            if (!npc.TryGetPlayerPosition(out Vector3 playerPosition))
                return AI_NPC.State.Patrol;

            float loseRadius = config != null ? Mathf.Max(config.chaseLoseRadius, config.chaseRadius) : 0f;
            if (loseRadius <= 0f)
                return AI_NPC.State.Patrol;

            Vector3 toPlayer = playerPosition - npc.transform.position;
            if (toPlayer.sqrMagnitude > loseRadius * loseRadius)
                return AI_NPC.State.Patrol;

            if (agent != null && agent.isOnNavMesh)
            {
                float stopDistance = config != null ? Mathf.Max(0.1f, config.chaseStopDistance) : 1.5f;
                agent.stoppingDistance = stopDistance;
                agent.SetDestination(playerPosition);
            }

            return AI_NPC.State.Chase;
        }

        public override Shared.AI.LocomotionIntent GetIntent()
        {
            Vector3 targetPos = npc.transform.position;
            if (agent != null && agent.isOnNavMesh && agent.hasPath)
                targetPos = agent.steeringTarget;
            else if (npc.TryGetPlayerPosition(out Vector3 playerPosition))
                targetPos = playerPosition;

            return new Shared.AI.LocomotionIntent
            {
                TargetPosition = targetPos,
                DesiredSpeed = npc.GetCurrentTargetSpeed(),
                ActionType = Shared.AI.LocomotionActionType.Sprint
            };
        }
    }
}
