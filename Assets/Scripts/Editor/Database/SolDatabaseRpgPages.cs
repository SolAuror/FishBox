using System.Collections.Generic;
using Sol.Rpg;

namespace Sol.Editor
{
    internal sealed class SolDatabaseStatsPage : SolDatabaseRpgDefinitionPage<RpgStatDefinition>
    {
        public override SolDatabaseTab Tab => SolDatabaseTab.Stats;
        public override string DisplayName => "Stats";
        protected override string IdPrefix => RpgDefinitionIds.StatPrefix;
        protected override string NewDisplayName => "New Stat";
        protected override bool IncludeRegistryIssues => true;
        protected override IReadOnlyList<RpgStatDefinition> GetDefinitions(RpgDefinitionRegistry registry) => registry?.Stats;
        protected override string BuildSubtitle(RpgStatDefinition definition)
        {
            return definition == null
                ? string.Empty
                : $"{definition.Category}  Base {definition.BaseValue:0.#}  Range {definition.MinimumValue:0.#}-{definition.MaximumValue:0.#}";
        }
    }

    internal sealed class SolDatabaseSkillsPage : SolDatabaseRpgDefinitionPage<RpgSkillDefinition>
    {
        public override SolDatabaseTab Tab => SolDatabaseTab.Skills;
        public override string DisplayName => "Skills";
        protected override string IdPrefix => RpgDefinitionIds.SkillPrefix;
        protected override string NewDisplayName => "New Skill";
        protected override IReadOnlyList<RpgSkillDefinition> GetDefinitions(RpgDefinitionRegistry registry) => registry?.Skills;
        protected override string BuildSubtitle(RpgSkillDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            string stat = string.IsNullOrWhiteSpace(definition.GoverningStatId) ? "No governing stat" : $"Stat {definition.GoverningStatId}";
            return $"{definition.Category}  Max {definition.MaxLevel}  {stat}";
        }
    }

    internal sealed class SolDatabaseFactionsPage : SolDatabaseRpgDefinitionPage<RpgFactionDefinition>
    {
        public override SolDatabaseTab Tab => SolDatabaseTab.Factions;
        public override string DisplayName => "Factions";
        protected override string IdPrefix => RpgDefinitionIds.FactionPrefix;
        protected override string NewDisplayName => "New Faction";
        protected override IReadOnlyList<RpgFactionDefinition> GetDefinitions(RpgDefinitionRegistry registry) => registry?.Factions;
        protected override string BuildSubtitle(RpgFactionDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            string legal = definition.LegalAuthority ? "Legal authority" : "No legal authority";
            string joinable = definition.Joinable ? "Joinable" : "Not joinable";
            return $"{joinable}  {legal}  Relations {definition.Relationships?.Count ?? 0}";
        }
    }

    internal sealed class SolDatabaseShopsPage : SolDatabaseRpgDefinitionPage<RpgShopDefinition>
    {
        public override SolDatabaseTab Tab => SolDatabaseTab.Shops;
        public override string DisplayName => "Shops";
        protected override string IdPrefix => RpgDefinitionIds.ShopPrefix;
        protected override string NewDisplayName => "New Shop";
        protected override IReadOnlyList<RpgShopDefinition> GetDefinitions(RpgDefinitionRegistry registry) => registry?.Shops;
        protected override string BuildSubtitle(RpgShopDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            string owner = string.IsNullOrWhiteSpace(definition.OwnerNpcId) ? "No owner" : $"Owner {definition.OwnerNpcId}";
            return $"{owner}  Gold {definition.Gold}  Stock {definition.Stock?.Count ?? 0}";
        }
    }

    internal sealed class SolDatabaseTagsPage : SolDatabaseRpgDefinitionPage<GameplayTagDefinition>
    {
        public override SolDatabaseTab Tab => SolDatabaseTab.Tags;
        public override string DisplayName => "Tags";
        protected override string IdPrefix => RpgDefinitionIds.TagPrefix;
        protected override string NewDisplayName => "New Tag";
        protected override bool IncludeRegistryIssues => true;
        protected override IReadOnlyList<GameplayTagDefinition> GetDefinitions(RpgDefinitionRegistry registry) => registry?.Tags;
        protected override string BuildSubtitle(GameplayTagDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            string path = string.IsNullOrWhiteSpace(definition.TagPath) ? "No tag path" : definition.TagPath;
            return $"{definition.Category}  {path}";
        }
    }

    internal sealed class SolDatabaseStatusEffectsPage : SolDatabaseRpgDefinitionPage<StatusEffectDefinition>
    {
        public override SolDatabaseTab Tab => SolDatabaseTab.StatusEffects;
        public override string DisplayName => "Statuses";
        protected override string IdPrefix => RpgDefinitionIds.StatusEffectPrefix;
        protected override string NewDisplayName => "New Status";
        protected override IReadOnlyList<StatusEffectDefinition> GetDefinitions(RpgDefinitionRegistry registry) => registry?.StatusEffects;
        protected override string BuildSubtitle(StatusEffectDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            string periodic = definition.PeriodicDamage > 0f ? $"Damage {definition.PeriodicDamage:0.#}/{definition.TickInterval:0.#}s" : "No periodic damage";
            return $"Duration {definition.Duration:0.#}s  {definition.StackRule}  {periodic}";
        }
    }

    internal sealed class SolDatabaseTraitsPage : SolDatabaseRpgDefinitionPage<TraitDefinition>
    {
        public override SolDatabaseTab Tab => SolDatabaseTab.Traits;
        public override string DisplayName => "Traits";
        protected override string IdPrefix => RpgDefinitionIds.TraitPrefix;
        protected override string NewDisplayName => "New Trait";
        protected override IReadOnlyList<TraitDefinition> GetDefinitions(RpgDefinitionRegistry registry) => registry?.Traits;
        protected override string BuildSubtitle(TraitDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            return $"Reactions {definition.Reactions?.Count ?? 0}  Modifiers {definition.StatModifiers?.Modifiers?.Count ?? 0}";
        }
    }
}
