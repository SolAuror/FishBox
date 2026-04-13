namespace Shared.Locomotion
{
    /// <summary>
    /// Interface for all locomotion controllers (player or NPC).
    /// </summary>
    public interface ILocomotionController
    {
        /// <summary>
        /// Returns the current locomotion state.
        /// </summary>
        LocomotionState GetState();
    }
}
