using Shared.Locomotion;

namespace Shared.AI
{
    /// <summary>
    /// Provides movement intent (target, speed, action) for NPCs or player.
    /// </summary>
    public interface ILocomotionIntentProvider
    {
        /// <summary>
        /// Returns the current movement intent for the agent.
        /// </summary>
        /// <param name="agent">The agent requesting intent.</param>
        /// <returns>Intent data (target, speed, action type).</returns>
        LocomotionIntent GetIntent(ILocomotionController agent);
    }

    public struct LocomotionIntent
    {
        public UnityEngine.Vector3 TargetPosition;
        public float DesiredSpeed;
        public LocomotionActionType ActionType;
    }

    public enum LocomotionActionType
    {
        Idle,
        Walk,
        Run,
        Sprint,
        Swim,
        Crouch,
        Climb,
        Custom
    }
}
