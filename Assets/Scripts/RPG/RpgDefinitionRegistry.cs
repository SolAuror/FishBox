using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sol.Rpg
{
    [CreateAssetMenu(fileName = "RpgDefinitionRegistry", menuName = "Sol/RPG/Definition Registry")]
    public sealed class RpgDefinitionRegistry : ScriptableObject
    {
        private const string LegacyResourceName = "RpgDefinitionRegistry";

        [SerializeField] private List<RpgStatDefinition> _stats = new();
        [SerializeField] private List<RpgSkillDefinition> _skills = new();
        [SerializeField] private List<RpgFactionDefinition> _factions = new();
        [SerializeField] private List<RpgShopDefinition> _shops = new();

        private static RpgDefinitionRegistry _instance;
        private Dictionary<string, RpgStatDefinition> _statsById;
        private Dictionary<string, RpgSkillDefinition> _skillsById;
        private Dictionary<string, RpgFactionDefinition> _factionsById;
        private Dictionary<string, RpgShopDefinition> _shopsById;

        public IReadOnlyList<RpgStatDefinition> Stats => _stats;
        public IReadOnlyList<RpgSkillDefinition> Skills => _skills;
        public IReadOnlyList<RpgFactionDefinition> Factions => _factions;
        public IReadOnlyList<RpgShopDefinition> Shops => _shops;

#if UNITY_EDITOR
        private const string DefaultAssetPath = "Assets/Data/RpgDefinitionRegistry.asset";
        private const string LegacyAssetPath = "Assets/Resources/RpgDefinitionRegistry.asset";
        private static bool _editorSyncScheduled;
        private static bool _isEditorSynchronizing;
#endif

        public static RpgDefinitionRegistry Get()
        {
            if (_instance != null)
                return _instance;

#if UNITY_EDITOR
            _instance = GetOrCreateEditorAsset();
#else
            _instance = FindLoadedRegistryAsset();
            if (_instance == null)
                _instance = Resources.Load<RpgDefinitionRegistry>(LegacyResourceName);
#endif
            return _instance;
        }

        public RpgStatDefinition GetStat(string id)
        {
            EnsureLookups();
            return !string.IsNullOrWhiteSpace(id) && _statsById.TryGetValue(id.Trim(), out RpgStatDefinition def) ? def : null;
        }

        public RpgSkillDefinition GetSkill(string id)
        {
            EnsureLookups();
            return !string.IsNullOrWhiteSpace(id) && _skillsById.TryGetValue(id.Trim(), out RpgSkillDefinition def) ? def : null;
        }

        public RpgFactionDefinition GetFaction(string id)
        {
            EnsureLookups();
            return !string.IsNullOrWhiteSpace(id) && _factionsById.TryGetValue(id.Trim(), out RpgFactionDefinition def) ? def : null;
        }

        public RpgShopDefinition GetShop(string id)
        {
            EnsureLookups();
            return !string.IsNullOrWhiteSpace(id) && _shopsById.TryGetValue(id.Trim(), out RpgShopDefinition def) ? def : null;
        }

        private static RpgDefinitionRegistry FindLoadedRegistryAsset()
        {
            RpgDefinitionRegistry[] registries = Resources.FindObjectsOfTypeAll<RpgDefinitionRegistry>();
            for (int i = 0; i < registries.Length; i++)
            {
                if (registries[i] != null && registries[i].name == nameof(RpgDefinitionRegistry))
                    return registries[i];
            }

            return registries.Length > 0 ? registries[0] : null;
        }

        private void EnsureLookups()
        {
            if (_statsById != null)
                return;

            _statsById = BuildLookup(_stats);
            _skillsById = BuildLookup(_skills);
            _factionsById = BuildLookup(_factions);
            _shopsById = BuildLookup(_shops);
        }

        private static Dictionary<string, T> BuildLookup<T>(IReadOnlyList<T> definitions) where T : RpgDefinition
        {
            Dictionary<string, T> lookup = new(System.StringComparer.OrdinalIgnoreCase);
            if (definitions == null)
                return lookup;

            for (int i = 0; i < definitions.Count; i++)
            {
                T definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                    continue;

                string key = definition.Id.Trim();
                if (!lookup.ContainsKey(key))
                    lookup[key] = definition;
            }

            return lookup;
        }

        private void OnValidate()
        {
            ClearLookups();
#if UNITY_EDITOR
            if (!_isEditorSynchronizing)
                ScheduleEditorSync();
#endif
        }

        private void OnDisable()
        {
            ClearLookups();
        }

        private void ClearLookups()
        {
            _statsById = null;
            _skillsById = null;
            _factionsById = null;
            _shopsById = null;
        }

#if UNITY_EDITOR
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
            RpgDefinitionRegistry registry = GetOrCreateEditorAsset();
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

        private static RpgDefinitionRegistry GetOrCreateEditorAsset()
        {
            RpgDefinitionRegistry loaded = AssetDatabase.LoadAssetAtPath<RpgDefinitionRegistry>(DefaultAssetPath);
            if (loaded != null)
            {
                EnsurePreloadedAsset(loaded);
                return loaded;
            }

            RpgDefinitionRegistry legacy = AssetDatabase.LoadAssetAtPath<RpgDefinitionRegistry>(LegacyAssetPath);
            if (legacy != null)
            {
                EnsureDataFolder();
                string moveError = AssetDatabase.MoveAsset(LegacyAssetPath, DefaultAssetPath);
                loaded = string.IsNullOrEmpty(moveError)
                    ? AssetDatabase.LoadAssetAtPath<RpgDefinitionRegistry>(DefaultAssetPath)
                    : legacy;
                EnsurePreloadedAsset(loaded);
                return loaded;
            }

            EnsureDataFolder();

            RpgDefinitionRegistry created = CreateInstance<RpgDefinitionRegistry>();
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

        private static void EnsurePreloadedAsset(RpgDefinitionRegistry registry)
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
            bool changed = false;
            changed |= ReplaceIfDifferent(_stats, DiscoverDefinitions<RpgStatDefinition>());
            changed |= ReplaceIfDifferent(_skills, DiscoverDefinitions<RpgSkillDefinition>());
            changed |= ReplaceIfDifferent(_factions, DiscoverDefinitions<RpgFactionDefinition>());
            changed |= ReplaceIfDifferent(_shops, DiscoverDefinitions<RpgShopDefinition>());

            if (!changed)
                return;

            ClearLookups();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        private static List<T> DiscoverDefinitions<T>() where T : RpgDefinition
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            List<T> discovered = new(guids.Length);
            HashSet<T> seen = new();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                T definition = AssetDatabase.LoadAssetAtPath<T>(path);
                if (definition == null || !seen.Add(definition))
                    continue;

                discovered.Add(definition);
            }

            discovered.Sort(static (a, b) =>
                System.StringComparer.OrdinalIgnoreCase.Compare(a != null ? a.Id : string.Empty, b != null ? b.Id : string.Empty));
            return discovered;
        }

        private static bool ReplaceIfDifferent<T>(List<T> current, List<T> rebuilt) where T : RpgDefinition
        {
            if (current.Count == rebuilt.Count)
            {
                bool identical = true;
                for (int i = 0; i < current.Count; i++)
                {
                    if (current[i] != rebuilt[i])
                    {
                        identical = false;
                        break;
                    }
                }

                if (identical)
                    return false;
            }

            current.Clear();
            current.AddRange(rebuilt);
            return true;
        }
#endif
    }
}
