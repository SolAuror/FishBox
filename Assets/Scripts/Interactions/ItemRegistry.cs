using System.Collections.Generic;
using UnityEngine;
using Sol.Grab;

namespace Sol
{
    /// <summary>
    /// Runtime lookup from item IDs to prefab assets used by save/load restoration.
    /// Create one asset in a Resources folder or rely on the editor fallback scan.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemRegistry", menuName = "Sol/Item Registry")]
    public class ItemRegistry : ScriptableObject
    {
        private const string ResourceName = "ItemRegistry";

        [SerializeField] private List<Entry> _entries = new();

        [System.Serializable]
        public class Entry
        {
            public string ItemId;
            public ItemComponent Prefab;
        }

        private static ItemRegistry _instance;
        private Dictionary<string, ItemComponent> _lookup;

        public IReadOnlyList<Entry> Entries => _entries;

        public static ItemRegistry Get()
        {
            if (_instance != null)
                return _instance;

            _instance = Resources.Load<ItemRegistry>(ResourceName);
            return _instance;
        }

        public ItemComponent GetPrefab(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            EnsureLookup();
            _lookup.TryGetValue(itemId.Trim(), out ItemComponent prefab);
            return prefab;
        }

        private void EnsureLookup()
        {
            if (_lookup != null)
                return;

            _lookup = new Dictionary<string, ItemComponent>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry == null || entry.Prefab == null || string.IsNullOrWhiteSpace(entry.ItemId))
                    continue;

                string key = entry.ItemId.Trim();
                if (!_lookup.ContainsKey(key))
                    _lookup[key] = entry.Prefab;
            }
        }

        private void OnDisable()
        {
            _lookup = null;
        }

        private void OnValidate()
        {
            if (_entries == null)
                return;

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry == null || entry.Prefab == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(entry.Prefab.ItemId))
                    entry.ItemId = entry.Prefab.ItemId.Trim();
            }

            _lookup = null;
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild From Project")]
        private void RebuildFromProject()
        {
            _entries.Clear();
            _lookup = null;

            string[] prefabGuids = UnityEditor.AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefabRoot = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabRoot == null)
                    continue;

                ItemComponent item = prefabRoot.GetComponent<ItemComponent>();
                if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
                    continue;

                _entries.Add(new Entry
                {
                    ItemId = item.ItemId.Trim(),
                    Prefab = item
                });
            }

            _entries.Sort((a, b) => string.Compare(a.ItemId, b.ItemId, System.StringComparison.OrdinalIgnoreCase));
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[ItemRegistry] Rebuilt with {_entries.Count} entries.");
        }
#endif
    }
}
