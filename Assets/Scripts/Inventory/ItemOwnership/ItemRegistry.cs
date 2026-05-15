using System;
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
#region Inspector Settings

        [Tooltip("Inspector: tunes entries.")]
        [SerializeField] private List<Entry> _entries = new();
#endregion

        [System.Serializable]
        public class Entry
        {
            public string ItemId;
            public string DisplayName = "Item";
            public ItemType ItemType = ItemType.Material;
            [Min(0)] public int Value;
            [Min(0f)] public float Weight;
            public Sprite Icon;
            [TextArea] public string FlavourText = string.Empty;
            public bool IsStackable;
            public bool IsConsumable;
            public bool IsTradeable = true;
            [Min(1)] public int MaxStackSize = 1;
            public ItemUseOccasion UseOccasion = ItemUseOccasion.InventoryOnly;
            public List<ItemUseEffect> UseEffects = new();
            public float Damage;
            public float Defense;
            public string EquipBone = string.Empty;
            public Vector3 EquipOffset;
            public Vector3 EquipRotation;
            public EquipDomain EquipDomain = EquipDomain.Auto;
            public WeaponHanding WeaponHanding = WeaponHanding.OneHanded;
            public List<EquipmentSlotType> AllowedEquipSlots = new();
            public ItemAuthoringTemplate AuthoringTemplate = ItemAuthoringTemplate.None;
            [TextArea] public string AuthoringNotes = string.Empty;
            public ItemComponent Prefab;

            public string NameOrId => string.IsNullOrWhiteSpace(DisplayName) ? ItemId : DisplayName.Trim();

            public void Normalize()
            {
                ItemId = EntityCodeUtility.NormalizeOrEmpty(ItemId, EntityCodeUtility.ItemPrefix);
                DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? "Item" : DisplayName.Trim();
                FlavourText = FlavourText ?? string.Empty;
                MaxStackSize = Mathf.Max(1, MaxStackSize);
                Value = ItemType == ItemType.Gold ? 1 : Mathf.Max(0, Value);
                Weight = Mathf.Max(0f, Weight);
                if (ItemType == ItemType.Gold)
                {
                    IsStackable = true;
                    IsTradeable = true;
                    MaxStackSize = Mathf.Max(MaxStackSize, 1);
                    UseOccasion = ItemUseOccasion.Never;
                }
                if (!IsStackable)
                    MaxStackSize = Mathf.Max(1, MaxStackSize);
                UseEffects ??= new List<ItemUseEffect>();
                AllowedEquipSlots ??= new List<EquipmentSlotType>();
                EquipBone = EquipBone?.Trim() ?? string.Empty;
                AuthoringNotes = AuthoringNotes?.Trim() ?? string.Empty;
            }

            public void CopyDesignFromPrefab(ItemComponent item)
            {
                if (item == null)
                    return;

                ItemId = item.LegacyItemId;
                DisplayName = item.LegacyItemName;
                ItemType = item.LegacyType;
                Value = item.LegacyValue;
                Weight = item.LegacyWeight;
                Icon = item.LegacyIcon;
                FlavourText = item.LegacyFlavourText;
                IsStackable = item.LegacyIsStackable;
                IsConsumable = item.LegacyIsConsumable;
                IsTradeable = item.LegacyIsTradeable;
                MaxStackSize = item.LegacyMaxStackSize;
                UseOccasion = item.LegacyUseOccasion;
                UseEffects = item.CloneLegacyUseEffects();
                Damage = item.LegacyDamage;
                Defense = item.LegacyDefense;
                EquipBone = item.LegacyEquipBone;
                EquipOffset = item.LegacyEquipOffset;
                EquipRotation = item.LegacyEquipRotation;
                EquipDomain = item.LegacyEquipDomain;
                WeaponHanding = item.LegacyWeaponHanding;
                AllowedEquipSlots = item.CloneLegacyAllowedEquipSlots();
                AuthoringTemplate = item.LegacyAuthoringTemplate;
                AuthoringNotes = item.LegacyAuthoringNotes;
                Prefab = item;
                Normalize();
            }
        }

        private static ItemRegistry _instance;
        private Dictionary<string, ItemComponent> _lookup;

        public IReadOnlyList<Entry> Entries => _entries;

