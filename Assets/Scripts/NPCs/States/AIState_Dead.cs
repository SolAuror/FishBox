namespace Sol.AI
{
    /// <summary>Terminal state — NPC is dead. No transitions out.</summary>
    public class AIState_Dead : AIStateBase
    {
        public AIState_Dead(AI_NPC npc) : base(npc) { }

        public override void Enter()
        {
            if (npc.Agent != null && npc.Agent.isOnNavMesh)
            {
                npc.Agent.ResetPath();
                npc.Agent.isStopped = true;
            }
        }

        public override AI_NPC.State Tick()
        {
            return AI_NPC.State.Dead; // Terminal — stays here forever.
        }

        public override void Exit()
        {
            // No-op for dead state
        }

        public override Shared.AI.LocomotionIntent GetIntent()
        {
            // Return a default LocomotionIntent (no movement)
            return new Shared.AI.LocomotionIntent {
                TargetPosition = npc.transform.position,
                DesiredSpeed = 0f,
                ActionType = Shared.AI.LocomotionActionType.Idle
            };
        }
    }
}
