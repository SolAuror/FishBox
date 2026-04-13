using UnityEngine;

namespace Sol.Locomotion
{
    public class LocomotionState : MonoBehaviour
    {
        [field: SerializeField] public MovementState CurrentMovementState { get; set; } = MovementState.Idle;

        public void SetMovementState(MovementState movementState)
        {
            CurrentMovementState = movementState;
        }
    
        public bool InGroundedState()
        {
            return IsStateGrounded(CurrentMovementState);
        }

        public bool IsStateGrounded(MovementState movementState)
        {
            return movementState == MovementState.Idle ||
                   movementState == MovementState.Crouching ||
                   movementState == MovementState.Walking ||
                   movementState == MovementState.Running ||
                   movementState == MovementState.Sprinting;
        }

        public bool InSwimmingState()
        {
            return CurrentMovementState == MovementState.Swimming;
        }

        public bool InFlyingState()
        {
            return CurrentMovementState == MovementState.Flying;
        }

        public bool InSpecialLocomotionState()
        {
            return InSwimmingState() || InFlyingState();
        }
    }

    public enum MovementState
    {
        Idle = 0,
        Walking = 1,
        Running = 2,
        Sprinting = 3,
        Jumping = 4,
        Falling = 5,
        Crouching = 6,
        Swimming = 7,
        Flying = 8,
        Climbing = 9,
    }
}
