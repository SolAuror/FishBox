using System.Collections.Generic;
using Sol;
using Sol.AI;
using Sol.Grab;
using Sol.Quests;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Sol.Editor
{
    public static class QuestAuthoringDrawerUtility
    {
        private static readonly Dictionary<int, ReorderableList> _objectiveListCache = new();
        private static readonly Dictionary<int, ReorderableList> _rewardListCache = new();
        private static readonly Dictionary<int, ReorderableList> _prereqListCache = new();

        public static int FocusedObjectiveIndex { get; private set; } = -1;

        public static void DrawQuestInspector(SerializedObject serializedObject, QuestDefinition quest, bool showWarnings = true)
        {
            if (serializedObject == null)
                return;

            if (showWarnings && quest != null)
                DrawWarnings(quest);

            DrawIdentitySection(serializedObject, quest);
            DrawGiverSection(serializedObject);
            DrawPrerequisitesSection(serializedObject, quest);
            DrawObjectivesSection(serializedObject);
            DrawRewardSection(serializedObject);
            DrawAuthoringSection(serializedObject);
        }

        public static void DrawWarnings(QuestDefinition quest)
        {
            List<QuestAuthoringWarning> warnings = QuestAuthoringValidator.Validate(quest);
            if (warnings.Count == 0)
                return;

            for (int i = 0; i < warnings.Count; i++)
            {
                QuestAuthoringWarning warning = warnings[i];
                MessageType type = warning.Severity switch
                {
                    QuestAuthoringWarningSeverity.Error => MessageType.Error,
                    QuestAuthoringWarningSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };
                EditorGUILayout.HelpBox(warning.Message, type);
            }

            EditorGUILayout.Space(4f);
        }

        public static void FocusObjective(int index)
        {
            FocusedObjectiveIndex = index;
        }

        public static void ClearFocusedObjective()
        {
            FocusedObjectiveIndex = -1;
        }

        private static void DrawIdentitySection(SerializedObject serializedObject, QuestDefinition quest)
        {
            DrawHeader("Identity");

            using (new EditorGUI.DisabledScope(true))
                DrawProperty(serializedObject, "_questId");

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(quest == null))
            {
                if (GUILayout.Button("Regenerate QuestId", GUILayout.Width(160f)))
                    RegenerateQuestId(serializedObject);
            }
            EditorGUILayout.EndHorizontal();

            DrawProperty(serializedObject, "_title");
            DrawProperty(serializedObject, "_summary");
            DrawProperty(serializedObject, "_autoOffer");
            DrawProperty(serializedObject, "_repeatable");

            SerializedProperty repeatable = serializedObject.FindProperty("_repeatable");
            using (new EditorGUI.DisabledScope(repeatable == null || !repeatable.boolValue))
            {
                DrawProperty(serializedObject, "_repeatMode");
                DrawProperty(serializedObject, "_repeatAfterRealtimeSeconds");
                DrawProperty(serializedObject, "_repeatAfterInGameDays");
            }

            DrawProperty(serializedObject, "_timeLimitSeconds");
            DrawProperty(serializedObject, "_retryAfterSeconds");
        }

        private static void RegenerateQuestId(SerializedObject serializedObject)
        {
            SerializedProperty questId = serializedObject.FindProperty("_questId");
            if (questId == null)
                return;

            string next = QuestAuthoringEditorUtility.NextQuestId();
            if (string.IsNullOrEmpty(next))
                return;

            questId.stringValue = next;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            if (serializedObject.targetObject != null)
                EditorUtility.SetDirty(serializedObject.targetObject);
            QuestRegistry.ScheduleEditorSync();
        }

        private static void DrawGiverSection(SerializedObject serializedObject)
        {
            DrawHeader("Giver");
            SerializedProperty giverProp = serializedObject.FindProperty("_giverNpcName");
            if (giverProp == null)
                return;

            EditorGUILayout.PropertyField(giverProp);

            string giver = giverProp.stringValue;
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(giver)))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open in NPC Database", GUILayout.Width(180f)))
                    NPCDatabaseWindow.SelectByOwnerId(giver);
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void DrawPrerequisitesSection(SerializedObject serializedObject, QuestDefinition quest)
        {
            DrawHeader("Prerequisites");
            SerializedProperty prereqProp = serializedObject.FindProperty("_prerequisiteQuestIds");
            if (prereqProp == null)
                return;

            int key = serializedObject.targetObject != null
                ? serializedObject.targetObject.GetInstanceID()
                : 0;
            if (!_prereqListCache.TryGetValue(key, out ReorderableList list) || list.serializedProperty != prereqProp)
            {
                list = BuildPrerequisitesList(prereqProp);
                _prereqListCache[key] = list;
            }

            list.DoLayoutList();

            if (quest != null && QuestAuthoringValidator.HasPrerequisiteCycle(quest))
                EditorGUILayout.HelpBox("Prerequisite cycle detected — at least one prereq chain loops back to this quest.", MessageType.Error);
        }

        private static ReorderableList BuildPrerequisitesList(SerializedProperty prereqProp)
        {
            ReorderableList list = new(
                prereqProp.serializedObject,
                prereqProp,
                draggable: true,
                displayHeader: true,
                displayAddButton: true,
                displayRemoveButton: true);

            list.drawHeaderCallback = rect => GUI.Label(rect, "Prerequisite Quest IDs");
            list.elementHeight = EditorGUIUtility.singleLineHeight + 6f;
            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                SerializedProperty element = prereqProp.GetArrayElementAtIndex(index);
                Rect fieldRect = new(rect.x, rect.y + 2f, rect.width - 32f, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(fieldRect, element, GUIContent.none);

                Rect openRect = new(rect.xMax - 28f, rect.y + 2f, 26f, EditorGUIUtility.singleLineHeight);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(element.stringValue)))
                {
                    if (GUI.Button(openRect, new GUIContent("→", "Select prerequisite quest")))
                        QuestDatabaseWindow.SelectByQuestId(element.stringValue);
                }
            };
            return list;
        }

        private static void DrawObjectivesSection(SerializedObject serializedObject)
        {
            DrawHeader("Objectives");
            SerializedProperty objectivesProp = serializedObject.FindProperty("_objectives");
            if (objectivesProp == null)
                return;

            int key = serializedObject.targetObject != null
                ? serializedObject.targetObject.GetInstanceID()
                : 0;
            if (!_objectiveListCache.TryGetValue(key, out ReorderableList list) || list.serializedProperty != objectivesProp)
            {
                list = BuildObjectivesList(objectivesProp);
                _objectiveListCache[key] = list;
            }

            if (FocusedObjectiveIndex >= 0 && FocusedObjectiveIndex < objectivesProp.arraySize)
            {
                list.index = FocusedObjectiveIndex;
                ClearFocusedObjective();
            }

            list.DoLayoutList();
        }

        private static ReorderableList BuildObjectivesList(SerializedProperty objectivesProp)
        {
            const float headerSpace = 4f;

            ReorderableList list = new(
                objectivesProp.serializedObject,
                objectivesProp,
                draggable: true,
                displayHeader: true,
                displayAddButton: true,
                displayRemoveButton: true);

            list.drawHeaderCallback = rect => GUI.Label(rect, "Objectives (sequential)");
            list.elementHeightCallback = index =>
            {
                if (index < 0 || index >= objectivesProp.arraySize)
                    return EditorGUIUtility.singleLineHeight + headerSpace;
                SerializedProperty element = objectivesProp.GetArrayElementAtIndex(index);
                return QuestObjectiveDrawer.GetObjectiveBodyHeight(element) + headerSpace * 2f;
            };
            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                if (index < 0 || index >= objectivesProp.arraySize)
                    return;
                SerializedProperty element = objectivesProp.GetArrayElementAtIndex(index);

                Rect headerRect = new(rect.x, rect.y + 2f, rect.width, EditorGUIUtility.singleLineHeight);
                GUI.Label(headerRect, $"Objective {index + 1}", EditorStyles.boldLabel);

                Rect bodyRect = new(rect.x, headerRect.yMax + headerSpace, rect.width, rect.height - (EditorGUIUtility.singleLineHeight + headerSpace * 2f));
                QuestObjectiveDrawer.DrawObjectiveBody(ref bodyRect, element);
            };
            return list;
        }

        private static void DrawRewardSection(SerializedObject serializedObject)
        {
            DrawHeader("Reward");
            SerializedProperty rewardProp = serializedObject.FindProperty("_reward");
            if (rewardProp == null)
                return;

            SerializedProperty goldProp = rewardProp.FindPropertyRelative("Gold");
            SerializedProperty itemsProp = rewardProp.FindPropertyRelative("Items");
            if (goldProp != null)
                EditorGUILayout.PropertyField(goldProp);

            if (itemsProp == null)
                return;

            int key = serializedObject.targetObject != null
                ? serializedObject.targetObject.GetInstanceID()
                : 0;
            if (!_rewardListCache.TryGetValue(key, out ReorderableList list) || list.serializedProperty != itemsProp)
            {
                list = BuildRewardList(itemsProp);
                _rewardListCache[key] = list;
            }

            list.DoLayoutList();
        }

        private static ReorderableList BuildRewardList(SerializedProperty itemsProp)
        {
            ReorderableList list = new(
                itemsProp.serializedObject,
                itemsProp,
                draggable: true,
                displayHeader: true,
                displayAddButton: true,
                displayRemoveButton: true);

            list.drawHeaderCallback = rect => GUI.Label(rect, "Reward Items");
            list.elementHeight = EditorGUIUtility.singleLineHeight * 2f + 8f;
            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                if (index < 0 || index >= itemsProp.arraySize)
                    return;
                SerializedProperty element = itemsProp.GetArrayElementAtIndex(index);
                SerializedProperty itemId = element.FindPropertyRelative("ItemId");
                SerializedProperty count = element.FindPropertyRelative("Count");

                Rect idRect = new(rect.x, rect.y + 2f, rect.width - 32f, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(idRect, itemId);

                Rect openRect = new(rect.xMax - 28f, rect.y + 2f, 26f, EditorGUIUtility.singleLineHeight);
                using (new EditorGUI.DisabledScope(itemId == null || string.IsNullOrWhiteSpace(itemId.stringValue)))
                {
                    if (GUI.Button(openRect, new GUIContent("→", "Select reward item")))
                        ItemDatabaseWindow.SelectByItemId(itemId.stringValue);
                }

                Rect countRect = new(rect.x, idRect.yMax + 2f, rect.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(countRect, count);
            };
            return list;
        }

        private static void DrawAuthoringSection(SerializedObject serializedObject)
        {
            DrawHeader("Authoring");
            DrawProperty(serializedObject, "_authoringTemplate");
            DrawProperty(serializedObject, "_authoringNotes");
        }

        private static void DrawHeader(string label)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        private static void DrawProperty(SerializedObject serializedObject, string propertyName, bool includeChildren = false)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, includeChildren);
        }
    }
}
