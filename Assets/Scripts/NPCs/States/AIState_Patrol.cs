using UnityEngine;
using UnityEngine.AI;
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

        public AIState_Patrol(AI_NPC npc) : base(npc) { }

        public override void Enter()
        {
            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = false;

            SelectNewPatrolPoint();
        }

        public override void Exit()
        {
            hasPatrolTarget = false;
        }

        public override AI_NPC.State Tick()
        {
            if (npc.CanChasePlayer())
                return AI_NPC.State.Chase;

            if (!hasPatrolTarget || (agent != null && agent.isOnNavMesh && agent.remainingDistance <= (config?.arrivalThreshold ?? 0.5f)))
            {
                SelectNewPatrolPoint();
            }

            return AI_NPC.State.Patrol;
        }

        public override Shared.AI.LocomotionIntent GetIntent()
        {
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
            // Waypoint patrol if waypoints are assigned.
            if (config != null && config.patrolPoints != null && config.patrolPoints.Length > 0)
            {
                Transform wp = config.patrolPoints[waypointIndex];
                if (wp != null)
                {
                    patrolTarget = wp.position;
                    if (agent != null && agent.isOnNavMesh)
                    {
                        agent.SetDestination(patrolTarget);
                        hasPatrolTarget = true;
                    }
                    else
                    {
                        hasPatrolTarget = false;
                    }
                }
                waypointIndex++;
                if (waypointIndex >= config.patrolPoints.Length)
                    waypointIndex = config.loop ? 0 : config.patrolPoints.Length - 1;
                return;
            }

            // Fallback: random NavMesh wandering.
            float radius = config?.patrolRadius ?? 10f;
            if (AI_NPC.RandomNavPoint(npc.transform.position, radius, out patrolTarget))
            {
                if (agent != null && agent.isOnNavMesh)
                {
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
