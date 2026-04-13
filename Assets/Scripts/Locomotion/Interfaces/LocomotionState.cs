namespace Shared.Locomotion
{
    /// <summary>
    /// High-level locomotion state shared across systems (AI, animation, etc.).
    /// Maps from Sol.Locomotion.MovementState via LocomotionController.ConvertToSharedLocomotionState.
    /// </summary>
    public enum LocomotionState
    {
        Grounded,
        Falling,
        Swimming,
        Climbing
    }
}
