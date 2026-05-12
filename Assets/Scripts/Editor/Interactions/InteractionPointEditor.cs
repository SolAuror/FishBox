#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    [CustomEditor(typeof(InteractionPoint))]
    internal sealed class InteractionPointEditor : UnityEditor.Editor
    {
        private InteractionPoint Point => (InteractionPoint)target;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6f);
            DrawValidation();
            DrawTools();
        }

        private void DrawValidation()
        {
            List<InteractionAuthoringWarning> warnings = InteractionAuthoringValidator.ValidatePoint(Point);
            for (int i = 0; i < warnings.Count; i++)
                EditorGUILayout.HelpBox(warnings[i].Message, InteractionAuthoringValidator.ToMessageType(warnings[i].Severity));
        }

        private void DrawTools()
        {
            EditorGUILayout.LabelField("Authoring Tools", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Align Child"))
                CreateAlignChild();
            if (GUILayout.Button("Open Database"))
                SolDatabaseWindow.Open(SolDatabaseTab.Interactions, Point.InteractionPointId);
            EditorGUILayout.EndHorizontal();
        }

        private void CreateAlignChild()
        {
            InteractionPoint point = Point;
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
            align.transform.localRotation = Quaternion.identity;
            Selection.activeTransform = align.transform;
        }

        private void OnSceneGUI()
        {
            InteractionPoint point = Point;
            if (point == null)
                return;

            Transform align = point.AlignPoint;
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.85f);
            Handles.DrawWireDisc(point.transform.position, Vector3.up, 0.55f);

            if (align != null)
            {
                Handles.color = Color.cyan;
                Handles.ArrowHandleCap(0, align.position, align.rotation, 0.8f, EventType.Repaint);
                Handles.DrawDottedLine(point.transform.position, align.position, 4f);
                Handles.Label(align.position + Vector3.up * 0.2f, "Interaction Align");
            }
        }
    }
}
#endif
