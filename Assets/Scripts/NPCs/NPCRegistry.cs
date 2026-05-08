using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sol.AI
{
    /// <summary>
    /// Authoritative NPC owner-id registry.
    /// In the editor this registry auto-discovers prefabs containing an <see cref="NPCSoul"/>,
    /// captures their OwnerIds, and acts as a fast index for the NPC Database window
    /// and the NpcId dropdown drawer. Mirrors the shape of <see cref="ItemRegistry"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "NPCRegistry", menuName = "Sol/NPC Registry")]
    public class NPCRegistry : ScriptableObject
    {
        private const string LegacyResourceName = "NPCRegistry";

        #region Inspector Settings
        [Tooltip("Inspector: tunes entries.")]
        [SerializeField] private List<Entry> _entries = new();
        #endregion

        [System.Serializable]
        public class Entry
        {
            public string OwnerId;
            public NPCSoul Prefab;
        }

        private static NPCRegistry _instance;
        private Dictionary<string, NPCSoul> _lookup;

        public IReadOnlyList<Entry> Entries => _entries;

#if UNITY_EDITOR
        private const string DefaultAssetPath = "Assets/Data/NPCRegistry.asset";
        private const string LegacyAssetPath = "Assets/Resources/NPCRegistry.asset";
        private static bool _editorSyncScheduled;
        private static bool _isEditorSynchronizing;
#endif

        public static NPCRegistry Get()
        {
            if (_instance != null)
                return _instance;

#if UNITY_EDITOR
            _instance = GetOrCreateEditorAsset();
#else
            _instance = FindLoadedRegistryAsset();
            if (_instance == null)
                _instance = Resources.Load<NPCRegistry>(LegacyResourceName);
#endif
            return _instance;
        }

        public NPCSoul GetPrefab(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                return null;

            EnsureLookup();
            _lookup.TryGetValue(ownerId.Trim(), out NPCSoul prefab);
            return prefab;
        }

        private static NPCRegistry FindLoadedRegistryAsset()
        {
            NPCRegistry[] registries = Resources.FindObjectsOfTypeAll<NPCRegistry>();
            for (int i = 0; i < registries.Length; i++)
            {
                if (registries[i] != null && registries[i].name == nameof(NPCRegistry))
                    return registries[i];
            }

            return registries.Length > 0 ? registries[0] : null;
        }

        private void EnsureLookup()
        {
            if (_lookup != null)
                return;

            _lookup = new Dictionary<string, NPCSoul>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry == null || entry.Prefab == null || string.IsNullOrWhiteSpace(entry.OwnerId))
                    continue;

                string key = entry.OwnerId.Trim();
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

                if (!string.IsNullOrWhiteSpace(entry.Prefab.OwnerId))
                    entry.OwnerId = entry.Prefab.OwnerId.Trim();
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

        public static void ForceEditorSyncNow()
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
            NPCRegistry registry = GetOrCreateEditorAsset();
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

        private static NPCRegistry GetOrCreateEditorAsset()
        {
            NPCRegistry loaded = AssetDatabase.LoadAssetAtPath<NPCRegistry>(DefaultAssetPath);
            if (loaded != null)
            {
                EnsurePreloadedAsset(loaded);
                return loaded;
            }

            NPCRegistry legacy = AssetDatabase.LoadAssetAtPath<NPCRegistry>(LegacyAssetPath);
            if (legacy != null)
            {
                EnsureDataFolder();
                string moveError = AssetDatabase.MoveAsset(LegacyAssetPath, DefaultAssetPath);
                loaded = string.IsNullOrEmpty(moveError)
                    ? AssetDatabase.LoadAssetAtPath<NPCRegistry>(DefaultAssetPath)
                    : legacy;
                EnsurePreloadedAsset(loaded);
                return loaded;
            }

            EnsureDataFolder();

            NPCRegistry created = CreateInstance<NPCRegistry>();
            AssetDatabase.CreateAsset(created, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            EnsurePreloadedAsset(created);
            return created;
        }

        private static void EnsureDataFolder()
        {
            const string dataFolder = "Assets/Data";
            if (!AssetDatabase.IsValidFolder(dataFolder))
                AssetDatabase.CreateFolder("Assets", "Data");
        }

        private static void EnsurePreloadedAsset(NPCRegistry registry)
        {
            if (registry == null)
                return;

            UnityEngine.Object[] assets = PlayerSettings.GetPreloadedAssets();
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] == registry)
                    return;
            }

            List<UnityEngine.Object> updated = new(assets) { registry };
            PlayerSettings.SetPreloadedAssets(updated.ToArray());
        }

        private void SyncFromProject()
        {
            List<DiscoveredNPC> discovered = DiscoverProjectNPCSouls();
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
            Dictionary<NPCSoul, string> assignedIds = new(discovered.Count);
            int nextCode = 1;

            for (int i = 0; i < discovered.Count; i++)
            {
                NPCSoul soul = discovered[i].Soul;
                string normalized = EntityCodeUtility.NormalizeOrEmpty(soul.OwnerId, EntityCodeUtility.OwnerPrefix);
                int numeric = 0;
                bool keepExisting = EntityCodeUtility.TryParse(normalized, EntityCodeUtility.OwnerPrefix, out numeric)
                    && usedCodes.Add(numeric);

                if (!keepExisting)
                {
                    while (nextCode <= 99999 && usedCodes.Contains(nextCode))
                        nextCode++;

                    if (nextCode > 99999)
                    {
                        Debug.LogWarning("[NPCRegistry] Exhausted available OWN codes (OWN00001-OWN99999).");
                        continue;
                    }

                    numeric = nextCode;
                    usedCodes.Add(numeric);
                    nextCode++;
                }

                assignedIds[soul] = EntityCodeUtility.Format(EntityCodeUtility.OwnerPrefix, numeric);
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

        private static List<DiscoveredNPC> DiscoverProjectNPCSouls()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            List<DiscoveredNPC> discovered = new(prefabGuids.Length);

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabRoot == null)
                    continue;

                NPCSoul[] souls = prefabRoot.GetComponentsInChildren<NPCSoul>(true);
                for (int j = 0; j < souls.Length; j++)
                {
                    NPCSoul soul = souls[j];
                    if (soul == null)
                        continue;

                    if (soul.EntityKind != EntityType.NPC)
                        continue;

                    discovered.Add(new DiscoveredNPC
                    {
                        Soul = soul,
                        SortKey = BuildSortKey(path, soul)
                    });
                }
            }

            discovered.Sort(static (a, b) => string.CompareOrdinal(a.SortKey, b.SortKey));
            return discovered;
        }

        private static string BuildSortKey(string prefabPath, NPCSoul soul)
        {
            GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(soul);
            return $"{prefabPath}|{globalObjectId.targetObjectId}";
        }

        private static bool ApplyAssignedIds(Dictionary<NPCSoul, string> assignedIds)
        {
            bool modifiedAny = false;
            foreach (KeyValuePair<NPCSoul, string> pair in assignedIds)
            {
                NPCSoul soul = pair.Key;
                string assignedId = pair.Value;
                if (soul == null || string.Equals(soul.OwnerId, assignedId, System.StringComparison.Ordinal))
                    continue;

                SerializedObject serializedObject = new(soul);
                SerializedProperty idProperty = serializedObject.FindProperty("_ownerId");
                if (idProperty == null)
                    continue;

                idProperty.stringValue = assignedId;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(soul);
                modifiedAny = true;
            }

            return modifiedAny;
        }

        private static List<Entry> BuildEntries(List<DiscoveredNPC> discovered, Dictionary<NPCSoul, string> assignedIds)
        {
            List<Entry> rebuilt = new(discovered.Count);
            for (int i = 0; i < discovered.Count; i++)
            {
                NPCSoul soul = discovered[i].Soul;
                if (soul == null || !assignedIds.TryGetValue(soul, out string assignedId))
                    continue;

                rebuilt.Add(new Entry
                {
                    OwnerId = assignedId,
                    Prefab = soul
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

                    bool sameId = string.Equals(current.OwnerId, incoming.OwnerId, System.StringComparison.Ordinal);
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

        private struct DiscoveredNPC
        {
            public NPCSoul Soul;
            public string SortKey;
        }
#endif
    }
}
