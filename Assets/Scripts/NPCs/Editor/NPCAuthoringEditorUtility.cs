using System.Collections.Generic;
using System.IO;
using Sol.AI;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    public static class NPCAuthoringEditorUtility
    {
        public const string DefaultNpcFolder = "Assets/Scripts/NPCs/Prefabs";

        public static NPCSoul CreateNPCPrefab(NPCAuthoringTemplate template, string npcName = null)
        {
            EnsureDefaultFolder();

            string resolvedName = string.IsNullOrWhiteSpace(npcName)
                ? DefaultNameFor(template)
                : npcName.Trim();

            GameObject npcObject = new(resolvedName);
            NPCSoul soul = npcObject.AddComponent<NPCSoul>();
            EnsureCanonicalComponents(soul, template);
            ApplyTemplate(soul, template, resolvedName, assignNewId: true);

            string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultNpcFolder}/{resolvedName}.prefab");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(npcObject, path);
            Object.DestroyImmediate(npcObject);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);

            NPCRegistry.ForceEditorSyncNow();
            return prefab != null ? prefab.GetComponent<NPCSoul>() : null;
        }

        public static NPCSoul DuplicateNPCPrefab(NPCSoul source)
        {
            if (source == null)
                return null;

            string sourcePath = AssetDatabase.GetAssetPath(source.gameObject);
            if (string.IsNullOrWhiteSpace(sourcePath))
                return null;

            string folder = Path.GetDirectoryName(sourcePath)?.Replace("\\", "/") ?? DefaultNpcFolder;
            string baseName = string.IsNullOrWhiteSpace(source.CharacterName) ? source.gameObject.name : source.CharacterName;
            string fileName = $"{baseName} Copy.prefab";
            string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}");
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                return null;

            AssetDatabase.ImportAsset(targetPath);
            GameObject duplicatedRoot = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
            NPCSoul duplicatedSoul = duplicatedRoot != null ? duplicatedRoot.GetComponentInChildren<NPCSoul>(true) : null;
            if (duplicatedSoul != null)
            {
                SerializedObject serializedObject = new(duplicatedSoul);
                serializedObject.Update();
                SetString(serializedObject, "_ownerId", NextOwnerId());
                SetString(serializedObject, "_characterName", $"{baseName} Copy");
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(duplicatedSoul);
            }

            AssetDatabase.SaveAssets();
            NPCRegistry.ForceEditorSyncNow();
            return duplicatedSoul;
        }

        public static void ApplyTemplate(NPCSoul soul, NPCAuthoringTemplate template, string npcName = null, bool assignNewId = false)
        {
            if (soul == null)
                return;

            Undo.RecordObject(soul, "Apply NPC Template");
            EnsureCanonicalComponents(soul, template);

            SerializedObject serializedObject = new(soul);
            serializedObject.Update();

            if (!string.IsNullOrWhiteSpace(npcName))
                SetString(serializedObject, "_characterName", npcName.Trim());

            SerializedProperty ownerId = serializedObject.FindProperty("_ownerId");
            if (ownerId != null && (assignNewId || string.IsNullOrWhiteSpace(ownerId.stringValue)))
                ownerId.stringValue = NextOwnerId();

            SetEnum(serializedObject, "_authoringTemplate", (int)template);
            SetEnum(serializedObject, "_soulType", (int)SoulType.NPC);

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(soul);
            PrefabUtility.RecordPrefabInstancePropertyModifications(soul);
        }

        public static void FillMissingTemplateDefaults(NPCSoul soul, NPCAuthoringTemplate template, string npcName = null)
        {
            if (soul == null || template == NPCAuthoringTemplate.None)
                return;

            Undo.RecordObject(soul, "Fill NPC Template Defaults");
            EnsureCanonicalComponents(soul, template);

            SerializedObject serializedObject = new(soul);
            serializedObject.Update();

            SetEnum(serializedObject, "_authoringTemplate", (int)template);

            SerializedProperty ownerId = serializedObject.FindProperty("_ownerId");
            if (ownerId != null && string.IsNullOrWhiteSpace(ownerId.stringValue))
                ownerId.stringValue = NextOwnerId();

            SerializedProperty nameProperty = serializedObject.FindProperty("_characterName");
            if (nameProperty != null
                && string.IsNullOrWhiteSpace(nameProperty.stringValue)
                && !string.IsNullOrWhiteSpace(npcName))
            {
                nameProperty.stringValue = npcName.Trim();
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(soul);
            PrefabUtility.RecordPrefabInstancePropertyModifications(soul);
        }

        public static List<string> BuildTemplateOverwritePreview(NPCAuthoringTemplate template)
        {
            List<string> changes = new();
            changes.Add("Authoring template marker");
            changes.Add("Soul type");

            switch (template)
            {
                case NPCAuthoringTemplate.Civilian:
                    changes.Add("Ensure AI_NPC + Inventory components");
                    break;
                case NPCAuthoringTemplate.Patroller:
                    changes.Add("Ensure AI_NPC + Inventory components");
                    changes.Add("Auto-patrol root expected");
                    break;
                case NPCAuthoringTemplate.Trader:
                    changes.Add("Ensure AI_NPC + Inventory + NpcTrader components");
                    break;
                case NPCAuthoringTemplate.QuestGiver:
                    changes.Add("Ensure AI_NPC + Inventory + NpcTrader components");
                    changes.Add("Quest-giver setup expected");
                    break;
            }

            return changes;
        }

        public static string NextOwnerId()
        {
            NPCRegistry registry = NPCRegistry.Get();
            return NextOwnerIdFromEntries(registry?.Entries);
        }

        internal static string NextOwnerIdFromEntries(IReadOnlyList<NPCRegistry.Entry> entries)
        {
            HashSet<int> used = new();
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    string id = entries[i]?.OwnerId;
                    if (EntityCodeUtility.TryParse(id, EntityCodeUtility.OwnerPrefix, out int numeric))
                        used.Add(numeric);
                }
            }

            for (int next = 1; next <= 99999; next++)
            {
                if (!used.Contains(next))
                    return EntityCodeUtility.Format(EntityCodeUtility.OwnerPrefix, next);
            }

            return string.Empty;
        }

        public static string GetPrefabPath(NPCSoul soul)
        {
            if (soul == null)
                return string.Empty;

            return AssetDatabase.GetAssetPath(soul.gameObject);
        }

        private static void EnsureDefaultFolder()
        {
            if (AssetDatabase.IsValidFolder(DefaultNpcFolder))
                return;

            EnsureFolderRecursive(DefaultNpcFolder);
        }

        private static void EnsureFolderRecursive(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
            string leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
                return;

            EnsureFolderRecursive(parent);
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(parent, leaf);
        }

        private static string DefaultNameFor(NPCAuthoringTemplate template)
        {
            return template switch
            {
                NPCAuthoringTemplate.Civilian => "NPC_NewCivilian",
                NPCAuthoringTemplate.Patroller => "NPC_NewPatroller",
                NPCAuthoringTemplate.Trader => "NPC_NewTrader",
                NPCAuthoringTemplate.QuestGiver => "NPC_NewQuestGiver",
                _ => "NPC_New"
            };
        }

        private static void EnsureCanonicalComponents(NPCSoul soul, NPCAuthoringTemplate template)
        {
            GameObject go = soul.gameObject;

            // AI_NPC pulls in LocomotionInput/Controller/State/Animation, CharacterController, NavMeshAgent via [RequireComponent].
            if (go.GetComponent<Sol.AI.AI_NPC>() == null)
                Undo.AddComponent<Sol.AI.AI_NPC>(go);

            if (go.GetComponent<Sol.Inventory>() == null)
                Undo.AddComponent<Sol.Inventory>(go);

            if (template == NPCAuthoringTemplate.Trader || template == NPCAuthoringTemplate.QuestGiver)
            {
                if (go.GetComponent<Sol.NpcTrader>() == null)
                    Undo.AddComponent<Sol.NpcTrader>(go);
            }

            EditorUtility.SetDirty(go);
        }

        private static void SetString(SerializedObject serializedObject, string propertyName, string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.stringValue = value ?? string.Empty;
        }

        private static void SetEnum(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.enumValueIndex = value;
        }
    }
}
