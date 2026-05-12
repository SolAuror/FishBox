#if UNITY_EDITOR
using System.Collections.Generic;
using Sol.AI;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    internal enum NpcScheduleAuthoringWarningSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    internal sealed class NpcScheduleAuthoringWarning
    {
        public NpcScheduleAuthoringWarningSeverity Severity;
        public string Message;

        public NpcScheduleAuthoringWarning(NpcScheduleAuthoringWarningSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }

    internal static class NpcScheduleAuthoringValidator
    {
        public static List<NpcScheduleAuthoringWarning> Validate(NpcScheduleDefinition schedule)
        {
            List<NpcScheduleAuthoringWarning> warnings = new();
            if (schedule == null)
            {
                warnings.Add(new NpcScheduleAuthoringWarning(NpcScheduleAuthoringWarningSeverity.Error, "Missing schedule asset."));
                return warnings;
            }

            if (string.IsNullOrWhiteSpace(schedule.ScheduleId))
                warnings.Add(new NpcScheduleAuthoringWarning(NpcScheduleAuthoringWarningSeverity.Error, "Missing schedule id."));
            else if (!EntityCodeUtility.TryParse(schedule.ScheduleId, EntityCodeUtility.SchedulePrefix, out _))
                warnings.Add(new NpcScheduleAuthoringWarning(NpcScheduleAuthoringWarningSeverity.Error, $"Schedule id '{schedule.ScheduleId}' does not match SCH##### format."));

            if (string.IsNullOrWhiteSpace(schedule.DisplayName))
                warnings.Add(new NpcScheduleAuthoringWarning(NpcScheduleAuthoringWarningSeverity.Warning, "Display name is empty."));

            IReadOnlyList<NpcScheduleEntry> entries = schedule.Entries;
            if (entries == null || entries.Count == 0)
            {
                warnings.Add(new NpcScheduleAuthoringWarning(NpcScheduleAuthoringWarningSeverity.Warning, "Schedule has no entries."));
                return warnings;
            }

            HashSet<string> openLocationIds = CollectOpenSceneLocationIds();
            for (int i = 0; i < entries.Count; i++)
            {
                NpcScheduleEntry entry = entries[i];
                if (entry == null)
                {
                    warnings.Add(new NpcScheduleAuthoringWarning(NpcScheduleAuthoringWarningSeverity.Warning, $"Entry {i + 1} is empty."));
                    continue;
                }

                if (entry.StartHour < 0f || entry.StartHour > 24f || entry.EndHour < 0f || entry.EndHour > 24f)
                    warnings.Add(new NpcScheduleAuthoringWarning(NpcScheduleAuthoringWarningSeverity.Error, $"Entry {i + 1} hours must be between 0 and 24."));

                if (Mathf.Approximately(Mathf.Repeat(entry.StartHour, 24f), Mathf.Repeat(entry.EndHour, 24f)))
                    warnings.Add(new NpcScheduleAuthoringWarning(NpcScheduleAuthoringWarningSeverity.Error, $"Entry {i + 1} start and end hours cannot be the same."));

                if (string.IsNullOrWhiteSpace(entry.LocationId))
                {
                    warnings.Add(new NpcScheduleAuthoringWarning(NpcScheduleAuthoringWarningSeverity.Warning, $"Entry {i + 1} has no location id."));
                }
                else if (openLocationIds.Count > 0 && !openLocationIds.Contains(entry.LocationId.Trim()))
                {
                    warnings.Add(new NpcScheduleAuthoringWarning(NpcScheduleAuthoringWarningSeverity.Warning, $"Entry {i + 1} location '{entry.LocationId}' is not present in the open scene."));
                }

                for (int j = i + 1; j < entries.Count; j++)
                {
                    if (NpcScheduleDefinition.HoursOverlap(entry, entries[j]))
                        warnings.Add(new NpcScheduleAuthoringWarning(NpcScheduleAuthoringWarningSeverity.Warning, $"Entry {i + 1} overlaps entry {j + 1}."));
                }
            }

            return warnings;
        }

        public static List<NpcScheduleAuthoringWarning> ValidateAll(IReadOnlyList<NpcScheduleDefinition> schedules)
        {
            List<NpcScheduleAuthoringWarning> warnings = new();
            HashSet<string> seen = new(System.StringComparer.OrdinalIgnoreCase);
            if (schedules == null)
                return warnings;

            for (int i = 0; i < schedules.Count; i++)
            {
                NpcScheduleDefinition schedule = schedules[i];
                if (schedule == null)
                {
                    warnings.Add(new NpcScheduleAuthoringWarning(NpcScheduleAuthoringWarningSeverity.Error, $"Schedule list entry {i + 1} is null."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(schedule.ScheduleId))
                    continue;

                if (!seen.Add(schedule.ScheduleId.Trim()))
                    warnings.Add(new NpcScheduleAuthoringWarning(NpcScheduleAuthoringWarningSeverity.Error, $"Duplicate schedule id {schedule.ScheduleId}."));
            }

            return warnings;
        }

        private static HashSet<string> CollectOpenSceneLocationIds()
        {
            HashSet<string> ids = new(System.StringComparer.OrdinalIgnoreCase);
            NpcScheduleLocation[] locations = Object.FindObjectsByType<NpcScheduleLocation>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < locations.Length; i++)
            {
                if (locations[i] != null && !string.IsNullOrWhiteSpace(locations[i].LocationId))
                    ids.Add(locations[i].LocationId.Trim());
            }

            return ids;
        }
    }
}
#endif
