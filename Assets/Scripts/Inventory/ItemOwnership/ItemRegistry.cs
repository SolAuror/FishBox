using System.Collections.Generic;
using UnityEngine;
using Sol.Grab;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sol
{
    /// <summary>
    /// Authoritative item-ID registry.
    /// In the editor this registry auto-discovers ItemComponent instances in prefabs,
    /// assigns unique IDs when missing/invalid/duplicated, and keeps the runtime lookup in sync.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemRegistry", menuName = "Sol/Item Registry")]
    public class ItemRegistry : ScriptableObject
    {
        private const string ResourceName = "ItemRegistry";
#region Inspector Settings

        [Tooltip("Inspector: tunes entries.")]
        [SerializeField] private List<Entry> _entries = new();
#endregion

        [System.Serializable]
        public class Entry
        {
            public string ItemId;
            public ItemComponent Prefab;
        }

        private static ItemRegistry _instance;
        private Dictionary<string, ItemComponent> _lookup;

        public IReadOnlyList<Entry> Entries => _entries;

#if UNITY_EDITOR
        private const string DefaultAssetPath = "Assets/Resources/ItemRegistry.asset";
        private static bool _editorSyncScheduled;
        private static bool _isEditorSynchronizing;
#endif

        public static ItemRegistry Get()
        {
            if (_instance != null)
                return _instance;

            _instance = Resources.Load<ItemRegistry>(ResourceName);
#if UNITY_EDITOR
            if (_instance == null)
                _instance = GetOrCreateEditorAsset();
#endif
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
#if UNITY_EDITOR
            if (_isEditorSynchronizing)
                return;
#endif

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

#if UNITY_EDITOR
            ScheduleEditorSync();
#endif
        }

#if UNITY_EDITOR
        internal static bool IsEditorSyncInProgress => _isEditorSynchronizing;

        [InitializeOnLoadMethod]
        private static void InitializeEditorHooks()
        {
            EditorApplication.projectChanged -= ScheduleEditorSync;
            EditorApplication.projectChanged += ScheduleEditorSync;
            ScheduleEditorSync();
        }

        public static void ScheduleEditorSync()
        {
            if (Application.isPlaying || _editorSyncScheduled)
                return;

            _editorSyncScheduled = true;
            EditorApplication.delayCall += ExecuteScheduledEditorSync;
        }

        [ContextMenu("Rebuild From Project")]
        private void RebuildFromProject()
        {
            ForceEditorSync();
        }

        private static void ExecuteScheduledEditorSync()
        {
            _editorSyncScheduled = false;
            if (Application.isPlaying || _isEditorSynchronizing)
                return;

            ForceEditorSync();
        }

        private static void ForceEditorSync()
        {
            ItemRegistry registry = GetOrCreateEditorAsset();
            if (registry == null)
                return;

            _isEditorSynchronizing = true;
            try
            {
                registry.SyncFromProject();
            }
            finally
            {
                _isEditorSynchronizing = false;
            }
        }

        private static ItemRegistry GetOrCreateEditorAsset()
        {
            ItemRegistry loaded = Resources.Load<ItemRegistry>(ResourceName);
            if (loaded != null)
                return loaded;

            loaded = AssetDatabase.LoadAssetAtPath<ItemRegistry>(DefaultAssetPath);
            if (loaded != null)
                return loaded;

            const string resourcesFolder = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");

            ItemRegistry created = CreateInstance<ItemRegistry>();
            AssetDatabase.CreateAsset(created, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            return created;
        }

        private void SyncFromProject()
        {
            List<DiscoveredItem> discovered = DiscoverProjectItemComponents();
            if (discovered.Count == 0)
            {
                if (_entries.Count == 0)
                    return;

                _entries.Clear();
                _lookup = null;
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
                return;
            }

            HashSet<int> usedCodes = new();
            Dictionary<ItemComponent, string> assignedIds = new(discovered.Count);
            int nextCode = 1;

            for (int i = 0; i < discovered.Count; i++)
            {
                ItemComponent item = discovered[i].Item;
                string normalized = EntityCodeUtility.NormalizeOrEmpty(item.ItemId, EntityCodeUtility.ItemPrefix);
                int numeric = 0;
                bool keepExisting = EntityCodeUtility.TryParse(normalized, EntityCodeUtility.ItemPrefix, out numeric)
                    && usedCodes.Add(numeric);

                if (!keepExisting)
                {
                    while (nextCode <= 99999 && usedCodes.Contains(nextCode))
                        nextCode++;

                    if (nextCode > 99999)
                    {
                        Debug.LogWarning("[ItemRegistry] Exhausted available ITM codes (ITM00001-ITM99999).");
                        continue;
                    }

                    numeric = nextCode;
                    usedCodes.Add(numeric);
                    nextCode++;
                }

                assignedIds[item] = EntityCodeUtility.Format(EntityCodeUtility.ItemPrefix, numeric);
            }

            bool modifiedAssets = ApplyAssignedIds(assignedIds);
            List<Entry> rebuiltEntries = BuildEntries(discovered, assignedIds);
            bool entriesChanged = ReplaceEntriesIfDifferent(rebuiltEntries);

            if (modifiedAssets || entriesChanged)
            {
                _lookup = null;
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
            }
        }

        private static List<DiscoveredItem> DiscoverProjectItemComponents()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            List<DiscoveredItem> discovered = new(prefabGuids.Length);

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabRoot == null)
                    continue;

                ItemComponent[] items = prefabRoot.GetComponentsInChildren<ItemComponent>(true);
                for (int j = 0; j < items.Length; j++)
                {
                    ItemComponent item = items[j];
                    if (item == null)
                        continue;

                    discovered.Add(new DiscoveredItem
                    {
                        Item = item,
                        SortKey = BuildSortKey(path, item)
                    });
                }
            }

            discovered.Sort(static (a, b) => string.CompareOrdinal(a.SortKey, b.SortKey));
            return discovered;
        }

        private static string BuildSortKey(string prefabPath, ItemComponent item)
        {
            GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(item);
            return $"{prefabPath}|{globalObjectId.targetObjectId}";
        }

        private static bool ApplyAssignedIds(Dictionary<ItemComponent, string> assignedIds)
        {
            bool modifiedAny = false;
            foreach (KeyValuePair<ItemComponent, string> pair in assignedIds)
            {
                ItemComponent item = pair.Key;
                string assignedId = pair.Value;
                if (item == null || string.Equals(item.ItemId, assignedId, System.StringComparison.Ordinal))
                    continue;

                SerializedObject serializedObject = new(item);
                SerializedProperty idProperty = serializedObject.FindProperty("_itemId");
                if (idProperty == null)
                    continue;

                idProperty.stringValue = assignedId;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);
                modifiedAny = true;
            }

            return modifiedAny;
        }

        private static List<Entry> BuildEntries(List<DiscoveredItem> discovered, Dictionary<ItemComponent, string> assignedIds)
        {
            List<Entry> rebuilt = new(discovered.Count);
            for (int i = 0; i < discovered.Count; i++)
            {
                ItemComponent item = discovered[i].Item;
                if (item == null || !assignedIds.TryGetValue(item, out string assignedId))
                    continue;

                rebuilt.Add(new Entry
                {
                    ItemId = assignedId,
                    Prefab = item
                });
            }

            return rebuilt;
        }

        private bool ReplaceEntriesIfDifferent(List<Entry> rebuiltEntries)
        {
            if (_entries.Count == rebuiltEntries.Count)
            {
                bool identical = true;
                for (int i = 0; i < _entries.Count; i++)
                {
                    Entry current = _entries[i];
                    Entry incoming = rebuiltEntries[i];
                    if (current == null || incoming == null)
                    {
                        identical = false;
                        break;
                    }

                    bool sameId = string.Equals(current.ItemId, incoming.ItemId, System.StringComparison.Ordinal);
                    bool samePrefab = current.Prefab == incoming.Prefab;
                    if (!sameId || !samePrefab)
                    {
                        identical = false;
                        break;
                    }
                }

                if (identical)
                    return false;
            }

            _entries.Clear();
            _entries.AddRange(rebuiltEntries);
            return true;
        }

        private struct DiscoveredItem
        {
            public ItemComponent Item;
            public string SortKey;
        }
#endif
    }
}
