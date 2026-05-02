using UnityEngine;

namespace Sol.AI
{
    /// <summary>
    /// Optional metadata for NPC-authored patrol waypoints.
    /// </summary>
    public sealed class NpcPatrolPoint : MonoBehaviour
    {
        [Tooltip("When enabled, this point uses its own wait time instead of the NPC config idle duration.")]
        [SerializeField] private bool overrideWaitSeconds;

        [Tooltip("Seconds to wait at this point when Override Wait Seconds is enabled. Set to 0 to continue immediately.")]
        [SerializeField, Min(0f)] private float waitSeconds;

        public bool OverrideWaitSeconds => overrideWaitSeconds;
        public float WaitSeconds => Mathf.Max(0f, waitSeconds);
    }
}
