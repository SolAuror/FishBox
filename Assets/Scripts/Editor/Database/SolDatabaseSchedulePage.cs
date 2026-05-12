using System.Collections.Generic;
using System.IO;
using Sol.AI;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    internal sealed class SolDatabaseSchedulePage : SolDatabasePageBase
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

        private const string DefaultFolder = "Assets/Data/NpcSchedules";
        private readonly List<NpcScheduleDefinition> _schedules = new();
        private readonly List<ScheduleRow> _rows = new();
        private readonly List<ScheduleRow> _filteredRows = new();
        private readonly Dictionary<NpcScheduleDefinition, List<NpcScheduleAuthoringWarning>> _warningCache = new();

        private string _lastFilterSearch = null;
        private ScheduleFilter _filter = ScheduleFilter.All;
        private ScheduleFilter _lastFilter = (ScheduleFilter)(-1);
        private ScheduleSort _sort = ScheduleSort.Name;
        private ScheduleSort _lastSort = (ScheduleSort)(-1);
        private bool _filteredRowsDirty = true;
        private NpcScheduleDefinition _selectedSchedule;
        private SerializedObject _selectedSerializedObject;

        public override SolDatabaseTab Tab => SolDatabaseTab.Schedules;
        public override string DisplayName => "Schedules";

        public override void DrawToolbar()
        {
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                CreateNewSchedule();
            using (new EditorGUI.DisabledScope(_selectedSchedule == null))
            {
                if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(76f)))
                    DuplicateSelectedSchedule();
                if (GUILayout.Button("Reveal Asset", EditorStyles.toolbarButton, GUILayout.Width(92f)))
                    RevealSelectedAsset();
                if (GUILayout.Button("Select", EditorStyles.toolbarButton, GUILayout.Width(54f)))
                    SelectCurrentAsset();
            }
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                RefreshIndex();
        }

        public override void DrawPage()
        {
            EditorGUILayout.BeginHorizontal();
            DrawLeftPane();
            DrawRightPane();
            EditorGUILayout.EndHorizontal();
        }

        public override void RefreshIndex()
        {
            _schedules.Clear();
            _rows.Clear();
            _warningCache.Clear();

            string[] guids = AssetDatabase.FindAssets("t:NpcScheduleDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                NpcScheduleDefinition schedule = AssetDatabase.LoadAssetAtPath<NpcScheduleDefinition>(path);
                if (schedule == null)
                    continue;

                _schedules.Add(schedule);
                _rows.Add(BuildRow(schedule));
            }

            _filteredRowsDirty = true;
            RebuildFilteredRowsIfNeeded();
            if (_selectedSchedule != null && !_schedules.Contains(_selectedSchedule))
                SelectSchedule(null);
        }

        public override bool SelectById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            RefreshIndex();
            for (int i = 0; i < _schedules.Count; i++)
            {
                NpcScheduleDefinition schedule = _schedules[i];
                if (schedule != null && string.Equals(schedule.ScheduleId, id, System.StringComparison.OrdinalIgnoreCase))
                {
                    SelectSchedule(schedule);
                    return true;
                }
            }

            return false;
        }

        public override List<SolDatabaseIssue> CollectIssues()
        {
            List<SolDatabaseIssue> issues = new();
            AddIssues(issues, "Schedules", null, NpcScheduleAuthoringValidator.ValidateAll(_schedules));
            for (int i = 0; i < _schedules.Count; i++)
            {
                NpcScheduleDefinition schedule = _schedules[i];
                AddIssues(issues, DisplayLabel(schedule), schedule, GetWarnings(schedule));
            }

            return issues;
        }

        internal static bool RowMatchesFilters(ScheduleRow row, string search, ScheduleFilter filter)
        {
            if (row == null || row.Schedule == null)
                return false;

            if (!string.IsNullOrWhiteSpace(search))
            {
                string needle = search.Trim().ToLowerInvariant();
                if (row.SearchText == null || row.SearchText.IndexOf(needle, System.StringComparison.Ordinal) < 0)
                    return false;
            }

            return filter switch
            {
                ScheduleFilter.MissingId => string.IsNullOrWhiteSpace(row.Schedule.ScheduleId),
                ScheduleFilter.HasWarnings => row.WarningCount > 0,
                _ => true
            };
        }

        private void DrawLeftPane()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(DefaultLeftPaneWidth));
            DrawSearchField(() => _filteredRowsDirty = true);

            EditorGUI.BeginChangeCheck();
            _filter = (ScheduleFilter)EditorGUILayout.EnumPopup(_filter);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Sort", GUILayout.Width(32f));
            _sort = (ScheduleSort)EditorGUILayout.EnumPopup(_sort);
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
                _filteredRowsDirty = true;

            RebuildFilteredRowsIfNeeded();
            DrawVirtualizedRows(_filteredRows.Count, "No schedules match the current filters.", (rect, index) => DrawScheduleRow(rect, _filteredRows[index]));
            EditorGUILayout.EndVertical();
        }

        private void DrawRightPane()
        {
            EditorGUILayout.BeginVertical();
            if (_selectedSchedule == null)
            {
                EditorGUILayout.HelpBox("Select a schedule asset from the database list.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            _selectedSerializedObject ??= new SerializedObject(_selectedSchedule);
            if (_selectedSerializedObject.targetObject != _selectedSchedule)
                _selectedSerializedObject = new SerializedObject(_selectedSchedule);

            DetailScroll = EditorGUILayout.BeginScrollView(DetailScroll);
            EditorGUILayout.LabelField(DisplayLabel(_selectedSchedule), EditorStyles.largeLabel);
            EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(_selectedSchedule), EditorStyles.miniLabel);
            DrawWarnings(_selectedSchedule);

            _selectedSerializedObject.Update();
            SerializedProperty iterator = _selectedSerializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                using (new EditorGUI.DisabledScope(iterator.propertyPath == "m_Script"))
                    EditorGUILayout.PropertyField(iterator, true);
                enterChildren = false;
            }

            if (_selectedSerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_selectedSchedule);
                RefreshRow(_selectedSchedule);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawScheduleRow(Rect rowRect, ScheduleRow row)
        {
            DrawRowChrome(rowRect, row.Schedule == _selectedSchedule, () => SelectSchedule(row.Schedule));
            DrawIcon(rowRect, AssetDatabase.GetCachedIcon(row.AssetPath));

            Rect textRect = TextColumnRect(rowRect);
            GUI.Label(new Rect(textRect.x, rowRect.y + 5f, textRect.width, EditorGUIUtility.singleLineHeight), $"{row.DisplayName} ({row.Id})", EditorStyles.boldLabel);
            GUI.Label(new Rect(textRect.x, rowRect.y + 22f, textRect.width, EditorGUIUtility.singleLineHeight), row.Subtitle, EditorStyles.miniLabel);
            GUI.Label(new Rect(textRect.x, rowRect.y + 39f, textRect.width, EditorGUIUtility.singleLineHeight), row.AssetPath, EditorStyles.miniLabel);
            DrawWarningBadge(rowRect, row.WarningCount);
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

        private void RefreshRow(NpcScheduleDefinition schedule)
        {
            if (schedule == null)
                return;

            _warningCache.Remove(schedule);
            ScheduleRow row = BuildRow(schedule);
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Schedule == schedule)
                {
                    _rows[i] = row;
                    _filteredRowsDirty = true;
                    return;
                }
            }
        }

        private void RebuildFilteredRowsIfNeeded()
        {
            if (!_filteredRowsDirty
                && string.Equals(_lastFilterSearch, Search, System.StringComparison.Ordinal)
                && _lastFilter == _filter
                && _lastSort == _sort)
            {
                return;
            }

            _filteredRows.Clear();
            for (int i = 0; i < _rows.Count; i++)
            {
                if (RowMatchesFilters(_rows[i], Search, _filter))
                    _filteredRows.Add(_rows[i]);
            }

            _filteredRows.Sort((a, b) => _sort == ScheduleSort.Id
                ? System.StringComparer.OrdinalIgnoreCase.Compare(a.Id, b.Id)
                : System.StringComparer.OrdinalIgnoreCase.Compare(a.DisplayName, b.DisplayName));
            _lastFilterSearch = Search;
            _lastFilter = _filter;
            _lastSort = _sort;
            _filteredRowsDirty = false;
        }

        private void SelectSchedule(NpcScheduleDefinition schedule)
        {
            _selectedSchedule = schedule;
            _selectedSerializedObject = schedule != null ? new SerializedObject(schedule) : null;
            Window?.Repaint();
        }

        private void CreateNewSchedule()
        {
            EnsureDefaultFolder();
            NpcScheduleDefinition schedule = ScriptableObject.CreateInstance<NpcScheduleDefinition>();
            ApplyIdentity(schedule, NextScheduleId(), "New Schedule");
            string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultFolder}/{schedule.ScheduleId}_NewSchedule.asset");
            AssetDatabase.CreateAsset(schedule, path);
            AssetDatabase.SaveAssets();
            RefreshIndex();
            SelectSchedule(AssetDatabase.LoadAssetAtPath<NpcScheduleDefinition>(path));
            SelectCurrentAsset();
        }

        private void DuplicateSelectedSchedule()
        {
            if (_selectedSchedule == null)
                return;

            string sourcePath = AssetDatabase.GetAssetPath(_selectedSchedule);
            string folder = Path.GetDirectoryName(sourcePath)?.Replace("\\", "/") ?? DefaultFolder;
            string copyName = $"{NextScheduleId()}_{DisplayLabel(_selectedSchedule)}_Copy";
            string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{SanitizeFileName(copyName)}.asset");
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                return;

            NpcScheduleDefinition copy = AssetDatabase.LoadAssetAtPath<NpcScheduleDefinition>(targetPath);
            ApplyIdentity(copy, NextScheduleId(), $"{DisplayLabel(_selectedSchedule)} Copy");
            AssetDatabase.SaveAssets();
            RefreshIndex();
            SelectSchedule(copy);
            SelectCurrentAsset();
        }

        private void RevealSelectedAsset()
        {
            string path = AssetDatabase.GetAssetPath(_selectedSchedule);
            if (!string.IsNullOrWhiteSpace(path))
                EditorUtility.RevealInFinder(path);
        }

        private void SelectCurrentAsset()
        {
            if (_selectedSchedule == null)
                return;

            Selection.activeObject = _selectedSchedule;
            EditorGUIUtility.PingObject(_selectedSchedule);
        }

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

        private string NextScheduleId()
        {
            HashSet<int> used = new();
            for (int i = 0; i < _schedules.Count; i++)
            {
                if (EntityCodeUtility.TryParse(_schedules[i]?.ScheduleId, EntityCodeUtility.SchedulePrefix, out int numeric))
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

        private static void EnsureDefaultFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
                AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(DefaultFolder))
                AssetDatabase.CreateFolder("Assets/Data", "NpcSchedules");
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
