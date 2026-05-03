#if UNITY_EDITOR
using System.Collections.Generic;
using Sol.AI;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Sol.Editor
{
    /// <summary>
    /// IMGUI drawer for an inline <see cref="DialogueGraph"/> serialized field.
    /// Designed to be embedded inside the NPC Database / inspector under a
    /// "Conversation" foldout. State (per-node foldout) is persisted in EditorPrefs
    /// keyed by the owner instance id so different NPCs keep their own UI state.
    /// </summary>
    public static class DialogueGraphDrawer
    {
        private const string NodeFoldoutPrefPrefix = "Sol.NPCEditor.Dialogue.Node.";
        private const string RootShortcut = "<root>";
        private const string MissingPrefix = "<Missing>";

        // ReorderableList caches keyed by node SerializedProperty propertyPath + ownerId.
        private static readonly Dictionary<string, ReorderableList> _optionListCache = new();

        public static void Draw(SerializedProperty graphProp, string ownerId)
        {
            if (graphProp == null)
                return;

            SerializedProperty rootProp = graphProp.FindPropertyRelative(nameof(DialogueGraph.RootNodeId));
            SerializedProperty nodesProp = graphProp.FindPropertyRelative(nameof(DialogueGraph.Nodes));
            if (nodesProp == null)
                return;

            DrawToolbar(graphProp, rootProp, nodesProp);

            if (nodesProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No dialogue nodes authored. NPC will use the legacy flat conversation flow.", MessageType.None);
                return;
            }

            for (int i = 0; i < nodesProp.arraySize; i++)
                DrawNode(nodesProp, i, ownerId);
        }

        // ----- Toolbar -----

        private static void DrawToolbar(SerializedProperty graphProp, SerializedProperty rootProp, SerializedProperty nodesProp)
        {
            EditorGUILayout.BeginHorizontal();

            if (rootProp != null)
            {
                List<string> nodeIds = CollectNodeIds(nodesProp);
                int currentIndex = Mathf.Max(0, nodeIds.IndexOf(rootProp.stringValue ?? string.Empty));
                if (nodeIds.Count == 0)
                {
                    EditorGUILayout.LabelField("Root Node", "(no nodes)");
                }
                else
                {
                    int newIndex = EditorGUILayout.Popup("Root Node", currentIndex, nodeIds.ToArray());
                    if (newIndex != currentIndex)
                        rootProp.stringValue = nodeIds[newIndex];
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Node", GUILayout.Width(110f)))
                AddNode(nodesProp, rootProp);

            EditorGUILayout.EndHorizontal();
        }

        private static void AddNode(SerializedProperty nodesProp, SerializedProperty rootProp)
        {
            int oldCount = nodesProp.arraySize;
            nodesProp.arraySize = oldCount + 1;
            SerializedProperty newNode = nodesProp.GetArrayElementAtIndex(oldCount);

            string nodeId = oldCount == 0 ? "root" : $"node_{oldCount + 1}";
            // Avoid id collisions.
            HashSet<string> existing = new(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < oldCount; i++)
            {
                SerializedProperty existingNode = nodesProp.GetArrayElementAtIndex(i);
                SerializedProperty existingIdProp = existingNode.FindPropertyRelative(nameof(DialogueNode.NodeId));
                if (existingIdProp != null)
                    existing.Add(existingIdProp.stringValue ?? string.Empty);
            }
            int suffix = oldCount + 1;
            while (existing.Contains(nodeId))
            {
                suffix++;
                nodeId = $"node_{suffix}";
            }

            SerializedProperty idProp = newNode.FindPropertyRelative(nameof(DialogueNode.NodeId));
            SerializedProperty lineProp = newNode.FindPropertyRelative(nameof(DialogueNode.SpeakerLine));
            SerializedProperty optsProp = newNode.FindPropertyRelative(nameof(DialogueNode.Options));
            if (idProp != null) idProp.stringValue = nodeId;
            if (lineProp != null) lineProp.stringValue = string.Empty;
            if (optsProp != null) optsProp.arraySize = 0;

            if (oldCount == 0 && rootProp != null && string.IsNullOrWhiteSpace(rootProp.stringValue))
                rootProp.stringValue = nodeId;
        }

        private static List<string> CollectNodeIds(SerializedProperty nodesProp)
        {
            List<string> ids = new();
            for (int i = 0; i < nodesProp.arraySize; i++)
            {
                SerializedProperty node = nodesProp.GetArrayElementAtIndex(i);
                SerializedProperty idProp = node.FindPropertyRelative(nameof(DialogueNode.NodeId));
                if (idProp != null && !string.IsNullOrWhiteSpace(idProp.stringValue))
                    ids.Add(idProp.stringValue);
            }
            return ids;
        }

        // ----- Node body -----

        private static void DrawNode(SerializedProperty nodesProp, int nodeIndex, string ownerId)
        {
            SerializedProperty node = nodesProp.GetArrayElementAtIndex(nodeIndex);
            SerializedProperty idProp = node.FindPropertyRelative(nameof(DialogueNode.NodeId));
            SerializedProperty lineProp = node.FindPropertyRelative(nameof(DialogueNode.SpeakerLine));
            SerializedProperty optsProp = node.FindPropertyRelative(nameof(DialogueNode.Options));

            string currentId = idProp != null ? idProp.stringValue : $"node_{nodeIndex}";
            string foldoutKey = $"{NodeFoldoutPrefPrefix}{ownerId}.{currentId}";
            bool open = EditorPrefs.GetBool(foldoutKey, true);

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUIStyle headerStyle = new(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            bool newOpen = EditorGUILayout.Foldout(open, $"Node: {currentId}", true, headerStyle);
            if (newOpen != open)
                EditorPrefs.SetBool(foldoutKey, newOpen);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", GUILayout.Width(24f)))
            {
                if (EditorUtility.DisplayDialog("Delete Node", $"Delete node '{currentId}'?", "Delete", "Cancel"))
                {
                    nodesProp.DeleteArrayElementAtIndex(nodeIndex);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (newOpen)
            {
                if (idProp != null)
                    EditorGUILayout.PropertyField(idProp);
                if (lineProp != null)
                    EditorGUILayout.PropertyField(lineProp);

                if (optsProp != null)
                    DrawOptionsList(node, optsProp, nodesProp, ownerId);
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawOptionsList(SerializedProperty nodeProp, SerializedProperty optsProp, SerializedProperty nodesProp, string ownerId)
        {
            string cacheKey = ownerId + "::" + optsProp.propertyPath;
            if (!_optionListCache.TryGetValue(cacheKey, out ReorderableList list)
                || list == null
                || list.serializedProperty == null
                || list.serializedProperty.serializedObject != optsProp.serializedObject)
            {
                list = BuildOptionsList(optsProp, nodesProp);
                _optionListCache[cacheKey] = list;
            }
            else
            {
                list.serializedProperty = optsProp;
            }

            list.DoLayoutList();
        }

        private static ReorderableList BuildOptionsList(SerializedProperty optsProp, SerializedProperty nodesProp)
        {
            ReorderableList list = new(optsProp.serializedObject, optsProp, true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(rect, $"Options ({optsProp.arraySize})", EditorStyles.boldLabel);
                },
                elementHeightCallback = index =>
                {
                    if (index < 0 || index >= optsProp.arraySize)
                        return EditorGUIUtility.singleLineHeight;
                    return CalcOptionHeight(optsProp.GetArrayElementAtIndex(index));
                },
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    if (index < 0 || index >= optsProp.arraySize)
                        return;
                    DrawOption(rect, optsProp.GetArrayElementAtIndex(index), nodesProp);
                },
                onAddCallback = l =>
                {
                    int last = optsProp.arraySize;
                    optsProp.arraySize = last + 1;
                    SerializedProperty newOpt = optsProp.GetArrayElementAtIndex(last);
                    SerializedProperty labelProp = newOpt.FindPropertyRelative(nameof(DialogueOption.Label));
                    SerializedProperty actionProp = newOpt.FindPropertyRelative(nameof(DialogueOption.Action));
                    SerializedProperty localFlagProp = newOpt.FindPropertyRelative(nameof(DialogueOption.LocalFlag));
                    SerializedProperty mutationProp = newOpt.FindPropertyRelative(nameof(DialogueOption.FlagMutation));
                    if (labelProp != null) labelProp.stringValue = string.Empty;
                    if (actionProp != null) actionProp.enumValueIndex = (int)DialogueOptionAction.GoToNode;
                    if (localFlagProp != null) localFlagProp.stringValue = string.Empty;
                    if (mutationProp != null) mutationProp.enumValueIndex = (int)DialogueFlagMutation.None;
                }
            };

            return list;
        }

        private static float CalcOptionHeight(SerializedProperty opt)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float space = EditorGUIUtility.standardVerticalSpacing;
            int rows = 4; // Label, Action, FlagMutation, Visibility.Rule
            DialogueOptionAction action = (DialogueOptionAction)(opt.FindPropertyRelative(nameof(DialogueOption.Action))?.enumValueIndex ?? 0);
            DialogueFlagMutation mutation = (DialogueFlagMutation)(opt.FindPropertyRelative(nameof(DialogueOption.FlagMutation))?.enumValueIndex ?? 0);

            switch (action)
            {
                case DialogueOptionAction.GoToNode: rows += 1; break;
                case DialogueOptionAction.OfferQuest:
                case DialogueOptionAction.TurnInQuest: rows += 1; break;
                case DialogueOptionAction.NotifyTalkedAboutTopic: rows += 2; break; // Topic + NextNode
                default: break;
            }

            DialogueVisibilityRule rule = (DialogueVisibilityRule)(opt.FindPropertyRelative(nameof(DialogueOption.Visibility))
                ?.FindPropertyRelative(nameof(DialogueOptionVisibility.Rule))?.enumValueIndex ?? 0);
            if (mutation != DialogueFlagMutation.None || IsLocalFlagVisibility(rule))
                rows += 1;
            if (rule != DialogueVisibilityRule.Always)
            {
                if (!IsLocalFlagVisibility(rule))
                    rows += 1; // QuestId
                if (rule == DialogueVisibilityRule.QuestOnObjective)
                    rows += 1; // RequiredObjectiveIndex
            }

            return rows * (line + space) + 6f;
        }

        private static void DrawOption(Rect rect, SerializedProperty opt, SerializedProperty nodesProp)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float space = EditorGUIUtility.standardVerticalSpacing;
            Rect row = new(rect.x, rect.y + 2f, rect.width, line);

            SerializedProperty labelProp = opt.FindPropertyRelative(nameof(DialogueOption.Label));
            SerializedProperty actionProp = opt.FindPropertyRelative(nameof(DialogueOption.Action));
            SerializedProperty nextNodeProp = opt.FindPropertyRelative(nameof(DialogueOption.NextNodeId));
            SerializedProperty questProp = opt.FindPropertyRelative(nameof(DialogueOption.QuestId));
            SerializedProperty topicProp = opt.FindPropertyRelative(nameof(DialogueOption.Topic));
            SerializedProperty localFlagProp = opt.FindPropertyRelative(nameof(DialogueOption.LocalFlag));
            SerializedProperty mutationProp = opt.FindPropertyRelative(nameof(DialogueOption.FlagMutation));
            SerializedProperty visProp = opt.FindPropertyRelative(nameof(DialogueOption.Visibility));

            if (labelProp != null)
            {
                EditorGUI.PropertyField(row, labelProp);
                row.y += line + space;
            }
            if (actionProp != null)
            {
                EditorGUI.PropertyField(row, actionProp);
                row.y += line + space;
            }

            DialogueOptionAction action = (DialogueOptionAction)(actionProp?.enumValueIndex ?? 0);
            switch (action)
            {
                case DialogueOptionAction.GoToNode:
                    DrawNodeIdPopup(row, nextNodeProp, nodesProp);
                    row.y += line + space;
                    break;
                case DialogueOptionAction.OfferQuest:
                case DialogueOptionAction.TurnInQuest:
                    if (questProp != null)
                    {
                        ReferenceDropdown.DrawQuest(row, new GUIContent("Quest"), questProp);
                        row.y += line + space;
                    }
                    break;
                case DialogueOptionAction.NotifyTalkedAboutTopic:
                    if (topicProp != null)
                    {
                        EditorGUI.PropertyField(row, topicProp);
                        row.y += line + space;
                    }
                    DrawNodeIdPopup(row, nextNodeProp, nodesProp, label: "Then go to");
                    row.y += line + space;
                    break;
            }

            if (mutationProp != null)
            {
                EditorGUI.PropertyField(row, mutationProp, new GUIContent("Flag Mutation"));
                row.y += line + space;
            }

            DialogueFlagMutation mutation = (DialogueFlagMutation)(mutationProp?.enumValueIndex ?? 0);
            DialogueVisibilityRule visibilityRule = (DialogueVisibilityRule)(visProp
                ?.FindPropertyRelative(nameof(DialogueOptionVisibility.Rule))?.enumValueIndex ?? 0);
            if ((mutation != DialogueFlagMutation.None || IsLocalFlagVisibility(visibilityRule)) && localFlagProp != null)
            {
                EditorGUI.PropertyField(row, localFlagProp, new GUIContent("Local Flag"));
                row.y += line + space;
            }

            if (visProp != null)
                DrawVisibility(ref row, visProp);
        }

        private static void DrawNodeIdPopup(Rect row, SerializedProperty nextNodeProp, SerializedProperty nodesProp, string label = "Go to Node")
        {
            if (nextNodeProp == null)
                return;

            List<string> ids = new() { RootShortcut };
            ids.AddRange(CollectNodeIds(nodesProp));

            string current = nextNodeProp.stringValue ?? string.Empty;
            int currentIndex = -1;
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], current, System.StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    break;
                }
            }

            string[] display;
            if (currentIndex < 0 && !string.IsNullOrEmpty(current))
            {
                ids.Insert(0, $"{MissingPrefix} {current}");
                currentIndex = 0;
                display = ids.ToArray();
            }
            else
            {
                display = ids.ToArray();
                if (currentIndex < 0)
                    currentIndex = 0;
            }

            int newIndex = EditorGUI.Popup(row, label, currentIndex, display);
            if (newIndex != currentIndex && newIndex >= 0 && newIndex < ids.Count)
            {
                string picked = ids[newIndex];
                if (picked.StartsWith(MissingPrefix))
                    return;
                nextNodeProp.stringValue = string.Equals(picked, RootShortcut, System.StringComparison.Ordinal) ? string.Empty : picked;
            }
        }

        private static void DrawVisibility(ref Rect row, SerializedProperty visProp)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float space = EditorGUIUtility.standardVerticalSpacing;

            SerializedProperty ruleProp = visProp.FindPropertyRelative(nameof(DialogueOptionVisibility.Rule));
            SerializedProperty questProp = visProp.FindPropertyRelative(nameof(DialogueOptionVisibility.QuestId));
            SerializedProperty objProp = visProp.FindPropertyRelative(nameof(DialogueOptionVisibility.RequiredObjectiveIndex));

            if (ruleProp != null)
            {
                EditorGUI.PropertyField(row, ruleProp, new GUIContent("Visible When"));
                row.y += line + space;
            }

            DialogueVisibilityRule rule = (DialogueVisibilityRule)(ruleProp?.enumValueIndex ?? 0);
            if (rule == DialogueVisibilityRule.Always)
                return;

            if (IsLocalFlagVisibility(rule))
                return;

            if (questProp != null)
            {
                ReferenceDropdown.DrawQuest(row, new GUIContent("Quest"), questProp);
                row.y += line + space;
            }
            if (rule == DialogueVisibilityRule.QuestOnObjective && objProp != null)
            {
                EditorGUI.PropertyField(row, objProp, new GUIContent("Objective Index (-1 = any)"));
                row.y += line + space;
            }
        }

        private static bool IsLocalFlagVisibility(DialogueVisibilityRule rule)
        {
            return rule == DialogueVisibilityRule.LocalFlagSet
                || rule == DialogueVisibilityRule.LocalFlagNotSet;
        }
    }
}
#endif
