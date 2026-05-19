namespace Sol.Rpg
{
    public static class GameplayCapabilityTags
    {
        public const string ItemConsumable = "Item.Consumable";
        public const string ItemTradeable = "Item.Tradeable";
        public const string ItemWeapon = "Item.Weapon";
        public const string ItemArmor = "Item.Armor";
        public const string ItemEquipment = "Item.Equipment";
        public const string ItemTool = "Item.Tool";
        public const string ItemKey = "Item.Key";
        public const string ItemCurrency = "Item.Currency";
        public const string ItemCurrencyGold = "Item.Currency.Gold";
        public const string ItemQuest = "Item.Quest";
        public const string ItemMaterial = "Item.Material";
        public const string ItemFood = "Item.Food";
        public const string ItemDrink = "Item.Drink";
        public const string ItemPotion = "Item.Potion";
        public const string ItemFishing = "Item.Fishing";
        public const string ItemFishingRod = "Item.Fishing.Rod";
        public const string ItemFishingBait = "Item.Fishing.Bait";
        public const string ItemFishingLure = "Item.Fishing.Lure";
        public const string ItemFishingCaught = "Item.Fishing.Caught";
        public const string ActorNpc = "Actor.NPC";
        public const string ActorPlayer = "Actor.Player";
        public const string ActorHostile = "Actor.Hostile";
        public const string ActorCivilian = "Actor.Civilian";
        public const string ActorUnique = "Actor.Unique";
        public const string ActorCriminal = "Actor.Criminal";
        public const string ActorWanted = "Actor.Wanted";
        public const string ActorDead = "Actor.Dead";
        public const string ActorInCombat = "Actor.InCombat";
        public const string JobTrader = "Job.Trader";
        public const string JobQuestGiver = "Job.QuestGiver";
        public const string JobGuard = "Job.Guard";
        public const string JobFarmer = "Job.Farmer";
        public const string JobFisher = "Job.Fisher";
        public const string JobCrafter = "Job.Crafter";
        public const string JobShopkeeper = "Job.Shopkeeper";
        public const string FactionCivilian = "Faction.Civilian";
        public const string FactionBandit = "Faction.Bandit";
        public const string FactionGuard = "Faction.Guard";
        public const string FactionLegalAuthority = "Faction.LegalAuthority";
        public const string InteractionRest = "Interaction.Rest";
        public const string InteractionSleep = "Interaction.Sleep";
        public const string InteractionWork = "Interaction.Work";
        public const string InteractionWaterSource = "Interaction.WaterSource";
        public const string InteractionHarvestable = "Interaction.Harvestable";
        public const string InteractionShop = "Interaction.Shop";
        public const string InteractionCrafting = "Interaction.Crafting";
        public const string InteractionFishing = "Interaction.Fishing";
        public const string InteractionSocial = "Interaction.Social";
        public const string LocationHome = "Location.Home";
        public const string LocationShop = "Location.Shop";
        public const string LocationWorkplace = "Location.Workplace";
        public const string LocationTavern = "Location.Tavern";
        public const string LocationWilderness = "Location.Wilderness";
        public const string FishPredator = "Fish.Predator";
        public const string FishRarityCommon = "Fish.Rarity.Common";
        public const string FishRarityUncommon = "Fish.Rarity.Uncommon";
        public const string FishRarityRare = "Fish.Rarity.Rare";
        public const string FishRarityLegendary = "Fish.Rarity.Legendary";
        public const string FishRarityMythical = "Fish.Rarity.Mythical";
        public const string StateLocked = "State.Locked";
        public const string StateLockpickable = "State.Lockpickable";
        public const string StateStolen = "State.Stolen";
        public const string StateOwned = "State.Owned";
        public const string StatePrivate = "State.Private";
        public const string StatePublic = "State.Public";
        public const string StateDepleted = "State.Depleted";
        public const string StateReserved = "State.Reserved";
        public const string StateInUse = "State.InUse";
        public const string OwnershipPublicUse = "Ownership.PublicUse";
        public const string OwnershipPrivateUse = "Ownership.PrivateUse";
        public const string CrimeTheft = "Crime.Theft";
        public const string CrimeTrespass = "Crime.Trespass";

        public static string FishRarity(AI.FishRarity rarity)
        {
            return rarity switch
            {
                AI.FishRarity.Uncommon => FishRarityUncommon,
                AI.FishRarity.Rare => FishRarityRare,
                AI.FishRarity.Legendary => FishRarityLegendary,
                AI.FishRarity.Mythical => FishRarityMythical,
                _ => FishRarityCommon,
            };
        }
    }
}
