using System.Collections.Generic;
using Sol.AI;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    public sealed class NPCDatabaseWindow : EditorWindow
    {
        internal enum NPCFilter
        {
            All,
            Trader,
            QuestGiver,
            Patroller,
            MissingAIConfig,
            HasWarnings
        }

        internal enum NPCSort
        {
            Name,
            OwnerId,
            SoulType
        }

        internal sealed class NPCDatabaseRow
        {
            public NPCSoul Soul;
            public string OwnerId;
            public string CharacterName;
            public SoulType SoulKind;
            public NPCAuthoringTemplate Template;
            public string PrefabPath;
            public string SearchText;
            public Texture Icon;
            public int WarningCount;
            public bool HasTrader;
            public bool MissingAIConfig;
        }

        private const float LeftPaneWidth = 380f;
        private const float RowHeight = 58f;
        private const float RowPadding = 4f;
        private const float IconSize = 32f;

        private readonly List<NPCSoul> _souls = new();
        private readonly List<NPCDatabaseRow> _rows = new();
        private readonly List<NPCDatabaseRow> _filteredRows = new();
        private readonly Dictionary<NPCSoul, List<NPCAuthoringWarning>> _warningCache = new();
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private string _search = string.Empty;
        private string _lastFilterSearch = null;
        private NPCFilter _filter = NPCFilter.All;
        private NPCFilter _lastFilter = (NPCFilter)(-1);
        private NPCSort _sort = NPCSort.Name;
        private NPCSort _lastSort = (NPCSort)(-1);
        private bool _filteredRowsDirty = true;
        private NPCAuthoringTemplate _newTemplate = NPCAuthoringTemplate.Civilian;
        private NPCSoul _selectedSoul;
        private SerializedObject _selectedSerializedObject;

        [MenuItem("Window/Sol/NPC Database")]
        public static void Open()
        {
            NPCDatabaseWindow window = GetWindow<NPCDatabaseWindow>("Sol NPC Database");
            window.minSize = new Vector2(900f, 520f);
            window.RefreshIndex();
        }

        public static void SelectByOwnerId(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                return;

            NPCDatabaseWindow window = GetWindow<NPCDatabaseWindow>("Sol NPC Database");
            window.minSize = new Vector2(900f, 520f);
            window.RefreshIndex();
            window.SelectOwnerInternal(ownerId.Trim());
            window.Repaint();
        }

        private void SelectOwnerInternal(string ownerId)
        {
            for (int i = 0; i < _souls.Count; i++)
            {
                NPCSoul soul = _souls[i];
                if (soul == null)
                    continue;
                if (string.Equals(soul.OwnerId, ownerId, System.StringComparison.OrdinalIgnoreCase))
                {
                    SelectSoul(soul);
                    return;
                }
            }
        }

        private void OnEnable()
        {
            RefreshIndex();
        }

        private void OnProjectChange()
        {
            RefreshIndex();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                CreateNewNPC();
            _newTemplate = (NPCAuthoringTemplate)EditorGUILayout.EnumPopup(_newTemplate, EditorStyles.toolbarPopup, GUILayout.Width(120f));
            using (new EditorGUI.DisabledScope(_selectedSoul == null))
            {
                if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(76f)))
                    DuplicateSelectedNPC();
                if (GUILayout.Button("Reveal Prefab", EditorStyles.toolbarButton, GUILayout.Width(92f)))
                    RevealSelectedPrefab();
                if (GUILayout.Button("Select", EditorStyles.toolbarButton, GUILayout.Width(54f)))
                    SelectCurrentPrefab();
            }
            if (GUILayout.Button("Refresh Index", EditorStyles.toolbarButton, GUILayout.Width(94f)))
                RefreshIndex();
            if (GUILayout.Button("Rebuild Registry", EditorStyles.toolbarButton, GUILayout.Width(108f)))
                RebuildRegistry();
            if (GUILayout.Button("Validate All", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                ValidateAll();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawLeftPane();
            DrawRightPane();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLeftPane()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftPaneWidth));

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _search = GUILayout.TextField(_search, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.MinWidth(120f));
            if (GUILayout.Button(GUIContent.none, GUI.skin.FindStyle("ToolbarSearchCancelButton")))
            {
                _search = string.Empty;
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

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
            DrawVirtualizedRows();
            EditorGUILayout.EndVertical();
        }

        private void DrawVirtualizedRows()
        {
            float viewportHeight = Mathf.Max(120f, position.height - 78f);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

            if (_filteredRows.Count == 0)
            {
                EditorGUILayout.HelpBox("No NPCs match the current filters.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            int firstRow = Mathf.Clamp(Mathf.FloorToInt(_listScroll.y / RowHeight), 0, Mathf.Max(0, _filteredRows.Count - 1));
            int visibleCount = Mathf.CeilToInt(viewportHeight / RowHeight) + 2;
            int lastRowExclusive = Mathf.Min(_filteredRows.Count, firstRow + visibleCount);

            float topSpace = firstRow * RowHeight;
            float bottomSpace = (_filteredRows.Count - lastRowExclusive) * RowHeight;
            if (topSpace > 0f)
                GUILayout.Space(topSpace);

            for (int i = firstRow; i < lastRowExclusive; i++)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, RowHeight);
                DrawNPCRow(rowRect, _filteredRows[i]);
            }

            if (bottomSpace > 0f)
                GUILayout.Space(bottomSpace);

            EditorGUILayout.EndScrollView();
        }

        private void DrawNPCRow(Rect rowRect, NPCDatabaseRow row)
        {
            Event evt = Event.current;
            bool selected = row.Soul == _selectedSoul;
            bool hover = rowRect.Contains(evt.mousePosition);
            if (selected)
                EditorGUI.DrawRect(rowRect, new Color(0.22f, 0.44f, 0.68f, 0.28f));
            else if (hover)
                EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.06f));

            if (evt.type == EventType.MouseDown && evt.button == 0 && rowRect.Contains(evt.mousePosition))
            {
                SelectSoul(row.Soul);
                evt.Use();
            }

            Rect iconRect = new(rowRect.x + RowPadding, rowRect.y + (RowHeight - IconSize) * 0.5f, IconSize, IconSize);
            if (row.Icon != null)
                GUI.DrawTexture(iconRect, row.Icon, ScaleMode.ScaleToFit);
            else
                EditorGUI.DrawRect(iconRect, new Color(0f, 0f, 0f, 0.18f));

            float textX = iconRect.xMax + RowPadding;
            Rect titleRect = new(textX, rowRect.y + 5f, rowRect.width - textX - 44f, EditorGUIUtility.singleLineHeight);
            Rect metaRect = new(textX, titleRect.yMax + 1f, titleRect.width, EditorGUIUtility.singleLineHeight);
            Rect pathRect = new(textX, metaRect.yMax + 1f, titleRect.width, EditorGUIUtility.singleLineHeight);

            string displayName = string.IsNullOrWhiteSpace(row.CharacterName) ? "<unnamed>" : row.CharacterName;
            GUI.Label(titleRect, $"{displayName} ({row.OwnerId})", EditorStyles.boldLabel);
            string traderTag = row.HasTrader ? "  Trader" : string.Empty;
            string templateTag = row.Template != NPCAuthoringTemplate.None ? $"  [{row.Template}]" : string.Empty;
            GUI.Label(metaRect, $"{row.SoulKind}{traderTag}{templateTag}", EditorStyles.miniLabel);
            GUI.Label(pathRect, row.PrefabPath, EditorStyles.miniLabel);

            if (row.WarningCount > 0)
            {
                Rect warningRect = new(rowRect.xMax - 38f, rowRect.y + 6f, 34f, EditorGUIUtility.singleLineHeight);
                GUI.Label(warningRect, $"! {row.WarningCount}", EditorStyles.miniBoldLabel);
            }
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

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            EditorGUILayout.BeginHorizontal();
            string headerName = string.IsNullOrWhiteSpace(_selectedSoul.CharacterName)
                ? _selectedSoul.gameObject.name
                : _selectedSoul.CharacterName;
            EditorGUILayout.LabelField(headerName, EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(_selectedSoul.AuthoringTemplate == NPCAuthoringTemplate.None))
            {
                if (GUILayout.Button("Fill Missing Defaults", GUILayout.Width(140f)))
                    FillMissingDefaultsForSelected();

                if (GUILayout.Button("Reapply...", GUILayout.Width(84f)))
                    ReapplyTemplateForSelected();
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

        private void RefreshIndex()
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

        private NPCDatabaseRow BuildRow(NPCSoul soul)
        {
            string path = NPCAuthoringEditorUtility.GetPrefabPath(soul);
            List<NPCAuthoringWarning> warnings = GetWarnings(soul);
            NpcTrader trader = soul.GetComponent<NpcTrader>();
            AI_NPC aiNpc = soul.GetComponent<AI_NPC>();
            Sprite portrait = trader != null ? GetSpeakerIcon(trader) : null;
            Texture icon = portrait != null
                ? AssetPreview.GetAssetPreview(portrait)
                : AssetDatabase.GetCachedIcon(path);
            string name = string.IsNullOrWhiteSpace(soul.CharacterName) ? soul.gameObject.name : soul.CharacterName.Trim();
            string ownerId = string.IsNullOrWhiteSpace(soul.OwnerId) ? "<missing>" : soul.OwnerId.Trim();

            return new NPCDatabaseRow
            {
                Soul = soul,
                OwnerId = ownerId,
                CharacterName = name,
                SoulKind = soul.SoulKind,
                Template = soul.AuthoringTemplate,
                PrefabPath = path,
                SearchText = $"{name} {ownerId} {soul.SoulKind} {path}".ToLowerInvariant(),
                Icon = icon,
                WarningCount = warnings.Count,
                HasTrader = trader != null,
                MissingAIConfig = aiNpc != null && aiNpc.Config == null
            };
        }

        private static Sprite GetSpeakerIcon(NpcTrader trader)
        {
            if (trader == null)
                return null;
            SerializedObject so = new(trader);
            SerializedProperty prop = so.FindProperty("_speakerIcon");
            return prop?.objectReferenceValue as Sprite;
        }

        private void RefreshRow(NPCSoul soul)
        {
            if (soul == null)
                return;

            InvalidateWarningCache(soul);
            NPCDatabaseRow row = BuildRow(soul);
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

        internal static bool RowMatchesFilters(NPCDatabaseRow row, string search, NPCFilter filter)
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
                NPCFilter.Trader => row.HasTrader && row.Template != NPCAuthoringTemplate.QuestGiver,
                NPCFilter.QuestGiver => row.Template == NPCAuthoringTemplate.QuestGiver,
                NPCFilter.Patroller => row.Template == NPCAuthoringTemplate.Patroller,
                NPCFilter.MissingAIConfig => row.MissingAIConfig,
                NPCFilter.HasWarnings => row.WarningCount > 0,
                _ => true
            };
        }

        private void RebuildFilteredRowsIfNeeded()
        {
            if (!_filteredRowsDirty
                && string.Equals(_lastFilterSearch, _search, System.StringComparison.Ordinal)
                && _lastFilter == _filter
                && _lastSort == _sort)
            {
                return;
            }

            _filteredRows.Clear();
            for (int i = 0; i < _rows.Count; i++)
            {
                NPCDatabaseRow row = _rows[i];
                if (RowMatchesFilters(row, _search, _filter))
                    _filteredRows.Add(row);
            }

            SortFilteredRows(_sort);

            _lastFilterSearch = _search;
            _lastFilter = _filter;
            _lastSort = _sort;
            _filteredRowsDirty = false;
            float maxScrollY = Mathf.Max(0f, _filteredRows.Count * RowHeight - Mathf.Max(120f, position.height - 78f));
            _listScroll.y = Mathf.Min(_listScroll.y, maxScrollY);
        }

        private void SortFilteredRows(NPCSort sort)
        {
            switch (sort)
            {
                case NPCSort.OwnerId:
                    _filteredRows.Sort((a, b) => System.StringComparer.OrdinalIgnoreCase.Compare(a.OwnerId, b.OwnerId));
                    break;
                case NPCSort.SoulType:
                    _filteredRows.Sort((a, b) =>
                    {
                        int cmp = a.SoulKind.CompareTo(b.SoulKind);
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
            RefreshSelectedSerializedObject();
            Repaint();
        }

        private void RefreshSelectedSerializedObject()
        {
            _selectedSerializedObject = _selectedSoul != null ? new SerializedObject(_selectedSoul) : null;
        }

        private void CreateNewNPC()
        {
            NPCSoul created = NPCAuthoringEditorUtility.CreateNPCPrefab(_newTemplate);
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

        private void ValidateAll()
        {
            InvalidateWarningCache();
            int warningCount = 0;
            NPCRegistry registry = NPCRegistry.Get();
            List<NPCAuthoringWarning> registryWarnings = NPCAuthoringValidator.ValidateRegistry(registry);
            for (int i = 0; i < registryWarnings.Count; i++)
            {
                Debug.LogWarning($"[NPC Database] {registryWarnings[i].Message}");
                warningCount++;
            }

            for (int i = 0; i < _souls.Count; i++)
            {
                NPCSoul soul = _souls[i];
                List<NPCAuthoringWarning> warnings = GetWarnings(soul);
                for (int j = 0; j < warnings.Count; j++)
                {
                    string label = string.IsNullOrWhiteSpace(soul.CharacterName) ? soul.gameObject.name : soul.CharacterName;
                    Debug.LogWarning($"[NPC Database] {label} ({soul.OwnerId}): {warnings[j].Message}", soul);
                    warningCount++;
                }
            }

            RefreshIndex();
            string message = warningCount == 0
                ? "No NPC authoring warnings found."
                : $"Found {warningCount} NPC authoring warning(s). See Console for details.";
            EditorUtility.DisplayDialog("Validate NPC Database", message, "OK");
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
            if (_selectedSoul == null || _selectedSoul.AuthoringTemplate == NPCAuthoringTemplate.None)
                return;

            NPCAuthoringEditorUtility.FillMissingTemplateDefaults(
                _selectedSoul,
                _selectedSoul.AuthoringTemplate,
                _selectedSoul.CharacterName);
            RefreshRow(_selectedSoul);
            RefreshSelectedSerializedObject();
            NPCRegistry.ScheduleEditorSync();
        }

        private void ReapplyTemplateForSelected()
        {
            if (_selectedSoul == null || _selectedSoul.AuthoringTemplate == NPCAuthoringTemplate.None)
                return;

            List<string> changes = NPCAuthoringEditorUtility.BuildTemplateOverwritePreview(_selectedSoul.AuthoringTemplate);
            string message = "This will reapply the template defaults:\n\n- "
                + string.Join("\n- ", changes)
                + "\n\nUse Fill Missing Defaults for the non-destructive path.";

            bool confirmed = EditorUtility.DisplayDialog(
                "Reapply NPC Template",
                message,
                "Apply",
                "Cancel");
            if (!confirmed)
                return;

            NPCAuthoringEditorUtility.ApplyTemplate(_selectedSoul, _selectedSoul.AuthoringTemplate, _selectedSoul.CharacterName);
            RefreshRow(_selectedSoul);
            RefreshSelectedSerializedObject();
            NPCRegistry.ScheduleEditorSync();
        }
    }
}