#if UNITY_EDITOR
        private const string DefaultAssetPath = "Assets/Data/ItemRegistry.asset";
        private static bool _editorSyncScheduled;
        private static bool _isEditorSynchronizing;
        private static bool _editorDefinitionsChangedScheduled;
#endif

        public static ItemRegistry Get()
        {
            if (_instance != null)
                return _instance;

#if UNITY_EDITOR
            _instance = GetOrCreateEditorAsset();
#else
            _instance = FindLoadedRegistryAsset();
#endif
            return _instance;
        }

        public ItemComponent GetPrefab(string itemId)
        {
            return GetVisualPrefab(itemId);
        }

        public Entry GetDefinition(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            EnsureLookup();
            _lookup.TryGetValue(itemId.Trim(), out ItemComponent prefab);
            if (prefab == null)
                return FindEntry(itemId);

            return FindEntry(prefab.ItemId);
        }

        public Entry GetDefinitionForPrefab(ItemComponent prefab)
        {
            if (prefab == null)
                return null;

            EnsureLookup();
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry != null && entry.Prefab == prefab)
                    return entry;
            }

            return GetDefinition(prefab.ItemId);
        }

        public ItemComponent GetVisualPrefab(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            EnsureLookup();
            _lookup.TryGetValue(itemId.Trim(), out ItemComponent prefab);
            return prefab;
        }

        public ItemComponent InstantiateWorldItem(string itemId, Transform parent = null)
        {
            ItemComponent prefab = GetVisualPrefab(itemId);
            if (prefab == null)
                return null;

            ItemComponent instance = Instantiate(prefab, parent);
            instance.gameObject.SetActive(false);
            return instance;
        }

        private static ItemRegistry FindLoadedRegistryAsset()
        {
            ItemRegistry[] registries = Resources.FindObjectsOfTypeAll<ItemRegistry>();
            for (int i = 0; i < registries.Length; i++)
            {
                if (registries[i] != null && registries[i].name == nameof(ItemRegistry))
                    return registries[i];
            }

            return registries.Length > 0 ? registries[0] : null;
        }

        private void EnsureLookup()
        {
            if (_lookup != null)
                return;

            _lookup = new Dictionary<string, ItemComponent>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId))
                    continue;

                entry.Normalize();
                string key = entry.ItemId.Trim();
                if (!_lookup.ContainsKey(key) && entry.Prefab != null)
                    _lookup[key] = entry.Prefab;
            }
        }

        private Entry FindEntry(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || _entries == null)
                return null;

            string normalized = EntityCodeUtility.NormalizeOrEmpty(itemId, EntityCodeUtility.ItemPrefix);
            if (string.IsNullOrEmpty(normalized))
                normalized = itemId.Trim();

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry == null)
                    continue;

                if (string.Equals(entry.ItemId, normalized, System.StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            return null;
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
                if (entry == null)
                    continue;

                if (entry.Prefab != null && string.IsNullOrWhiteSpace(entry.ItemId) && !string.IsNullOrWhiteSpace(entry.Prefab.LegacyItemId))
                    entry.ItemId = entry.Prefab.LegacyItemId.Trim();
                entry.Normalize();
            }

            _lookup = null;

#if UNITY_EDITOR
            ScheduleEditorSync();
            NotifyEditorDefinitionsChanged();
#endif
        }

