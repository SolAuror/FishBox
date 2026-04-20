using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Splines;
using Sol.Locomotion;

// AI_NPC partial class root. All implementation is in partials:
//  - AI_NPC.Core.cs
//  - AI_NPC.Locomotion.cs
//  - AI_NPC.Animation.cs
//  - AI_NPC.Inventory.cs
//  - (state classes)

namespace Sol.AI
{
    [RequireComponent(typeof(LocomotionInput))]
    [RequireComponent(typeof(LocomotionController))]
    [RequireComponent(typeof(LocomotionState))]
    [RequireComponent(typeof(LocomotionAnimation))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NavMeshAgent))]
    public partial class AI_NPC : MonoBehaviour, Shared.AI.ILocomotionIntentProvider
    {
        /// <summary>True if this NPC is currently in a conversation (suppresses rotation).</summary>
        public bool IsConversing { get; private set; } = false;

        /// <summary>Call when conversation starts.</summary>
        public void BeginConversation()
        {
            IsConversing = true;
            if (locoController != null)
                locoController.IsConversing = true;
        }

        /// <summary>Call when conversation ends.</summary>
        public void EndConversation()
        {
            IsConversing = false;
            if (locoController != null)
                locoController.IsConversing = false;
        }
        // See partial class files for implementation.
        public enum State { Idle, Patrol, Chase, Dead }
    }
}
