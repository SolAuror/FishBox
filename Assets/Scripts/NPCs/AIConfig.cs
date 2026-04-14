using UnityEngine;

namespace Sol.AI
{
    /// <summary>
    /// Simplified AI profile for the trimmed submission build.
    /// Keeps only the data needed for roaming and simple chase behaviour.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAIConfig", menuName = "Sol/AI/AI Config")]
    public class AIConfig : ScriptableObject
    {
        [Header("Patrol")]
        public float patrolRadius = 15f;
        [Tooltip("How long the NPC idles between patrol segments.")]
        public float idleDuration = 3f;

        [Header("Waypoint Patrol")]
        [Tooltip("Ordered waypoints. If assigned, patrol uses these instead of random wandering.")]
        public Transform[] patrolPoints;
        [Tooltip("If true, loops back to the first waypoint after the last. Otherwise ping-pongs.")]
        public bool loop = true;

        [Header("Navigation")]
        public float arrivalThreshold = 0.5f;
        [Tooltip("Degrees per second the NPC can turn toward its steering target.")]
        public float turnSpeed = 360f;

        [Header("Chase")]
        [Tooltip("If enabled, the NPC will automatically enter chase when the player comes within chase radius. Leave disabled to require an explicit gameplay trigger.")]
        public bool autoChasePlayer = false;
        [Tooltip("Distance at which the NPC begins chasing the player. Set to 0 to disable chase.")]
        public float chaseRadius = 8f;
        [Tooltip("Distance at which the NPC gives up chasing and returns to roaming.")]
        public float chaseLoseRadius = 12f;
        [Tooltip("How close the NPC tries to get before stopping its chase advance.")]
        public float chaseStopDistance = 1.5f;
    }
}
