using System.Collections.Generic;
using Sol.AI;
using Sol.Grab;
using Sol.Rpg;

namespace Sol.Editor
{
    internal enum RpgAuthoringWarningSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    internal sealed class RpgAuthoringWarning
    {
        public RpgAuthoringWarningSeverity Severity;
        public string Message;

        public RpgAuthoringWarning(RpgAuthoringWarningSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }

    internal static class RpgDefinitionValidator
    {
        public static List<RpgAuthoringWarning> Validate(RpgDefinition definition, RpgDefinitionRegistry registry)
        {
            List<RpgAuthoringWarning> warnings = new();
            if (definition == null)
            {
                warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Error, "Missing RPG definition asset."));
                return warnings;
            }

            string prefix = PrefixFor(definition);
            if (string.IsNullOrWhiteSpace(definition.Id))
                warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Error, "Missing definition id."));
            else if (!EntityCodeUtility.TryParse(definition.Id, prefix, out _))
                warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Error, $"Definition id '{definition.Id}' does not match {prefix}##### format."));

            if (string.IsNullOrWhiteSpace(definition.DisplayName))
                warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, "Display name is empty."));

            switch (definition)
            {
                case RpgSkillDefinition skill:
                    ValidateSkill(skill, registry, warnings);
                    break;
                case RpgFactionDefinition faction:
                    ValidateFaction(faction, registry, warnings);
                    break;
                case RpgShopDefinition shop:
                    ValidateShop(shop, registry, warnings);
                    break;
                case GameplayTagDefinition tag:
                    ValidateGameplayTag(tag, warnings);
                    break;
                case StatusEffectDefinition status:
                    ValidateStatusEffect(status, registry, warnings);
                    break;
                case TraitDefinition trait:
                    ValidateTrait(trait, registry, warnings);
                    break;
            }

            return warnings;
        }

        public static List<RpgAuthoringWarning> ValidateRegistry(RpgDefinitionRegistry registry)
        {
            List<RpgAuthoringWarning> warnings = new();
            if (registry == null)
            {
                warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Error, "RpgDefinitionRegistry asset is missing."));
                return warnings;
            }

            ValidateDuplicateIds(registry.Stats, RpgDefinitionIds.StatPrefix, "stat", warnings);
            ValidateDuplicateIds(registry.Skills, RpgDefinitionIds.SkillPrefix, "skill", warnings);
            ValidateDuplicateIds(registry.Factions, RpgDefinitionIds.FactionPrefix, "faction", warnings);
            ValidateDuplicateIds(registry.Shops, RpgDefinitionIds.ShopPrefix, "shop", warnings);
            ValidateDuplicateIds(registry.Tags, RpgDefinitionIds.TagPrefix, "gameplay tag", warnings);
            ValidateDuplicateIds(registry.StatusEffects, RpgDefinitionIds.StatusEffectPrefix, "status effect", warnings);
            ValidateDuplicateIds(registry.Traits, RpgDefinitionIds.TraitPrefix, "trait", warnings);
            ValidateGameplayTagPaths(registry.Tags, warnings);
            return warnings;
        }

        public static string PrefixFor(RpgDefinition definition)
        {
            return definition switch
            {
                RpgStatDefinition => RpgDefinitionIds.StatPrefix,
                RpgSkillDefinition => RpgDefinitionIds.SkillPrefix,
                RpgFactionDefinition => RpgDefinitionIds.FactionPrefix,
                RpgShopDefinition => RpgDefinitionIds.ShopPrefix,
                GameplayTagDefinition => RpgDefinitionIds.TagPrefix,
                StatusEffectDefinition => RpgDefinitionIds.StatusEffectPrefix,
                TraitDefinition => RpgDefinitionIds.TraitPrefix,
                _ => string.Empty
            };
        }

        private static void ValidateDuplicateIds<T>(
            IReadOnlyList<T> definitions,
            string prefix,
            string label,
            List<RpgAuthoringWarning> warnings) where T : RpgDefinition
        {
            HashSet<string> seen = new(System.StringComparer.OrdinalIgnoreCase);
            if (definitions == null)
                return;

            for (int i = 0; i < definitions.Count; i++)
            {
                T definition = definitions[i];
                if (definition == null)
                {
                    warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Error, $"Registry {label} entry {i} is null."));
                    continue;
                }

                string id = definition.Id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Error, $"{definition.name} has no {label} id."));
                    continue;
                }

                if (!EntityCodeUtility.TryParse(id, prefix, out _))
                    warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Error, $"{definition.name} id '{id}' does not match {prefix}##### format."));

                if (!seen.Add(id.Trim()))
                    warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Error, $"Duplicate {label} id {id}."));
            }
        }

        private static void ValidateSkill(RpgSkillDefinition skill, RpgDefinitionRegistry registry, List<RpgAuthoringWarning> warnings)
        {
            if (skill.MaxLevel <= 0)
                warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Error, "Skill max level must be greater than zero."));
            if (skill.UseXpMultiplier <= 0f)
                warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, "Use XP multiplier should be greater than zero."));

            if (!string.IsNullOrWhiteSpace(skill.GoverningStatId)
                && (registry == null || registry.GetStat(skill.GoverningStatId) == null))
            {
                warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, $"Governing stat '{skill.GoverningStatId}' is not in the RPG registry."));
            }
        }

        private static void ValidateFaction(RpgFactionDefinition faction, RpgDefinitionRegistry registry, List<RpgAuthoringWarning> warnings)
        {
            IReadOnlyList<RpgFactionRelationship> relationships = faction.Relationships;
            if (relationships == null)
                return;

            HashSet<string> seen = new(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < relationships.Count; i++)
            {
                RpgFactionRelationship relationship = relationships[i];
                if (relationship == null)
                    continue;

                if (string.IsNullOrWhiteSpace(relationship.FactionId))
                {
                    warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, $"Relationship {i + 1} has no faction id."));
                    continue;
                }

                if (string.Equals(relationship.FactionId, faction.Id, System.StringComparison.OrdinalIgnoreCase))
                    warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, "Faction has a relationship entry pointing to itself."));

                if (!seen.Add(relationship.FactionId.Trim()))
                    warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, $"Duplicate relationship for faction '{relationship.FactionId}'."));

                if (registry != null && registry.GetFaction(relationship.FactionId) == null)
                    warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, $"Relationship faction '{relationship.FactionId}' is not in the RPG registry."));
            }
        }

        private static void ValidateShop(RpgShopDefinition shop, RpgDefinitionRegistry registry, List<RpgAuthoringWarning> warnings)
        {
            NPCRegistry npcRegistry = NPCRegistry.Get();
            if (!string.IsNullOrWhiteSpace(shop.OwnerNpcId) && npcRegistry != null && npcRegistry.GetPrefab(shop.OwnerNpcId) == null)
                warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, $"Shop owner NPC '{shop.OwnerNpcId}' is not in the NPC registry."));

            if (!string.IsNullOrWhiteSpace(shop.FactionId) && (registry == null || registry.GetFaction(shop.FactionId) == null))
                warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, $"Shop faction '{shop.FactionId}' is not in the RPG registry."));

            if (shop.BuyPriceMultiplier <= 0f)
                warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, "Buy price multiplier should be greater than zero."));
            if (shop.SellPriceMultiplier < 0f)
                warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, "Sell price multiplier should not be negative."));

            ItemRegistry itemRegistry = ItemRegistry.Get();
            IReadOnlyList<RpgShopStockEntry> stock = shop.Stock;
            if (stock == null)
                return;

            for (int i = 0; i < stock.Count; i++)
            {
                RpgShopStockEntry entry = stock[i];
                if (entry == null)
                {
                    warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, $"Stock entry {i + 1} is empty."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.ItemId))
                    warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, $"Stock entry {i + 1} has no item id."));
                else if (itemRegistry != null && itemRegistry.GetPrefab(entry.ItemId) == null)
                    warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, $"Stock item '{entry.ItemId}' is not in the item registry."));

                if (entry.MaxQuantity < entry.MinQuantity)
                    warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, $"Stock entry {i + 1} max quantity is below min quantity."));
                if (entry.PriceMultiplier <= 0f)
                    warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, $"Stock entry {i + 1} price multiplier should be greater than zero."));
            }
        }

        private static void ValidateGameplayTag(GameplayTagDefinition tag, List<RpgAuthoringWarning> warnings)
        {
            if (string.IsNullOrWhiteSpace(tag.TagPath))
            {
                warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Error, "Gameplay tag path is empty."));
                return;
            }

            if (!GameplayTagUtility.IsValidPath(tag.TagPath))
                warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Error, $"Gameplay tag path '{tag.TagPath}' is not a valid dotted path."));
        }

        private static void ValidateGameplayTagPaths(IReadOnlyList<GameplayTagDefinition> tags, List<RpgAuthoringWarning> warnings)
        {
            if (tags == null)
                return;

            HashSet<string> paths = new(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < tags.Count; i++)
            {
                GameplayTagDefinition tag = tags[i];
                if (tag == null || string.IsNullOrWhiteSpace(tag.TagPath))
                    continue;

                string path = tag.TagPath.Trim();
                if (!paths.Add(path))
                    warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Error, $"Duplicate gameplay tag path {path}."));
            }

            for (int i = 0; i < tags.Count; i++)
            {
                GameplayTagDefinition tag = tags[i];
                if (tag == null || string.IsNullOrWhiteSpace(tag.TagPath))
                    continue;

                foreach (string parentPath in GameplayTagUtility.EnumerateParentPaths(tag.TagPath))
                {
                    if (!paths.Contains(parentPath))
                        warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Info, $"Gameplay tag '{tag.TagPath}' has no authored parent tag '{parentPath}'."));
                }
            }
        }

        private static void ValidateStatusEffect(StatusEffectDefinition status, RpgDefinitionRegistry registry, List<RpgAuthoringWarning> warnings)
        {
            ValidateTagSet(status.Tags, registry, warnings, "status tag");
            ValidateTagSet(status.GrantedTags, registry, warnings, "granted tag");

            bool hasTags = HasAny(status.Tags) || HasAny(status.GrantedTags);
            bool hasDamage = status.PeriodicDamage > 0f;
            bool hasModifiers = status.StatModifiers?.Modifiers != null && status.StatModifiers.Modifiers.Count > 0;
            if (!hasTags && !hasDamage && !hasModifiers)
                warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, "Status effect has no tags, periodic damage, or stat modifiers."));

            if (status.PeriodicDamage > 0f && status.TickInterval <= 0f)
                warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Error, "Periodic damage requires a tick interval above zero."));
        }

        private static void ValidateTrait(TraitDefinition trait, RpgDefinitionRegistry registry, List<RpgAuthoringWarning> warnings)
        {
            ValidateTagSet(trait.Tags, registry, warnings, "trait tag");
            ValidateTagSet(trait.GrantedTags, registry, warnings, "granted tag");

            bool hasTags = HasAny(trait.Tags) || HasAny(trait.GrantedTags);
            bool hasModifiers = trait.StatModifiers?.Modifiers != null && trait.StatModifiers.Modifiers.Count > 0;
            bool hasReactions = trait.Reactions != null && trait.Reactions.Count > 0;
            if (!hasTags && !hasModifiers && !hasReactions)
                warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, "Trait has no tags, stat modifiers, or reactions."));
        }

        private static void ValidateTagSet(GameplayTagSet tags, RpgDefinitionRegistry registry, List<RpgAuthoringWarning> warnings, string label)
        {
            if (tags == null)
                return;

            foreach (string tagId in tags.EnumerateTagIds())
            {
                if (registry == null || registry.GetTag(tagId) == null)
                    warnings.Add(new RpgAuthoringWarning(RpgAuthoringWarningSeverity.Warning, $"Referenced {label} '{tagId}' is not in the RPG registry."));
            }
        }

        private static bool HasAny(GameplayTagSet tags)
        {
            if (tags == null)
                return false;

            foreach (string _ in tags.EnumerateTagIds())
                return true;

            return false;
        }
    }
}
