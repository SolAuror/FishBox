using System;
using System.Collections.Generic;
using Sol.Rpg;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    internal abstract class SolDatabaseRpgDefinitionPage<TDefinition>
        : SolDatabaseListPage<
            SolDatabaseRpgDefinitionPage<TDefinition>.DefinitionRow,
            SolDatabaseRpgDefinitionPage<TDefinition>.DefinitionFilter,
            SolDatabaseRpgDefinitionPage<TDefinition>.DefinitionSort>
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

        private readonly Dictionary<TDefinition, List<RpgAuthoringWarning>> _warningCache = new();

        protected abstract string IdPrefix { get; }
        protected abstract string NewDisplayName { get; }
        protected virtual bool IncludeRegistryIssues => false;
        protected abstract IReadOnlyList<TDefinition> GetDefinitions(RpgDefinitionRegistry registry);
        protected abstract string BuildSubtitle(TDefinition definition);

        public override void CommitPendingEdits()
        {
            if (SelectedSerializedObject == null || SelectedRow?.Definition == null)
                return;

            if (SelectedSerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(SelectedRow.Definition);
                RpgDefinitionRegistry.ScheduleEditorSync();
                RefreshRow(SelectedRow);
            }
        }

        // -- Row contract --------------------------------------------------------------

        protected override UnityEngine.Object GetRowAsset(DefinitionRow row) => row?.Definition;
        protected override string GetRowId(DefinitionRow row) => row?.Definition?.Id;
        protected override string GetRowSearchText(DefinitionRow row) => row?.SearchText;
        protected override int GetRowWarningCount(DefinitionRow row) => row?.WarningCount ?? 0;

        protected override void OnBeforeLoadRows()
        {
            _warningCache.Clear();
        }

        protected override void LoadRows()
        {
            RpgDefinitionRegistry registry = RpgDefinitionRegistry.Get();
            IReadOnlyList<TDefinition> definitions = GetDefinitions(registry);
            if (definitions == null)
                return;

            HashSet<TDefinition> seen = new();
            for (int i = 0; i < definitions.Count; i++)
            {
                TDefinition definition = definitions[i];
                if (definition == null || !seen.Add(definition))
                    continue;
                Rows.Add(BuildRow(definition));
            }
        }

        protected override DefinitionRow RebuildRow(DefinitionRow row)
        {
            if (row?.Definition == null)
                return row;
            _warningCache.Remove(row.Definition);
            return BuildRow(row.Definition);
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

        protected override bool MatchesCustomFilter(DefinitionRow row, DefinitionFilter filter)
        {
            if (row?.Definition == null)
                return false;

            return filter switch
            {
                DefinitionFilter.MissingId => string.IsNullOrWhiteSpace(row.Definition.Id),
                DefinitionFilter.HasWarnings => row.WarningCount > 0,
                _ => true
            };
        }

        protected override void SortRows(List<DefinitionRow> rows, DefinitionSort sort)
        {
            switch (sort)
            {
                case DefinitionSort.Id:
                    rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Id, b.Id));
                    break;
                default:
                    rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.DisplayName, b.DisplayName));
                    break;
            }
        }

        // -- Toolbar -------------------------------------------------------------------

        protected override void DrawToolbarTrailing()
        {
            if (GUILayout.Button("Rebuild Registry", EditorStyles.toolbarButton, GUILayout.Width(SolDatabaseStyles.ButtonXL)))
                RebuildRegistry();
        }

        protected override void OnNewClicked()
        {
            TDefinition created = RpgDefinitionEditorUtility.CreateDefinition<TDefinition>(IdPrefix, NewDisplayName);
            RefreshIndex();
            DefinitionRow row = FindRowByDefinition(created);
            if (row != null)
                SetSelectedRow(row);
            OnSelectAssetClicked();
        }

        protected override void OnDuplicateClicked()
        {
            if (SelectedRow?.Definition == null)
                return;
            TDefinition duplicated = RpgDefinitionEditorUtility.DuplicateDefinition(SelectedRow.Definition, IdPrefix);
            RefreshIndex();
            DefinitionRow row = FindRowByDefinition(duplicated);
            if (row != null)
                SetSelectedRow(row);
            OnSelectAssetClicked();
        }

        protected override void OnRevealClicked()
        {
            string path = SelectedRow?.Definition != null ? RpgDefinitionEditorUtility.GetAssetPath(SelectedRow.Definition) : null;
            if (!string.IsNullOrWhiteSpace(path))
                EditorUtility.RevealInFinder(path);
        }

        private static void RebuildRegistry()
        {
            RpgDefinitionRegistry.ForceEditorSyncNow();
        }

        // -- Row drawing ---------------------------------------------------------------

        protected override void DrawRow(Rect rowRect, DefinitionRow row)
        {
            DrawRowChrome(rowRect, IsSelected(row), () => SetSelectedRow(row));
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

        // -- Detail --------------------------------------------------------------------

        protected override string EmptyDetailMessage => $"Select a {NewDisplayName.ToLowerInvariant()} from the database list.";

        protected override void DrawDetail(DefinitionRow row)
        {
            TDefinition definition = row.Definition;
            if (definition == null)
                return;

            EditorGUILayout.LabelField(DisplayLabel(definition), EditorStyles.largeLabel);
            EditorGUILayout.LabelField(RpgDefinitionEditorUtility.GetAssetPath(definition), EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            DrawDefaultSerializedInspector();
            if (SelectedSerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(definition);
                RpgDefinitionRegistry.ScheduleEditorSync();
                RefreshRow(row);
            }

            DrawWarnings(definition);
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

        // -- Issues --------------------------------------------------------------------

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
                    issues.Add(new SolDatabaseIssue(MapSeverity(warning.Severity), Tab, string.Empty, "RPG Registry", warning.Message, registry));
                }
            }

            for (int i = 0; i < Rows.Count; i++)
            {
                TDefinition definition = Rows[i]?.Definition;
                if (definition == null)
                    continue;

                List<RpgAuthoringWarning> warnings = GetWarnings(definition);
                for (int j = 0; j < warnings.Count; j++)
                {
                    RpgAuthoringWarning warning = warnings[j];
                    issues.Add(new SolDatabaseIssue(
                        MapSeverity(warning.Severity),
                        Tab,
                        definition.Id,
                        DisplayLabel(definition),
                        warning.Message,
                        definition));
                }
            }

            return issues;
        }

        // -- Helpers -------------------------------------------------------------------

        private DefinitionRow FindRowByDefinition(TDefinition definition)
        {
            if (definition == null)
                return null;
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i]?.Definition == definition)
                    return Rows[i];
            }
            return null;
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

        private static string DisplayLabel(RpgDefinition definition)
        {
            if (definition == null)
                return "<missing>";
            return string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.name : definition.DisplayName.Trim();
        }

        private static SolDatabaseIssueSeverity MapSeverity(RpgAuthoringWarningSeverity severity)
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
