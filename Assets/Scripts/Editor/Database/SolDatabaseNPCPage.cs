using System.Collections.Generic;
using Sol.AI;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    internal sealed class SolDatabaseNPCPage : SolDatabasePageBase
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

        private readonly List<NPCSoul> _souls = new();
        private readonly List<NPCRow> _rows = new();
        private readonly List<NPCRow> _filteredRows = new();
        private readonly Dictionary<NPCSoul, List<NPCAuthoringWarning>> _warningCache = new();

        private string _lastFilterSearch = null;
        private NPCFilter _filter = NPCFilter.All;
        private NPCFilter _lastFilter = (NPCFilter)(-1);
        private NPCSort _sort = NPCSort.Name;
        private NPCSort _lastSort = (NPCSort)(-1);
        private bool _filteredRowsDirty = true;
        private NPCArchetype _newArchetype = NPCArchetype.Civilian;
        private NPCSoul _selectedSoul;
        private SerializedObject _selectedSerializedObject;

        public override SolDatabaseTab Tab => SolDatabaseTab.NPCs;
        public override string DisplayName => "NPCs";

        public override void DrawToolbar()
        {
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                CreateNewNPC();
            _newArchetype = DrawArchetypeToolbarPopup(_newArchetype, GUILayout.Width(120f));
            using (new EditorGUI.DisabledScope(_selectedSoul == null))
            {
                if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(76f)))
                    DuplicateSelectedNPC();
                if (GUILayout.Button("Reveal Prefab", EditorStyles.toolbarButton, GUILayout.Width(92f)))
                    RevealSelectedPrefab();
                if (GUILayout.Button("Select", EditorStyles.toolbarButton, GUILayout.Width(54f)))
                    SelectCurrentPrefab();
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
            _souls.Clear();
            _rows.Clear();
            InvalidateWarningCache();
            NPCRegistry registry = NPCRegistry.Get();
            if (registry?.Entries != null)
            {
                HashSet<NPCSoul> seen = new();
                for (int i = 0; i < registry.Entries.Count; i++)
                {
                    NPCSoul soul = registry.Entries[i]?.Prefab;
                    if (soul == null || !seen.Add(soul))
                        continue;

                    _souls.Add(soul);
                    _rows.Add(BuildRow(soul));
                }
            }

            _filteredRowsDirty = true;
            RebuildFilteredRowsIfNeeded();
            if (_selectedSoul != null && !_souls.Contains(_selectedSoul))
                SelectSoul(null);
        }

        public override bool SelectById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            RefreshIndex();
            for (int i = 0; i < _souls.Count; i++)
            {
                NPCSoul soul = _souls[i];
                if (soul == null)
                    continue;
                if (string.Equals(soul.OwnerId, id, System.StringComparison.OrdinalIgnoreCase))
                {
                    SelectSoul(soul);
                    return true;
                }
            }

            return false;
        }

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
            for (int i = 0; i < _souls.Count; i++)
            {
                NPCSoul soul = _souls[i];
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

        private void DrawLeftPane()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(DefaultLeftPaneWidth));

            DrawSearchField(() => _filteredRowsDirty = true);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            _filter = (NPCFilter)EditorGUILayout.EnumPopup(_filter);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Sort", GUILayout.Width(32f));
            _sort = (NPCSort)EditorGUILayout.EnumPopup(_sort);
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
                _filteredRowsDirty = true;

            RebuildFilteredRowsIfNeeded();
            DrawVirtualizedRows(_filteredRows.Count, "No NPCs match the current filters.", (rect, index) => DrawNPCRow(rect, _filteredRows[index]));
            EditorGUILayout.EndVertical();
        }

        private void DrawNPCRow(Rect rowRect, NPCRow row)
        {
            DrawRowChrome(rowRect, row.Soul == _selectedSoul, () => SelectSoul(row.Soul));
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

        private void DrawRightPane()
        {
            EditorGUILayout.BeginVertical();
            if (_selectedSoul == null)
            {
                EditorGUILayout.HelpBox("Select an NPC prefab from the database list.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            if (_selectedSerializedObject == null || _selectedSerializedObject.targetObject != _selectedSoul)
                _selectedSerializedObject = new SerializedObject(_selectedSoul);

            DetailScroll = EditorGUILayout.BeginScrollView(DetailScroll);
            EditorGUILayout.BeginHorizontal();
            string headerName = string.IsNullOrWhiteSpace(_selectedSoul.CharacterName)
                ? _selectedSoul.gameObject.name
                : _selectedSoul.CharacterName;
            EditorGUILayout.LabelField(headerName, EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(_selectedSoul.Archetype == NPCArchetype.None))
            {
                if (GUILayout.Button("Fill Missing Defaults", GUILayout.Width(140f)))
                    FillMissingDefaultsForSelected();
                if (GUILayout.Button("Reapply...", GUILayout.Width(84f)))
                    ReapplyArchetypeForSelected();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(NPCAuthoringEditorUtility.GetPrefabPath(_selectedSoul), EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            _selectedSerializedObject.Update();
            NPCAuthoringDrawerUtility.DrawNPCInspector(_selectedSerializedObject, _selectedSoul);
            if (_selectedSerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_selectedSoul);
                NPCRegistry.ScheduleEditorSync();
                RefreshRow(_selectedSoul);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
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

        private static Sprite GetSpeakerIcon(AI_NPC aiNpc)
        {
            if (aiNpc == null)
                return null;
            SerializedObject so = new(aiNpc);
            SerializedProperty prop = so.FindProperty("_speakerIcon");
            return prop?.objectReferenceValue as Sprite;
        }

        private void RefreshRow(NPCSoul soul)
        {
            if (soul == null)
                return;

            InvalidateWarningCache(soul);
            NPCRow row = BuildRow(soul);
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Soul != soul)
                    continue;

                _rows[i] = row;
                _filteredRowsDirty = true;
                return;
            }

            _souls.Add(soul);
            _rows.Add(row);
            _filteredRowsDirty = true;
        }

        internal static bool RowMatchesFilters(NPCRow row, string search, NPCFilter filter)
        {
            if (row == null || row.Soul == null)
                return false;

            if (!string.IsNullOrWhiteSpace(search))
            {
                string needle = search.Trim().ToLowerInvariant();
                if (row.SearchText == null || row.SearchText.IndexOf(needle, System.StringComparison.Ordinal) < 0)
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
                NPCRow row = _rows[i];
                if (RowMatchesFilters(row, Search, _filter))
                    _filteredRows.Add(row);
            }

            SortFilteredRows(_sort);
            _lastFilterSearch = Search;
            _lastFilter = _filter;
            _lastSort = _sort;
            _filteredRowsDirty = false;
        }

        private void SortFilteredRows(NPCSort sort)
        {
            switch (sort)
            {
                case NPCSort.OwnerId:
                    _filteredRows.Sort((a, b) => System.StringComparer.OrdinalIgnoreCase.Compare(a.OwnerId, b.OwnerId));
                    break;
                case NPCSort.EntityType:
                    _filteredRows.Sort((a, b) =>
                    {
                        int cmp = a.EntityKind.CompareTo(b.EntityKind);
                        return cmp != 0 ? cmp : System.StringComparer.OrdinalIgnoreCase.Compare(a.CharacterName, b.CharacterName);
                    });
                    break;
                default:
                    _filteredRows.Sort((a, b) => System.StringComparer.OrdinalIgnoreCase.Compare(a.CharacterName, b.CharacterName));
                    break;
            }
        }

        private void SelectSoul(NPCSoul soul)
        {
            _selectedSoul = soul;
            _selectedSerializedObject = _selectedSoul != null ? new SerializedObject(_selectedSoul) : null;
            Window?.Repaint();
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

        private void CreateNewNPC()
        {
            NPCSoul created = NPCAuthoringEditorUtility.CreateNPCPrefab(_newArchetype);
            RefreshIndex();
            SelectSoul(created);
            SelectCurrentPrefab();
        }

        private void DuplicateSelectedNPC()
        {
            NPCSoul duplicated = NPCAuthoringEditorUtility.DuplicateNPCPrefab(_selectedSoul);
            RefreshIndex();
            SelectSoul(duplicated);
            SelectCurrentPrefab();
        }

        private void RevealSelectedPrefab()
        {
            string path = NPCAuthoringEditorUtility.GetPrefabPath(_selectedSoul);
            if (!string.IsNullOrWhiteSpace(path))
                EditorUtility.RevealInFinder(path);
        }

        private void SelectCurrentPrefab()
        {
            if (_selectedSoul == null)
                return;

            Selection.activeObject = _selectedSoul.gameObject;
            EditorGUIUtility.PingObject(_selectedSoul.gameObject);
        }

        private void RebuildRegistry()
        {
            NPCRegistry.ForceEditorSyncNow();
            RefreshIndex();
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

        private void InvalidateWarningCache()
        {
            _warningCache.Clear();
        }

        private void InvalidateWarningCache(NPCSoul soul)
        {
            if (soul != null)
                _warningCache.Remove(soul);
        }

        private void FillMissingDefaultsForSelected()
        {
            if (_selectedSoul == null || _selectedSoul.Archetype == NPCArchetype.None)
                return;

            NPCAuthoringEditorUtility.FillMissingArchetypeDefaults(_selectedSoul, _selectedSoul.Archetype, _selectedSoul.CharacterName);
            RefreshRow(_selectedSoul);
            _selectedSerializedObject = new SerializedObject(_selectedSoul);
            NPCRegistry.ScheduleEditorSync();
        }

        private void ReapplyArchetypeForSelected()
        {
            if (_selectedSoul == null || _selectedSoul.Archetype == NPCArchetype.None)
                return;

            List<string> changes = NPCAuthoringEditorUtility.BuildArchetypeOverwritePreview(_selectedSoul.Archetype);
            string message = "This will reapply the archetype defaults:\n\n- "
                + string.Join("\n- ", changes)
                + "\n\nUse Fill Missing Defaults for the non-destructive path.";

            bool confirmed = EditorUtility.DisplayDialog("Reapply NPC Archetype", message, "Apply", "Cancel");
            if (!confirmed)
                return;

            NPCAuthoringEditorUtility.ApplyArchetype(_selectedSoul, _selectedSoul.Archetype, _selectedSoul.CharacterName);
            RefreshRow(_selectedSoul);
            _selectedSerializedObject = new SerializedObject(_selectedSoul);
            NPCRegistry.ScheduleEditorSync();
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
    }
}
