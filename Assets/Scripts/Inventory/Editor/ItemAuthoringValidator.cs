using System.Collections.Generic;
using Sol.Fishing;
using Sol.Grab;
using Sol.Rpg;
using UnityEngine;

namespace Sol.Editor
{
    public enum ItemAuthoringWarningSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public sealed class ItemAuthoringWarning
    {
        public ItemAuthoringWarningSeverity Severity;
        public string Message;

        public ItemAuthoringWarning(ItemAuthoringWarningSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }

    public static class ItemAuthoringValidator
    {
        public static List<ItemAuthoringWarning> Validate(ItemComponent item)
        {
            List<ItemAuthoringWarning> warnings = new();
            if (item == null)
            {
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Error, "Missing item component."));
                return warnings;
            }

            if (item.GetComponent<CaughtFishItem>() != null)
            {
                warnings.Add(new ItemAuthoringWarning(
                    ItemAuthoringWarningSeverity.Info,
                    "Runtime catch prefab - name, value, icon, and visual are populated at runtime by CaughtFishItem.ConfigureFromFish/Data. Standard authoring checks are skipped."));
                return warnings;
            }

            if (string.IsNullOrWhiteSpace(item.ItemId))
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Error, "Missing item id."));
            else
            {
                ItemRegistry.Entry definition = ItemRegistry.Get()?.GetDefinition(item.ItemId);
                if (definition == null)
                {
                    warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Error, $"Item id '{item.ItemId}' is missing from ItemRegistry."));
                }
                else
                {
                    ValidatePrefabDrift(item, definition, warnings);
                }
            }

            if (string.IsNullOrWhiteSpace(item.ItemName) || string.Equals(item.ItemName.Trim(), "Item", System.StringComparison.Ordinal))
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, "Item name still looks like a placeholder."));

            if (item.Icon == null)
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, "Missing inventory icon."));

            if (item.IsStackable && item.MaxStackSize <= 1)
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, "Stackable items should have a max stack size above 1."));

            if (!item.IsStackable && item.MaxStackSize > 1)
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Info, "Max stack size is ignored when the item is not stackable."));

            if (item.Type == ItemType.Gold && (!item.IsStackable || item.MaxStackSize <= 1 || item.Value != 1))
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, "Gold should stay stackable with value normalized to 1."));

            ValidateUse(item, warnings);
            ValidateEquipment(item, warnings);
            ValidateFishing(item, warnings);

            return warnings;
        }

        public static List<ItemAuthoringWarning> ValidateDefinition(ItemRegistry.Entry definition)
        {
            List<ItemAuthoringWarning> warnings = new();
            if (definition == null)
            {
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Error, "Missing item registry definition."));
                return warnings;
            }

            definition.Normalize();

            if (string.IsNullOrWhiteSpace(definition.ItemId))
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Error, "Missing item id."));

            if (string.IsNullOrWhiteSpace(definition.DisplayName) || string.Equals(definition.DisplayName.Trim(), "Item", System.StringComparison.Ordinal))
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, "Item name still looks like a placeholder."));

            if (definition.Icon == null)
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, "Missing inventory icon."));

            if (definition.Prefab == null)
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, "Missing visual/world prefab. This item cannot spawn in-world or render a 3D preview."));

            if (definition.IsStackable && definition.MaxStackSize <= 1)
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, "Stackable items should have a max stack size above 1."));

            if (!definition.IsStackable && definition.MaxStackSize > 1)
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Info, "Max stack size is ignored when the item is not stackable."));

            if (definition.ItemType == ItemType.Gold && (!definition.IsStackable || definition.MaxStackSize <= 1 || definition.Value != 1))
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, "Gold should stay stackable with value normalized to 1."));

            ValidateUse(definition, warnings);
            ValidateEquipment(definition, warnings);
            ValidateTags(definition.Tags, warnings);
            return warnings;
        }

        public static List<ItemAuthoringWarning> ValidateRegistry(ItemRegistry registry)
        {
            List<ItemAuthoringWarning> warnings = new();
            if (registry == null)
            {
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Error, "ItemRegistry asset is missing."));
                return warnings;
            }

            HashSet<string> seen = new(System.StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<ItemRegistry.Entry> entries = registry.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                ItemRegistry.Entry entry = entries[i];
                if (entry == null)
                {
                    warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Error, $"Registry entry {i} is null."));
                    continue;
                }

                string id = string.IsNullOrWhiteSpace(entry.ItemId) ? entry.Prefab != null ? entry.Prefab.ItemId : string.Empty : entry.ItemId.Trim();
                if (string.IsNullOrWhiteSpace(id))
                {
                    warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Error, $"Registry entry {i} has no item id."));
                    continue;
                }

                if (!seen.Add(id))
                    warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Error, $"Duplicate item id {id}."));

                warnings.AddRange(ValidateDefinition(entry));
            }

            return warnings;
        }

        public static bool HasWarnings(ItemComponent item)
        {
            List<ItemAuthoringWarning> warnings = Validate(item);
            return warnings.Count > 0;
        }

        private static void ValidateUse(ItemComponent item, List<ItemAuthoringWarning> warnings)
        {
            IReadOnlyList<ItemUseEffect> effects = item.UseEffects;
            int effectCount = effects?.Count ?? 0;

            if (item.CanUseFromInventory && effectCount == 0)
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, "Consumable item has no use effects."));

            if (!item.IsConsumable && effectCount > 0)
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, "Use effects are ignored because this item is not consumable."));

            if (item.IsConsumable && item.UseOccasion == ItemUseOccasion.Never)
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Info, "Use action is hidden because occasion is set to Never."));

            if (effects == null)
                return;

            for (int i = 0; i < effects.Count; i++)
            {
                ItemUseEffect effect = effects[i];
                if (effect == null)
                {
                    warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, $"Use effect {i + 1} is empty."));
                    continue;
                }

                if (effect.Amount <= 0f)
                    warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, $"Use effect {i + 1} has no amount."));
            }
        }

        private static void ValidateUse(ItemRegistry.Entry definition, List<ItemAuthoringWarning> warnings)
        {
            IReadOnlyList<ItemUseEffect> effects = definition.UseEffects;
            int effectCount = effects?.Count ?? 0;

            bool isConsumable = definition.HasConsumableTag;
            if (isConsumable && definition.UseOccasion != ItemUseOccasion.Never && effectCount == 0)
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, "Consumable item has no use effects."));

            if (!isConsumable && effectCount > 0)
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, "Use effects are ignored because this item is not consumable."));

            if (effects == null)
                return;

            for (int i = 0; i < effects.Count; i++)
            {
                ItemUseEffect effect = effects[i];
                if (effect == null)
                    warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, $"Use effect {i + 1} is empty."));
                else if (effect.Amount <= 0f)
                    warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, $"Use effect {i + 1} has no amount."));
            }
        }

        private static void ValidateEquipment(ItemComponent item, List<ItemAuthoringWarning> warnings)
        {
            if (!ItemTypeRules.IsEquipableType(item.Type))
                return;

            if (string.IsNullOrWhiteSpace(item.EquipBone))
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, "Equipment item is missing an equip bone/socket."));

            IReadOnlyList<EquipmentSlotType> allowed = item.AllowedEquipSlots;
            if (allowed == null)
                return;

            for (int i = 0; i < allowed.Count; i++)
            {
                EquipmentSlotType slot = allowed[i];
                if (!ItemTypeRules.CanItemUseSlot(item, slot))
                    warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, $"{slot} is not valid for this equipment domain."));
            }
        }

        private static void ValidateEquipment(ItemRegistry.Entry definition, List<ItemAuthoringWarning> warnings)
        {
            if (!ItemTypeRules.IsEquipableType(definition.ItemType))
                return;

            if (string.IsNullOrWhiteSpace(definition.EquipBone))
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, "Equipment item is missing an equip bone/socket."));

            IReadOnlyList<EquipmentSlotType> allowed = definition.AllowedEquipSlots;
            if (allowed == null)
                return;

            for (int i = 0; i < allowed.Count; i++)
            {
                EquipmentSlotType slot = allowed[i];
                if (!ItemTypeRules.CanItemUseSlot(definition.ItemType, definition.EquipDomain, definition.WeaponHanding, slot))
                    warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, $"{slot} is not valid for this equipment domain."));
            }
        }

        private static void ValidateTags(GameplayTagSet tags, List<ItemAuthoringWarning> warnings)
        {
            if (tags == null)
                return;

            RpgDefinitionRegistry registry = RpgDefinitionRegistry.Get();
            HashSet<string> seen = new(System.StringComparer.OrdinalIgnoreCase);
            foreach (string tagId in tags.EnumerateTagIds())
            {
                if (!seen.Add(tagId))
                    warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Info, $"Duplicate gameplay tag '{tagId}' is ignored at runtime."));

                if (registry == null || registry.GetTag(tagId) == null)
                    warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, $"Gameplay tag '{tagId}' is not in the RPG registry."));
            }
        }

        private static void ValidatePrefabDrift(ItemComponent item, ItemRegistry.Entry definition, List<ItemAuthoringWarning> warnings)
        {
            if (item == null || definition == null)
                return;

            if (!string.Equals(item.LegacyItemName, definition.DisplayName, System.StringComparison.Ordinal)
                || item.LegacyType != definition.ItemType
                || item.LegacyValue != definition.Value
                || !Mathf.Approximately(item.LegacyWeight, definition.Weight)
                || item.LegacyIcon != definition.Icon)
            {
                warnings.Add(new ItemAuthoringWarning(
                    ItemAuthoringWarningSeverity.Info,
                    "Legacy prefab design fields differ from ItemRegistry. Registry values are authoritative."));
            }
        }

        private static void ValidateFishing(ItemComponent item, List<ItemAuthoringWarning> warnings)
        {
            bool hasBait = item.GetComponent<FishingBaitItem>() != null;
            bool hasLure = item.GetComponent<FishingLureItem>() != null;

            if (item.AuthoringTemplate == ItemAuthoringTemplate.FishingBait && !hasBait)
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, "Fishing bait template is missing FishingBaitItem."));

            if (item.AuthoringTemplate == ItemAuthoringTemplate.FishingLure && !hasLure)
                warnings.Add(new ItemAuthoringWarning(ItemAuthoringWarningSeverity.Warning, "Fishing lure template is missing FishingLureItem."));
        }
    }
}
