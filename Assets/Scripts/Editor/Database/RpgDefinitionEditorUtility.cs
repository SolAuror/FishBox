using System.Collections.Generic;
using System.IO;
using Sol.Rpg;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    internal static class RpgDefinitionEditorUtility
    {
        public const string DefaultRpgFolder = "Assets/Data/RPG";

        public static T CreateDefinition<T>(string idPrefix, string displayName) where T : RpgDefinition
        {
            EnsureDefaultFolder();

            T definition = ScriptableObject.CreateInstance<T>();
            string nextId = NextId(idPrefix);
            string resolvedName = string.IsNullOrWhiteSpace(displayName) ? typeof(T).Name : displayName.Trim();
            ApplyIdentity(definition, nextId, resolvedName);

            string fileName = SanitizeFileName($"{nextId}_{resolvedName}");
            string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultRpgFolder}/{fileName}.asset");
            AssetDatabase.CreateAsset(definition, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);
            RpgDefinitionRegistry.ForceEditorSyncNow();
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        public static T DuplicateDefinition<T>(T source, string idPrefix) where T : RpgDefinition
        {
            if (source == null)
                return null;

            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrWhiteSpace(sourcePath))
                return null;

            string folder = Path.GetDirectoryName(sourcePath)?.Replace("\\", "/") ?? DefaultRpgFolder;
            string nextId = NextId(idPrefix);
            string copyName = string.IsNullOrWhiteSpace(source.DisplayName) ? $"{typeof(T).Name} Copy" : $"{source.DisplayName} Copy";
            string fileName = SanitizeFileName($"{nextId}_{copyName}");
            string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset");
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                return null;

            AssetDatabase.ImportAsset(targetPath);
            T duplicated = AssetDatabase.LoadAssetAtPath<T>(targetPath);
            if (duplicated != null)
                ApplyIdentity(duplicated, nextId, copyName);

            AssetDatabase.SaveAssets();
            RpgDefinitionRegistry.ForceEditorSyncNow();
            return duplicated;
        }

        public static string GetAssetPath(RpgDefinition definition)
        {
            return definition == null ? string.Empty : AssetDatabase.GetAssetPath(definition);
        }

        internal static string NextId(string idPrefix)
        {
            RpgDefinitionRegistry registry = RpgDefinitionRegistry.Get();
            return NextIdFromDefinitions(idPrefix, AllDefinitions(registry));
        }

        internal static string NextIdFromDefinitions(string idPrefix, IReadOnlyList<RpgDefinition> definitions)
        {
            HashSet<int> used = new();
            if (definitions != null)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    string id = definitions[i]?.Id;
                    if (EntityCodeUtility.TryParse(id, idPrefix, out int numeric))
                        used.Add(numeric);
                }
            }

            for (int next = 1; next <= 99999; next++)
            {
                if (!used.Contains(next))
                    return EntityCodeUtility.Format(idPrefix, next);
            }

            return string.Empty;
        }

        private static List<RpgDefinition> AllDefinitions(RpgDefinitionRegistry registry)
        {
            List<RpgDefinition> definitions = new();
            if (registry == null)
                return definitions;

            AddRange(definitions, registry.Stats);
            AddRange(definitions, registry.Skills);
            AddRange(definitions, registry.Factions);
            AddRange(definitions, registry.Shops);
            AddRange(definitions, registry.Tags);
            AddRange(definitions, registry.StatusEffects);
            AddRange(definitions, registry.Traits);
            return definitions;
        }

        private static void AddRange<T>(List<RpgDefinition> target, IReadOnlyList<T> source) where T : RpgDefinition
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                    target.Add(source[i]);
            }
        }

        private static void ApplyIdentity(RpgDefinition definition, string id, string displayName)
        {
            if (definition == null)
                return;

            SerializedObject so = new(definition);
            so.Update();
            SerializedProperty idProp = so.FindProperty("_id");
            SerializedProperty displayProp = so.FindProperty("_displayName");
            if (idProp != null)
                idProp.stringValue = id ?? string.Empty;
            if (displayProp != null)
                displayProp.stringValue = displayName ?? string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void EnsureDefaultFolder()
        {
            EnsureFolderRecursive(DefaultRpgFolder);
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

        private static string SanitizeFileName(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return "NewDefinition";

            char[] invalid = Path.GetInvalidFileNameChars();
            string trimmed = source.Trim();
            for (int i = 0; i < invalid.Length; i++)
                trimmed = trimmed.Replace(invalid[i], '_');

            return trimmed.Replace(' ', '_');
        }
    }
}
