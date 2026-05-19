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
        [SerializeField] private List<RpgStatDefinition> _stats = new();
        [SerializeField] private List<RpgSkillDefinition> _skills = new();
        [SerializeField] private List<RpgFactionDefinition> _factions = new();
        [SerializeField] private List<RpgShopDefinition> _shops = new();
        [SerializeField] private List<GameplayTagDefinition> _tags = new();
        [SerializeField] private List<StatusEffectDefinition> _statusEffects = new();
        [SerializeField] private List<TraitDefinition> _traits = new();

        private static RpgDefinitionRegistry _instance;
        private Dictionary<string, RpgStatDefinition> _statsById;
        private Dictionary<string, RpgSkillDefinition> _skillsById;
        private Dictionary<string, RpgFactionDefinition> _factionsById;
        private Dictionary<string, RpgShopDefinition> _shopsById;
        private Dictionary<string, GameplayTagDefinition> _tagsById;
        private Dictionary<string, GameplayTagDefinition> _tagsByPath;
        private Dictionary<string, StatusEffectDefinition> _statusEffectsById;
        private Dictionary<string, TraitDefinition> _traitsById;

        public IReadOnlyList<RpgStatDefinition> Stats => _stats;
        public IReadOnlyList<RpgSkillDefinition> Skills => _skills;
        public IReadOnlyList<RpgFactionDefinition> Factions => _factions;
        public IReadOnlyList<RpgShopDefinition> Shops => _shops;
        public IReadOnlyList<GameplayTagDefinition> Tags => _tags;
        public IReadOnlyList<StatusEffectDefinition> StatusEffects => _statusEffects;
        public IReadOnlyList<TraitDefinition> Traits => _traits;

