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
            DrawConversationSection(soul);
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

            DrawProperty(serializedObject, "_entityType", label: "Entity Type");

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
            DrawArchetype(serializedObject);
            DrawProperty(serializedObject, "_canTrade", label: "Trader");
            DrawProperty(serializedObject, "_isHostile", label: "Hostile");
            DrawArchetypeActions(serializedObject, soul);
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

        private static void DrawArchetypeActions(SerializedObject serializedObject, NPCSoul soul)
        {
            NPCArchetype archetype = GetArchetype(serializedObject);
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Fill Missing Defaults adds only empty/default archetype setup. Reapply Archetype overwrites archetype-owned fields.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(soul == null || archetype == NPCArchetype.None))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Fill Missing Defaults", GUILayout.Width(150f)))
                    FillMissingArchetypeDefaults(serializedObject, soul, archetype);
                if (GUILayout.Button("Reapply Archetype...", GUILayout.Width(150f)))
                    ReapplyArchetype(serializedObject, soul, archetype);
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void FillMissingArchetypeDefaults(SerializedObject serializedObject, NPCSoul soul, NPCArchetype archetype)
        {
            if (soul == null || archetype == NPCArchetype.None)
                return;

            serializedObject.ApplyModifiedProperties();
            NPCAuthoringEditorUtility.FillMissingArchetypeDefaults(soul, archetype, GetCharacterName(serializedObject, soul));
            serializedObject.Update();
            NPCRegistry.ScheduleEditorSync();
        }

        private static void ReapplyArchetype(SerializedObject serializedObject, NPCSoul soul, NPCArchetype archetype)
        {
            if (soul == null || archetype == NPCArchetype.None)
                return;

            List<string> changes = NPCAuthoringEditorUtility.BuildArchetypeOverwritePreview(archetype);
            string message = "This will reapply the archetype defaults:\n\n- "
                + string.Join("\n- ", changes)
                + "\n\nUse Fill Missing Defaults for the non-destructive path.";

            if (!EditorUtility.DisplayDialog("Reapply NPC Archetype", message, "Apply", "Cancel"))
                return;

            serializedObject.ApplyModifiedProperties();
            NPCAuthoringEditorUtility.ApplyArchetype(soul, archetype, GetCharacterName(serializedObject, soul));
            serializedObject.Update();
            NPCRegistry.ScheduleEditorSync();
        }

        private static string GetCharacterName(SerializedObject serializedObject, NPCSoul soul)
        {
            SerializedProperty nameProp = serializedObject?.FindProperty("_characterName");
            if (nameProp != null && !string.IsNullOrWhiteSpace(nameProp.stringValue))
                return nameProp.stringValue;

            return soul != null ? soul.CharacterName : string.Empty;
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
            DrawProperty(aiSO, "_scheduleDefinition");
            DrawProperty(aiSO, "_scheduleRefreshSeconds");
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
            if (!BeginSection("Dialogue & Trading", defaultOpen: true))
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
                EditorGUILayout.HelpBox("No AI_NPC component on this prefab. Trade, conversation, and corpse-loot prompts live on AI_NPC.", MessageType.Info);
                if (GUILayout.Button("Add AI_NPC Component"))
                    Undo.AddComponent<AI_NPC>(soul.gameObject);
                EndSection();
                return;
            }

            EditorGUILayout.HelpBox(
                aiNpc.IsTrader
                    ? "Trader checkbox enables trade options."
                    : "Trader checkbox is off. Trade-related conversation options are hidden until this NPC can trade.",
                aiNpc.IsTrader ? MessageType.None : MessageType.Info);

            SerializedObject aiSO = new(aiNpc);
            aiSO.Update();
            SerializedProperty shopIdProp = aiSO.FindProperty("_shopId");
            if (shopIdProp != null)
                ReferenceDropdown.DrawShopLayout(new GUIContent("Shop"), shopIdProp);
            DrawProperty(aiSO, "_useConversationWindow");
            DrawProperty(aiSO, "_talkPrompt");
            DrawProperty(aiSO, "_prompt");
            DrawProperty(aiSO, "_tradeOptionLabel");
            DrawProperty(aiSO, "_goodbyeOptionLabel");
            DrawProperty(aiSO, "_greetingLine");
            DrawProperty(aiSO, "_speakerIcon");
            aiSO.ApplyModifiedProperties();

            EndSection();
        }

        // ----- Section: Conversation -----

        private static void DrawConversationSection(NPCSoul soul)
        {
            if (!BeginSection("Conversation", defaultOpen: true))
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
                EditorGUILayout.HelpBox("Add an AI_NPC component to author a dialogue graph.", MessageType.Info);
                EndSection();
                return;
            }

            SerializedObject aiSO = new(aiNpc);
            aiSO.Update();
            SerializedProperty graphProp = aiSO.FindProperty("_dialogueGraph");
            if (graphProp != null)
                DialogueGraphDrawer.Draw(graphProp, aiNpc.GetInstanceID().ToString());
            else
                EditorGUILayout.HelpBox("AI_NPC has no _dialogueGraph field - recompile required.", MessageType.Warning);
            aiSO.ApplyModifiedProperties();

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

            if (aiNpc != null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Corpse Loot", EditorStyles.miniBoldLabel);
                SerializedObject lootSO = new(aiNpc);
                lootSO.Update();
                DrawProperty(lootSO, "_lootPrompt");

                SerializedProperty goldIdProp = lootSO.FindProperty("_goldLootItemId");
                if (goldIdProp != null)
                    ReferenceDropdown.DrawItemLayout(new GUIContent("Gold Loot Item Id"), goldIdProp);

                DrawProperty(lootSO, "_goldLootItemTemplate");
                DrawProperty(lootSO, "_maxGoldItemizeAttemptsPerOpen");
                lootSO.ApplyModifiedProperties();
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

        // ----- Archetype helper -----

        private static NPCArchetype GetArchetype(SerializedObject serializedObject)
        {
            SerializedProperty archetypeProp = serializedObject?.FindProperty("_npcArchetype");
            return archetypeProp != null
                ? (NPCArchetype)archetypeProp.enumValueIndex
                : NPCArchetype.None;
        }

        private static void DrawArchetype(SerializedObject serializedObject)
        {
            SerializedProperty archetypeProp = serializedObject?.FindProperty("_npcArchetype");
            if (archetypeProp == null)
                return;

            NPCArchetype current = (NPCArchetype)archetypeProp.enumValueIndex;

            NPCArchetype[] options =
            {
                NPCArchetype.None,
                NPCArchetype.Civilian,
                NPCArchetype.Guard,
                NPCArchetype.Bandit,
                NPCArchetype.QuestGiver,
                NPCArchetype.Unique
            };
            string[] labels = { "None", "Civilian", "Guard", "Bandit", "Quest Giver", "Unique" };

            int currentIndex = 0;
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == current)
                {
                    currentIndex = i;
                    break;
                }
            }

            int picked = EditorGUILayout.Popup("NPC Archetype", currentIndex, labels);
            if (picked != currentIndex)
                archetypeProp.enumValueIndex = (int)options[Mathf.Clamp(picked, 0, options.Length - 1)];
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

        private static void DrawProperty(SerializedObject serializedObject, string propertyName, bool includeChildren = true, string label = null)
        {
            SerializedProperty property = serializedObject?.FindProperty(propertyName);
            if (property != null)
            {
                if (string.IsNullOrWhiteSpace(label))
                    EditorGUILayout.PropertyField(property, includeChildren);
                else
                    EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren);
            }
        }
    }
}
