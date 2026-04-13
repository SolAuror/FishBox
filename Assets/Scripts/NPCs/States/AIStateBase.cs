using UnityEngine;
using UnityEngine.AI;

namespace Sol.AI
{
    public abstract class AIStateBase
    {
        protected readonly AI_NPC npc;
        protected AIStateBase(AI_NPC npc) { this.npc = npc; }
        public abstract void Enter();
        public abstract void Exit();
        public abstract AI_NPC.State Tick();
        // Required for intent-based movement
        public abstract Shared.AI.LocomotionIntent GetIntent();

        /// <summary>Returns true when the NavMeshAgent's current path is invalid or stale.</summary>
        protected bool IsPathInvalid()
        {
            var agent = npc.Agent;
            return agent.hasPath && agent.pathStatus != NavMeshPathStatus.PathComplete;
        }
    }
}
