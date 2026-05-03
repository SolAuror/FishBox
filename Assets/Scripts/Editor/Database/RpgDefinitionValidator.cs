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
    }
}
