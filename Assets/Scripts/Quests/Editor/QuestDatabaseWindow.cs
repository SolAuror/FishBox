using System.Collections.Generic;
using Sol.AI;
using Sol.Quests;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    public sealed class QuestDatabaseWindow : EditorWindow
    {
        internal enum QuestFilter
        {
            All,
            AutoOffer,
            Repeatable,
            Timed,
            Untimed,
            MissingGiver,
            HasWarnings,
            OrphanedPrereqs
        }

        internal enum QuestSort
        {
            QuestId,
            Title,
            GiverName
        }

        internal sealed class QuestDatabaseRow
        {
            public QuestDefinition Quest;
            public string QuestId;
            public string Title;
            public string GiverName;
            public string GiverOwnerId;
            public int ObjectiveCount;
            public bool AutoOffer;
            public bool Repeatable;
            public bool IsTimed;
            public bool MissingGiver;
            public bool HasOrphanedPrereqs;
            public string AssetPath;
            public string SearchText;
            public Texture Icon;
            public int WarningCount;
        }

        private const float LeftPaneWidth = 340f;
        private const float FlowPaneWidth = 340f;
        private const float RowHeight = 58f;
        private const float RowPadding = 4f;
        private const float IconSize = 32f;

        private readonly List<QuestDefinition> _quests = new();
        private readonly List<QuestDatabaseRow> _rows = new();
        private readonly List<QuestDatabaseRow> _filteredRows = new();
        private readonly Dictionary<QuestDefinition, List<QuestAuthoringWarning>> _warningCache = new();
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private Vector2 _flowScroll;
        private string _search = string.Empty;
        private string _lastFilterSearch = null;
        private QuestFilter _filter = QuestFilter.All;
        private QuestFilter _lastFilter = (QuestFilter)(-1);
        private QuestSort _sort = QuestSort.QuestId;
        private QuestSort _lastSort = (QuestSort)(-1);
        private bool _filteredRowsDirty = true;
        private QuestAuthoringTemplate _newTemplate = QuestAuthoringTemplate.FishCatch;
        private QuestDefinition _selectedQuest;
        private SerializedObject _selectedSerializedObject;
        private bool _showFlowPanel = true;

        [MenuItem("Window/Sol/Quest Database")]
        public static void Open()
        {
            QuestDatabaseWindow window = GetWindow<QuestDatabaseWindow>("Sol Quest Database");
            window.minSize = new Vector2(1100f, 560f);
            window.RefreshIndex();
        }

        public static void SelectByQuestId(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
                return;

            QuestDatabaseWindow window = GetWindow<QuestDatabaseWindow>("Sol Quest Database");
            window.minSize = new Vector2(1100f, 560f);
            window.RefreshIndex();
            window.SelectQuestIdInternal(questId.Trim());
            window.Repaint();
        }

        private void SelectQuestIdInternal(string questId)
        {
            for (int i = 0; i < _quests.Count; i++)
            {
                QuestDefinition quest = _quests[i];
                if (quest == null)
                    continue;
                if (string.Equals(quest.QuestId, questId, System.StringComparison.OrdinalIgnoreCase))
                {
                    SelectQuest(quest);
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
                CreateNewQuest();
            _newTemplate = (QuestAuthoringTemplate)EditorGUILayout.EnumPopup(_newTemplate, EditorStyles.toolbarPopup, GUILayout.Width(120f));
            using (new EditorGUI.DisabledScope(_selectedQuest == null))
            {
                if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(76f)))
                    DuplicateSelectedQuest();
                if (GUILayout.Button("Reveal Asset", EditorStyles.toolbarButton, GUILayout.Width(92f)))
                    RevealSelectedAsset();
                if (GUILayout.Button("Select", EditorStyles.toolbarButton, GUILayout.Width(54f)))
                    SelectCurrentAsset();
            }
            if (GUILayout.Button("Refresh Index", EditorStyles.toolbarButton, GUILayout.Width(94f)))
                RefreshIndex();
            if (GUILayout.Button("Rebuild Registry", EditorStyles.toolbarButton, GUILayout.Width(108f)))
                RebuildRegistry();
            if (GUILayout.Button("Validate All", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                ValidateAll();
            GUILayout.FlexibleSpace();
            _showFlowPanel = GUILayout.Toggle(_showFlowPanel, "Flow", EditorStyles.toolbarButton, GUILayout.Width(48f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawLeftPane();
            DrawCenterPane();
            if (_showFlowPanel)
                DrawFlowPane();
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
            _filter = (QuestFilter)EditorGUILayout.EnumPopup(_filter);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Sort", GUILayout.Width(32f));
            _sort = (QuestSort)EditorGUILayout.EnumPopup(_sort);
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
                EditorGUILayout.HelpBox("No quests match the current filters.", MessageType.Info);
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
                DrawQuestRow(rowRect, _filteredRows[i]);
            }

            if (bottomSpace > 0f)
                GUILayout.Space(bottomSpace);

            EditorGUILayout.EndScrollView();
        }

        private void DrawQuestRow(Rect rowRect, QuestDatabaseRow row)
        {
            Event evt = Event.current;
            bool selected = row.Quest == _selectedQuest;
            bool hover = rowRect.Contains(evt.mousePosition);
            if (selected)
                EditorGUI.DrawRect(rowRect, new Color(0.22f, 0.44f, 0.68f, 0.28f));
            else if (hover)
                EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.06f));

            if (evt.type == EventType.MouseDown && evt.button == 0 && rowRect.Contains(evt.mousePosition))
            {
                SelectQuest(row.Quest);
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

            string title = string.IsNullOrWhiteSpace(row.Title) ? "<untitled>" : row.Title;
            GUI.Label(titleRect, $"{title} ({row.QuestId})", EditorStyles.boldLabel);
            string flags = BuildFlagSummary(row);
            GUI.Label(metaRect, $"{row.GiverName}  |  {row.ObjectiveCount} obj{flags}", EditorStyles.miniLabel);
            GUI.Label(pathRect, row.AssetPath, EditorStyles.miniLabel);

            if (row.WarningCount > 0)
            {
                Rect warningRect = new(rowRect.xMax - 38f, rowRect.y + 6f, 34f, EditorGUIUtility.singleLineHeight);
                GUI.Label(warningRect, $"! {row.WarningCount}", EditorStyles.miniBoldLabel);
            }
        }

        private static string BuildFlagSummary(QuestDatabaseRow row)
        {
            string flags = string.Empty;
            if (row.AutoOffer) flags += "  [Auto]";
            if (row.Repeatable) flags += "  [Repeat]";
            if (row.IsTimed) flags += "  [Timed]";
            return flags;
        }

        private void DrawCenterPane()
        {
            EditorGUILayout.BeginVertical();
            if (_selectedQuest == null)
            {
                EditorGUILayout.HelpBox("Select a quest from the database list.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            if (_selectedSerializedObject == null || _selectedSerializedObject.targetObject != _selectedQuest)
                _selectedSerializedObject = new SerializedObject(_selectedQuest);

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            EditorGUILayout.BeginHorizontal();
            string headerName = string.IsNullOrWhiteSpace(_selectedQuest.Title) ? _selectedQuest.name : _selectedQuest.Title;
            EditorGUILayout.LabelField(headerName, EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(_selectedQuest.AuthoringTemplate == QuestAuthoringTemplate.None))
            {
                if (GUILayout.Button("Reapply...", GUILayout.Width(84f)))
                    ReapplyTemplateForSelected();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(QuestAuthoringEditorUtility.GetAssetPath(_selectedQuest), EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            _selectedSerializedObject.Update();
            QuestAuthoringDrawerUtility.DrawQuestInspector(_selectedSerializedObject, _selectedQuest);
            if (_selectedSerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_selectedQuest);
                QuestRegistry.ScheduleEditorSync();
                RefreshRow(_selectedQuest);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawFlowPane()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(FlowPaneWidth));
            EditorGUILayout.LabelField("Flow", EditorStyles.boldLabel);
            if (_selectedQuest == null)
            {
                EditorGUILayout.HelpBox("Select a quest to inspect prerequisite chain and objective timeline.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            _flowScroll = EditorGUILayout.BeginScrollView(_flowScroll);
            DrawPrerequisiteChain(_selectedQuest);
            EditorGUILayout.Space(8f);
            DrawObjectiveTimeline(_selectedQuest);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawPrerequisiteChain(QuestDefinition quest)
        {
            EditorGUILayout.LabelField("Prerequisite chain", EditorStyles.boldLabel);
            QuestRegistry registry = QuestRegistry.Get();
            bool cycle = QuestAuthoringValidator.HasPrerequisiteCycle(quest);
            if (cycle)
                EditorGUILayout.HelpBox("Cycle detected — chain rendering is truncated.", MessageType.Error);

            HashSet<string> walked = new(System.StringComparer.OrdinalIgnoreCase);
            DrawUpstream(quest, registry, walked, depth: 0, cycle);

            walked.Clear();
            walked.Add(quest.QuestId ?? string.Empty);
            DrawSelfNode(quest);

            HashSet<string> downstreamWalked = new(System.StringComparer.OrdinalIgnoreCase) { quest.QuestId ?? string.Empty };
            DrawDownstream(quest, registry, downstreamWalked, depth: 0);
        }

        private void DrawUpstream(QuestDefinition quest, QuestRegistry registry, HashSet<string> walked, int depth, bool cycleDetected)
        {
            if (quest == null || registry == null)
                return;
            IReadOnlyList<string> prereqs = quest.PrerequisiteQuestIds;
            if (prereqs == null)
                return;

            for (int i = 0; i < prereqs.Count; i++)
            {
                string prereqId = prereqs[i];
                if (string.IsNullOrWhiteSpace(prereqId))
                    continue;
                if (cycleDetected && !walked.Add(prereqId))
                {
                    DrawCycleNode(prereqId, depth);
                    continue;
                }

                QuestDefinition prereq = registry.Find(prereqId);
                if (prereq == null)
                {
                    DrawMissingNode(prereqId, depth, "missing prereq");
                    continue;
                }

                DrawUpstream(prereq, registry, walked, depth + 1, cycleDetected);
                DrawNode(prereq, depth, "prereq");
            }
        }

        private void DrawDownstream(QuestDefinition quest, QuestRegistry registry, HashSet<string> walked, int depth)
        {
            if (registry == null)
                return;
            IReadOnlyList<QuestDefinition> all = registry.Quests;
            if (all == null)
                return;

            for (int i = 0; i < all.Count; i++)
            {
                QuestDefinition candidate = all[i];
                if (candidate == null || candidate == quest)
                    continue;
                IReadOnlyList<string> prereqs = candidate.PrerequisiteQuestIds;
                if (prereqs == null)
                    continue;

                bool referencesUs = false;
                for (int j = 0; j < prereqs.Count; j++)
                {
                    if (string.Equals(prereqs[j], quest.QuestId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        referencesUs = true;
                        break;
                    }
                }
                if (!referencesUs)
                    continue;

                if (!walked.Add(candidate.QuestId ?? string.Empty))
                {
                    DrawCycleNode(candidate.QuestId, depth);
                    continue;
                }

                DrawNode(candidate, depth, "downstream");
                DrawDownstream(candidate, registry, walked, depth + 1);
            }
        }

        private void DrawSelfNode(QuestDefinition quest)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(4f);
            GUI.backgroundColor = new Color(0.4f, 0.6f, 0.9f, 0.6f);
            GUILayout.Label($"● {quest.QuestId}  {quest.Title}", EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNode(QuestDefinition quest, int depth, string label)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8f + depth * 12f);
            string title = string.IsNullOrWhiteSpace(quest.Title) ? quest.name : quest.Title;
            if (GUILayout.Button($"{label}: {quest.QuestId}  {title}", EditorStyles.miniButton))
                SelectByQuestId(quest.QuestId);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMissingNode(string questId, int depth, string label)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8f + depth * 12f);
            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f, 0.5f);
            GUILayout.Label($"{label}: {questId} (not found)", EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCycleNode(string questId, int depth)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8f + depth * 12f);
            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f, 0.6f);
            GUILayout.Label($"↺ {questId} (cycle)", EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawObjectiveTimeline(QuestDefinition quest)
        {
            EditorGUILayout.LabelField("Objective timeline", EditorStyles.boldLabel);
            IReadOnlyList<QuestObjective> objectives = quest.Objectives;
            if (objectives == null || objectives.Count == 0)
            {
                EditorGUILayout.HelpBox("No objectives.", MessageType.Info);
                return;
            }

            for (int i = 0; i < objectives.Count; i++)
            {
                QuestObjective objective = objectives[i];
                if (objective == null)
                    continue;
                string summary = SummarizeObjective(objective);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button($"{i + 1}. {summary}", EditorStyles.miniButton))
                    QuestAuthoringDrawerUtility.FocusObjective(i);
                EditorGUILayout.EndHorizontal();
            }
        }

        private static string SummarizeObjective(QuestObjective objective)
        {
            string display = string.IsNullOrWhiteSpace(objective.DisplayText) ? null : objective.DisplayText.Trim();
            switch (objective.Type)
            {
                case QuestObjectiveType.CatchCount:
                    return display ?? $"Catch {objective.Count}";
                case QuestObjectiveType.CatchTotalValue:
                    return display ?? $"Catch total value {objective.GoldAmount}g";
                case QuestObjectiveType.CatchRarity:
                    return display ?? $"Catch {objective.Count} {objective.Rarity}";
                case QuestObjectiveType.CatchByPrefix:
                    return display ?? "Catch by prefix";
                case QuestObjectiveType.CollectItem:
                    return display ?? $"Collect {objective.Count} item(s)";
                case QuestObjectiveType.DeliverItem:
                    return display ?? $"Deliver to {objective.NpcName}";
                case QuestObjectiveType.TalkToNpc:
                    return display ?? $"Talk to {objective.NpcName}";
                case QuestObjectiveType.EquipItem:
                    return display ?? "Equip item";
                default:
                    return display ?? objective.Type.ToString();
            }
        }

        private void RefreshIndex()
        {
            _quests.Clear();
            _rows.Clear();
            InvalidateWarningCache();
            QuestRegistry registry = QuestRegistry.Get();
            if (registry?.Quests != null)
            {
                HashSet<QuestDefinition> seen = new();
                for (int i = 0; i < registry.Quests.Count; i++)
                {
                    QuestDefinition quest = registry.Quests[i];
                    if (quest == null || !seen.Add(quest))
                        continue;
                    _quests.Add(quest);
                    _rows.Add(BuildRow(quest));
                }
            }

            _filteredRowsDirty = true;
            RebuildFilteredRowsIfNeeded();
            if (_selectedQuest != null && !_quests.Contains(_selectedQuest))
                SelectQuest(null);
        }

        private QuestDatabaseRow BuildRow(QuestDefinition quest)
        {
            string path = QuestAuthoringEditorUtility.GetAssetPath(quest);
            List<QuestAuthoringWarning> warnings = GetWarnings(quest);
            NPCRegistry npcRegistry = NPCRegistry.Get();
            NPCSoul giverPrefab = !string.IsNullOrWhiteSpace(quest.GiverNpcName) && npcRegistry != null
                ? npcRegistry.GetPrefab(quest.GiverNpcName)
                : null;
            string giverName = giverPrefab != null
                ? (string.IsNullOrWhiteSpace(giverPrefab.CharacterName) ? giverPrefab.name : giverPrefab.CharacterName)
                : (string.IsNullOrWhiteSpace(quest.GiverNpcName) ? "<board>" : quest.GiverNpcName);

            string title = string.IsNullOrWhiteSpace(quest.Title) ? quest.name : quest.Title.Trim();
            string questId = string.IsNullOrWhiteSpace(quest.QuestId) ? "<missing>" : quest.QuestId.Trim();
            int objectiveCount = quest.Objectives?.Count ?? 0;
            string objectiveSearchText = BuildObjectiveSearchText(quest);

            return new QuestDatabaseRow
            {
                Quest = quest,
                QuestId = questId,
                Title = title,
                GiverName = giverName,
                GiverOwnerId = quest.GiverNpcName,
                ObjectiveCount = objectiveCount,
                AutoOffer = quest.AutoOffer,
                Repeatable = quest.Repeatable,
                IsTimed = quest.IsTimed,
                MissingGiver = !string.IsNullOrWhiteSpace(quest.GiverNpcName) && giverPrefab == null,
                HasOrphanedPrereqs = HasOrphanedPrereqs(quest),
                AssetPath = path,
                SearchText = $"{title} {questId} {giverName} {objectiveSearchText} {path}".ToLowerInvariant(),
                Icon = AssetDatabase.GetCachedIcon(path),
                WarningCount = warnings.Count
            };
        }

        private static string BuildObjectiveSearchText(QuestDefinition quest)
        {
            if (quest.Objectives == null || quest.Objectives.Count == 0)
                return string.Empty;
            System.Text.StringBuilder sb = new();
            for (int i = 0; i < quest.Objectives.Count; i++)
            {
                QuestObjective objective = quest.Objectives[i];
                if (objective == null) continue;
                sb.Append(objective.Type).Append(' ');
                if (!string.IsNullOrWhiteSpace(objective.DisplayText))
                    sb.Append(objective.DisplayText).Append(' ');
                if (!string.IsNullOrWhiteSpace(objective.NpcName))
                    sb.Append(objective.NpcName).Append(' ');
            }
            return sb.ToString();
        }

        private static bool HasOrphanedPrereqs(QuestDefinition quest)
        {
            IReadOnlyList<string> prereqs = quest.PrerequisiteQuestIds;
            if (prereqs == null || prereqs.Count == 0)
                return false;
            QuestRegistry registry = QuestRegistry.Get();
            if (registry == null)
                return false;
            for (int i = 0; i < prereqs.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(prereqs[i]) && registry.Find(prereqs[i]) == null)
                    return true;
            }
            return false;
        }

        private void RefreshRow(QuestDefinition quest)
        {
            if (quest == null)
                return;

            InvalidateWarningCache(quest);
            QuestDatabaseRow row = BuildRow(quest);
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Quest != quest)
                    continue;
                _rows[i] = row;
                _filteredRowsDirty = true;
                return;
            }

            _quests.Add(quest);
            _rows.Add(row);
            _filteredRowsDirty = true;
        }

        internal static bool RowMatchesFilters(QuestDatabaseRow row, string search, QuestFilter filter)
        {
            if (row == null || row.Quest == null)
                return false;

            if (!string.IsNullOrWhiteSpace(search))
            {
                string needle = search.Trim().ToLowerInvariant();
                if (row.SearchText == null || row.SearchText.IndexOf(needle, System.StringComparison.Ordinal) < 0)
                    return false;
            }

            return filter switch
            {
                QuestFilter.AutoOffer => row.AutoOffer,
                QuestFilter.Repeatable => row.Repeatable,
                QuestFilter.Timed => row.IsTimed,
                QuestFilter.Untimed => !row.IsTimed,
                QuestFilter.MissingGiver => row.MissingGiver,
                QuestFilter.HasWarnings => row.WarningCount > 0,
                QuestFilter.OrphanedPrereqs => row.HasOrphanedPrereqs,
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
                QuestDatabaseRow row = _rows[i];
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

        private void SortFilteredRows(QuestSort sort)
        {
            switch (sort)
            {
                case QuestSort.Title:
                    _filteredRows.Sort((a, b) => System.StringComparer.OrdinalIgnoreCase.Compare(a.Title, b.Title));
                    break;
                case QuestSort.GiverName:
                    _filteredRows.Sort((a, b) =>
                    {
                        int cmp = System.StringComparer.OrdinalIgnoreCase.Compare(a.GiverName, b.GiverName);
                        return cmp != 0 ? cmp : System.StringComparer.OrdinalIgnoreCase.Compare(a.QuestId, b.QuestId);
                    });
                    break;
                default:
                    _filteredRows.Sort((a, b) => System.StringComparer.OrdinalIgnoreCase.Compare(a.QuestId, b.QuestId));
                    break;
            }
        }

        private void SelectQuest(QuestDefinition quest)
        {
            _selectedQuest = quest;
            QuestAuthoringDrawerUtility.ClearFocusedObjective();
            RefreshSelectedSerializedObject();
            Repaint();
        }

        private void RefreshSelectedSerializedObject()
        {
            _selectedSerializedObject = _selectedQuest != null ? new SerializedObject(_selectedQuest) : null;
        }

        private void CreateNewQuest()
        {
            QuestDefinition created = QuestAuthoringEditorUtility.CreateQuestAsset(_newTemplate);
            RefreshIndex();
            SelectQuest(created);
            SelectCurrentAsset();
        }

        private void DuplicateSelectedQuest()
        {
            QuestDefinition duplicated = QuestAuthoringEditorUtility.DuplicateQuestAsset(_selectedQuest);
            RefreshIndex();
            SelectQuest(duplicated);
            SelectCurrentAsset();
        }

        private void RevealSelectedAsset()
        {
            string path = QuestAuthoringEditorUtility.GetAssetPath(_selectedQuest);
            if (!string.IsNullOrWhiteSpace(path))
                EditorUtility.RevealInFinder(path);
        }

        private void SelectCurrentAsset()
        {
            if (_selectedQuest == null)
                return;
            Selection.activeObject = _selectedQuest;
            EditorGUIUtility.PingObject(_selectedQuest);
        }

        private void RebuildRegistry()
        {
            QuestRegistry.ForceEditorSyncNow();
            RefreshIndex();
        }

        private void ValidateAll()
        {
            InvalidateWarningCache();
            int warningCount = 0;
            QuestRegistry registry = QuestRegistry.Get();
            List<QuestAuthoringWarning> registryWarnings = QuestAuthoringValidator.ValidateRegistry(registry);
            for (int i = 0; i < registryWarnings.Count; i++)
            {
                Debug.LogWarning($"[Quest Database] {registryWarnings[i].Message}");
                warningCount++;
            }

            for (int i = 0; i < _quests.Count; i++)
            {
                QuestDefinition quest = _quests[i];
                List<QuestAuthoringWarning> warnings = GetWarnings(quest);
                for (int j = 0; j < warnings.Count; j++)
                {
                    Debug.LogWarning($"[Quest Database] {quest.Title} ({quest.QuestId}): {warnings[j].Message}", quest);
                    warningCount++;
                }
            }

            RefreshIndex();
            string message = warningCount == 0
                ? "No quest authoring warnings found."
                : $"Found {warningCount} quest authoring warning(s). See Console for details.";
            EditorUtility.DisplayDialog("Validate Quest Database", message, "OK");
        }

        private List<QuestAuthoringWarning> GetWarnings(QuestDefinition quest)
        {
            if (quest == null)
                return new List<QuestAuthoringWarning>();
            if (!_warningCache.TryGetValue(quest, out List<QuestAuthoringWarning> warnings))
            {
                warnings = QuestAuthoringValidator.Validate(quest);
                _warningCache[quest] = warnings;
            }
            return warnings;
        }

        private void InvalidateWarningCache()
        {
            _warningCache.Clear();
        }

        private void InvalidateWarningCache(QuestDefinition quest)
        {
            if (quest != null)
                _warningCache.Remove(quest);
        }

        private void ReapplyTemplateForSelected()
        {
            if (_selectedQuest == null || _selectedQuest.AuthoringTemplate == QuestAuthoringTemplate.None)
                return;

            List<string> changes = QuestAuthoringEditorUtility.BuildTemplateOverwritePreview(_selectedQuest.AuthoringTemplate);
            string message = "This will reapply the template defaults:\n\n- "
                + string.Join("\n- ", changes)
                + "\n\nObjectives are only seeded when the list is empty.";

            bool confirmed = EditorUtility.DisplayDialog(
                "Reapply Quest Template",
                message,
                "Apply",
                "Cancel");
            if (!confirmed)
                return;

            QuestAuthoringEditorUtility.ApplyTemplate(_selectedQuest, _selectedQuest.AuthoringTemplate, _selectedQuest.Title);
            RefreshRow(_selectedQuest);
            RefreshSelectedSerializedObject();
            QuestRegistry.ScheduleEditorSync();
        }
    }
}
