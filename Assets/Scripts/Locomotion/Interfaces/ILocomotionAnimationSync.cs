namespace Shared.Animation
{
    /// <summary>
    /// Interface for syncing locomotion state with animation.
    /// Kept for compatibility with existing project references.
    /// </summary>
    public interface ILocomotionAnimationSync
    {
        void UpdateAnimation(Shared.Locomotion.LocomotionState state, float speed);
    }
}
