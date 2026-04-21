using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Sol.Actions;

namespace Sol
{
    public static class EntityCodeUtility
    {
        public const string ItemPrefix = "ITM";
        public const string NpcPrefix = "NPC";
        public const string ContainerPrefix = "CNT";
        public const string OwnerPrefix = "OWN";
        public const string DefaultPlayerOwnerId = "PLY00001";

        private const int CodeWidth = 5;
        private const int MaxCodeValue = 99999;

        public static string NormalizeOrEmpty(string rawCode, string prefix)
        {
            if (!TryParse(rawCode, prefix, out int numeric))
                return string.Empty;

            return Format(prefix, numeric);
        }

        public static string Format(string prefix, int numeric)
        {
            return $"{prefix}{numeric.ToString($"D{CodeWidth}")}";
        }

        public static bool TryParse(string rawCode, string prefix, out int numeric)
        {
            numeric = 0;
            if (string.IsNullOrWhiteSpace(rawCode) || string.IsNullOrWhiteSpace(prefix))
                return false;

            string trimmed = rawCode.Trim().ToUpperInvariant();
            string normalizedPrefix = prefix.Trim().ToUpperInvariant();
            if (!trimmed.StartsWith(normalizedPrefix, StringComparison.Ordinal))
                return false;

            string suffix = trimmed.Substring(normalizedPrefix.Length);
            if (suffix.Length != CodeWidth || !int.TryParse(suffix, out int parsed))
                return false;

            if (parsed <= 0 || parsed > MaxCodeValue)
                return false;

            numeric = parsed;
            return true;
        }

#if UNITY_EDITOR
        public static string EnsureAssignedCode<T>(
            T self,
            string currentCode,
            string prefix,
            Func<T, string> codeSelector)
            where T : Component
        {
            string normalized = NormalizeOrEmpty(currentCode, prefix);
            if (!string.IsNullOrEmpty(normalized))
                return normalized;

            int nextCode = FindNextAvailableCode(prefix, codeSelector);
            if (nextCode <= 0)
            {
                Debug.LogWarning(
                    $"[EntityCodeUtility] Unable to assign '{prefix}' code to '{self?.name ?? "Unknown"}'.",
                    self);
                return string.Empty;
            }

            return Format(prefix, nextCode);
        }

        private static int FindNextAvailableCode<T>(string prefix, Func<T, string> codeSelector)
            where T : Component
        {
            bool[] used = new bool[MaxCodeValue + 1];
            T[] loaded = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < loaded.Length; i++)
            {
                T component = loaded[i];
                if (component == null)
                    continue;

                RegisterCode(codeSelector(component), prefix, used);
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabRoot == null)
                    continue;

                T[] prefabComponents = prefabRoot.GetComponentsInChildren<T>(true);
                for (int j = 0; j < prefabComponents.Length; j++)
                    RegisterCode(codeSelector(prefabComponents[j]), prefix, used);
            }

            for (int i = 1; i <= MaxCodeValue; i++)
            {
                if (!used[i])
                    return i;
            }

            return 0;
        }

        private static void RegisterCode(string rawCode, string prefix, bool[] used)
        {
            if (!TryParse(rawCode, prefix, out int numeric))
                return;

            if (numeric > 0 && numeric < used.Length)
                used[numeric] = true;
        }
#endif
    }

    /// <summary>
    /// Draws a string field as an ItemRegistry-backed ItemId dropdown in the Unity Inspector.
    /// </summary>
    public sealed class ItemIdDropdownAttribute : PropertyAttribute
    {
        public bool AllowEmpty { get; }

        public ItemIdDropdownAttribute(bool allowEmpty = true)
        {
            AllowEmpty = allowEmpty;
        }
    }

    /// <summary>
    /// Draws a string field as an owner-id dropdown in the Unity Inspector.
    /// </summary>
    public sealed class OwnerIdDropdownAttribute : PropertyAttribute
    {
        public bool AllowEmpty { get; }

        public OwnerIdDropdownAttribute(bool allowEmpty = true)
        {
            AllowEmpty = allowEmpty;
        }
    }

    public interface IInteractable
    {
        string InteractionPrompt { get; }
        bool CanInteract(Interactor interactor);

        /// <summary>
        /// Return the action that should be dispatched when this object is interacted with.
        /// The caller (InteractAction / AI) dispatches the returned action through ActionSystem.
        /// Return null if no action should be dispatched.
        /// </summary>
        GameAction GetInteraction(Interactor interactor);
    }
}

