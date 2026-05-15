using System;
using System.Collections.Generic;
using Sol.AI;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    internal sealed class SolDatabaseNPCPage : SolDatabaseListPage<SolDatabaseNPCPage.NPCRow, SolDatabaseNPCPage.NPCFilter, SolDatabaseNPCPage.NPCSort>
    {
        internal enum NPCFilter
        {
            All,
            Trader,
            QuestGiver,
            Guard,
            Bandit,
            Hostile,
            MissingAIConfig,
            HasWarnings
        }

        internal enum NPCSort
        {
            Name,
            OwnerId,
            EntityType
        }

        internal sealed class NPCRow
        {
            public NPCSoul Soul;
            public string OwnerId;
            public string CharacterName;
            public EntityType EntityKind;
            public NPCArchetype Archetype;
            public string PrefabPath;
            public string SearchText;
            public Texture Icon;
            public int WarningCount;
            public bool HasTrader;
            public bool IsHostile;
            public bool MissingAIConfig;
        }

        private readonly Dictionary<NPCSoul, List<NPCAuthoringWarning>> _warningCache = new();
        private NPCArchetype _newArchetype = NPCArchetype.Civilian;

        public override SolDatabaseTab Tab => SolDatabaseTab.NPCs;
        public override string DisplayName => "NPCs";

        protected override string RevealButtonLabel => "Reveal Prefab";

        public override void CommitPendingEdits()
        {
            if (SelectedSerializedObject == null || SelectedRow?.Soul == null)
                return;

            if (SelectedSerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(SelectedRow.Soul);
                NPCRegistry.ScheduleEditorSync();
                RefreshRow(SelectedRow);
            }
        }

        // -- Row contract --------------------------------------------------------------

        protected override UnityEngine.Object GetRowAsset(NPCRow row) => row?.Soul;
        protected override string GetRowId(NPCRow row) => row?.Soul != null ? row.Soul.OwnerId : null;
        protected override string GetRowSearchText(NPCRow row) => row?.SearchText;
        protected override int GetRowWarningCount(NPCRow row) => row?.WarningCount ?? 0;

        protected override void OnBeforeLoadRows()
        {
            _warningCache.Clear();
        }

        protected override void LoadRows()
        {
            NPCRegistry registry = NPCRegistry.Get();
            if (registry?.Entries == null)
                return;

            HashSet<NPCSoul> seen = new();
            for (int i = 0; i < registry.Entries.Count; i++)
            {
                NPCSoul soul = registry.Entries[i]?.Prefab;
                if (soul == null || !seen.Add(soul))
                    continue;
                Rows.Add(BuildRow(soul));
            }
        }

        protected override NPCRow RebuildRow(NPCRow row)
        {
            if (row?.Soul == null)
                return row;
            _warningCache.Remove(row.Soul);
            return BuildRow(row.Soul);
        }

        private NPCRow BuildRow(NPCSoul soul)
        {
            string path = NPCAuthoringEditorUtility.GetPrefabPath(soul);
            List<NPCAuthoringWarning> warnings = GetWarnings(soul);
            AI_NPC aiNpc = soul.GetComponent<AI_NPC>();
            Sprite portrait = aiNpc != null ? GetSpeakerIcon(aiNpc) : null;
            Texture icon = portrait != null
                ? AssetPreview.GetAssetPreview(portrait)
                : AssetDatabase.GetCachedIcon(path);
            string name = string.IsNullOrWhiteSpace(soul.CharacterName) ? soul.gameObject.name : soul.CharacterName.Trim();
            string ownerId = string.IsNullOrWhiteSpace(soul.OwnerId) ? "<missing>" : soul.OwnerId.Trim();

            return new NPCRow
            {
                Soul = soul,
                OwnerId = ownerId,
                CharacterName = name,
                EntityKind = soul.EntityKind,
                Archetype = soul.Archetype,
                PrefabPath = path,
                SearchText = $"{name} {ownerId} {soul.EntityKind} {soul.Archetype} {path}".ToLowerInvariant(),
                Icon = icon,
                WarningCount = warnings.Count,
                HasTrader = aiNpc != null && aiNpc.IsTrader,
                IsHostile = soul.IsHostile,
                MissingAIConfig = aiNpc != null && aiNpc.Config == null
            };
        }

        protected override bool MatchesCustomFilter(NPCRow row, NPCFilter filter)
        {
            if (row?.Soul == null)
                return false;

            return filter switch
            {
                NPCFilter.Trader => row.HasTrader,
                NPCFilter.QuestGiver => row.Archetype == NPCArchetype.QuestGiver,
                NPCFilter.Guard => row.Archetype == NPCArchetype.Guard,
                NPCFilter.Bandit => row.Archetype == NPCArchetype.Bandit,
                NPCFilter.Hostile => row.IsHostile,
                NPCFilter.MissingAIConfig => row.MissingAIConfig,
                NPCFilter.HasWarnings => row.WarningCount > 0,
                _ => true
            };
        }

        protected override void SortRows(List<NPCRow> rows, NPCSort sort)
        {
            switch (sort)
            {
                case NPCSort.OwnerId:
                    rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.OwnerId, b.OwnerId));
                    break;
                case NPCSort.EntityType:
                    rows.Sort((a, b) =>
                    {
                        int cmp = a.EntityKind.CompareTo(b.EntityKind);
                        return cmp != 0 ? cmp : StringComparer.OrdinalIgnoreCase.Compare(a.CharacterName, b.CharacterName);
                    });
                    break;
                default:
                    rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.CharacterName, b.CharacterName));
                    break;
            }
        }

        // Legacy entry point retained for tests / external callers.
        internal static bool RowMatchesFilters(NPCRow row, string search, NPCFilter filter)
        {
            if (row == null || row.Soul == null)
                return false;

            if (!string.IsNullOrWhiteSpace(search))
            {
                string needle = search.Trim().ToLowerInvariant();
                if (row.SearchText == null || row.SearchText.IndexOf(needle, StringComparison.Ordinal) < 0)
                    return false;
            }

            return filter switch
            {
                NPCFilter.Trader => row.HasTrader,
                NPCFilter.QuestGiver => row.Archetype == NPCArchetype.QuestGiver,
                NPCFilter.Guard => row.Archetype == NPCArchetype.Guard,
                NPCFilter.Bandit => row.Archetype == NPCArchetype.Bandit,
                NPCFilter.Hostile => row.IsHostile,
                NPCFilter.MissingAIConfig => row.MissingAIConfig,
                NPCFilter.HasWarnings => row.WarningCount > 0,
                _ => true
            };
        }

        // -- Toolbar -------------------------------------------------------------------

        protected override void DrawToolbarBeforeRowOps()
        {
            _newArchetype = DrawArchetypeToolbarPopup(_newArchetype, GUILayout.Width(SolDatabaseStyles.ButtonXXL));
        }

        protected override void DrawToolbarTrailing()
        {
            if (GUILayout.Button("Rebuild Registry", EditorStyles.toolbarButton, GUILayout.Width(SolDatabaseStyles.ButtonXL)))
                RebuildRegistry();
        }

        protected override void OnNewClicked()
        {
            NPCSoul created = NPCAuthoringEditorUtility.CreateNPCPrefab(_newArchetype);
            RefreshIndex();
            NPCRow row = FindRowBySoul(created);
            if (row != null)
                SetSelectedRow(row);
            OnSelectAssetClicked();
        }

        protected override void OnDuplicateClicked()
        {
            if (SelectedRow?.Soul == null)
                return;
            NPCSoul duplicated = NPCAuthoringEditorUtility.DuplicateNPCPrefab(SelectedRow.Soul);
            RefreshIndex();
            NPCRow row = FindRowBySoul(duplicated);
            if (row != null)
                SetSelectedRow(row);
            OnSelectAssetClicked();
        }

        protected override void OnRevealClicked()
        {
            string path = SelectedRow?.Soul != null ? NPCAuthoringEditorUtility.GetPrefabPath(SelectedRow.Soul) : null;
            if (!string.IsNullOrWhiteSpace(path))
                EditorUtility.RevealInFinder(path);
        }

        protected override void OnSelectAssetClicked()
        {
            if (SelectedRow?.Soul == null)
                return;
            Selection.activeObject = SelectedRow.Soul.gameObject;
            EditorGUIUtility.PingObject(SelectedRow.Soul.gameObject);
        }

        private static void RebuildRegistry()
        {
            NPCRegistry.ForceEditorSyncNow();
        }

        protected override void DoBulkDelete(IReadOnlyList<NPCRow> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                NPCSoul soul = rows[i]?.Soul;
                if (soul == null)
                    continue;
                string path = NPCAuthoringEditorUtility.GetPrefabPath(soul);
                if (!string.IsNullOrWhiteSpace(path))
                    AssetDatabase.DeleteAsset(path);
            }
            NPCRegistry.ScheduleEditorSync();
        }

        protected override void DoBulkDuplicate(IReadOnlyList<NPCRow> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                NPCSoul soul = rows[i]?.Soul;
                if (soul == null)
                    continue;
                NPCAuthoringEditorUtility.DuplicateNPCPrefab(soul);
            }
            NPCRegistry.ScheduleEditorSync();
        }

        // -- Row drawing ---------------------------------------------------------------

        protected override void DrawRow(Rect rowRect, NPCRow row)
        {
            DrawRowChrome(rowRect, IsSelected(row), () => HandleRowClick(row));
            DrawIcon(rowRect, row.Icon);

            Rect textRect = TextColumnRect(rowRect);
            Rect titleRect = new(textRect.x, rowRect.y + 5f, textRect.width, EditorGUIUtility.singleLineHeight);
            Rect metaRect = new(textRect.x, titleRect.yMax + 1f, textRect.width, EditorGUIUtility.singleLineHeight);
            Rect pathRect = new(textRect.x, metaRect.yMax + 1f, textRect.width, EditorGUIUtility.singleLineHeight);

            string displayName = string.IsNullOrWhiteSpace(row.CharacterName) ? "<unnamed>" : row.CharacterName;
            GUI.Label(titleRect, $"{displayName} ({row.OwnerId})", EditorStyles.boldLabel);
            string traderTag = row.HasTrader ? "  Trader" : string.Empty;
            string hostileTag = row.IsHostile ? "  Hostile" : string.Empty;
            string archetypeTag = row.Archetype != NPCArchetype.None ? $"  [{FormatArchetype(row.Archetype)}]" : string.Empty;
            GUI.Label(metaRect, $"{row.EntityKind}{traderTag}{hostileTag}{archetypeTag}", EditorStyles.miniLabel);
            GUI.Label(pathRect, row.PrefabPath, EditorStyles.miniLabel);
            DrawWarningBadge(rowRect, row.WarningCount);
        }

        // -- Detail --------------------------------------------------------------------

        protected override string EmptyDetailMessage => "Select an NPC prefab from the database list.";

        protected override void DrawDetail(NPCRow row)
        {
            NPCSoul soul = row.Soul;
            if (soul == null)
                return;

            EditorGUILayout.BeginHorizontal();
            string headerName = string.IsNullOrWhiteSpace(soul.CharacterName) ? soul.gameObject.name : soul.CharacterName;
            EditorGUILayout.LabelField(headerName, EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(soul.Archetype == NPCArchetype.None))
            {
                if (GUILayout.Button("Fill Missing Defaults", GUILayout.Width(140f)))
                    FillMissingDefaultsForSelected();
                if (GUILayout.Button("Reapply...", GUILayout.Width(84f)))
                    ReapplyArchetypeForSelected();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(NPCAuthoringEditorUtility.GetPrefabPath(soul), EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            SelectedSerializedObject.Update();
            NPCAuthoringDrawerUtility.DrawNPCInspector(SelectedSerializedObject, soul, showWarnings: false);
            if (SelectedSerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(soul);
                NPCRegistry.ScheduleEditorSync();
                RefreshRow(row);
            }

            DrawWarnings(soul);
        }

        private void DrawWarnings(NPCSoul soul)
        {
            List<NPCAuthoringWarning> warnings = GetWarnings(soul);
            for (int i = 0; i < warnings.Count; i++)
            {
                MessageType type = warnings[i].Severity switch
                {
                    NPCAuthoringWarningSeverity.Error => MessageType.Error,
                    NPCAuthoringWarningSeverity.Warning => MessageType.Warning,
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
            NPCRegistry registry = NPCRegistry.Get();
            List<NPCAuthoringWarning> registryWarnings = NPCAuthoringValidator.ValidateRegistry(registry);
            for (int i = 0; i < registryWarnings.Count; i++)
            {
                NPCAuthoringWarning warning = registryWarnings[i];
                issues.Add(new SolDatabaseIssue(MapIssueSeverity(warning.Severity), Tab, string.Empty, "NPC Registry", warning.Message, registry));
            }

            ItemRegistry itemRegistry = ItemRegistry.Get();
            for (int i = 0; i < Rows.Count; i++)
            {
                NPCSoul soul = Rows[i]?.Soul;
                if (soul == null)
                    continue;

                string label = string.IsNullOrWhiteSpace(soul.CharacterName) ? soul.gameObject.name : soul.CharacterName;
                List<NPCAuthoringWarning> warnings = GetWarnings(soul);
                for (int j = 0; j < warnings.Count; j++)
                {
                    NPCAuthoringWarning warning = warnings[j];
                    issues.Add(new SolDatabaseIssue(MapIssueSeverity(warning.Severity), Tab, soul.OwnerId, label, warning.Message, soul));
                }

                CollectInventorySeedIssues(soul, label, itemRegistry, issues);
            }

            return issues;
        }

        private void CollectInventorySeedIssues(NPCSoul soul, string label, ItemRegistry itemRegistry, List<SolDatabaseIssue> issues)
        {
            Sol.Inventory inventory = soul.GetComponent<Sol.Inventory>();
            if (inventory == null)
                return;

            SerializedObject invSO = new(inventory);
            SerializedProperty contents = invSO.FindProperty("_inspectorContents");
            if (contents == null)
                return;

            for (int i = 0; i < contents.arraySize; i++)
            {
                SerializedProperty element = contents.GetArrayElementAtIndex(i);
                SerializedProperty itemIdProp = element.FindPropertyRelative("_itemId");
                SerializedProperty quantityProp = element.FindPropertyRelative("Quantity");
                string itemId = itemIdProp?.stringValue;

                if (quantityProp != null && quantityProp.intValue <= 0)
                {
                    issues.Add(new SolDatabaseIssue(
                        SolDatabaseIssueSeverity.Warning,
                        Tab,
                        soul.OwnerId,
                        label,
                        $"Inventory seed item {i + 1} has non-positive quantity.",
                        soul));
                }

                if (string.IsNullOrWhiteSpace(itemId))
                    continue;

                if (itemRegistry != null && itemRegistry.GetPrefab(itemId) == null)
                {
                    issues.Add(new SolDatabaseIssue(
                        SolDatabaseIssueSeverity.Warning,
                        Tab,
                        soul.OwnerId,
                        label,
                        $"Inventory seed item '{itemId}' is not in the item registry.",
                        soul));
                }
            }
        }

        // -- Helpers -------------------------------------------------------------------

        private NPCRow FindRowBySoul(NPCSoul soul)
        {
            if (soul == null)
                return null;
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i]?.Soul == soul)
                    return Rows[i];
            }
            return null;
        }

        private List<NPCAuthoringWarning> GetWarnings(NPCSoul soul)
        {
            if (soul == null)
                return new List<NPCAuthoringWarning>();

            if (!_warningCache.TryGetValue(soul, out List<NPCAuthoringWarning> warnings))
            {
                warnings = NPCAuthoringValidator.Validate(soul);
                _warningCache[soul] = warnings;
            }

            return warnings;
        }

        private static Sprite GetSpeakerIcon(AI_NPC aiNpc)
        {
            if (aiNpc == null)
                return null;
            SerializedObject so = new(aiNpc);
            SerializedProperty prop = so.FindProperty("_speakerIcon");
            return prop?.objectReferenceValue as Sprite;
        }

        private static NPCArchetype DrawArchetypeToolbarPopup(NPCArchetype current, params GUILayoutOption[] options)
        {
            NPCArchetype[] archetypes =
            {
                NPCArchetype.None,
                NPCArchetype.Civilian,
                NPCArchetype.Guard,
                NPCArchetype.Bandit,
                NPCArchetype.QuestGiver,
                NPCArchetype.Unique
            };
            string[] labels = { "None", "Civilian", "Guard", "Bandit", "Quest Giver", "Unique" };

            int currentIndex = 0;
            for (int i = 0; i < archetypes.Length; i++)
            {
                if (archetypes[i] == current)
                {
                    currentIndex = i;
                    break;
                }
            }

            int picked = EditorGUILayout.Popup(currentIndex, labels, EditorStyles.toolbarPopup, options);
            return archetypes[Mathf.Clamp(picked, 0, archetypes.Length - 1)];
        }

        private static string FormatArchetype(NPCArchetype archetype)
        {
            return archetype == NPCArchetype.QuestGiver ? "Quest Giver" : archetype.ToString();
        }

        private void FillMissingDefaultsForSelected()
        {
            NPCSoul soul = SelectedRow?.Soul;
            if (soul == null || soul.Archetype == NPCArchetype.None)
                return;

            NPCAuthoringEditorUtility.FillMissingArchetypeDefaults(soul, soul.Archetype, soul.CharacterName);
            RefreshRow(SelectedRow);
            SelectedSerializedObject = new SerializedObject(soul);
            NPCRegistry.ScheduleEditorSync();
        }

        private void ReapplyArchetypeForSelected()
        {
            NPCSoul soul = SelectedRow?.Soul;
            if (soul == null || soul.Archetype == NPCArchetype.None)
                return;

            List<string> changes = NPCAuthoringEditorUtility.BuildArchetypeOverwritePreview(soul.Archetype);
            string message = "This will reapply the archetype defaults:\n\n- "
                + string.Join("\n- ", changes)
                + "\n\nUse Fill Missing Defaults for the non-destructive path.";

            bool confirmed = EditorUtility.DisplayDialog("Reapply NPC Archetype", message, "Apply", "Cancel");
            if (!confirmed)
                return;

            NPCAuthoringEditorUtility.ApplyArchetype(soul, soul.Archetype, soul.CharacterName);
            RefreshRow(SelectedRow);
            SelectedSerializedObject = new SerializedObject(soul);
            NPCRegistry.ScheduleEditorSync();
        }
    }
}