#if UNITY_EDITOR
        private const string DefaultAssetPath = "Assets/Data/RpgDefinitionRegistry.asset";
        private const string DefaultRpgFolder = "Assets/Data/RPG";
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

        public GameplayTagDefinition GetTag(string id)
        {
            EnsureLookups();
            return !string.IsNullOrWhiteSpace(id) && _tagsById.TryGetValue(id.Trim(), out GameplayTagDefinition def) ? def : null;
        }

        public GameplayTagDefinition GetTagByPath(string tagPath)
        {
            EnsureLookups();
            string normalized = GameplayTagUtility.NormalizePathOrEmpty(tagPath);
            return !string.IsNullOrWhiteSpace(normalized) && _tagsByPath.TryGetValue(normalized, out GameplayTagDefinition def) ? def : null;
        }

        public StatusEffectDefinition GetStatusEffect(string id)
        {
            EnsureLookups();
            return !string.IsNullOrWhiteSpace(id) && _statusEffectsById.TryGetValue(id.Trim(), out StatusEffectDefinition def) ? def : null;
        }

        public TraitDefinition GetTrait(string id)
        {
            EnsureLookups();
            return !string.IsNullOrWhiteSpace(id) && _traitsById.TryGetValue(id.Trim(), out TraitDefinition def) ? def : null;
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
            _tagsById = BuildLookup(_tags);
            _tagsByPath = BuildTagPathLookup(_tags);
            _statusEffectsById = BuildLookup(_statusEffects);
            _traitsById = BuildLookup(_traits);
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

        private static Dictionary<string, GameplayTagDefinition> BuildTagPathLookup(IReadOnlyList<GameplayTagDefinition> definitions)
        {
            Dictionary<string, GameplayTagDefinition> lookup = new(System.StringComparer.OrdinalIgnoreCase);
            if (definitions == null)
                return lookup;

            for (int i = 0; i < definitions.Count; i++)
            {
                GameplayTagDefinition definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.TagPath))
                    continue;

                string key = definition.TagPath.Trim();
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
            _tagsById = null;
            _tagsByPath = null;
            _statusEffectsById = null;
            _traitsById = null;
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
            EnsureStarterTags();
            EnsureStarterStatusEffects();
            EnsureStarterTraits();
            changed |= ReplaceIfDifferent(_tags, DiscoverDefinitions<GameplayTagDefinition>());
            changed |= ReplaceIfDifferent(_statusEffects, DiscoverDefinitions<StatusEffectDefinition>());
            changed |= ReplaceIfDifferent(_traits, DiscoverDefinitions<TraitDefinition>());

            if (!changed)
                return;

            ClearLookups();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureStarterTags()
        {
            EnsureGameplayTag("Actor", GameplayTagCategory.Actor);
            EnsureGameplayTag("Actor.Player", GameplayTagCategory.Actor);
            EnsureGameplayTag("Actor.NPC", GameplayTagCategory.Actor);
            EnsureGameplayTag("Actor.Hostile", GameplayTagCategory.Actor);
            EnsureGameplayTag("Actor.Civilian", GameplayTagCategory.Actor);
            EnsureGameplayTag("Actor.Unique", GameplayTagCategory.Actor);
            EnsureGameplayTag("Actor.Criminal", GameplayTagCategory.Actor);
            EnsureGameplayTag("Actor.Wanted", GameplayTagCategory.Actor);
            EnsureGameplayTag("Actor.Dead", GameplayTagCategory.Actor);
            EnsureGameplayTag("Actor.InCombat", GameplayTagCategory.Actor);
            EnsureGameplayTag("Item", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Weapon", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Armor", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Equipment", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Tool", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Key", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Currency", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Currency.Gold", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Quest", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Material", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Consumable", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Tradeable", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Food", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Drink", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Potion", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Fishing", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Fishing.Rod", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Fishing.Bait", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Fishing.Lure", GameplayTagCategory.Item);
            EnsureGameplayTag("Item.Fishing.Caught", GameplayTagCategory.Item);
            EnsureGameplayTag("Faction", GameplayTagCategory.Faction);
            EnsureGameplayTag("Faction.Civilian", GameplayTagCategory.Faction);
            EnsureGameplayTag("Faction.Bandit", GameplayTagCategory.Faction);
            EnsureGameplayTag("Faction.Guard", GameplayTagCategory.Faction);
            EnsureGameplayTag("Faction.LegalAuthority", GameplayTagCategory.Faction);
            EnsureGameplayTag("Job", GameplayTagCategory.Job);
            EnsureGameplayTag("Job.Trader", GameplayTagCategory.Job);
            EnsureGameplayTag("Job.QuestGiver", GameplayTagCategory.Job);
            EnsureGameplayTag("Job.Guard", GameplayTagCategory.Job);
            EnsureGameplayTag("Job.Farmer", GameplayTagCategory.Job);
            EnsureGameplayTag("Job.Fisher", GameplayTagCategory.Job);
            EnsureGameplayTag("Job.Crafter", GameplayTagCategory.Job);
            EnsureGameplayTag("Job.Shopkeeper", GameplayTagCategory.Job);
            EnsureGameplayTag("Interaction", GameplayTagCategory.Interaction);
            EnsureGameplayTag("Interaction.Rest", GameplayTagCategory.Interaction);
            EnsureGameplayTag("Interaction.Sleep", GameplayTagCategory.Interaction);
            EnsureGameplayTag("Interaction.Work", GameplayTagCategory.Interaction);
            EnsureGameplayTag("Interaction.WaterSource", GameplayTagCategory.Interaction);
            EnsureGameplayTag("Interaction.Harvestable", GameplayTagCategory.Interaction);
            EnsureGameplayTag("Interaction.Shop", GameplayTagCategory.Interaction);
            EnsureGameplayTag("Interaction.Crafting", GameplayTagCategory.Interaction);
            EnsureGameplayTag("Interaction.Fishing", GameplayTagCategory.Interaction);
            EnsureGameplayTag("Interaction.Social", GameplayTagCategory.Interaction);
            EnsureGameplayTag("Location", GameplayTagCategory.Location);
            EnsureGameplayTag("Location.Home", GameplayTagCategory.Location);
            EnsureGameplayTag("Location.Shop", GameplayTagCategory.Location);
            EnsureGameplayTag("Location.Workplace", GameplayTagCategory.Location);
            EnsureGameplayTag("Location.Tavern", GameplayTagCategory.Location);
            EnsureGameplayTag("Location.Wilderness", GameplayTagCategory.Location);
            EnsureGameplayTag("Fish", GameplayTagCategory.Fish);
            EnsureGameplayTag("Fish.Predator", GameplayTagCategory.Fish);
            EnsureGameplayTag("Fish.Rarity", GameplayTagCategory.Fish);
            EnsureGameplayTag("Fish.Rarity.Common", GameplayTagCategory.Fish);
            EnsureGameplayTag("Fish.Rarity.Uncommon", GameplayTagCategory.Fish);
            EnsureGameplayTag("Fish.Rarity.Rare", GameplayTagCategory.Fish);
            EnsureGameplayTag("Fish.Rarity.Legendary", GameplayTagCategory.Fish);
            EnsureGameplayTag("Fish.Rarity.Mythical", GameplayTagCategory.Fish);
            EnsureGameplayTag("Surface", GameplayTagCategory.Surface);
            EnsureGameplayTag("Surface.Dirt", GameplayTagCategory.Surface);
            EnsureGameplayTag("Surface.Grass", GameplayTagCategory.Surface);
            EnsureGameplayTag("Surface.Stone", GameplayTagCategory.Surface);
            EnsureGameplayTag("State", GameplayTagCategory.State);
            EnsureGameplayTag("State.Locked", GameplayTagCategory.State);
            EnsureGameplayTag("State.Lockpickable", GameplayTagCategory.State);
            EnsureGameplayTag("State.Stolen", GameplayTagCategory.State);
            EnsureGameplayTag("State.Owned", GameplayTagCategory.State);
            EnsureGameplayTag("State.Private", GameplayTagCategory.State);
            EnsureGameplayTag("State.Public", GameplayTagCategory.State);
            EnsureGameplayTag("State.Depleted", GameplayTagCategory.State);
            EnsureGameplayTag("State.Reserved", GameplayTagCategory.State);
            EnsureGameplayTag("State.InUse", GameplayTagCategory.State);
            EnsureGameplayTag("Ownership", GameplayTagCategory.Ownership);
            EnsureGameplayTag("Ownership.PublicUse", GameplayTagCategory.Ownership);
            EnsureGameplayTag("Ownership.PrivateUse", GameplayTagCategory.Ownership);
            EnsureGameplayTag("Crime", GameplayTagCategory.Crime);
            EnsureGameplayTag("Crime.Theft", GameplayTagCategory.Crime);
            EnsureGameplayTag("Crime.Trespass", GameplayTagCategory.Crime);
            EnsureGameplayTag("Damage", GameplayTagCategory.Damage);
            EnsureGameplayTag("Damage.Physical", GameplayTagCategory.Damage);
            EnsureGameplayTag("Damage.Poison", GameplayTagCategory.Damage);
            EnsureGameplayTag("Damage.Fire", GameplayTagCategory.Damage);
            EnsureGameplayTag("Damage.Shock", GameplayTagCategory.Damage);
            EnsureGameplayTag("Status", GameplayTagCategory.Status);
            EnsureGameplayTag("Status.Poisoned", GameplayTagCategory.Status);
            EnsureGameplayTag("Status.Burning", GameplayTagCategory.Status);
            EnsureGameplayTag("Status.Blessed", GameplayTagCategory.Status);
            EnsureGameplayTag("Status.Cursed", GameplayTagCategory.Status);
            EnsureGameplayTag("Trait", GameplayTagCategory.Trait);
            EnsureGameplayTag("Trait.Immortal", GameplayTagCategory.Trait);
            EnsureGameplayTag("Trait.Tireless", GameplayTagCategory.Trait);
            EnsureGameplayTag("Resist", GameplayTagCategory.Defense);
            EnsureGameplayTag("Resist.Poison", GameplayTagCategory.Defense);
            EnsureGameplayTag("Immune", GameplayTagCategory.Defense);
            EnsureGameplayTag("Immune.Poison", GameplayTagCategory.Defense);
        }

        private static void EnsureStarterStatusEffects()
        {
            string poisonedTagId = FindGameplayTagIdByPath("Status.Poisoned");
            string poisonDamageTagId = FindGameplayTagIdByPath("Damage.Poison");
            if (string.IsNullOrEmpty(poisonedTagId) || string.IsNullOrEmpty(poisonDamageTagId) || HasStatusEffectWithTag(poisonedTagId))
                return;

            const string folder = DefaultRpgFolder + "/Statuses";
            EnsureFolderRecursive(folder);

            StatusEffectDefinition definition = CreateInstance<StatusEffectDefinition>();
            definition.Tags.AddSerializedTagId(poisonedTagId);
            definition.GrantedTags.AddSerializedTagId(poisonedTagId);

            SerializedObject serializedObject = new(definition);
            serializedObject.Update();
            serializedObject.FindProperty("_id").stringValue = NextDefinitionId<StatusEffectDefinition>(RpgDefinitionIds.StatusEffectPrefix);
            serializedObject.FindProperty("_displayName").stringValue = "Poisoned";
            serializedObject.FindProperty("_description").stringValue = "Deals poison damage over time and grants Status.Poisoned while active.";
            serializedObject.FindProperty("_duration").floatValue = 8f;
            serializedObject.FindProperty("_stackRule").enumValueIndex = (int)StatusEffectStackRule.AddStackRefreshDuration;
            serializedObject.FindProperty("_maxStacks").intValue = 3;
            serializedObject.FindProperty("_tickInterval").floatValue = 1f;
            serializedObject.FindProperty("_periodicDamage").floatValue = 2f;

            SerializedProperty damageTags = serializedObject.FindProperty("_periodicDamageTags");
            damageTags.arraySize = 1;
            damageTags.GetArrayElementAtIndex(0).stringValue = "Damage.Poison";
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{definition.Id}_Poisoned.asset");
            AssetDatabase.CreateAsset(definition, assetPath);
            AssetDatabase.ImportAsset(assetPath);
        }

        private static void EnsureStarterTraits()
        {
            EnsureStarterTrait(
                "Trait.Immortal",
                "Immortal",
                "Prevents lethal damage from killing the actor.",
                TraitReaction.PreventDeath);

            EnsureStarterTrait(
                "Trait.Tireless",
                "Tireless",
                "Ignores stamina costs while active.",
                TraitReaction.IgnoreStaminaCosts);
        }

        private static void EnsureStarterTrait(string tagPath, string displayName, string description, TraitReaction reaction)
        {
            string traitTagId = FindGameplayTagIdByPath(tagPath);
            if (string.IsNullOrEmpty(traitTagId) || HasTraitWithTag(traitTagId))
                return;

            const string folder = DefaultRpgFolder + "/Traits";
            EnsureFolderRecursive(folder);

            TraitDefinition definition = CreateInstance<TraitDefinition>();
            definition.Tags.AddSerializedTagId(traitTagId);
            definition.GrantedTags.AddSerializedTagId(traitTagId);

            SerializedObject serializedObject = new(definition);
            serializedObject.Update();
            serializedObject.FindProperty("_id").stringValue = NextDefinitionId<TraitDefinition>(RpgDefinitionIds.TraitPrefix);
            serializedObject.FindProperty("_displayName").stringValue = displayName;
            serializedObject.FindProperty("_description").stringValue = description;

            SerializedProperty reactions = serializedObject.FindProperty("_reactions");
            reactions.arraySize = 1;
            reactions.GetArrayElementAtIndex(0).enumValueIndex = (int)reaction;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{definition.Id}_{displayName}.asset");
            AssetDatabase.CreateAsset(definition, assetPath);
            AssetDatabase.ImportAsset(assetPath);
        }

        private static void EnsureGameplayTag(string tagPath, GameplayTagCategory category)
        {
            string normalizedPath = GameplayTagUtility.NormalizePathOrEmpty(tagPath);
            if (string.IsNullOrEmpty(normalizedPath))
                return;

            string[] existingGuids = AssetDatabase.FindAssets("t:GameplayTagDefinition");
            for (int i = 0; i < existingGuids.Length; i++)
            {
                string existingPath = AssetDatabase.GUIDToAssetPath(existingGuids[i]);
                GameplayTagDefinition existing = AssetDatabase.LoadAssetAtPath<GameplayTagDefinition>(existingPath);
                if (existing != null && string.Equals(existing.TagPath, normalizedPath, System.StringComparison.OrdinalIgnoreCase))
                    return;
            }

            const string folder = DefaultRpgFolder + "/Tags";
            EnsureFolderRecursive(folder);

            string id = NextTagId();
            string displayName = normalizedPath.Substring(normalizedPath.LastIndexOf('.') + 1);
            GameplayTagDefinition definition = CreateInstance<GameplayTagDefinition>();
            SerializedObject serializedObject = new(definition);
            serializedObject.Update();
            serializedObject.FindProperty("_id").stringValue = id;
            serializedObject.FindProperty("_displayName").stringValue = displayName;
            serializedObject.FindProperty("_tagPath").stringValue = normalizedPath;
            serializedObject.FindProperty("_category").enumValueIndex = (int)category;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            string fileName = normalizedPath.Replace('.', '_');
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{id}_{fileName}.asset");
            AssetDatabase.CreateAsset(definition, assetPath);
            AssetDatabase.ImportAsset(assetPath);
        }

        private static string NextTagId()
        {
            HashSet<int> used = new();
            string[] guids = AssetDatabase.FindAssets("t:GameplayTagDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameplayTagDefinition definition = AssetDatabase.LoadAssetAtPath<GameplayTagDefinition>(path);
                if (definition != null && EntityCodeUtility.TryParse(definition.Id, RpgDefinitionIds.TagPrefix, out int numeric))
                    used.Add(numeric);
            }

            for (int next = 1; next <= 99999; next++)
            {
                if (!used.Contains(next))
                    return EntityCodeUtility.Format(RpgDefinitionIds.TagPrefix, next);
            }

            return string.Empty;
        }

        private static string NextDefinitionId<T>(string prefix) where T : RpgDefinition
        {
            HashSet<int> used = new();
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                T definition = AssetDatabase.LoadAssetAtPath<T>(path);
                if (definition != null && EntityCodeUtility.TryParse(definition.Id, prefix, out int numeric))
                    used.Add(numeric);
            }

            for (int next = 1; next <= 99999; next++)
            {
                if (!used.Contains(next))
                    return EntityCodeUtility.Format(prefix, next);
            }

            return string.Empty;
        }

        private static string FindGameplayTagIdByPath(string tagPath)
        {
            string normalizedPath = GameplayTagUtility.NormalizePathOrEmpty(tagPath);
            if (string.IsNullOrEmpty(normalizedPath))
                return string.Empty;

            string[] guids = AssetDatabase.FindAssets("t:GameplayTagDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameplayTagDefinition definition = AssetDatabase.LoadAssetAtPath<GameplayTagDefinition>(path);
                if (definition != null && string.Equals(definition.TagPath, normalizedPath, System.StringComparison.OrdinalIgnoreCase))
                    return definition.Id;
            }

            return string.Empty;
        }

        private static bool HasStatusEffectWithTag(string tagId)
        {
            string normalizedId = EntityCodeUtility.NormalizeOrEmpty(tagId, RpgDefinitionIds.TagPrefix);
            if (string.IsNullOrEmpty(normalizedId))
                return false;

            string[] guids = AssetDatabase.FindAssets("t:StatusEffectDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                StatusEffectDefinition definition = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(path);
                if (definition != null && (HasSerializedTag(definition.Tags, normalizedId) || HasSerializedTag(definition.GrantedTags, normalizedId)))
                    return true;
            }

            return false;
        }

        private static bool HasTraitWithTag(string tagId)
        {
            string normalizedId = EntityCodeUtility.NormalizeOrEmpty(tagId, RpgDefinitionIds.TagPrefix);
            if (string.IsNullOrEmpty(normalizedId))
                return false;

            string[] guids = AssetDatabase.FindAssets("t:TraitDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TraitDefinition definition = AssetDatabase.LoadAssetAtPath<TraitDefinition>(path);
                if (definition != null && (HasSerializedTag(definition.Tags, normalizedId) || HasSerializedTag(definition.GrantedTags, normalizedId)))
                    return true;
            }

            return false;
        }

        private static bool HasSerializedTag(GameplayTagSet tagSet, string tagId)
        {
            if (tagSet == null || string.IsNullOrWhiteSpace(tagId))
                return false;

            foreach (string existingId in tagSet.EnumerateTagIds())
            {
                if (string.Equals(existingId, tagId, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void EnsureFolderRecursive(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            string parent = System.IO.Path.GetDirectoryName(folder)?.Replace("\\", "/");
            string leaf = System.IO.Path.GetFileName(folder);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(leaf))
                return;

            EnsureFolderRecursive(parent);
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(parent, leaf);
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
