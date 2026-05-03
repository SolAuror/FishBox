using System.Collections.Generic;
using Sol.Rpg;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    internal abstract class SolDatabaseRpgDefinitionPage<TDefinition> : SolDatabasePageBase
        where TDefinition : RpgDefinition
    {
        internal enum DefinitionFilter
        {
            All,
            MissingId,
            HasWarnings
        }

        internal enum DefinitionSort
        {
            Name,
            Id
        }

        internal sealed class DefinitionRow
        {
            public TDefinition Definition;
            public string Id;
            public string DisplayName;
            public string Subtitle;
            public string AssetPath;
            public string SearchText;
            public int WarningCount;
        }

        private readonly List<TDefinition> _definitions = new();
        private readonly List<DefinitionRow> _rows = new();
        private readonly List<DefinitionRow> _filteredRows = new();
        private readonly Dictionary<TDefinition, List<RpgAuthoringWarning>> _warningCache = new();

        private string _lastFilterSearch = null;
        private DefinitionFilter _filter = DefinitionFilter.All;
        private DefinitionFilter _lastFilter = (DefinitionFilter)(-1);
        private DefinitionSort _sort = DefinitionSort.Name;
        private DefinitionSort _lastSort = (DefinitionSort)(-1);
        private bool _filteredRowsDirty = true;
        private TDefinition _selectedDefinition;
        private SerializedObject _selectedSerializedObject;

        protected abstract string IdPrefix { get; }
        protected abstract string NewDisplayName { get; }
        protected virtual bool IncludeRegistryIssues => false;
        protected abstract IReadOnlyList<TDefinition> GetDefinitions(RpgDefinitionRegistry registry);
        protected abstract string BuildSubtitle(TDefinition definition);

        public override void DrawToolbar()
        {
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                CreateNewDefinition();
            using (new EditorGUI.DisabledScope(_selectedDefinition == null))
            {
                if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(76f)))
                    DuplicateSelectedDefinition();
                if (GUILayout.Button("Reveal Asset", EditorStyles.toolbarButton, GUILayout.Width(92f)))
                    RevealSelectedAsset();
                if (GUILayout.Button("Select", EditorStyles.toolbarButton, GUILayout.Width(54f)))
                    SelectCurrentAsset();
            }
            if (GUILayout.Button("Rebuild Registry", EditorStyles.toolbarButton, GUILayout.Width(108f)))
                RebuildRegistry();
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
            _definitions.Clear();
            _rows.Clear();
            InvalidateWarningCache();

            RpgDefinitionRegistry registry = RpgDefinitionRegistry.Get();
            IReadOnlyList<TDefinition> definitions = GetDefinitions(registry);
            if (definitions != null)
            {
                HashSet<TDefinition> seen = new();
                for (int i = 0; i < definitions.Count; i++)
                {
                    TDefinition definition = definitions[i];
                    if (definition == null || !seen.Add(definition))
                        continue;

                    _definitions.Add(definition);
                    _rows.Add(BuildRow(definition));
                }
            }

            _filteredRowsDirty = true;
            RebuildFilteredRowsIfNeeded();
            if (_selectedDefinition != null && !_definitions.Contains(_selectedDefinition))
                SelectDefinition(null);
        }

        public override bool SelectById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            RefreshIndex();
            for (int i = 0; i < _definitions.Count; i++)
            {
                TDefinition definition = _definitions[i];
                if (definition == null)
                    continue;
                if (string.Equals(definition.Id, id, System.StringComparison.OrdinalIgnoreCase))
                {
                    SelectDefinition(definition);
                    return true;
                }
            }

            return false;
        }

        public override List<SolDatabaseIssue> CollectIssues()
        {
            List<SolDatabaseIssue> issues = new();
            RpgDefinitionRegistry registry = RpgDefinitionRegistry.Get();
            if (IncludeRegistryIssues)
            {
                List<RpgAuthoringWarning> registryWarnings = RpgDefinitionValidator.ValidateRegistry(registry);
                for (int i = 0; i < registryWarnings.Count; i++)
                {
                    RpgAuthoringWarning warning = registryWarnings[i];
                    issues.Add(new SolDatabaseIssue(MapIssueSeverity(warning.Severity), Tab, string.Empty, "RPG Registry", warning.Message, registry));
                }
            }

            for (int i = 0; i < _definitions.Count; i++)
            {
                TDefinition definition = _definitions[i];
                if (definition == null)
                    continue;

                List<RpgAuthoringWarning> warnings = GetWarnings(definition);
                for (int j = 0; j < warnings.Count; j++)
                {
                    RpgAuthoringWarning warning = warnings[j];
                    issues.Add(new SolDatabaseIssue(
                        MapIssueSeverity(warning.Severity),
                        Tab,
                        definition.Id,
                        DisplayLabel(definition),
                        warning.Message,
                        definition));
                }
            }

            return issues;
        }

        private void DrawLeftPane()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(DefaultLeftPaneWidth));

            DrawSearchField(() => _filteredRowsDirty = true);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            _filter = (DefinitionFilter)EditorGUILayout.EnumPopup(_filter);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Sort", GUILayout.Width(32f));
            _sort = (DefinitionSort)EditorGUILayout.EnumPopup(_sort);
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
                _filteredRowsDirty = true;

            RebuildFilteredRowsIfNeeded();
            DrawVirtualizedRows(_filteredRows.Count, $"No {DisplayName.ToLowerInvariant()} match the current filters.", (rect, index) => DrawDefinitionRow(rect, _filteredRows[index]));
            EditorGUILayout.EndVertical();
        }

        private void DrawDefinitionRow(Rect rowRect, DefinitionRow row)
        {
            DrawRowChrome(rowRect, row.Definition == _selectedDefinition, () => SelectDefinition(row.Definition));
            DrawIcon(rowRect, AssetDatabase.GetCachedIcon(row.AssetPath));

            Rect textRect = TextColumnRect(rowRect);
            Rect titleRect = new(textRect.x, rowRect.y + 5f, textRect.width, EditorGUIUtility.singleLineHeight);
            Rect metaRect = new(textRect.x, titleRect.yMax + 1f, textRect.width, EditorGUIUtility.singleLineHeight);
            Rect pathRect = new(textRect.x, metaRect.yMax + 1f, textRect.width, EditorGUIUtility.singleLineHeight);

            GUI.Label(titleRect, $"{row.DisplayName} ({row.Id})", EditorStyles.boldLabel);
            GUI.Label(metaRect, row.Subtitle, EditorStyles.miniLabel);
            GUI.Label(pathRect, row.AssetPath, EditorStyles.miniLabel);
            DrawWarningBadge(rowRect, row.WarningCount);
        }

        private void DrawRightPane()
        {
            EditorGUILayout.BeginVertical();
            if (_selectedDefinition == null)
            {
                EditorGUILayout.HelpBox($"Select a {NewDisplayName.ToLowerInvariant()} from the database list.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            if (_selectedSerializedObject == null || _selectedSerializedObject.targetObject != _selectedDefinition)
                _selectedSerializedObject = new SerializedObject(_selectedDefinition);

            DetailScroll = EditorGUILayout.BeginScrollView(DetailScroll);
            EditorGUILayout.LabelField(DisplayLabel(_selectedDefinition), EditorStyles.largeLabel);
            EditorGUILayout.LabelField(RpgDefinitionEditorUtility.GetAssetPath(_selectedDefinition), EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            DrawWarnings(_selectedDefinition);
            DrawSerializedObject(_selectedSerializedObject);

            if (_selectedSerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_selectedDefinition);
                RpgDefinitionRegistry.ScheduleEditorSync();
                RefreshRow(_selectedDefinition);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static void DrawSerializedObject(SerializedObject serializedObject)
        {
            serializedObject.Update();
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                using (new EditorGUI.DisabledScope(iterator.propertyPath == "m_Script"))
                    EditorGUILayout.PropertyField(iterator, true);
                enterChildren = false;
            }
        }

        private void DrawWarnings(TDefinition definition)
        {
            List<RpgAuthoringWarning> warnings = GetWarnings(definition);
            for (int i = 0; i < warnings.Count; i++)
            {
                MessageType type = warnings[i].Severity switch
                {
                    RpgAuthoringWarningSeverity.Error => MessageType.Error,
                    RpgAuthoringWarningSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };
                EditorGUILayout.HelpBox(warnings[i].Message, type);
            }

            if (warnings.Count > 0)
                EditorGUILayout.Space(4f);
        }

        private DefinitionRow BuildRow(TDefinition definition)
        {
            string path = RpgDefinitionEditorUtility.GetAssetPath(definition);
            List<RpgAuthoringWarning> warnings = GetWarnings(definition);
            string label = DisplayLabel(definition);
            string id = string.IsNullOrWhiteSpace(definition.Id) ? "<missing>" : definition.Id.Trim();
            string subtitle = BuildSubtitle(definition);

            return new DefinitionRow
            {
                Definition = definition,
                Id = id,
                DisplayName = label,
                Subtitle = subtitle,
                AssetPath = path,
                SearchText = $"{label} {id} {subtitle} {path}".ToLowerInvariant(),
                WarningCount = warnings.Count
            };
        }

        private void RefreshRow(TDefinition definition)
        {
            if (definition == null)
                return;

            InvalidateWarningCache(definition);
            DefinitionRow row = BuildRow(definition);
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Definition != definition)
                    continue;

                _rows[i] = row;
                _filteredRowsDirty = true;
                return;
            }

            _definitions.Add(definition);
            _rows.Add(row);
            _filteredRowsDirty = true;
        }

        internal static bool RowMatchesFilters(DefinitionRow row, string search, DefinitionFilter filter)
        {
            if (row == null || row.Definition == null)
                return false;

            if (!string.IsNullOrWhiteSpace(search))
            {
                string needle = search.Trim().ToLowerInvariant();
                if (row.SearchText == null || row.SearchText.IndexOf(needle, System.StringComparison.Ordinal) < 0)
                    return false;
            }

            return filter switch
            {
                DefinitionFilter.MissingId => string.IsNullOrWhiteSpace(row.Definition.Id),
                DefinitionFilter.HasWarnings => row.WarningCount > 0,
                _ => true
            };
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
                DefinitionRow row = _rows[i];
                if (RowMatchesFilters(row, Search, _filter))
                    _filteredRows.Add(row);
            }

            SortFilteredRows(_sort);
            _lastFilterSearch = Search;
            _lastFilter = _filter;
            _lastSort = _sort;
            _filteredRowsDirty = false;
        }

        private void SortFilteredRows(DefinitionSort sort)
        {
            switch (sort)
            {
                case DefinitionSort.Id:
                    _filteredRows.Sort((a, b) => System.StringComparer.OrdinalIgnoreCase.Compare(a.Id, b.Id));
                    break;
                default:
                    _filteredRows.Sort((a, b) => System.StringComparer.OrdinalIgnoreCase.Compare(a.DisplayName, b.DisplayName));
                    break;
            }
        }

        private void SelectDefinition(TDefinition definition)
        {
            _selectedDefinition = definition;
            _selectedSerializedObject = _selectedDefinition != null ? new SerializedObject(_selectedDefinition) : null;
            Window?.Repaint();
        }

        private void CreateNewDefinition()
        {
            TDefinition created = RpgDefinitionEditorUtility.CreateDefinition<TDefinition>(IdPrefix, NewDisplayName);
            RefreshIndex();
            SelectDefinition(created);
            SelectCurrentAsset();
        }

        private void DuplicateSelectedDefinition()
        {
            TDefinition duplicated = RpgDefinitionEditorUtility.DuplicateDefinition(_selectedDefinition, IdPrefix);
            RefreshIndex();
            SelectDefinition(duplicated);
            SelectCurrentAsset();
        }

        private void RevealSelectedAsset()
        {
            string path = RpgDefinitionEditorUtility.GetAssetPath(_selectedDefinition);
            if (!string.IsNullOrWhiteSpace(path))
                EditorUtility.RevealInFinder(path);
        }

        private void SelectCurrentAsset()
        {
            if (_selectedDefinition == null)
                return;
            Selection.activeObject = _selectedDefinition;
            EditorGUIUtility.PingObject(_selectedDefinition);
        }

        private static void RebuildRegistry()
        {
            RpgDefinitionRegistry.ForceEditorSyncNow();
        }

        private List<RpgAuthoringWarning> GetWarnings(TDefinition definition)
        {
            if (definition == null)
                return new List<RpgAuthoringWarning>();

            if (!_warningCache.TryGetValue(definition, out List<RpgAuthoringWarning> warnings))
            {
                warnings = RpgDefinitionValidator.Validate(definition, RpgDefinitionRegistry.Get());
                _warningCache[definition] = warnings;
            }

            return warnings;
        }

        private void InvalidateWarningCache()
        {
            _warningCache.Clear();
        }

        private void InvalidateWarningCache(TDefinition definition)
        {
            if (definition != null)
                _warningCache.Remove(definition);
        }

        private static string DisplayLabel(RpgDefinition definition)
        {
            if (definition == null)
                return "<missing>";
            return string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.name : definition.DisplayName.Trim();
        }

        private static SolDatabaseIssueSeverity MapIssueSeverity(RpgAuthoringWarningSeverity severity)
        {
            return severity switch
            {
                RpgAuthoringWarningSeverity.Error => SolDatabaseIssueSeverity.Error,
                RpgAuthoringWarningSeverity.Warning => SolDatabaseIssueSeverity.Warning,
                _ => SolDatabaseIssueSeverity.Info
            };
        }
    }
}
