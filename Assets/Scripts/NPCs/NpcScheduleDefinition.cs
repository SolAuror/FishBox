using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sol.AI
{
    public enum NpcScheduleActivity
    {
        Travel = 0,
        Sleep = 1,
        Work = 2,
        Eat = 3,
        Socialize = 4
    }

    [Serializable]
    public sealed class NpcScheduleEntry
    {
        [Range(0f, 24f)]
        [SerializeField] private float _startHour;
        [Range(0f, 24f)]
        [SerializeField] private float _endHour;
        [SerializeField] private string _locationId = string.Empty;
        [SerializeField] private NpcScheduleActivity _activity = NpcScheduleActivity.Work;
        [Min(0f)]
        [SerializeField] private float _waitSeconds;
        [SerializeField] private bool _useLocationFacing = true;

        public float StartHour => _startHour;
        public float EndHour => _endHour;
        public string LocationId => _locationId;
        public NpcScheduleActivity Activity => _activity;
        public float WaitSeconds => _waitSeconds;
        public bool UseLocationFacing => _useLocationFacing;

        public bool ContainsHour(float hour)
            => ContainsHour(_startHour, _endHour, hour);

        public static bool ContainsHour(float startHour, float endHour, float hour)
        {
            float wrappedHour = WrapHour(hour);
            float start = WrapHour(startHour);
            float end = WrapHour(endHour);

            if (Mathf.Approximately(start, end))
                return false;

            if (start < end)
                return wrappedHour >= start && wrappedHour < end;

            return wrappedHour >= start || wrappedHour < end;
        }

        private static float WrapHour(float hour) => Mathf.Repeat(hour, 24f);
    }

    [CreateAssetMenu(fileName = "SCH00001_NewSchedule", menuName = "Sol/NPC Schedule")]
    public sealed class NpcScheduleDefinition : ScriptableObject
    {
        [SerializeField] private string _scheduleId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [TextArea(2, 5)]
        [SerializeField] private string _description = string.Empty;
        [SerializeField] private List<NpcScheduleEntry> _entries = new();

        public string ScheduleId => _scheduleId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public IReadOnlyList<NpcScheduleEntry> Entries => _entries;

        public bool TryGetEntryForHour(float hour, out NpcScheduleEntry entry)
        {
            entry = null;
            if (_entries == null)
                return false;

            for (int i = 0; i < _entries.Count; i++)
            {
                NpcScheduleEntry candidate = _entries[i];
                if (candidate != null && candidate.ContainsHour(hour))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool HoursOverlap(NpcScheduleEntry a, NpcScheduleEntry b)
        {
            if (a == null || b == null)
                return false;

            return a.ContainsHour(b.StartHour)
                || a.ContainsHour(Mathf.Repeat(b.EndHour - 0.001f, 24f))
                || b.ContainsHour(a.StartHour)
                || b.ContainsHour(Mathf.Repeat(a.EndHour - 0.001f, 24f));
        }
    }
}
