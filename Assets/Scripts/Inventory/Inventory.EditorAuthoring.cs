using System;
using System.Collections.Generic;
using UnityEngine;
using Sol.Grab;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sol
{
    public partial class Inventory : MonoBehaviour
    {
        private void SyncRequiredKeyFieldsFromAuthoringData()
        {
            _requiredKeyItemName = _requiredKeyItemName?.Trim() ?? string.Empty;
            _requiredKeyItemId = NormalizeItemIdOrEmpty(_requiredKeyItemId);

            if (string.IsNullOrEmpty(_requiredKeyItemId) && string.IsNullOrEmpty(_requiredKeyItemName))
                return;

            if (!string.IsNullOrEmpty(_requiredKeyItemId))
            {
                if (TryResolveKeyNameById(_requiredKeyItemId, out string keyNameById))
                {
                    _requiredKeyItemName = keyNameById;
                    return;
                }

                _requiredKeyItemId = string.Empty;
            }

            if (!string.IsNullOrEmpty(_requiredKeyItemName)
                && TryResolveKeyIdByName(_requiredKeyItemName, out string keyIdByName, out string canonicalName))
            {
                _requiredKeyItemId = keyIdByName;
                _requiredKeyItemName = canonicalName;
            }
        }


        private static bool TryResolveKeyNameById(string itemId, out string itemName)
        {
            itemName = string.Empty;
            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            string normalizedId = NormalizeItemIdOrEmpty(itemId);
            if (string.IsNullOrEmpty(normalizedId))
                return false;

            ItemComponent[] keyPrefabs = LoadAllKeyPrefabs();
            for (int i = 0; i < keyPrefabs.Length; i++)
            {
                ItemComponent key = keyPrefabs[i];
                if (key == null || !string.Equals(key.ItemId, normalizedId, StringComparison.OrdinalIgnoreCase))
                    continue;

                itemName = key.ItemName?.Trim() ?? string.Empty;
                return !string.IsNullOrEmpty(itemName);
            }

            return false;
        }


        private static bool TryResolveKeyIdByName(string itemName, out string itemId, out string canonicalName)
        {
            itemId = string.Empty;
            canonicalName = string.Empty;
            if (string.IsNullOrWhiteSpace(itemName))
                return false;

            string normalizedName = itemName.Trim();
            ItemComponent[] keyPrefabs = LoadAllKeyPrefabs();
            for (int i = 0; i < keyPrefabs.Length; i++)
            {
                ItemComponent key = keyPrefabs[i];
                if (key == null || !string.Equals(key.ItemName, normalizedName, StringComparison.OrdinalIgnoreCase))
                    continue;

                string normalizedId = NormalizeItemIdOrEmpty(key.ItemId);
                if (string.IsNullOrEmpty(normalizedId))
                    continue;

                itemId = normalizedId;
                canonicalName = key.ItemName?.Trim() ?? normalizedName;
                return true;
            }

            return false;
        }


        private static ItemComponent[] LoadAllKeyPrefabs()
        {
#if UNITY_EDITOR
            List<ItemComponent> keys = new();
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabRoot == null)
                    continue;

                ItemComponent keyItem = prefabRoot.GetComponentInChildren<ItemComponent>(true);
                if (keyItem == null || keyItem.Type != ItemType.Key)
                    continue;

                keys.Add(keyItem);
            }

            return keys.ToArray();
#else
            return Array.Empty<ItemComponent>();
#endif
        }
    }
}
