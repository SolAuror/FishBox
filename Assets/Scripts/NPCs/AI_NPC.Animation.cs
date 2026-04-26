namespace Sol.AI
{
    public partial class AI_NPC
    {
        private void SyncAnimationWithLocomotion()
        {
            // Legacy compatibility shim: call sites still invoke this hook, but LocomotionAnimation
            // is now the single owner of runtime Animator parameter updates.
        }
    }
}
