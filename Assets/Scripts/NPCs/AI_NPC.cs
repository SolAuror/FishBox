using UnityEngine;
using UnityEngine.AI;
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
        // See partial class files for implementation.
        public enum State { Idle, Patrol, Chase, Dead }
    }
}
