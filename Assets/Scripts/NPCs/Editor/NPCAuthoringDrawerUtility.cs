using System.Collections.Generic;
using Sol.AI;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    public static class NPCAuthoringDrawerUtility
    {
        private static readonly HashSet<int> _traderRevealOverrides = new();

        public static void DrawNPCInspector(SerializedObject serializedObject, NPCSoul soul, bool showWarnings = true)
        {
            if (serializedObject == null)
                return;

            if (showWarnings && soul != null)
                DrawWarnings(soul);

            DrawIdentitySection(serializedObject, soul);
            DrawVitalsSection(serializedObject);
            DrawAISection(soul);
            DrawInventorySection(soul);
            DrawTraderSection(serializedObject, soul);
            DrawAuthoringSection(serializedObject);
        }

        public static void DrawWarnings(NPCSoul soul)
        {
            List<NPCAuthoringWarning> warnings = NPCAuthoringValidator.Validate(soul);
            if (warnings.Count == 0)
                return;

            for (int i = 0; i < warnings.Count; i++)
            {
                NPCAuthoringWarning warning = warnings[i];
                MessageType type = warning.Severity switch
                {
                    NPCAuthoringWarningSeverity.Error => MessageType.Error,
                    NPCAuthoringWarningSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };
                EditorGUILayout.HelpBox(warning.Message, type);
            }

            EditorGUILayout.Space(4f);
        }

        private static void DrawIdentitySection(SerializedObject serializedObject, NPCSoul soul)
        {
            DrawHeader("Identity");

            using (new EditorGUI.DisabledScope(true))
                DrawProperty(serializedObject, "_ownerId");

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(soul == null))
            {
                if (GUILayout.Button("Regenerate OwnerId", GUILayout.Width(160f)))
                    RegenerateOwnerId(serializedObject);
            }
            EditorGUILayout.EndHorizontal();

            DrawProperty(serializedObject, "_soulType");
            DrawProperty(serializedObject, "_characterName");
        }

        private static void RegenerateOwnerId(SerializedObject serializedObject)
        {
            SerializedProperty ownerId = serializedObject.FindProperty("_ownerId");
            if (ownerId == null)
                return;

            string next = NPCAuthoringEditorUtility.NextOwnerId();
            if (string.IsNullOrEmpty(next))
                return;

            ownerId.stringValue = next;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            if (serializedObject.targetObject != null)
                EditorUtility.SetDirty(serializedObject.targetObject);
            NPCRegistry.ScheduleEditorSync();
        }

        private static void DrawVitalsSection(SerializedObject serializedObject)
        {
            DrawHeader("Vitals");
            DrawProperty(serializedObject, "_healthStat", includeChildren: true);
            DrawProperty(serializedObject, "_staminaStat", includeChildren: true);
        }

        private static void DrawAISection(NPCSoul soul)
        {
            if (soul == null)
                return;

            AI_NPC aiNpc = soul.GetComponent<AI_NPC>();
            DrawHeader("AI");

            if (aiNpc == null)
            {
                EditorGUILayout.HelpBox("No AI_NPC component on this prefab.", MessageType.Info);
                if (GUILayout.Button("Add AI_NPC Component"))
                    Undo.AddComponent<AI_NPC>(soul.gameObject);
                return;
            }

            SerializedObject aiSO = new(aiNpc);
            aiSO.Update();
            DrawProperty(aiSO, "config");
            DrawProperty(aiSO, "patrolRoot");
            DrawProperty(aiSO, "autoUsePatrolRootChildren");
            DrawProperty(aiSO, "patrolPoints", includeChildren: true);
            DrawProperty(aiSO, "patrolPointNavMeshSnapDistance");
            DrawProperty(aiSO, "patrolSplineOverride");
            DrawProperty(aiSO, "soul");
            DrawProperty(aiSO, "animator");
            DrawProperty(aiSO, "fakeCam");
            DrawProperty(aiSO, "deathTrigger");
            DrawProperty(aiSO, "deathStateName");
            DrawProperty(aiSO, "deathAnimDuration");
            DrawProperty(aiSO, "deathHoldNormalizedTime");
            DrawProperty(aiSO, "ragdollAfterDeath");
            aiSO.ApplyModifiedProperties();
        }

        private static void DrawInventorySection(NPCSoul soul)
        {
            if (soul == null)
                return;

            Sol.Inventory inventory = soul.GetComponent<Sol.Inventory>();
            DrawHeader("Inventory");

            if (inventory == null)
            {
                EditorGUILayout.HelpBox("No Inventory component on this prefab. Trade and corpse loot rely on it.", MessageType.Info);
                if (GUILayout.Button("Add Inventory Component"))
                    Undo.AddComponent<Sol.Inventory>(soul.gameObject);
                return;
            }

            SerializedObject invSO = new(inventory);
            invSO.Update();
            SerializedProperty iter = invSO.GetIterator();
            iter.NextVisible(true); // skip script
            while (iter.NextVisible(false))
                EditorGUILayout.PropertyField(iter, true);
            invSO.ApplyModifiedProperties();
        }

        private static void DrawTraderSection(SerializedObject serializedObject, NPCSoul soul)
        {
            if (soul == null)
                return;

            NpcTrader trader = soul.GetComponent<NpcTrader>();
            NPCAuthoringTemplate template = GetTemplate(serializedObject);
            bool hasComponent = trader != null;
            bool sectionApplies = NPCTemplateRules.ShowTraderSection(template, hasComponent);

            if (!sectionApplies)
            {
                if (hasComponent && !IsTraderRevealed(serializedObject))
                {
                    DrawHiddenSectionNotice(
                        "Trader / Conversation",
                        $"This NPC has an NpcTrader component but the '{template}' template hides the Trader section. Reveal to edit, or change the template to Trader/QuestGiver.",
                        () => SetTraderRevealed(serializedObject, true),
                        () => SetTemplate(serializedObject, NPCAuthoringTemplate.Trader));
                    return;
                }

                if (!IsTraderRevealed(serializedObject))
                    return;
            }
            else
            {
                SetTraderRevealed(serializedObject, false);
            }

            DrawHeader("Trader / Conversation");

            if (trader == null)
            {
                EditorGUILayout.HelpBox("No NpcTrader component. Add one to enable trade, conversation, and corpse-loot prompts.", MessageType.Info);
                if (GUILayout.Button("Add NpcTrader Component"))
                {
                    if (soul.GetComponent<Sol.Inventory>() == null)
                        Undo.AddComponent<Sol.Inventory>(soul.gameObject);
                    Undo.AddComponent<NpcTrader>(soul.gameObject);
                }
                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Remove NpcTrader", GUILayout.Width(160f)))
            {
                if (EditorUtility.DisplayDialog(
                    "Remove NpcTrader",
                    "Remove the NpcTrader component from this prefab? Trade and corpse-loot interactions will be disabled.",
                    "Remove",
                    "Cancel"))
                {
                    Undo.DestroyObjectImmediate(trader);
                    return;
                }
            }
            EditorGUILayout.EndHorizontal();

            SerializedObject traderSO = new(trader);
            traderSO.Update();
            SerializedProperty iter = traderSO.GetIterator();
            iter.NextVisible(true); // skip script
            while (iter.NextVisible(false))
                EditorGUILayout.PropertyField(iter, true);
            traderSO.ApplyModifiedProperties();
        }

        private static NPCAuthoringTemplate GetTemplate(SerializedObject serializedObject)
        {
            SerializedProperty templateProp = serializedObject?.FindProperty("_authoringTemplate");
            return templateProp != null
                ? (NPCAuthoringTemplate)templateProp.enumValueIndex
                : NPCAuthoringTemplate.None;
        }

        private static void SetTemplate(SerializedObject serializedObject, NPCAuthoringTemplate template)
        {
            SerializedProperty templateProp = serializedObject?.FindProperty("_authoringTemplate");
            if (templateProp == null)
                return;

            templateProp.enumValueIndex = (int)template;
            serializedObject.ApplyModifiedProperties();
        }

        private static bool IsTraderRevealed(SerializedObject serializedObject)
        {
            int id = GetTargetId(serializedObject);
            return id != 0 && _traderRevealOverrides.Contains(id);
        }

        private static void SetTraderRevealed(SerializedObject serializedObject, bool revealed)
        {
            int id = GetTargetId(serializedObject);
            if (id == 0)
                return;

            if (revealed)
                _traderRevealOverrides.Add(id);
            else
                _traderRevealOverrides.Remove(id);
        }

        private static int GetTargetId(SerializedObject serializedObject)
        {
            return serializedObject != null && serializedObject.targetObject != null
                ? serializedObject.targetObject.GetInstanceID()
                : 0;
        }

        private static void DrawHiddenSectionNotice(string label, string reason, System.Action onReveal, System.Action onChangeTemplate)
        {
            DrawHeader(label);
            EditorGUILayout.HelpBox(reason, MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reveal Section", GUILayout.Width(140f)))
                onReveal?.Invoke();
            if (onChangeTemplate != null && GUILayout.Button("Set Template: Trader", GUILayout.Width(170f)))
                onChangeTemplate.Invoke();
            EditorGUILayout.EndHorizontal();
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
