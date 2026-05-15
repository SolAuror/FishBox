#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    internal sealed class SolDatabaseInteractionPage : SolDatabaseListPage<SolDatabaseInteractionPage.Row, SolDatabaseInteractionPage.ViewMode, SolDatabaseInteractionPage.RowSort>
    {
        internal enum ViewMode
        {
            Definitions,
            SceneAudit,
            Beds,
            Ownership
        }

        internal enum RowSort
        {
            Title
        }

        internal enum RowKind
        {
            Definition,
            Point,
            Bed
        }

        internal sealed class Row
        {
            public UnityEngine.Object Context;
            public RowKind Kind;
            public string Id;
            public string Title;
            public string Subtitle;
            public string SearchText;
            public int WarningCount;
            public string WarningTooltip;
            public bool IsOwned;
        }

        private readonly Dictionary<UnityEngine.Object, List<InteractionAuthoringWarning>> _warningCache = new();

        public override SolDatabaseTab Tab => SolDatabaseTab.Interactions;
        public override string DisplayName => "Interactions";

        protected override bool ShowDefaultToolbarOps => false;

        public override void CommitPendingEdits()
        {
            UnityEngine.Object selected = SelectedRow?.Context;
            if (SelectedSerializedObject == null || selected == null)
                return;

            if (SelectedSerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(selected);
                _warningCache.Remove(selected);
                RefreshIndex();
            }
        }

        // -- Row contract --------------------------------------------------------------

        protected override UnityEngine.Object GetRowAsset(Row row) => row?.Context;
        protected override string GetRowId(Row row) => row?.Id;
        protected override string GetRowSearchText(Row row) => row?.SearchText;
        protected override int GetRowWarningCount(Row row) => row?.WarningCount ?? 0;

        protected override void OnBeforeLoadRows()
        {
            _warningCache.Clear();
        }

        protected override void LoadRows()
        {
            string[] definitionGuids = AssetDatabase.FindAssets("t:InteractionDefinition");
            HashSet<InteractionDefinition> seenDefinitions = new();
            for (int i = 0; i < definitionGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(definitionGuids[i]);
                InteractionDefinition definition = AssetDatabase.LoadAssetAtPath<InteractionDefinition>(path);
                if (definition != null && seenDefinitions.Add(definition))
                    Rows.Add(BuildDefinitionRow(definition));
            }

            HashSet<InteractionPoint> seenPoints = new();
            InteractionPoint[] scenePoints = UnityEngine.Object.FindObjectsByType<InteractionPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < scenePoints.Length; i++)
            {
                if (scenePoints[i] != null && seenPoints.Add(scenePoints[i]))
                    Rows.Add(BuildPointRow(scenePoints[i]));
            }

            HashSet<SleepInteractable> seenBeds = new();
            SleepInteractable[] sceneBeds = UnityEngine.Object.FindObjectsByType<SleepInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sceneBeds.Length; i++)
            {
                if (sceneBeds[i] != null && seenBeds.Add(sceneBeds[i]))
                    Rows.Add(BuildBedRow(sceneBeds[i]));
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                InteractionPoint[] prefabPoints = prefab.GetComponentsInChildren<InteractionPoint>(true);
                for (int j = 0; j < prefabPoints.Length; j++)
                {
                    if (prefabPoints[j] != null && seenPoints.Add(prefabPoints[j]))
                        Rows.Add(BuildPointRow(prefabPoints[j]));
                }

                SleepInteractable[] prefabBeds = prefab.GetComponentsInChildren<SleepInteractable>(true);
                for (int j = 0; j < prefabBeds.Length; j++)
                {
                    if (prefabBeds[j] != null && seenBeds.Add(prefabBeds[j]))
                        Rows.Add(BuildBedRow(prefabBeds[j]));
                }
            }
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
                Kind = RowKind.Definition,
                Id = definition != null ? definition.DefinitionId : string.Empty,
                Title = title,
                Subtitle = subtitle,
                SearchText = $"{title} {subtitle} {path}".ToLowerInvariant(),
                WarningCount = warnings.Count,
                WarningTooltip = JoinWarningMessages(warnings)
            };
        }

        private Row BuildPointRow(InteractionPoint point)
        {
            List<InteractionAuthoringWarning> warnings = GetWarnings(point);
            string owner = point.IsOwned ? point.OwnerId : "Public";
            string title = point.name;
            string subtitle = $"{point.AnimationType} / {owner}";
            return new Row
            {
                Context = point,
                Kind = RowKind.Point,
                Id = point.InteractionPointId,
                Title = title,
                Subtitle = subtitle,
                SearchText = $"{title} {subtitle} {AssetDatabase.GetAssetPath(point)}".ToLowerInvariant(),
                WarningCount = warnings.Count,
                WarningTooltip = JoinWarningMessages(warnings),
                IsOwned = point.IsOwned
            };
        }

        private Row BuildBedRow(SleepInteractable bed)
        {
            List<InteractionAuthoringWarning> warnings = GetWarnings(bed);
            InteractionPoint point = bed.InteractionPoint;
            string title = bed.name;
            string subtitle = point != null ? $"{point.AnimationType} / {(point.IsOwned ? point.OwnerId : "Public")}" : "Missing InteractionPoint";
            return new Row
            {
                Context = bed,
                Kind = RowKind.Bed,
                Id = point != null ? point.InteractionPointId : title,
                Title = title,
                Subtitle = subtitle,
                SearchText = $"{title} {subtitle}".ToLowerInvariant(),
                WarningCount = warnings.Count,
                WarningTooltip = JoinWarningMessages(warnings),
                IsOwned = point != null && point.IsOwned
            };
        }

        protected override bool MatchesCustomFilter(Row row, ViewMode filter)
        {
            if (row == null || row.Context == null)
                return false;

            return filter switch
            {
                ViewMode.SceneAudit => row.Kind == RowKind.Point,
                ViewMode.Beds => row.Kind == RowKind.Bed,
                ViewMode.Ownership => row.Kind == RowKind.Point && row.IsOwned,
                _ => row.Kind == RowKind.Definition
            };
        }

        protected override void SortRows(List<Row> rows, RowSort sort)
        {
            rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Title, b.Title));
        }

        protected override void DrawFilterControls()
        {
            Filter = (ViewMode)EditorGUILayout.EnumPopup("View", Filter);
        }

        protected override string EmptyListMessage => "No interactions match the current filters.";

        // -- Toolbar -------------------------------------------------------------------

        public override void DrawToolbar()
        {
            if (GUILayout.Button("Create Presets", EditorStyles.toolbarButton, GUILayout.Width(98f)))
                CreateMissingPresetDefinitions();

            UnityEngine.Object selected = SelectedRow?.Context;
            int definitionCount = CountDefinitions();
            using (new EditorGUI.DisabledScope(!(selected is InteractionPoint) || definitionCount == 0))
            {
                if (GUILayout.Button("Apply Definition", EditorStyles.toolbarButton, GUILayout.Width(108f)))
                    ApplyFirstDefinitionToSelectedPoint();
            }
            using (new EditorGUI.DisabledScope(!(selected is InteractionPoint)))
            {
                if (GUILayout.Button("Align Child", EditorStyles.toolbarButton, GUILayout.Width(78f)))
                    CreateAlignChildForSelectedPoint();
            }
            using (new EditorGUI.DisabledScope(selected == null))
            {
                if (GUILayout.Button("Select", EditorStyles.toolbarButton, GUILayout.Width(54f)))
                    Selection.activeObject = selected;
            }
        }

        // -- Row drawing ---------------------------------------------------------------

        protected override void DrawRow(Rect rect, Row row)
        {
            DrawRowChrome(rect, IsSelected(row), () => HandleRowClick(row));
            DrawIcon(rect, row.Context != null ? AssetDatabase.GetCachedIcon(AssetDatabase.GetAssetPath(row.Context)) : null);

            Rect text = TextColumnRect(rect);
            GUI.Label(new Rect(text.x, rect.y + 5f, text.width, EditorGUIUtility.singleLineHeight), row.Title, EditorStyles.boldLabel);
            GUI.Label(new Rect(text.x, rect.y + 22f, text.width, EditorGUIUtility.singleLineHeight), row.Subtitle, EditorStyles.miniLabel);
            GUI.Label(new Rect(text.x, rect.y + 39f, text.width, EditorGUIUtility.singleLineHeight), row.Id, EditorStyles.miniLabel);
            DrawWarningBadge(rect, row.WarningCount, row.WarningTooltip);
        }

        // -- Detail --------------------------------------------------------------------

        protected override void DrawRightPane()
        {
            EditorGUILayout.BeginVertical();
            if (SelectedRow == null || SelectedRow.Context == null)
            {
                EditorGUILayout.HelpBox("Select an interaction definition, scene point, or bed.", MessageType.Info);
                DrawSummary();
                EditorGUILayout.EndVertical();
                return;
            }

            EnsureSelectedSerializedObject();

            DetailScroll = EditorGUILayout.BeginScrollView(DetailScroll);
            DrawDetail(SelectedRow);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        protected override void DrawDetail(Row row)
        {
            UnityEngine.Object selected = row.Context;
            if (selected == null)
                return;

            EditorGUILayout.LabelField(selected.name, EditorStyles.largeLabel);
            EditorGUILayout.LabelField(GetContextSubtitle(selected), EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            DrawDefaultSerializedInspector();
            if (SelectedSerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(selected);
                _warningCache.Remove(selected);
                RefreshIndex();
            }

            DrawWarnings(selected);
        }

        private void DrawSummary()
        {
            int definitions = CountByKind(RowKind.Definition);
            int points = CountByKind(RowKind.Point);
            int beds = CountByKind(RowKind.Bed);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Project + Open Scene", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Definitions", definitions.ToString());
            EditorGUILayout.LabelField("Interaction Points", points.ToString());
            EditorGUILayout.LabelField("Beds", beds.ToString());
        }

        private int CountByKind(RowKind kind)
        {
            int count = 0;
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i] != null && Rows[i].Kind == kind && Rows[i].Context != null)
                    count++;
            }
            return count;
        }

        private int CountDefinitions() => CountByKind(RowKind.Definition);

        private void DrawWarnings(UnityEngine.Object context)
        {
            List<InteractionAuthoringWarning> warnings = GetWarnings(context);
            for (int i = 0; i < warnings.Count; i++)
                EditorGUILayout.HelpBox(warnings[i].Message, InteractionAuthoringValidator.ToMessageType(warnings[i].Severity));
            if (warnings.Count > 0)
                EditorGUILayout.Space(4f);
        }

        // -- Issues --------------------------------------------------------------------

        public override List<SolDatabaseIssue> CollectIssues()
        {
            List<SolDatabaseIssue> issues = new();
            for (int i = 0; i < Rows.Count; i++)
            {
                Row row = Rows[i];
                if (row?.Context == null)
                    continue;

                switch (row.Kind)
                {
                    case RowKind.Definition:
                        {
                            InteractionDefinition definition = (InteractionDefinition)row.Context;
                            AddIssues(issues, definition, definition.DefinitionId, definition.name, GetWarnings(definition));
                            break;
                        }
                    case RowKind.Point:
                        {
                            InteractionPoint point = (InteractionPoint)row.Context;
                            AddIssues(issues, point, point.InteractionPointId, point.name, GetWarnings(point));
                            break;
                        }
                    case RowKind.Bed:
                        {
                            SleepInteractable bed = (SleepInteractable)row.Context;
                            InteractionPoint point = bed.InteractionPoint;
                            AddIssues(issues, bed, point != null ? point.InteractionPointId : bed.name, bed.name, GetWarnings(bed));
                            break;
                        }
                }
            }

            return issues;
        }

        private void AddIssues(List<SolDatabaseIssue> issues, UnityEngine.Object context, string id, string label, List<InteractionAuthoringWarning> warnings)
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

        // -- Toolbar actions -----------------------------------------------------------

        private void ApplyFirstDefinitionToSelectedPoint()
        {
            if (!(SelectedRow?.Context is InteractionPoint point))
                return;

            InteractionDefinition firstDefinition = FindFirstDefinition();
            if (firstDefinition == null)
                return;

            Undo.RecordObject(point, "Apply Interaction Definition");
            SerializedObject serializedPoint = new(point);
            serializedPoint.FindProperty("_definition").objectReferenceValue = firstDefinition;
            serializedPoint.FindProperty("_overrideDefinitionSettings").boolValue = false;
            serializedPoint.ApplyModifiedProperties();
            EditorUtility.SetDirty(point);
            RefreshIndex();
        }

        private InteractionDefinition FindFirstDefinition()
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i]?.Kind == RowKind.Definition && Rows[i].Context is InteractionDefinition def)
                    return def;
            }
            return null;
        }

        private void CreateAlignChildForSelectedPoint()
        {
            if (!(SelectedRow?.Context is InteractionPoint point))
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

        private void CreateMissingPresetDefinitions()
        {
            SolDatabaseStyles.EnsureFolder(SolDatabaseStyles.Folders.Interactions);
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
            string path = $"{SolDatabaseStyles.Folders.Interactions}/{id}.asset";
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

        // -- Helpers -------------------------------------------------------------------

        private static string GetContextSubtitle(UnityEngine.Object context)
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

        private static string JoinWarningMessages(List<InteractionAuthoringWarning> warnings)
        {
            if (warnings == null || warnings.Count == 0)
                return null;
            System.Text.StringBuilder sb = new();
            for (int i = 0; i < warnings.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(warnings[i].Message);
            }
            return sb.ToString();
        }

        private List<InteractionAuthoringWarning> GetWarnings(UnityEngine.Object context)
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
    }
}
#endif
