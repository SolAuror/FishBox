using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    internal sealed class SolDatabaseOverviewPage : SolDatabasePageBase
    {
        private readonly List<SolDatabaseIssue> _issues = new();
        private Vector2 _scroll;

        public override SolDatabaseTab Tab => SolDatabaseTab.Overview;
        public override string DisplayName => "Overview";

        public override void DrawToolbar()
        {
            if (GUILayout.Button("Refresh Issues", EditorStyles.toolbarButton, GUILayout.Width(96f)))
                RefreshIndex();
        }

        public override void DrawPage()
        {
            RefreshIndex();

            CountIssues(_issues, out int errors, out int warnings, out int infos);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Database Health", EditorStyles.largeLabel);
            EditorGUILayout.BeginHorizontal();
            DrawSummaryBox("Errors", errors, MessageType.Error);
            DrawSummaryBox("Warnings", warnings, MessageType.Warning);
            DrawSummaryBox("Info", infos, MessageType.Info);
            DrawSummaryBox("Total", _issues.Count, MessageType.None);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8f);

            if (_issues.Count == 0)
            {
                EditorGUILayout.HelpBox("No current database authoring issues found.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField("Issues", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _issues.Count; i++)
                DrawIssueRow(_issues[i]);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        public override void RefreshIndex()
        {
            _issues.Clear();
            if (Window != null)
                _issues.AddRange(Window.CollectIssues(includeOverview: false));
        }

        public override bool SelectById(string id)
        {
            return false;
        }

        public override List<SolDatabaseIssue> CollectIssues()
        {
            return new List<SolDatabaseIssue>();
        }

        internal static void CountIssues(IReadOnlyList<SolDatabaseIssue> issues, out int errors, out int warnings, out int infos)
        {
            errors = 0;
            warnings = 0;
            infos = 0;
            if (issues == null)
                return;

            for (int i = 0; i < issues.Count; i++)
            {
                switch (issues[i].Severity)
                {
                    case SolDatabaseIssueSeverity.Error:
                        errors++;
                        break;
                    case SolDatabaseIssueSeverity.Warning:
                        warnings++;
                        break;
                    default:
                        infos++;
                        break;
                }
            }
        }

        private static void DrawSummaryBox(string label, int count, MessageType type)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = type switch
            {
                MessageType.Error => new Color(0.9f, 0.35f, 0.35f, 1f),
                MessageType.Warning => new Color(0.95f, 0.75f, 0.3f, 1f),
                MessageType.Info => new Color(0.45f, 0.65f, 0.95f, 1f),
                _ => previous
            };

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(120f), GUILayout.Height(52f));
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(count.ToString(), EditorStyles.largeLabel);
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = previous;
        }

        private void DrawIssueRow(SolDatabaseIssue issue)
        {
            MessageType messageType = issue.Severity switch
            {
                SolDatabaseIssueSeverity.Error => MessageType.Error,
                SolDatabaseIssueSeverity.Warning => MessageType.Warning,
                _ => MessageType.Info
            };

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(issue.Severity.ToString(), EditorStyles.miniBoldLabel, GUILayout.Width(58f));
            GUILayout.Label(issue.Tab.ToString(), EditorStyles.miniLabel, GUILayout.Width(52f));
            string record = string.IsNullOrWhiteSpace(issue.RecordId)
                ? issue.RecordName
                : $"{issue.RecordName} ({issue.RecordId})";
            GUILayout.Label(record, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(issue.Tab == SolDatabaseTab.Overview))
            {
                if (GUILayout.Button("Open", GUILayout.Width(58f)))
                    Window.OpenIssue(issue);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(issue.Message, messageType);
            EditorGUILayout.EndVertical();
        }
    }
}
