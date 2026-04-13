namespace Sol.AI
{
    public partial class AI_NPC
    {
        private void SyncAnimationWithLocomotion()
        {
            // Runtime animation parameter ownership is centralized in LocomotionAnimation.
            // This method is intentionally a no-op while existing call sites remain in place.
        }
    }
}
