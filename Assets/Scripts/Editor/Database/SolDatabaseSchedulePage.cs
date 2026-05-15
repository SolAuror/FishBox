using System;
using System.Collections.Generic;
using System.IO;
using Sol.AI;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    internal sealed class SolDatabaseSchedulePage : SolDatabaseListPage<SolDatabaseSchedulePage.ScheduleRow, SolDatabaseSchedulePage.ScheduleFilter, SolDatabaseSchedulePage.ScheduleSort>
    {
        internal enum ScheduleFilter
        {
            All,
            MissingId,
            HasWarnings
        }

        internal enum ScheduleSort
        {
            Name,
            Id
        }

        internal sealed class ScheduleRow
        {
            public NpcScheduleDefinition Schedule;
            public string Id;
            public string DisplayName;
            public string Subtitle;
            public string AssetPath;
            public string SearchText;
            public int WarningCount;
        }

        private readonly Dictionary<NpcScheduleDefinition, List<NpcScheduleAuthoringWarning>> _warningCache = new();

        public override SolDatabaseTab Tab => SolDatabaseTab.Schedules;
        public override string DisplayName => "Schedules";

        protected override string EmptyDetailMessage => "Select a schedule asset from the database list.";

        public override void CommitPendingEdits()
        {
            if (SelectedSerializedObject == null || SelectedRow?.Schedule == null)
                return;

            if (SelectedSerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(SelectedRow.Schedule);
                RefreshRow(SelectedRow);
            }
        }

        // -- Row contract --------------------------------------------------------------

        protected override UnityEngine.Object GetRowAsset(ScheduleRow row) => row?.Schedule;
        protected override string GetRowId(ScheduleRow row) => row?.Schedule?.ScheduleId;
        protected override string GetRowSearchText(ScheduleRow row) => row?.SearchText;
        protected override int GetRowWarningCount(ScheduleRow row) => row?.WarningCount ?? 0;

        protected override void OnBeforeLoadRows()
        {
            _warningCache.Clear();
        }

        protected override void LoadRows()
        {
            string[] guids = AssetDatabase.FindAssets("t:NpcScheduleDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                NpcScheduleDefinition schedule = AssetDatabase.LoadAssetAtPath<NpcScheduleDefinition>(path);
                if (schedule != null)
                    Rows.Add(BuildRow(schedule));
            }
        }

        protected override ScheduleRow RebuildRow(ScheduleRow row)
        {
            if (row?.Schedule == null)
                return row;
            _warningCache.Remove(row.Schedule);
            return BuildRow(row.Schedule);
        }

        private ScheduleRow BuildRow(NpcScheduleDefinition schedule)
        {
            string path = AssetDatabase.GetAssetPath(schedule);
            string label = DisplayLabel(schedule);
            string id = string.IsNullOrWhiteSpace(schedule.ScheduleId) ? "<missing>" : schedule.ScheduleId.Trim();
            string subtitle = $"Entries {schedule.Entries?.Count ?? 0}";
            List<NpcScheduleAuthoringWarning> warnings = GetWarnings(schedule);
            return new ScheduleRow
            {
                Schedule = schedule,
                Id = id,
                DisplayName = label,
                Subtitle = subtitle,
                AssetPath = path,
                SearchText = $"{label} {id} {subtitle} {path}".ToLowerInvariant(),
                WarningCount = warnings.Count
            };
        }

        protected override bool MatchesCustomFilter(ScheduleRow row, ScheduleFilter filter)
        {
            if (row?.Schedule == null)
                return false;

            return filter switch
            {
                ScheduleFilter.MissingId => string.IsNullOrWhiteSpace(row.Schedule.ScheduleId),
                ScheduleFilter.HasWarnings => row.WarningCount > 0,
                _ => true
            };
        }

        protected override void SortRows(List<ScheduleRow> rows, ScheduleSort sort)
        {
            switch (sort)
            {
                case ScheduleSort.Id:
                    rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Id, b.Id));
                    break;
                default:
                    rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.DisplayName, b.DisplayName));
                    break;
            }
        }

        internal static bool RowMatchesFilters(ScheduleRow row, string search, ScheduleFilter filter)
        {
            if (row == null || row.Schedule == null)
                return false;

            if (!string.IsNullOrWhiteSpace(search))
            {
                string needle = search.Trim().ToLowerInvariant();
                if (row.SearchText == null || row.SearchText.IndexOf(needle, StringComparison.Ordinal) < 0)
                    return false;
            }

            return filter switch
            {
                ScheduleFilter.MissingId => string.IsNullOrWhiteSpace(row.Schedule.ScheduleId),
                ScheduleFilter.HasWarnings => row.WarningCount > 0,
                _ => true
            };
        }

        // -- Toolbar -------------------------------------------------------------------

        protected override void DrawToolbarTrailing()
        {
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                RefreshIndex();
        }

        protected override void OnNewClicked()
        {
            SolDatabaseStyles.EnsureFolder(SolDatabaseStyles.Folders.NpcSchedules);
            NpcScheduleDefinition schedule = ScriptableObject.CreateInstance<NpcScheduleDefinition>();
            ApplyIdentity(schedule, NextScheduleId(), "New Schedule");
            string path = AssetDatabase.GenerateUniqueAssetPath($"{SolDatabaseStyles.Folders.NpcSchedules}/{schedule.ScheduleId}_NewSchedule.asset");
            AssetDatabase.CreateAsset(schedule, path);
            AssetDatabase.SaveAssets();
            RefreshIndex();
            SetSelectedRowByAsset(AssetDatabase.LoadAssetAtPath<NpcScheduleDefinition>(path));
            OnSelectAssetClicked();
        }

        protected override void OnDuplicateClicked()
        {
            NpcScheduleDefinition source = SelectedRow?.Schedule;
            if (source == null)
                return;

            string sourcePath = AssetDatabase.GetAssetPath(source);
            string folder = Path.GetDirectoryName(sourcePath)?.Replace("\\", "/") ?? SolDatabaseStyles.Folders.NpcSchedules;
            string copyName = $"{NextScheduleId()}_{DisplayLabel(source)}_Copy";
            string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{SanitizeFileName(copyName)}.asset");
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                return;

            NpcScheduleDefinition copy = AssetDatabase.LoadAssetAtPath<NpcScheduleDefinition>(targetPath);
            ApplyIdentity(copy, NextScheduleId(), $"{DisplayLabel(source)} Copy");
            AssetDatabase.SaveAssets();
            RefreshIndex();
            SetSelectedRowByAsset(copy);
            OnSelectAssetClicked();
        }

        protected override void DoBulkDelete(IReadOnlyList<ScheduleRow> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                NpcScheduleDefinition schedule = rows[i]?.Schedule;
                if (schedule == null)
                    continue;
                string path = AssetDatabase.GetAssetPath(schedule);
                if (!string.IsNullOrWhiteSpace(path))
                    AssetDatabase.DeleteAsset(path);
            }
        }

        protected override void DoBulkDuplicate(IReadOnlyList<ScheduleRow> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                NpcScheduleDefinition source = rows[i]?.Schedule;
                if (source == null)
                    continue;
                string sourcePath = AssetDatabase.GetAssetPath(source);
                string folder = Path.GetDirectoryName(sourcePath)?.Replace("\\", "/") ?? SolDatabaseStyles.Folders.NpcSchedules;
                string copyName = $"{NextScheduleId()}_{DisplayLabel(source)}_Copy";
                string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{SanitizeFileName(copyName)}.asset");
                if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                    continue;
                NpcScheduleDefinition copy = AssetDatabase.LoadAssetAtPath<NpcScheduleDefinition>(targetPath);
                ApplyIdentity(copy, NextScheduleId(), $"{DisplayLabel(source)} Copy");
            }
        }

        // -- Row drawing ---------------------------------------------------------------

        protected override void DrawRow(Rect rowRect, ScheduleRow row)
        {
            DrawRowChrome(rowRect, IsSelected(row), () => HandleRowClick(row));
            DrawIcon(rowRect, AssetDatabase.GetCachedIcon(row.AssetPath));

            Rect textRect = TextColumnRect(rowRect);
            GUI.Label(new Rect(textRect.x, rowRect.y + 5f, textRect.width, EditorGUIUtility.singleLineHeight), $"{row.DisplayName} ({row.Id})", EditorStyles.boldLabel);
            GUI.Label(new Rect(textRect.x, rowRect.y + 22f, textRect.width, EditorGUIUtility.singleLineHeight), row.Subtitle, EditorStyles.miniLabel);
            GUI.Label(new Rect(textRect.x, rowRect.y + 39f, textRect.width, EditorGUIUtility.singleLineHeight), row.AssetPath, EditorStyles.miniLabel);
            DrawWarningBadge(rowRect, row.WarningCount);
        }

        // -- Detail --------------------------------------------------------------------

        protected override void DrawDetail(ScheduleRow row)
        {
            NpcScheduleDefinition schedule = row.Schedule;
            if (schedule == null)
                return;

            EditorGUILayout.LabelField(DisplayLabel(schedule), EditorStyles.largeLabel);
            EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(schedule), EditorStyles.miniLabel);

            DrawDefaultSerializedInspector();
            if (SelectedSerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(schedule);
                RefreshRow(row);
            }

            DrawWarnings(schedule);
        }

        private void DrawWarnings(NpcScheduleDefinition schedule)
        {
            List<NpcScheduleAuthoringWarning> warnings = GetWarnings(schedule);
            for (int i = 0; i < warnings.Count; i++)
            {
                MessageType type = warnings[i].Severity switch
                {
                    NpcScheduleAuthoringWarningSeverity.Error => MessageType.Error,
                    NpcScheduleAuthoringWarningSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };
                EditorGUILayout.HelpBox(warnings[i].Message, type);
            }
        }

        // -- Issues --------------------------------------------------------------------

        public override List<SolDatabaseIssue> CollectIssues()
        {
            List<SolDatabaseIssue> issues = new();
            List<NpcScheduleDefinition> schedules = new();
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i]?.Schedule != null)
                    schedules.Add(Rows[i].Schedule);
            }

            AddIssues(issues, "Schedules", null, NpcScheduleAuthoringValidator.ValidateAll(schedules));
            for (int i = 0; i < schedules.Count; i++)
            {
                NpcScheduleDefinition schedule = schedules[i];
                AddIssues(issues, DisplayLabel(schedule), schedule, GetWarnings(schedule));
            }

            return issues;
        }

        private void AddIssues(List<SolDatabaseIssue> issues, string label, NpcScheduleDefinition context, List<NpcScheduleAuthoringWarning> warnings)
        {
            for (int i = 0; i < warnings.Count; i++)
            {
                issues.Add(new SolDatabaseIssue(MapSeverity(warnings[i].Severity), Tab, context != null ? context.ScheduleId : string.Empty, label, warnings[i].Message, context));
            }
        }

        private static SolDatabaseIssueSeverity MapSeverity(NpcScheduleAuthoringWarningSeverity severity)
        {
            return severity switch
            {
                NpcScheduleAuthoringWarningSeverity.Error => SolDatabaseIssueSeverity.Error,
                NpcScheduleAuthoringWarningSeverity.Warning => SolDatabaseIssueSeverity.Warning,
                _ => SolDatabaseIssueSeverity.Info
            };
        }

        // -- Helpers -------------------------------------------------------------------

        private List<NpcScheduleAuthoringWarning> GetWarnings(NpcScheduleDefinition schedule)
        {
            if (schedule == null)
                return new List<NpcScheduleAuthoringWarning>();

            if (!_warningCache.TryGetValue(schedule, out List<NpcScheduleAuthoringWarning> warnings))
            {
                warnings = NpcScheduleAuthoringValidator.Validate(schedule);
                _warningCache[schedule] = warnings;
            }

            return warnings;
        }

        private string NextScheduleId()
        {
            HashSet<int> used = new();
            for (int i = 0; i < Rows.Count; i++)
            {
                if (EntityCodeUtility.TryParse(Rows[i]?.Schedule?.ScheduleId, EntityCodeUtility.SchedulePrefix, out int numeric))
                    used.Add(numeric);
            }

            for (int i = 1; i <= 99999; i++)
            {
                if (!used.Contains(i))
                    return EntityCodeUtility.Format(EntityCodeUtility.SchedulePrefix, i);
            }

            return string.Empty;
        }

        private static void ApplyIdentity(NpcScheduleDefinition schedule, string id, string displayName)
        {
            if (schedule == null)
                return;

            SerializedObject so = new(schedule);
            so.Update();
            so.FindProperty("_scheduleId").stringValue = id;
            so.FindProperty("_displayName").stringValue = displayName;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(schedule);
        }

        private static string DisplayLabel(NpcScheduleDefinition schedule)
        {
            if (schedule == null)
                return "<missing>";
            return string.IsNullOrWhiteSpace(schedule.DisplayName) ? schedule.name : schedule.DisplayName.Trim();
        }

        private static string SanitizeFileName(string source)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string result = string.IsNullOrWhiteSpace(source) ? "Schedule" : source.Trim();
            for (int i = 0; i < invalid.Length; i++)
                result = result.Replace(invalid[i], '_');
            return result.Replace(' ', '_');
        }
    }
}
