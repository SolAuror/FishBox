using System.Collections.Generic;
using System.IO;
using Sol.AI;
using Sol.Rpg;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    public static class NPCAuthoringEditorUtility
    {
        public const string DefaultNpcFolder = "Assets/Scripts/NPCs/Prefabs";

        public static NPCSoul CreateNPCPrefab(NPCArchetype archetype, string npcName = null)
        {
            EnsureDefaultFolder();

            string resolvedName = string.IsNullOrWhiteSpace(npcName)
                ? DefaultNameFor(archetype)
                : npcName.Trim();

            GameObject npcObject = new(resolvedName);
            NPCSoul soul = npcObject.AddComponent<NPCSoul>();
            EnsureCanonicalComponents(soul, archetype);
            ApplyArchetype(soul, archetype, resolvedName, assignNewId: true);

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

        public static void ApplyArchetype(NPCSoul soul, NPCArchetype archetype, string npcName = null, bool assignNewId = false)
        {
            if (soul == null)
                return;

            Undo.RecordObject(soul, "Apply NPC Archetype");
            EnsureCanonicalComponents(soul, archetype);

            SerializedObject serializedObject = new(soul);
            serializedObject.Update();

            if (!string.IsNullOrWhiteSpace(npcName))
                SetString(serializedObject, "_characterName", npcName.Trim());

            SerializedProperty ownerId = serializedObject.FindProperty("_ownerId");
            if (ownerId != null && (assignNewId || string.IsNullOrWhiteSpace(ownerId.stringValue)))
                ownerId.stringValue = NextOwnerId();

            SetEnum(serializedObject, "_npcArchetype", (int)archetype);
            SetEnum(serializedObject, "_entityType", (int)EntityType.NPC);
            SetBool(serializedObject, "_isHostile", archetype == NPCArchetype.Bandit);
            SetBool(serializedObject, "_identityMigratedV2", true);

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            ApplyArchetypeTags(soul, archetype, overwrite: true);
            EditorUtility.SetDirty(soul);
            PrefabUtility.RecordPrefabInstancePropertyModifications(soul);
        }

        public static void FillMissingArchetypeDefaults(NPCSoul soul, NPCArchetype archetype, string npcName = null)
        {
            if (soul == null || archetype == NPCArchetype.None)
                return;

            Undo.RecordObject(soul, "Fill NPC Archetype Defaults");
            EnsureCanonicalComponents(soul, archetype);

            SerializedObject serializedObject = new(soul);
            serializedObject.Update();

            SetEnum(serializedObject, "_npcArchetype", (int)archetype);
            if (archetype == NPCArchetype.Bandit)
                SetBool(serializedObject, "_isHostile", true);
            SetBool(serializedObject, "_identityMigratedV2", true);

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
            ApplyArchetypeTags(soul, archetype, overwrite: false);
            EditorUtility.SetDirty(soul);
            PrefabUtility.RecordPrefabInstancePropertyModifications(soul);
        }

        public static List<string> BuildArchetypeOverwritePreview(NPCArchetype archetype)
        {
            List<string> changes = new();
            changes.Add("NPC archetype marker");
            changes.Add("Entity type");

            switch (archetype)
            {
                case NPCArchetype.Civilian:
                    changes.Add("Ensure AI_NPC + Inventory components");
                    break;
                case NPCArchetype.Guard:
                    changes.Add("Ensure AI_NPC + Inventory components");
                    changes.Add("Auto-patrol root expected");
                    break;
                case NPCArchetype.Bandit:
                    changes.Add("Ensure AI_NPC + Inventory components");
                    changes.Add("Hostile checkbox enabled");
                    break;
                case NPCArchetype.QuestGiver:
                    changes.Add("Ensure AI_NPC + Inventory components");
                    changes.Add("Quest-giver setup expected");
                    break;
                case NPCArchetype.Unique:
                    changes.Add("Ensure AI_NPC + Inventory components");
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

        private static string DefaultNameFor(NPCArchetype archetype)
        {
            return archetype switch
            {
                NPCArchetype.Civilian => "NPC_NewCivilian",
                NPCArchetype.Guard => "NPC_NewGuard",
                NPCArchetype.Bandit => "NPC_NewBandit",
                NPCArchetype.QuestGiver => "NPC_NewQuestGiver",
                NPCArchetype.Unique => "NPC_NewUnique",
                _ => "NPC_New"
            };
        }

        private static void EnsureCanonicalComponents(NPCSoul soul, NPCArchetype archetype)
        {
            GameObject go = soul.gameObject;

            // AI_NPC pulls in LocomotionInput/Controller/State/Animation, CharacterController, NavMeshAgent via [RequireComponent].
            if (go.GetComponent<Sol.AI.AI_NPC>() == null)
                Undo.AddComponent<Sol.AI.AI_NPC>(go);

            if (go.GetComponent<Sol.Inventory>() == null)
                Undo.AddComponent<Sol.Inventory>(go);

            EditorUtility.SetDirty(go);
        }

        private static void ApplyArchetypeTags(NPCSoul soul, NPCArchetype archetype, bool overwrite)
        {
            if (soul == null)
                return;

            GameplayTagSet tags = soul.Tags;
            if (overwrite)
            {
                tags.RemoveSerializedTagPath(GameplayCapabilityTags.ActorHostile);
                tags.RemoveSerializedTagPath(GameplayCapabilityTags.ActorCivilian);
                if (archetype != NPCArchetype.QuestGiver)
                    tags.RemoveSerializedTagPath(GameplayCapabilityTags.JobQuestGiver);
            }

            if (archetype == NPCArchetype.Bandit)
                tags.AddSerializedTagPath(GameplayCapabilityTags.ActorHostile);
            else if (overwrite || !tags.HasTagOrChild(GameplayCapabilityTags.ActorHostile))
                tags.AddSerializedTagPath(GameplayCapabilityTags.ActorCivilian);

            if (archetype == NPCArchetype.QuestGiver)
                tags.AddSerializedTagPath(GameplayCapabilityTags.JobQuestGiver);

            tags.Normalize();
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

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.boolValue = value;
        }
    }
}
