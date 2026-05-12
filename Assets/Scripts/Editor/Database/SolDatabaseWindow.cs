using System;
using System.Collections.Generic;
using Sol.AI;
using Sol.Grab;
using Sol.Quests;
using Sol.Rpg;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    public enum SolDatabaseTab
    {
        Overview = 0,
        Items = 1,
        NPCs = 2,
        Quests = 3,
        Stats = 4,
        Skills = 5,
        Factions = 6,
        Shops = 7,
        Schedules = 8,
        Interactions = 9
    }

    internal enum SolDatabaseIssueSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    internal sealed class SolDatabaseIssue
    {
        public SolDatabaseIssueSeverity Severity;
        public SolDatabaseTab Tab;
        public string RecordId;
        public string RecordName;
        public string Message;
        public UnityEngine.Object Context;

        public SolDatabaseIssue(
            SolDatabaseIssueSeverity severity,
            SolDatabaseTab tab,
            string recordId,
            string recordName,
            string message,
            UnityEngine.Object context = null)
        {
            Severity = severity;
            Tab = tab;
            RecordId = recordId ?? string.Empty;
            RecordName = recordName ?? string.Empty;
            Message = message ?? string.Empty;
            Context = context;
        }
    }

    internal interface ISolDatabasePage
    {
        SolDatabaseTab Tab { get; }
        string DisplayName { get; }
        void Bind(SolDatabaseWindow window);
        void RefreshIndex();
        void DrawToolbar();
        void DrawPage();
        bool SelectById(string id);
        List<SolDatabaseIssue> CollectIssues();
    }

    internal abstract class SolDatabasePageBase : ISolDatabasePage
    {
        protected const float DefaultLeftPaneWidth = 380f;
        protected const float RowHeight = 58f;
        protected const float RowPadding = 4f;
        protected const float IconSize = 32f;

        protected SolDatabaseWindow Window;
        protected Vector2 ListScroll;
        protected Vector2 DetailScroll;
        protected string Search = string.Empty;

        public abstract SolDatabaseTab Tab { get; }
        public abstract string DisplayName { get; }

        public virtual void Bind(SolDatabaseWindow window)
        {
            Window = window;
        }

        public abstract void RefreshIndex();
        public abstract void DrawToolbar();
        public abstract void DrawPage();
        public abstract bool SelectById(string id);
        public abstract List<SolDatabaseIssue> CollectIssues();

        protected void DrawSearchField(Action onChanged)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            Search = GUILayout.TextField(Search, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.MinWidth(120f));
            if (GUILayout.Button(GUIContent.none, GUI.skin.FindStyle("ToolbarSearchCancelButton")))
            {
                Search = string.Empty;
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
                onChanged?.Invoke();
        }

        protected void DrawVirtualizedRows(int rowCount, string emptyMessage, Action<Rect, int> drawRow)
        {
            float viewportHeight = Mathf.Max(120f, Window != null ? Window.position.height - 108f : 360f);
            ListScroll = EditorGUILayout.BeginScrollView(ListScroll);

            if (rowCount == 0)
            {
                EditorGUILayout.HelpBox(emptyMessage, MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            int firstRow = Mathf.Clamp(Mathf.FloorToInt(ListScroll.y / RowHeight), 0, Mathf.Max(0, rowCount - 1));
            int visibleCount = Mathf.CeilToInt(viewportHeight / RowHeight) + 2;
            int lastRowExclusive = Mathf.Min(rowCount, firstRow + visibleCount);

            float topSpace = firstRow * RowHeight;
            float bottomSpace = (rowCount - lastRowExclusive) * RowHeight;
            if (topSpace > 0f)
                GUILayout.Space(topSpace);

            for (int i = firstRow; i < lastRowExclusive; i++)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, RowHeight);
                drawRow(rowRect, i);
            }

            if (bottomSpace > 0f)
                GUILayout.Space(bottomSpace);

            EditorGUILayout.EndScrollView();
        }

        protected static bool DrawRowChrome(Rect rowRect, bool selected, Action onClick)
        {
            Event evt = Event.current;
            bool hover = rowRect.Contains(evt.mousePosition);
            if (selected)
                EditorGUI.DrawRect(rowRect, new Color(0.22f, 0.44f, 0.68f, 0.28f));
            else if (hover)
                EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.06f));

            if (evt.type == EventType.MouseDown && evt.button == 0 && rowRect.Contains(evt.mousePosition))
            {
                onClick?.Invoke();
                evt.Use();
                return true;
            }

            return false;
        }

        protected static void DrawWarningBadge(Rect rowRect, int warningCount)
        {
            if (warningCount <= 0)
                return;

            Rect warningRect = new(rowRect.xMax - 38f, rowRect.y + 6f, 34f, EditorGUIUtility.singleLineHeight);
            GUI.Label(warningRect, $"! {warningCount}", EditorStyles.miniBoldLabel);
        }

        protected static void DrawIcon(Rect rowRect, Texture icon)
        {
            Rect iconRect = new(rowRect.x + RowPadding, rowRect.y + (RowHeight - IconSize) * 0.5f, IconSize, IconSize);
            if (icon != null)
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            else
                EditorGUI.DrawRect(iconRect, new Color(0f, 0f, 0f, 0.18f));
        }

        protected static Rect TextColumnRect(Rect rowRect)
        {
            float textX = rowRect.x + RowPadding + IconSize + RowPadding;
            return new Rect(textX, rowRect.y, rowRect.width - (textX - rowRect.x) - 44f, rowRect.height);
        }

        protected static SolDatabaseIssueSeverity MapIssueSeverity(ItemAuthoringWarningSeverity severity)
        {
            return severity switch
            {
                ItemAuthoringWarningSeverity.Error => SolDatabaseIssueSeverity.Error,
                ItemAuthoringWarningSeverity.Warning => SolDatabaseIssueSeverity.Warning,
                _ => SolDatabaseIssueSeverity.Info
            };
        }

        protected static SolDatabaseIssueSeverity MapIssueSeverity(NPCAuthoringWarningSeverity severity)
        {
            return severity switch
            {
                NPCAuthoringWarningSeverity.Error => SolDatabaseIssueSeverity.Error,
                NPCAuthoringWarningSeverity.Warning => SolDatabaseIssueSeverity.Warning,
                _ => SolDatabaseIssueSeverity.Info
            };
        }

        protected static SolDatabaseIssueSeverity MapIssueSeverity(QuestAuthoringWarningSeverity severity)
        {
            return severity switch
            {
                QuestAuthoringWarningSeverity.Error => SolDatabaseIssueSeverity.Error,
                QuestAuthoringWarningSeverity.Warning => SolDatabaseIssueSeverity.Warning,
                _ => SolDatabaseIssueSeverity.Info
            };
        }
    }

    public sealed class SolDatabaseWindow : EditorWindow
    {
        private readonly Dictionary<SolDatabaseTab, ISolDatabasePage> _pages = new();
        private readonly List<SolDatabaseTab> _tabOrder = new()
        {
            SolDatabaseTab.Overview,
            SolDatabaseTab.Stats,
            SolDatabaseTab.Skills,
            SolDatabaseTab.Factions,
            SolDatabaseTab.Items,
            SolDatabaseTab.Interactions,
            SolDatabaseTab.NPCs,
            SolDatabaseTab.Schedules,
            SolDatabaseTab.Shops,
            SolDatabaseTab.Quests
        };

        private SolDatabaseTab _selectedTab = SolDatabaseTab.Overview;
        private Vector2 _tabScroll;

        internal static SolDatabaseTab LastOpenedTab { get; private set; } = SolDatabaseTab.Overview;
        internal static string LastRequestedSelectionId { get; private set; } = string.Empty;

        [MenuItem("Window/Sol/Database")]
        public static void Open()
        {
            Open(SolDatabaseTab.Overview);
        }

        public static void Open(SolDatabaseTab tab, string selectId = null)
        {
            SolDatabaseWindow window = GetWindow<SolDatabaseWindow>("Sol Database");
            window.minSize = new Vector2(1040f, 560f);
            window.EnsurePages();
            window.SelectTab(tab);
            LastOpenedTab = tab;
            LastRequestedSelectionId = selectId ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(selectId) && window._pages.TryGetValue(tab, out ISolDatabasePage page))
                page.SelectById(selectId.Trim());

            window.Repaint();
        }

        internal void OpenIssue(SolDatabaseIssue issue)
        {
            if (issue == null)
                return;

            SelectTab(issue.Tab);
            if (_pages.TryGetValue(issue.Tab, out ISolDatabasePage page) && !string.IsNullOrWhiteSpace(issue.RecordId))
                page.SelectById(issue.RecordId);

            if (issue.Context != null)
                EditorGUIUtility.PingObject(issue.Context);
        }

        internal List<SolDatabaseIssue> CollectIssues(bool includeOverview = false)
        {
            EnsurePages();
            List<SolDatabaseIssue> issues = new();
            foreach (SolDatabaseTab tab in _tabOrder)
            {
                if (!includeOverview && tab == SolDatabaseTab.Overview)
                    continue;
                if (_pages.TryGetValue(tab, out ISolDatabasePage page))
                    issues.AddRange(page.CollectIssues());
            }

            issues.Sort(CompareIssues);
            return issues;
        }

        private static int CompareIssues(SolDatabaseIssue a, SolDatabaseIssue b)
        {
            int severity = b.Severity.CompareTo(a.Severity);
            if (severity != 0) return severity;
            int tab = a.Tab.CompareTo(b.Tab);
            if (tab != 0) return tab;
            return StringComparer.OrdinalIgnoreCase.Compare(a.RecordName, b.RecordName);
        }

        private void OnEnable()
        {
            EnsurePages();
            RefreshAll();
        }

        private void OnProjectChange()
        {
            RefreshAll();
            Repaint();
        }

        private void OnGUI()
        {
            EnsurePages();
            DrawMainToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawTabRail();
            DrawSelectedPage();
            EditorGUILayout.EndHorizontal();
        }

        private void EnsurePages()
        {
            if (_pages.Count > 0)
                return;

            AddPage(new SolDatabaseOverviewPage());
            AddPage(new SolDatabaseStatsPage());
            AddPage(new SolDatabaseSkillsPage());
            AddPage(new SolDatabaseFactionsPage());
            AddPage(new SolDatabaseItemPage());
            AddPage(new SolDatabaseInteractionPage());
            AddPage(new SolDatabaseNPCPage());
            AddPage(new SolDatabaseSchedulePage());
            AddPage(new SolDatabaseShopsPage());
            AddPage(new SolDatabaseQuestPage());
        }

        private void AddPage(ISolDatabasePage page)
        {
            page.Bind(this);
            _pages[page.Tab] = page;
        }

        private void RefreshAll()
        {
            EnsurePages();
            foreach (ISolDatabasePage page in _pages.Values)
                page.RefreshIndex();
        }

        private void SelectTab(SolDatabaseTab tab)
        {
            if (!_pages.ContainsKey(tab))
                tab = SolDatabaseTab.Overview;

            _selectedTab = tab;
            LastOpenedTab = tab;
        }

        private void DrawMainToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Sol RPG Database", EditorStyles.boldLabel, GUILayout.Width(160f));
            if (GUILayout.Button("Refresh All", EditorStyles.toolbarButton, GUILayout.Width(78f)))
                RefreshAll();
            if (GUILayout.Button("Validate All", EditorStyles.toolbarButton, GUILayout.Width(78f)))
                ShowValidationDialog();

            GUILayout.FlexibleSpace();
            if (_pages.TryGetValue(_selectedTab, out ISolDatabasePage page))
                page.DrawToolbar();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTabRail()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(140f));
            _tabScroll = EditorGUILayout.BeginScrollView(_tabScroll);
            for (int i = 0; i < _tabOrder.Count; i++)
            {
                SolDatabaseTab tab = _tabOrder[i];
                if (!_pages.TryGetValue(tab, out ISolDatabasePage page))
                    continue;

                bool selected = _selectedTab == tab;
                GUIStyle style = selected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                if (GUILayout.Button(page.DisplayName, style, GUILayout.Height(28f)))
                    SelectTab(tab);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedPage()
        {
            EditorGUILayout.BeginVertical();
            if (_pages.TryGetValue(_selectedTab, out ISolDatabasePage page))
                page.DrawPage();
            else
                EditorGUILayout.HelpBox("Database page is not available.", MessageType.Warning);
            EditorGUILayout.EndVertical();
        }

        private void ShowValidationDialog()
        {
            List<SolDatabaseIssue> issues = CollectIssues();
            int errors = 0;
            int warnings = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == SolDatabaseIssueSeverity.Error) errors++;
                if (issues[i].Severity == SolDatabaseIssueSeverity.Warning) warnings++;
            }

            string message = issues.Count == 0
                ? "No database authoring issues found."
                : $"Found {issues.Count} issue(s): {errors} error(s), {warnings} warning(s). Open the Overview tab for details.";
            EditorUtility.DisplayDialog("Validate Sol Database", message, "OK");
            SelectTab(SolDatabaseTab.Overview);
        }
    }
}
