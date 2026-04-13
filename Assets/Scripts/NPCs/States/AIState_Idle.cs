using UnityEngine;

namespace Sol.AI
{
    /// <summary>NPC stands still for a configured duration, then transitions to Patrol.</summary>
    public class AIState_Idle : AIStateBase
    {
        private float timer;

        public AIState_Idle(AI_NPC npc) : base(npc) { }

        public override void Enter()
        {
            timer = npc.Config != null ? npc.Config.idleDuration : 3f;
            if (npc.Agent != null && npc.Agent.isOnNavMesh)
                npc.Agent.isStopped = true;
        }

        public override AI_NPC.State Tick()
        {
            if (npc.CanChasePlayer())
                return AI_NPC.State.Chase;

            timer -= Time.deltaTime;
            return timer <= 0f ? AI_NPC.State.Patrol : AI_NPC.State.Idle;
        }

        public override void Exit()
        {
            // No special exit logic for idle
        }

        public override Shared.AI.LocomotionIntent GetIntent()
        {
            // Stand still
            return new Shared.AI.LocomotionIntent {
                TargetPosition = npc.transform.position,
                DesiredSpeed = 0f,
                ActionType = Shared.AI.LocomotionActionType.Idle
            };
        }
    }
}
