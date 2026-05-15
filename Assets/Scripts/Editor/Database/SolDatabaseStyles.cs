using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    internal static class SolDatabaseStyles
    {
        public const float DefaultLeftPaneWidth = 380f;
        public const float MinPaneWidth = 240f;
        public const float ListRowHeight = 58f;
        public const float ListRowPadding = 4f;
        public const float ListRowIconSize = 32f;

        public const float ButtonXS = 48f;
        public const float ButtonS = 54f;
        public const float ButtonM = 76f;
        public const float ButtonL = 92f;
        public const float ButtonXL = 108f;
        public const float ButtonXXL = 120f;

        public static Color RowSelectedBg => EditorGUIUtility.isProSkin
            ? new Color(0.22f, 0.44f, 0.68f, 0.32f)
            : new Color(0.18f, 0.36f, 0.60f, 0.28f);

        public static Color RowHoverBg => EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.06f)
            : new Color(0f, 0f, 0f, 0.06f);

        public static Color IconPlaceholderBg => EditorGUIUtility.isProSkin
            ? new Color(0f, 0f, 0f, 0.18f)
            : new Color(0f, 0f, 0f, 0.10f);

        public static Color PreviewBg => EditorGUIUtility.isProSkin
            ? new Color(0.11f, 0.12f, 0.13f, 1f)
            : new Color(0.78f, 0.78f, 0.78f, 1f);

        public static Color LinkColor => EditorGUIUtility.isProSkin
            ? new Color(0.50f, 0.78f, 1f, 1f)
            : new Color(0.10f, 0.40f, 0.85f, 1f);

        public static class Folders
        {
            public const string DataRoot = "Assets/Data";
            public const string NpcSchedules = "Assets/Data/NpcSchedules";
            public const string Interactions = "Assets/Data/Interactions";
        }

        public static bool LinkButton(string label, params GUILayoutOption[] options)
        {
            Color previous = GUI.contentColor;
            GUI.contentColor = LinkColor;
            GUIStyle style = new(EditorStyles.label) { richText = true };
            bool clicked = GUILayout.Button(label, style, options);
            GUI.contentColor = previous;
            Rect rect = GUILayoutUtility.GetLastRect();
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            return clicked;
        }

        public static void EnsureFolder(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || AssetDatabase.IsValidFolder(assetPath))
                return;

            string parent = System.IO.Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            string leaf = System.IO.Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
                return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
