using Sol.ToD;
using UnityEngine;
using UnityEngine.AI;

namespace Sol.AI
{
    public partial class AI_NPC
    {
        public enum ScheduleRoutePreviewMode
        {
            SelectedNpcOnly = 0,
            AlwaysVisible = 1
        }

        [Header("Schedule")]
        [SerializeField] private NpcScheduleDefinition _scheduleDefinition;
        [Min(0.05f)]
        [SerializeField] private float _scheduleRefreshSeconds = 0.5f;
        [SerializeField] private bool _showScheduleRoute = true;
        [SerializeField] private ScheduleRoutePreviewMode _scheduleRoutePreviewMode = ScheduleRoutePreviewMode.SelectedNpcOnly;

        private TimeOfDay _timeOfDay;
        private NpcScheduleEntry _currentScheduleEntry;
        private NpcScheduleLocation _currentScheduleLocation;
        private string _currentScheduleLocationId = string.Empty;
        private float _nextScheduleRefreshTime;

        public NpcScheduleDefinition ScheduleDefinition => _scheduleDefinition;
        public NpcScheduleEntry CurrentScheduleEntry => _currentScheduleEntry;
        public NpcScheduleActivity CurrentScheduleActivity => _currentScheduleEntry != null ? _currentScheduleEntry.Activity : NpcScheduleActivity.Travel;
        public string CurrentScheduleLocationId => _currentScheduleLocationId;
        public bool HasActiveSchedule => _scheduleDefinition != null && _currentScheduleEntry != null;
        public bool ShowScheduleRoute => _showScheduleRoute;
        public ScheduleRoutePreviewMode RoutePreviewMode => _scheduleRoutePreviewMode;

        private void UpdateScheduleDriver()
        {
            if (CurrentState == State.Dead || CurrentState == State.Chase)
                return;

            if (!RefreshSchedule(force: false))
                return;

            State desired = GetDesiredScheduleState();
            if (desired != CurrentState)
                SetState(desired);
        }

        public bool RefreshSchedule(bool force)
        {
            if (_scheduleDefinition == null)
            {
                ClearScheduleContext();
                return false;
            }

            if (!force && Time.time < _nextScheduleRefreshTime && _currentScheduleEntry != null)
                return true;

            _nextScheduleRefreshTime = Time.time + Mathf.Max(0.05f, _scheduleRefreshSeconds);
            float hour = ResolveScheduleHour();
            if (!_scheduleDefinition.TryGetEntryForHour(hour, out NpcScheduleEntry entry))
            {
                ClearScheduleContext();
                return false;
            }

            _currentScheduleEntry = entry;
            _currentScheduleLocationId = string.IsNullOrWhiteSpace(entry.LocationId) ? string.Empty : entry.LocationId.Trim();
            _currentScheduleLocation = NpcScheduleLocation.TryResolve(_currentScheduleLocationId, out NpcScheduleLocation location)
                ? location
                : null;
            return true;
        }

        public void SnapToCurrentScheduleTarget()
        {
            if (!RefreshSchedule(force: true))
                return;

            if (!TryGetCurrentScheduleTarget(out Vector3 target, out NpcScheduleLocation location))
                return;

            if (agent != null && agent.enabled)
            {
                agent.Warp(target);
            }

            transform.position = target;
            ApplyScheduleFacing(location);
        }

        internal State GetDesiredScheduleState()
        {
            if (!HasActiveSchedule)
                return State.Idle;

            if (_currentScheduleEntry.Activity == NpcScheduleActivity.Travel || !IsAtCurrentScheduleTarget())
                return State.Travel;

            return StateForActivity(_currentScheduleEntry.Activity);
        }

        internal bool TryGetActiveScheduleState(out State state)
        {
            if (RefreshSchedule(force: false) && HasActiveSchedule)
            {
                state = GetDesiredScheduleState();
                return true;
            }

            state = State.Idle;
            return false;
        }

        internal State GetScheduleFallbackState(State fallback)
        {
            return TryGetActiveScheduleState(out State state) ? state : fallback;
        }

        public bool TryGetCurrentScheduleTarget(out Vector3 target, out NpcScheduleLocation location)
        {
            target = transform.position;
            location = _currentScheduleLocation;

            if (location == null)
            {
                if (!NpcScheduleLocation.TryResolve(_currentScheduleLocationId, out location))
                    return false;

                _currentScheduleLocation = location;
            }

            int areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas;
            return location.TryGetNavigablePosition(areaMask, out target);
        }

        internal bool IsAtCurrentScheduleTarget()
        {
            if (!TryGetCurrentScheduleTarget(out Vector3 target, out _))
                return true;

            float arrival = config != null ? Mathf.Max(0.1f, config.arrivalThreshold) : 0.5f;
            Vector3 delta = target - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= arrival * arrival;
        }

        internal void ApplyScheduleFacing(NpcScheduleLocation location)
        {
            if (_currentScheduleEntry == null || location == null)
                return;

            if (!_currentScheduleEntry.UseLocationFacing || !location.UseFacing)
                return;

            location.TryGetScheduleAnchor(out _, out Vector3 facing);
            facing.y = 0f;
            if (facing.sqrMagnitude <= 0.0001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            if (fakeCam != null)
                fakeCam.rotation = targetRotation;

            transform.rotation = targetRotation;
        }

        internal bool IsScheduleState(State state)
        {
            return state == State.Travel
                || state == State.Sleep
                || state == State.Work
                || state == State.Eat
                || state == State.Socialize;
        }

        public bool IsCurrentScheduleActivity(NpcScheduleActivity activity)
        {
            RefreshSchedule(force: true);
            return _currentScheduleEntry != null && _currentScheduleEntry.Activity == activity;
        }

        public bool IsCurrentScheduleLocation(string locationId)
        {
            RefreshSchedule(force: true);
            return !string.IsNullOrWhiteSpace(locationId)
                && string.Equals(_currentScheduleLocationId, locationId.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

        public bool IsScheduleHourInWindow(float startHour, float endHour)
        {
            float hour = ResolveScheduleHour();
            return NpcScheduleEntry.ContainsHour(startHour, endHour, hour);
        }

        private float ResolveScheduleHour()
        {
            if (_timeOfDay == null)
                _timeOfDay = TimeOfDay.ResolveInstance();

            return _timeOfDay != null ? _timeOfDay.ClockHour : 0f;
        }

        private void ClearScheduleContext()
        {
            _currentScheduleEntry = null;
            _currentScheduleLocation = null;
            _currentScheduleLocationId = string.Empty;
        }

        private static State StateForActivity(NpcScheduleActivity activity)
        {
            return activity switch
            {
                NpcScheduleActivity.Sleep => State.Sleep,
                NpcScheduleActivity.Work => State.Work,
                NpcScheduleActivity.Eat => State.Eat,
                NpcScheduleActivity.Socialize => State.Socialize,
                _ => State.Travel
            };
        }
    }
}