#if UNITY_EDITOR
        internal static bool IsEditorSyncInProgress => _isEditorSynchronizing;
        public static event Action EditorDefinitionsChanged;

        public static void NotifyEditorDefinitionsChanged()
        {
            if (_instance != null)
                _instance._lookup = null;

            if (_editorDefinitionsChangedScheduled)
                return;

            _editorDefinitionsChangedScheduled = true;
            EditorApplication.delayCall += ExecuteEditorDefinitionsChanged;
        }

        private static void ExecuteEditorDefinitionsChanged()
        {
            _editorDefinitionsChangedScheduled = false;
            EditorDefinitionsChanged?.Invoke();
        }

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

        [ContextMenu("Migrate Prefab Design Into Registry")]
        private void MigratePrefabDesignIntoRegistry()
        {
            MigratePrefabDesignIntoRegistryNow();
        }

        [MenuItem("Tools/Sol/Items/Migrate Prefab Design Into Registry")]
        public static void MigratePrefabDesignIntoRegistryNow()
        {
            ForceEditorSync();

            ItemRegistry registry = GetOrCreateEditorAsset();
            if (registry == null || registry._entries == null)
                return;

            int migrated = 0;
            for (int i = 0; i < registry._entries.Count; i++)
            {
                Entry entry = registry._entries[i];
                ItemComponent prefab = entry?.Prefab;
                if (prefab == null || prefab.LegacyDesignMigratedToRegistry)
                    continue;

                entry.CopyDesignFromPrefab(prefab);
                MarkPrefabDesignMigrated(prefab);
                migrated++;
            }

            if (migrated > 0)
            {
                registry._lookup = null;
                EditorUtility.SetDirty(registry);
                AssetDatabase.SaveAssets();
                NotifyEditorDefinitionsChanged();
            }

            Debug.Log($"[ItemRegistry] Migrated prefab design into {migrated} registry item definition(s).");
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
            ItemRegistry loaded = AssetDatabase.LoadAssetAtPath<ItemRegistry>(DefaultAssetPath);
            if (loaded != null)
            {
                EnsurePreloadedAsset(loaded);
                return loaded;
            }

            EnsureDataFolder();

            ItemRegistry created = CreateInstance<ItemRegistry>();
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

        private static void EnsurePreloadedAsset(ItemRegistry registry)
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
                NotifyEditorDefinitionsChanged();
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

                Entry entry = FindExistingEntryForBuild(assignedId);
                if (entry == null)
                {
                    entry = new Entry();
                    entry.CopyDesignFromPrefab(item);
                    MarkPrefabDesignMigrated(item);
                }
                else if (entry.Prefab == null)
                {
                    entry.Prefab = item;
                }

                entry.ItemId = assignedId;
                entry.Prefab = item;
                entry.Normalize();
                rebuilt.Add(entry);
            }

            return rebuilt;
        }

        private static void MarkPrefabDesignMigrated(ItemComponent item)
        {
            if (item == null || item.LegacyDesignMigratedToRegistry)
                return;

            SerializedObject serializedObject = new(item);
            SerializedProperty migrated = serializedObject.FindProperty("_legacyDesignMigratedToRegistry");
            if (migrated == null)
                return;

            migrated.boolValue = true;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static Entry FindExistingEntryForBuild(string itemId)
        {
            ItemRegistry registry = _instance;
            if (registry?._entries == null || string.IsNullOrWhiteSpace(itemId))
                return null;

            for (int i = 0; i < registry._entries.Count; i++)
            {
                Entry entry = registry._entries[i];
                if (entry == null)
                    continue;

                if (string.Equals(entry.ItemId, itemId, System.StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            return null;
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
                    bool sameDesign = sameId
                        && string.Equals(current.DisplayName, incoming.DisplayName, System.StringComparison.Ordinal)
                        && current.ItemType == incoming.ItemType
                        && current.Value == incoming.Value
                        && Mathf.Approximately(current.Weight, incoming.Weight)
                        && current.Icon == incoming.Icon
                        && current.IsStackable == incoming.IsStackable
                        && current.IsConsumable == incoming.IsConsumable
                        && current.IsTradeable == incoming.IsTradeable
                        && current.MaxStackSize == incoming.MaxStackSize;
                    if (!sameId || !samePrefab || !sameDesign)
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
