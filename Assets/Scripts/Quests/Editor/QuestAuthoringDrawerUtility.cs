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
        private const string FoldoutPrefPrefix = "Sol.QuestEditor.Section.";

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

        // ----- Section: Identity -----

        private static void DrawIdentitySection(SerializedObject serializedObject, QuestDefinition quest)
        {
            if (!BeginSection("Identity", defaultOpen: true))
            {
                EndSection();
                return;
            }

            SerializedProperty titleProp = serializedObject.FindProperty("_title");
            if (titleProp != null)
            {
                EditorGUILayout.LabelField("Title", EditorStyles.miniLabel);
                GUIStyle bigField = new GUIStyle(EditorStyles.textField) { fontSize = 14, fixedHeight = 22f };
                titleProp.stringValue = EditorGUILayout.TextField(titleProp.stringValue ?? string.Empty, bigField);
            }

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

            EndSection();
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

        // ----- Section: Giver -----

        private static void DrawGiverSection(SerializedObject serializedObject)
        {
            if (!BeginSection("Giver", defaultOpen: true))
            {
                EndSection();
                return;
            }

            SerializedProperty giverProp = serializedObject.FindProperty("_giverNpcName");
            if (giverProp == null)
            {
                EndSection();
                return;
            }

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

            EndSection();
        }

        // ----- Section: Prerequisites -----

        private static void DrawPrerequisitesSection(SerializedObject serializedObject, QuestDefinition quest)
        {
            if (!BeginSection("Prerequisites", defaultOpen: true))
            {
                EndSection();
                return;
            }

            SerializedProperty prereqProp = serializedObject.FindProperty("_prerequisiteQuestIds");
            if (prereqProp == null)
            {
                EndSection();
                return;
            }

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

            EndSection();
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
                ReferenceDropdown.DrawQuest(fieldRect, GUIContent.none, element);

                Rect openRect = new(rect.xMax - 28f, rect.y + 2f, 26f, EditorGUIUtility.singleLineHeight);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(element.stringValue)))
                {
                    if (GUI.Button(openRect, new GUIContent("→", "Select prerequisite quest")))
                        QuestDatabaseWindow.SelectByQuestId(element.stringValue);
                }
            };
            return list;
        }

        // ----- Section: Objectives -----

        private static void DrawObjectivesSection(SerializedObject serializedObject)
        {
            if (!BeginSection("Objectives", defaultOpen: true))
            {
                EndSection();
                return;
            }

            SerializedProperty objectivesProp = serializedObject.FindProperty("_objectives");
            if (objectivesProp == null)
            {
                EndSection();
                return;
            }

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

            EndSection();
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

        // ----- Section: Reward -----

        private static void DrawRewardSection(SerializedObject serializedObject)
        {
            if (!BeginSection("Reward", defaultOpen: true))
            {
                EndSection();
                return;
            }

            SerializedProperty rewardProp = serializedObject.FindProperty("_reward");
            if (rewardProp == null)
            {
                EndSection();
                return;
            }

            SerializedProperty goldProp = rewardProp.FindPropertyRelative("Gold");
            SerializedProperty itemsProp = rewardProp.FindPropertyRelative("Items");
            if (goldProp != null)
                EditorGUILayout.PropertyField(goldProp);

            if (itemsProp == null)
            {
                EndSection();
                return;
            }

            int key = serializedObject.targetObject != null
                ? serializedObject.targetObject.GetInstanceID()
                : 0;
            if (!_rewardListCache.TryGetValue(key, out ReorderableList list) || list.serializedProperty != itemsProp)
            {
                list = BuildRewardList(itemsProp);
                _rewardListCache[key] = list;
            }

            list.DoLayoutList();

            EndSection();
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
            list.elementHeight = EditorGUIUtility.singleLineHeight + 6f;
            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                if (index < 0 || index >= itemsProp.arraySize)
                    return;
                SerializedProperty element = itemsProp.GetArrayElementAtIndex(index);
                SerializedProperty itemId = element.FindPropertyRelative("ItemId");
                SerializedProperty count = element.FindPropertyRelative("Count");

                Rect row = new(rect.x, rect.y + 3f, rect.width, EditorGUIUtility.singleLineHeight);
                float openWidth = 26f;
                float countWidth = 60f;
                float gap = 4f;
                float idWidth = row.width - openWidth - countWidth - gap * 2f;

                Rect idRect = new(row.x, row.y, idWidth, row.height);
                Rect openRect = new(idRect.xMax + gap, row.y, openWidth, row.height);
                Rect countRect = new(openRect.xMax + gap, row.y, countWidth, row.height);

                if (itemId != null)
                    ReferenceDropdown.DrawItem(idRect, GUIContent.none, itemId);

                using (new EditorGUI.DisabledScope(itemId == null || string.IsNullOrWhiteSpace(itemId.stringValue)))
                {
                    if (GUI.Button(openRect, new GUIContent("→", "Select reward item")))
                        ItemDatabaseWindow.SelectByItemId(itemId.stringValue);
                }

                if (count != null)
                    EditorGUI.PropertyField(countRect, count, GUIContent.none);
            };
            return list;
        }

        // ----- Section: Authoring -----

        private static void DrawAuthoringSection(SerializedObject serializedObject)
        {
            if (!BeginSection("Authoring", defaultOpen: false))
            {
                EndSection();
                return;
            }

            DrawProperty(serializedObject, "_authoringTemplate");
            DrawProperty(serializedObject, "_authoringNotes");

            EndSection();
        }

        // ----- Section helpers -----

        private static bool BeginSection(string title, bool defaultOpen)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string key = FoldoutPrefPrefix + title;
            bool stored = EditorPrefs.GetBool(key, defaultOpen);

            GUIStyle style = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };
            bool open = EditorGUILayout.Foldout(stored, title, true, style);
            if (open != stored)
                EditorPrefs.SetBool(key, open);

            return open;
        }

        private static void EndSection()
        {
            EditorGUILayout.EndVertical();
        }

        private static void DrawProperty(SerializedObject serializedObject, string propertyName, bool includeChildren = true)
        {
            SerializedProperty property = serializedObject?.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, includeChildren);
        }
    }
}
