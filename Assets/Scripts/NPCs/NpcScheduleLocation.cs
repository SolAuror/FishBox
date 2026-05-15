using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Sol.AI
{
    public sealed class NpcScheduleLocation : MonoBehaviour
    {
        private static readonly Dictionary<string, NpcScheduleLocation> Locations = new(StringComparer.OrdinalIgnoreCase);

        [SerializeField] private string _locationId = string.Empty;
        [SerializeField] private bool _useFacing = true;
        [Tooltip("Optional interaction point used when an NPC schedule activity waits at this location.")]
        [SerializeField] private InteractionPoint _interactionPoint;
        [Min(0.1f)]
        [SerializeField] private float _navMeshSnapDistance = 4f;

        public string LocationId => _locationId;
        public bool UseFacing => _useFacing;
        public InteractionPoint InteractionPoint => ResolveInteractionPoint();
        public Transform ScheduleAnchor => ResolveScheduleAnchor();

        private void OnEnable()
        {
            Register(this);
        }

        private void OnDisable()
        {
            if (!string.IsNullOrWhiteSpace(_locationId)
                && Locations.TryGetValue(_locationId.Trim(), out NpcScheduleLocation current)
                && current == this)
            {
                Locations.Remove(_locationId.Trim());
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                return;

            Register(this);
        }

        public bool TryGetNavigablePosition(int areaMask, out Vector3 position)
        {
            Transform target = ResolveScheduleAnchor();
            position = target.position;
            int resolvedMask = areaMask == 0 ? NavMesh.AllAreas : areaMask;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, Mathf.Max(0.1f, _navMeshSnapDistance), resolvedMask))
                position = hit.position;

            return true;
        }

        public bool TryGetScheduleAnchor(out Vector3 position, out Vector3 forward)
        {
            Transform anchor = ResolveScheduleAnchor();
            position = anchor.position;
            forward = anchor.forward;
            return true;
        }

        private InteractionPoint ResolveInteractionPoint()
        {
            if (_interactionPoint != null)
                return _interactionPoint;

            _interactionPoint = GetComponent<InteractionPoint>()
                ?? GetComponentInChildren<InteractionPoint>(true);
            return _interactionPoint;
        }

        private Transform ResolveScheduleAnchor()
        {
            InteractionPoint point = InteractionPoint;
            if (point != null && point.AlignPoint != null)
                return point.AlignPoint;

            return transform;
        }

        public static bool TryResolve(string locationId, out NpcScheduleLocation location)
        {
            location = null;
            if (string.IsNullOrWhiteSpace(locationId))
                return false;

            string key = locationId.Trim();
            if (Locations.TryGetValue(key, out location) && location != null)
                return true;

            NpcScheduleLocation[] all = FindObjectsByType<NpcScheduleLocation>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                Register(all[i]);

            return Locations.TryGetValue(key, out location) && location != null;
        }

        private static void Register(NpcScheduleLocation location)
        {
            if (location == null || string.IsNullOrWhiteSpace(location._locationId))
                return;

            Locations[location._locationId.Trim()] = location;
        }
    }
}
