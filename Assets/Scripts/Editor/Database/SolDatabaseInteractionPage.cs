#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    internal sealed class SolDatabaseInteractionPage : SolDatabasePageBase
    {
        private enum ViewMode
        {
            Definitions,
            SceneAudit,
            Beds,
            Ownership
        }

        private sealed class Row
        {
            public Object Context;
            public string Id;
            public string Title;
            public string Subtitle;
            public string SearchText;
            public int WarningCount;
        }

        private const string DefaultFolder = "Assets/Data/Interactions";

        private readonly List<InteractionDefinition> _definitions = new();
        private readonly List<InteractionPoint> _points = new();
        private readonly List<SleepInteractable> _beds = new();
        private readonly List<Row> _rows = new();
        private readonly List<Row> _filteredRows = new();
        private readonly Dictionary<Object, List<InteractionAuthoringWarning>> _warningCache = new();

        private ViewMode _mode = ViewMode.Definitions;
        private ViewMode _lastMode = (ViewMode)(-1);
        private string _lastSearch = null;
        private bool _rowsDirty = true;
        private Object _selected;
        private SerializedObject _selectedSerializedObject;

        public override SolDatabaseTab Tab => SolDatabaseTab.Interactions;
        public override string DisplayName => "Interactions";

        public override void CommitPendingEdits()
        {
            if (_selectedSerializedObject == null || _selected == null)
                return;

            if (_selectedSerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_selected);
                _warningCache.Remove(_selected);
                RefreshIndex();
            }
        }

        public override void DrawToolbar()
        {
            if (GUILayout.Button("Create Presets", EditorStyles.toolbarButton, GUILayout.Width(98f)))
                CreateMissingPresetDefinitions();
            using (new EditorGUI.DisabledScope(!(_selected is InteractionPoint point) || _definitions.Count == 0))
            {
                if (GUILayout.Button("Apply Definition", EditorStyles.toolbarButton, GUILayout.Width(108f)))
                    ApplyFirstDefinitionToSelectedPoint();
            }
            using (new EditorGUI.DisabledScope(!(_selected is InteractionPoint)))
            {
                if (GUILayout.Button("Align Child", EditorStyles.toolbarButton, GUILayout.Width(78f)))
                    CreateAlignChildForSelectedPoint();
            }
            using (new EditorGUI.DisabledScope(_selected == null))
            {
                if (GUILayout.Button("Select", EditorStyles.toolbarButton, GUILayout.Width(54f)))
                    Selection.activeObject = _selected;
            }
        }

        public override void RefreshIndex()
        {
            _definitions.Clear();
            _points.Clear();
            _beds.Clear();
            _warningCache.Clear();

            string[] definitionGuids = AssetDatabase.FindAssets("t:InteractionDefinition");
            for (int i = 0; i < definitionGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(definitionGuids[i]);
                InteractionDefinition definition = AssetDatabase.LoadAssetAtPath<InteractionDefinition>(path);
                if (definition != null && !_definitions.Contains(definition))
                    _definitions.Add(definition);
            }

            AddSceneInteractions();
            AddPrefabInteractions();
            PruneDestroyedReferences();
            _rowsDirty = true;
            RebuildRowsIfNeeded();
        }

        public override void DrawPage()
        {
            EditorGUILayout.BeginHorizontal();
            DrawLeftPane();
            DrawRightPane();
            EditorGUILayout.EndHorizontal();
        }

        public override bool SelectById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            RefreshIndex();
            for (int i = 0; i < _definitions.Count; i++)
            {
                InteractionDefinition definition = _definitions[i];
                if (definition == null)
                    continue;

                if (string.Equals(definition.DefinitionId, id, System.StringComparison.OrdinalIgnoreCase))
                {
                    SelectObject(definition);
                    return true;
                }
            }

            for (int i = 0; i < _points.Count; i++)
            {
                InteractionPoint point = _points[i];
                if (point == null)
                    continue;

                if (string.Equals(point.InteractionPointId, id, System.StringComparison.OrdinalIgnoreCase))
                {
                    SelectObject(point);
                    return true;
                }
            }

            return false;
        }

        public override List<SolDatabaseIssue> CollectIssues()
        {
            PruneDestroyedReferences();
            List<SolDatabaseIssue> issues = new();
            for (int i = 0; i < _definitions.Count; i++)
            {
                InteractionDefinition definition = _definitions[i];
                if (definition == null)
                    continue;

                AddIssues(issues, definition, definition.DefinitionId, definition.name, GetWarnings(definition));
            }

            for (int i = 0; i < _points.Count; i++)
            {
                InteractionPoint point = _points[i];
                if (point == null)
                    continue;

                AddIssues(issues, point, point.InteractionPointId, point.name, GetWarnings(point));
            }

            for (int i = 0; i < _beds.Count; i++)
            {
                SleepInteractable bed = _beds[i];
                if (bed == null)
                    continue;

                InteractionPoint point = bed.InteractionPoint;
                AddIssues(issues, bed, point != null ? point.InteractionPointId : bed.name, bed.name, GetWarnings(bed));
            }

            return issues;
        }

        private void DrawLeftPane()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(DefaultLeftPaneWidth));
            DrawSearchField(() => _rowsDirty = true);

            EditorGUI.BeginChangeCheck();
            _mode = (ViewMode)EditorGUILayout.EnumPopup("View", _mode);
            if (EditorGUI.EndChangeCheck())
                _rowsDirty = true;

            RebuildRowsIfNeeded();
            DrawVirtualizedRows(_filteredRows.Count, "No interactions match the current filters.", DrawRow);
            EditorGUILayout.EndVertical();
        }

        private void DrawRow(Rect rect, int index)
        {
            Row row = _filteredRows[index];
            DrawRowChrome(rect, row.Context == _selected, () => SelectObject(row.Context));
            DrawIcon(rect, row.Context != null ? AssetDatabase.GetCachedIcon(AssetDatabase.GetAssetPath(row.Context)) : null);

            Rect text = TextColumnRect(rect);
            GUI.Label(new Rect(text.x, rect.y + 5f, text.width, EditorGUIUtility.singleLineHeight), row.Title, EditorStyles.boldLabel);
            GUI.Label(new Rect(text.x, rect.y + 22f, text.width, EditorGUIUtility.singleLineHeight), row.Subtitle, EditorStyles.miniLabel);
            GUI.Label(new Rect(text.x, rect.y + 39f, text.width, EditorGUIUtility.singleLineHeight), row.Id, EditorStyles.miniLabel);
            DrawWarningBadge(rect, row.WarningCount);
        }

        private void DrawRightPane()
        {
            EditorGUILayout.BeginVertical();
            if (_selected == null)
            {
                _selected = null;
                _selectedSerializedObject = null;
                EditorGUILayout.HelpBox("Select an interaction definition, scene point, or bed.", MessageType.Info);
                DrawSummary();
                EditorGUILayout.EndVertical();
                return;
            }

            if (_selectedSerializedObject == null || _selectedSerializedObject.targetObject != _selected)
                _selectedSerializedObject = new SerializedObject(_selected);

            DetailScroll = EditorGUILayout.BeginScrollView(DetailScroll);
            EditorGUILayout.LabelField(_selected.name, EditorStyles.largeLabel);
            EditorGUILayout.LabelField(GetContextSubtitle(_selected), EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            DrawSelectedInspector();
            DrawWarnings(_selected);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSummary()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Project + Open Scene", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Definitions", _definitions.Count.ToString());
            EditorGUILayout.LabelField("Interaction Points", _points.Count.ToString());
            EditorGUILayout.LabelField("Beds", _beds.Count.ToString());
        }

        private void DrawSelectedInspector()
        {
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
                EditorUtility.SetDirty(_selected);
                _warningCache.Remove(_selected);
                RefreshIndex();
            }
        }

        private void DrawWarnings(Object context)
        {
            List<InteractionAuthoringWarning> warnings = GetWarnings(context);
            for (int i = 0; i < warnings.Count; i++)
                EditorGUILayout.HelpBox(warnings[i].Message, InteractionAuthoringValidator.ToMessageType(warnings[i].Severity));
            if (warnings.Count > 0)
                EditorGUILayout.Space(4f);
        }

        private void RebuildRowsIfNeeded()
        {
            if (!_rowsDirty && _lastMode == _mode && string.Equals(_lastSearch, Search, System.StringComparison.Ordinal))
                return;

            _rows.Clear();
            switch (_mode)
            {
                case ViewMode.SceneAudit:
                    for (int i = 0; i < _points.Count; i++)
                    {
                        if (_points[i] != null)
                            _rows.Add(BuildPointRow(_points[i]));
                    }
                    break;
                case ViewMode.Beds:
                    for (int i = 0; i < _beds.Count; i++)
                    {
                        if (_beds[i] != null)
                            _rows.Add(BuildBedRow(_beds[i]));
                    }
                    break;
                case ViewMode.Ownership:
                    for (int i = 0; i < _points.Count; i++)
                    {
                        if (_points[i] != null && _points[i].IsOwned)
                            _rows.Add(BuildPointRow(_points[i]));
                    }
                    break;
                default:
                    for (int i = 0; i < _definitions.Count; i++)
                    {
                        if (_definitions[i] != null)
                            _rows.Add(BuildDefinitionRow(_definitions[i]));
                    }
                    break;
            }

            _filteredRows.Clear();
            string needle = Search?.Trim().ToLowerInvariant() ?? string.Empty;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (string.IsNullOrEmpty(needle) || (_rows[i].SearchText != null && _rows[i].SearchText.Contains(needle)))
                    _filteredRows.Add(_rows[i]);
            }

            _filteredRows.Sort((a, b) => System.StringComparer.OrdinalIgnoreCase.Compare(a.Title, b.Title));
            _lastMode = _mode;
            _lastSearch = Search;
            _rowsDirty = false;
        }

        private Row BuildDefinitionRow(InteractionDefinition definition)
        {
            List<InteractionAuthoringWarning> warnings = GetWarnings(definition);
            string path = AssetDatabase.GetAssetPath(definition);
            string title = definition != null ? definition.name : "<missing>";
            string subtitle = definition != null ? $"{definition.AnimationType} / {definition.CompletionMode}" : string.Empty;
            return new Row
            {
                Context = definition,
                Id = definition != null ? definition.DefinitionId : string.Empty,
                Title = title,
                Subtitle = subtitle,
                SearchText = $"{title} {subtitle} {path}".ToLowerInvariant(),
                WarningCount = warnings.Count
            };
        }

        private Row BuildPointRow(InteractionPoint point)
        {
            if (point == null)
                return BuildMissingRow("Missing InteractionPoint");

            List<InteractionAuthoringWarning> warnings = GetWarnings(point);
            string owner = point.IsOwned ? point.OwnerId : "Public";
            string title = point.name;
            string subtitle = $"{point.AnimationType} / {owner}";
            return new Row
            {
                Context = point,
                Id = point.InteractionPointId,
                Title = title,
                Subtitle = subtitle,
                SearchText = $"{title} {subtitle} {AssetDatabase.GetAssetPath(point)}".ToLowerInvariant(),
                WarningCount = warnings.Count
            };
        }

        private Row BuildBedRow(SleepInteractable bed)
        {
            if (bed == null)
                return BuildMissingRow("Missing SleepInteractable");

            List<InteractionAuthoringWarning> warnings = GetWarnings(bed);
            InteractionPoint point = bed.InteractionPoint;
            string title = bed.name;
            string subtitle = point != null ? $"{point.AnimationType} / {(point.IsOwned ? point.OwnerId : "Public")}" : "Missing InteractionPoint";
            return new Row
            {
                Context = bed,
                Id = point != null ? point.InteractionPointId : title,
                Title = title,
                Subtitle = subtitle,
                SearchText = $"{title} {subtitle}".ToLowerInvariant(),
                WarningCount = warnings.Count
            };
        }

        private static Row BuildMissingRow(string title)
        {
            return new Row
            {
                Title = title,
                Subtitle = "Destroyed or unloaded reference",
                SearchText = title.ToLowerInvariant()
            };
        }

        private List<InteractionAuthoringWarning> GetWarnings(Object context)
        {
            if (context == null)
                return new List<InteractionAuthoringWarning>();

            if (_warningCache.TryGetValue(context, out List<InteractionAuthoringWarning> warnings))
                return warnings;

            warnings = context switch
            {
                InteractionDefinition definition => InteractionAuthoringValidator.ValidateDefinition(definition),
                InteractionPoint point => InteractionAuthoringValidator.ValidatePoint(point),
                SleepInteractable bed => InteractionAuthoringValidator.ValidateBed(bed),
                _ => new List<InteractionAuthoringWarning>()
            };
            _warningCache[context] = warnings;
            return warnings;
        }

        private void AddIssues(List<SolDatabaseIssue> issues, Object context, string id, string label, List<InteractionAuthoringWarning> warnings)
        {
            for (int i = 0; i < warnings.Count; i++)
            {
                issues.Add(new SolDatabaseIssue(
                    InteractionAuthoringValidator.ToDatabaseSeverity(warnings[i].Severity),
                    Tab,
                    id,
                    label,
                    warnings[i].Message,
                    context));
            }
        }

        private void SelectObject(Object context)
        {
            if (_selected != context)
            {
                CommitPendingEdits();
                ClearEditorTextFocus();
            }

            if (context == null)
            {
                _selected = null;
                _selectedSerializedObject = null;
                Window?.Repaint();
                return;
            }

            _selected = context;
            _selectedSerializedObject = new SerializedObject(context);
            Window?.Repaint();
        }

        private static string GetContextSubtitle(Object context)
        {
            if (context == null)
                return string.Empty;

            if (context is InteractionDefinition definition)
                return AssetDatabase.GetAssetPath(definition);
            if (context is InteractionPoint point)
                return point.InteractionPointId;
            if (context is SleepInteractable bed && bed.InteractionPoint != null)
                return bed.InteractionPoint.InteractionPointId;
            return string.Empty;
        }

        private void CreateAlignChildForSelectedPoint()
        {
            if (!(_selected is InteractionPoint point))
                return;

            Transform existing = point.transform.Find("AlignPoint");
            if (existing != null)
            {
                Selection.activeTransform = existing;
                return;
            }

            GameObject align = new("AlignPoint");
            Undo.RegisterCreatedObjectUndo(align, "Create Interaction Align Point");
            align.transform.SetParent(point.transform, false);
            align.transform.localPosition = Vector3.forward;
            Selection.activeTransform = align.transform;
            RefreshIndex();
        }

        private void AddSceneInteractions()
        {
            InteractionPoint[] scenePoints = Object.FindObjectsByType<InteractionPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < scenePoints.Length; i++)
                AddPoint(scenePoints[i]);

            SleepInteractable[] sceneBeds = Object.FindObjectsByType<SleepInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sceneBeds.Length; i++)
                AddBed(sceneBeds[i]);
        }

        private void AddPrefabInteractions()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                InteractionPoint[] prefabPoints = prefab.GetComponentsInChildren<InteractionPoint>(true);
                for (int j = 0; j < prefabPoints.Length; j++)
                    AddPoint(prefabPoints[j]);

                SleepInteractable[] prefabBeds = prefab.GetComponentsInChildren<SleepInteractable>(true);
                for (int j = 0; j < prefabBeds.Length; j++)
                    AddBed(prefabBeds[j]);
            }
        }

        private void AddPoint(InteractionPoint point)
        {
            if (point == null || _points.Contains(point))
                return;

            _points.Add(point);
        }

        private void AddBed(SleepInteractable bed)
        {
            if (bed == null || _beds.Contains(bed))
                return;

            _beds.Add(bed);
        }

        private void PruneDestroyedReferences()
        {
            _definitions.RemoveAll(definition => definition == null);
            _points.RemoveAll(point => point == null);
            _beds.RemoveAll(bed => bed == null);

            if (_selected == null)
            {
                _selected = null;
                _selectedSerializedObject = null;
            }

            List<Object> staleKeys = null;
            foreach (Object key in _warningCache.Keys)
            {
                if (key != null)
                    continue;

                staleKeys ??= new List<Object>();
                staleKeys.Add(key);
            }

            if (staleKeys == null)
                return;

            for (int i = 0; i < staleKeys.Count; i++)
                _warningCache.Remove(staleKeys[i]);
        }

        private void ApplyFirstDefinitionToSelectedPoint()
        {
            if (!(_selected is InteractionPoint point) || _definitions.Count == 0)
                return;

            Undo.RecordObject(point, "Apply Interaction Definition");
            SerializedObject serializedPoint = new(point);
            serializedPoint.FindProperty("_definition").objectReferenceValue = _definitions[0];
            serializedPoint.FindProperty("_overrideDefinitionSettings").boolValue = false;
            serializedPoint.ApplyModifiedProperties();
            EditorUtility.SetDirty(point);
            RefreshIndex();
        }

        private void CreateMissingPresetDefinitions()
        {
            EnsureFolder(DefaultFolder);
            CreatePreset("Bed.Sleep", InteractionPointType.Rest, InteractionPointAnimationType.RestSleep, InteractionCompletionMode.WaitForReadyThenExternalCompletion);
            CreatePreset("Work.Anvil", InteractionPointType.Work, InteractionPointAnimationType.WorkAnvil, InteractionCompletionMode.Timed);
            CreatePreset("Work.Forge", InteractionPointType.Work, InteractionPointAnimationType.WorkForge, InteractionCompletionMode.Timed);
            CreatePreset("Water.Well", InteractionPointType.Water, InteractionPointAnimationType.DrawWater, InteractionCompletionMode.Timed);
            CreatePreset("Food.FruitTree", InteractionPointType.Food, InteractionPointAnimationType.GatherFruit, InteractionCompletionMode.Timed);
            CreatePreset("Container.Open", InteractionPointType.Utility, InteractionPointAnimationType.None, InteractionCompletionMode.Timed);
            CreatePreset("QuestBoard.Open", InteractionPointType.Utility, InteractionPointAnimationType.None, InteractionCompletionMode.Timed);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshIndex();
        }

        private static void CreatePreset(string id, InteractionPointType type, InteractionPointAnimationType animationType, InteractionCompletionMode completionMode)
        {
            string path = $"{DefaultFolder}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<InteractionDefinition>(path) != null)
                return;

            InteractionDefinition definition = ScriptableObject.CreateInstance<InteractionDefinition>();
            AssetDatabase.CreateAsset(definition, path);
            SerializedObject serialized = new(definition);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.FindProperty("_prompt").stringValue = id.StartsWith("Bed.") ? "Sleep" : id.StartsWith("Food.") ? "Harvest" : id.StartsWith("Water.") ? "Drink" : "Use";
            serialized.FindProperty("_displayName").stringValue = id.Contains(".") ? id[(id.IndexOf('.') + 1)..] : id;
            serialized.FindProperty("_type").enumValueIndex = (int)type;
            serialized.FindProperty("_animationType").enumValueIndex = (int)animationType;
            serialized.FindProperty("_completionMode").enumValueIndex = (int)completionMode;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
