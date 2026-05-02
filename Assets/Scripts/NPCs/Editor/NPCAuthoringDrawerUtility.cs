using System;
using System.Collections.Generic;
using Sol.AI;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Sol.Editor
{
    public static class NPCAuthoringDrawerUtility
    {
        private static readonly HashSet<int> _traderRevealOverrides = new();
        private const string FoldoutPrefPrefix = "Sol.NPCEditor.Section.";

        // ReorderableList caches keyed by inventory instanceId.
        private static readonly Dictionary<int, ReorderableList> _seedListCache = new();

        public static void DrawNPCInspector(SerializedObject serializedObject, NPCSoul soul, bool showWarnings = true)
        {
            if (serializedObject == null)
                return;

            if (showWarnings && soul != null)
                DrawWarnings(soul);

            DrawIdentitySection(serializedObject, soul);
            DrawAIMovementSection(soul);
            DrawInventorySection(soul);
            DrawDialogueAndTradingSection(serializedObject, soul);
            DrawDeathAndLootSection(serializedObject, soul);
            DrawReferencesSection(serializedObject, soul);
            DrawDebugSection(serializedObject, soul);
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

        // ----- Section: Identity -----

        private static void DrawIdentitySection(SerializedObject serializedObject, NPCSoul soul)
        {
            if (!BeginSection("Identity", defaultOpen: true))
            {
                EndSection();
                return;
            }

            SerializedProperty nameProp = serializedObject.FindProperty("_characterName");
            if (nameProp != null)
            {
                EditorGUILayout.LabelField("Character Name", EditorStyles.miniLabel);
                GUIStyle bigField = new GUIStyle(EditorStyles.textField) { fontSize = 14, fixedHeight = 22f };
                nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue ?? string.Empty, bigField);
            }

            DrawProperty(serializedObject, "_soulType");

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

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Vitals", EditorStyles.miniBoldLabel);
            DrawProperty(serializedObject, "_healthStat", includeChildren: true);
            DrawProperty(serializedObject, "_staminaStat", includeChildren: true);

            EditorGUILayout.Space(4f);
            DrawProperty(serializedObject, "_authoringTemplate");
            DrawProperty(serializedObject, "_authoringNotes");

            EndSection();
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

        // ----- Section: AI & Movement -----

        private static void DrawAIMovementSection(NPCSoul soul)
        {
            if (!BeginSection("AI & Movement", defaultOpen: true))
            {
                EndSection();
                return;
            }

            if (soul == null)
            {
                EndSection();
                return;
            }

            AI_NPC aiNpc = soul.GetComponent<AI_NPC>();
            if (aiNpc == null)
            {
                EditorGUILayout.HelpBox("No AI_NPC component on this prefab.", MessageType.Info);
                if (GUILayout.Button("Add AI_NPC Component"))
                    Undo.AddComponent<AI_NPC>(soul.gameObject);
                EndSection();
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
            aiSO.ApplyModifiedProperties();

            EndSection();
        }

        // ----- Section: Inventory -----

        private static void DrawInventorySection(NPCSoul soul)
        {
            if (!BeginSection("Inventory", defaultOpen: true))
            {
                EndSection();
                return;
            }

            if (soul == null)
            {
                EndSection();
                return;
            }

            Sol.Inventory inventory = soul.GetComponent<Sol.Inventory>();
            if (inventory == null)
            {
                EditorGUILayout.HelpBox("No Inventory component on this prefab. Trade and corpse loot rely on it.", MessageType.Info);
                if (GUILayout.Button("Add Inventory Component"))
                    Undo.AddComponent<Sol.Inventory>(soul.gameObject);
                EndSection();
                return;
            }

            SerializedObject invSO = new(inventory);
            invSO.Update();
            DrawProperty(invSO, "_capacity");
            DrawProperty(invSO, "_gold");

            EditorGUILayout.Space(4f);
            DrawSeedContentsList(invSO, inventory);

            invSO.ApplyModifiedProperties();
            EndSection();
        }

        private static void DrawSeedContentsList(SerializedObject invSO, Sol.Inventory inventory)
        {
            SerializedProperty contents = invSO.FindProperty("_inspectorContents");
            if (contents == null)
                return;

            int key = inventory.GetInstanceID();
            if (!_seedListCache.TryGetValue(key, out ReorderableList list) || list == null || list.serializedProperty == null || list.serializedProperty.serializedObject != invSO)
            {
                list = BuildSeedList(invSO, contents);
                _seedListCache[key] = list;
            }

            if (contents.arraySize == 0)
                EditorGUILayout.HelpBox("No seed items. Click + to add.", MessageType.None);

            list.DoLayoutList();
        }

        private static ReorderableList BuildSeedList(SerializedObject invSO, SerializedProperty contents)
        {
            ReorderableList list = new ReorderableList(invSO, contents, true, true, true, true)
            {
                elementHeight = EditorGUIUtility.singleLineHeight + 6f,
                drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(rect, $"Seed Contents ({contents.arraySize})", EditorStyles.boldLabel);
                },
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    if (index < 0 || index >= contents.arraySize)
                        return;
                    SerializedProperty element = contents.GetArrayElementAtIndex(index);
                    SerializedProperty idProp = element.FindPropertyRelative("_itemId");
                    SerializedProperty qtyProp = element.FindPropertyRelative("Quantity");

                    Rect row = new Rect(rect.x, rect.y + 3f, rect.width, EditorGUIUtility.singleLineHeight);
                    float gap = 6f;
                    float qtyWidth = 70f;
                    float itemWidth = row.width - qtyWidth - gap;
                    Rect itemRect = new Rect(row.x, row.y, itemWidth, row.height);
                    Rect qtyRect = new Rect(row.x + itemWidth + gap, row.y, qtyWidth, row.height);

                    if (idProp != null)
                        ReferenceDropdown.DrawItem(itemRect, GUIContent.none, idProp);
                    if (qtyProp != null)
                        EditorGUI.PropertyField(qtyRect, qtyProp, GUIContent.none);
                },
                onAddCallback = l =>
                {
                    int last = contents.arraySize;
                    contents.arraySize = last + 1;
                    SerializedProperty element = contents.GetArrayElementAtIndex(last);
                    SerializedProperty idProp = element.FindPropertyRelative("_itemId");
                    SerializedProperty qtyProp = element.FindPropertyRelative("Quantity");
                    if (idProp != null) idProp.stringValue = string.Empty;
                    if (qtyProp != null) qtyProp.intValue = 1;
                }
            };

            return list;
        }

        // ----- Section: Dialogue & Trading -----

        private static void DrawDialogueAndTradingSection(SerializedObject serializedObject, NPCSoul soul)
        {
            if (soul == null)
            {
                if (BeginSection("Dialogue & Trading", defaultOpen: true)) { /* nothing */ }
                EndSection();
                return;
            }

            NpcTrader trader = soul.GetComponent<NpcTrader>();
            NPCAuthoringTemplate template = GetTemplate(serializedObject);
            bool hasComponent = trader != null;
            bool sectionApplies = NPCTemplateRules.ShowTraderSection(template, hasComponent);

            if (!sectionApplies)
            {
                if (hasComponent && !IsTraderRevealed(serializedObject))
                {
                    if (BeginSection("Dialogue & Trading", defaultOpen: true))
                    {
                        DrawHiddenSectionNotice(
                            $"This NPC has an NpcTrader component but the '{template}' template hides the Trader section.",
                            () => SetTraderRevealed(serializedObject, true),
                            () => SetTemplate(serializedObject, NPCAuthoringTemplate.Trader));
                    }
                    EndSection();
                    return;
                }

                if (!IsTraderRevealed(serializedObject))
                    return;
            }
            else
            {
                SetTraderRevealed(serializedObject, false);
            }

            if (!BeginSection("Dialogue & Trading", defaultOpen: true))
            {
                EndSection();
                return;
            }

            if (trader == null)
            {
                EditorGUILayout.HelpBox("No NpcTrader component. Add one to enable trade, conversation, and corpse-loot prompts.", MessageType.Info);
                if (GUILayout.Button("Add NpcTrader Component"))
                {
                    if (soul.GetComponent<Sol.Inventory>() == null)
                        Undo.AddComponent<Sol.Inventory>(soul.gameObject);
                    Undo.AddComponent<NpcTrader>(soul.gameObject);
                }
                EndSection();
                return;
            }

            SerializedObject traderSO = new(trader);
            traderSO.Update();
            DrawProperty(traderSO, "_useConversationWindow");
            DrawProperty(traderSO, "_talkPrompt");
            DrawProperty(traderSO, "_prompt");
            DrawProperty(traderSO, "_tradeOptionLabel");
            DrawProperty(traderSO, "_goodbyeOptionLabel");
            DrawProperty(traderSO, "_greetingLine");
            DrawProperty(traderSO, "_speakerIcon");
            traderSO.ApplyModifiedProperties();

            EditorGUILayout.Space(4f);
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
                    EditorGUILayout.EndHorizontal();
                    EndSection();
                    return;
                }
            }
            EditorGUILayout.EndHorizontal();

            EndSection();
        }

        // ----- Section: Death & Loot -----

        private static void DrawDeathAndLootSection(SerializedObject serializedObject, NPCSoul soul)
        {
            if (!BeginSection("Death & Loot", defaultOpen: true))
            {
                EndSection();
                return;
            }

            if (soul == null)
            {
                EndSection();
                return;
            }

            AI_NPC aiNpc = soul.GetComponent<AI_NPC>();
            if (aiNpc != null)
            {
                SerializedObject aiSO = new(aiNpc);
                aiSO.Update();
                DrawProperty(aiSO, "deathTrigger");
                DrawProperty(aiSO, "deathStateName");
                DrawProperty(aiSO, "deathAnimDuration");
                DrawProperty(aiSO, "deathHoldNormalizedTime");
                DrawProperty(aiSO, "ragdollAfterDeath");
                aiSO.ApplyModifiedProperties();
            }

            NpcTrader trader = soul.GetComponent<NpcTrader>();
            if (trader != null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Corpse Loot", EditorStyles.miniBoldLabel);
                SerializedObject traderSO = new(trader);
                traderSO.Update();
                DrawProperty(traderSO, "_lootPrompt");

                SerializedProperty goldIdProp = traderSO.FindProperty("_goldLootItemId");
                if (goldIdProp != null)
                    ReferenceDropdown.DrawItemLayout(new GUIContent("Gold Loot Item Id"), goldIdProp);

                DrawProperty(traderSO, "_goldLootItemTemplate");
                traderSO.ApplyModifiedProperties();
            }

            EndSection();
        }

        // ----- Section: References -----

        private static void DrawReferencesSection(SerializedObject serializedObject, NPCSoul soul)
        {
            if (!BeginSection("References", defaultOpen: false))
            {
                EndSection();
                return;
            }

            if (soul == null)
            {
                EndSection();
                return;
            }

            AI_NPC aiNpc = soul.GetComponent<AI_NPC>();
            if (aiNpc != null)
            {
                EditorGUILayout.LabelField("AI_NPC", EditorStyles.miniBoldLabel);
                SerializedObject aiSO = new(aiNpc);
                aiSO.Update();
                DrawProperty(aiSO, "soul");
                DrawProperty(aiSO, "animator");
                DrawProperty(aiSO, "fakeCam");
                aiSO.ApplyModifiedProperties();
                EditorGUILayout.Space(4f);
            }

            Sol.Inventory inventory = soul.GetComponent<Sol.Inventory>();
            if (inventory != null)
            {
                EditorGUILayout.LabelField("Inventory", EditorStyles.miniBoldLabel);
                SerializedObject invSO = new(inventory);
                invSO.Update();
                DrawProperty(invSO, "_owner");

                SerializedProperty ownerIdProp = invSO.FindProperty("_ownerId");
                if (ownerIdProp != null)
                    ReferenceDropdown.DrawNpcLayout(new GUIContent("Owner Id"), ownerIdProp);

                SerializedProperty keyIdProp = invSO.FindProperty("_requiredKeyItemId");
                if (keyIdProp != null)
                    ReferenceDropdown.DrawItemLayout(new GUIContent("Required Key Item"), keyIdProp);

                invSO.ApplyModifiedProperties();
            }

            EndSection();
        }

        // ----- Section: Debug -----

        private static void DrawDebugSection(SerializedObject serializedObject, NPCSoul soul)
        {
            if (!BeginSection("Debug", defaultOpen: false))
            {
                EndSection();
                return;
            }

            UnityEngine.Object target = serializedObject?.targetObject;
            GameObject prefabRoot = ResolvePrefabGameObject(target, soul);
            using (new EditorGUI.DisabledScope(target == null))
            {
                if (GUILayout.Button("Reveal in Project"))
                    EditorGUIUtility.PingObject(target);

                using (new EditorGUI.DisabledScope(prefabRoot == null))
                {
                    if (GUILayout.Button("Spawn in Scene"))
                        SpawnAtSceneViewPivot(prefabRoot);
                }
            }

            EndSection();
        }

        private static GameObject ResolvePrefabGameObject(UnityEngine.Object target, NPCSoul soul)
        {
            if (target is GameObject go)
                return go;
            if (target is Component component && component != null)
                return component.gameObject;
            return soul != null ? soul.gameObject : null;
        }

        private static void SpawnAtSceneViewPivot(GameObject prefab)
        {
            if (prefab == null)
                return;

            GameObject root = prefab;
            while (root.transform.parent != null)
                root = root.transform.parent.gameObject;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(root);
            if (instance == null)
                return;

            SceneView sv = SceneView.lastActiveSceneView;
            if (sv != null)
                instance.transform.position = sv.pivot;

            Undo.RegisterCreatedObjectUndo(instance, "Spawn NPC From Database");
            Selection.activeGameObject = instance;
        }

        // ----- Trader-reveal helpers -----

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

        private static void DrawHiddenSectionNotice(string reason, Action onReveal, Action onChangeTemplate)
        {
            EditorGUILayout.HelpBox(reason, MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reveal Section", GUILayout.Width(140f)))
                onReveal?.Invoke();
            if (onChangeTemplate != null && GUILayout.Button("Set Template: Trader", GUILayout.Width(170f)))
                onChangeTemplate.Invoke();
            EditorGUILayout.EndHorizontal();
        }

        // ----- Foldout / section helpers -----

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
