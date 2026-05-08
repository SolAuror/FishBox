using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Sol;
using Sol.AI;
using Sol.Editor;
using Sol.Grab;
using Sol.Quests;
using Sol.Rpg;
using UnityEditor;
using UnityEngine;

public sealed class RpgDatabaseFoundationTests
{
    private sealed class ReferenceDropdownTarget : ScriptableObject
    {
        [SerializeField] private string _referenceId;

        public string ReferenceId => _referenceId;
    }

    [Test]
    public void ReferenceDropdownSelection_WritesToSerializedTarget()
    {
        ReferenceDropdownTarget target = ScriptableObject.CreateInstance<ReferenceDropdownTarget>();
        try
        {
            SerializedObject serializedObject = new(target);
            serializedObject.Update();

            bool changed = ReferenceDropdown.ApplyReferenceSelection(
                ReferenceDropdown.ReferenceKind.Item,
                serializedObject,
                target,
                "_referenceId",
                "ITM12345");

            Assert.That(changed, Is.True);
            Assert.That(target.ReferenceId, Is.EqualTo("ITM12345"));
            Assert.That(serializedObject.FindProperty("_referenceId").stringValue, Is.EqualTo("ITM12345"));
        }
        finally
        {
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void LegacyDatabaseSelectors_RouteToUnifiedHub()
    {
        ItemDatabaseWindow.SelectByItemId("ITM12345");
        Assert.That(SolDatabaseWindow.LastOpenedTab, Is.EqualTo(SolDatabaseTab.Items));
        Assert.That(SolDatabaseWindow.LastRequestedSelectionId, Is.EqualTo("ITM12345"));

        NPCDatabaseWindow.SelectByOwnerId("OWN12345");
        Assert.That(SolDatabaseWindow.LastOpenedTab, Is.EqualTo(SolDatabaseTab.NPCs));
        Assert.That(SolDatabaseWindow.LastRequestedSelectionId, Is.EqualTo("OWN12345"));

        QuestDatabaseWindow.SelectByQuestId("QST12345");
        Assert.That(SolDatabaseWindow.LastOpenedTab, Is.EqualTo(SolDatabaseTab.Quests));
        Assert.That(SolDatabaseWindow.LastRequestedSelectionId, Is.EqualTo("QST12345"));

        EditorWindow.GetWindow<SolDatabaseWindow>()?.Close();
    }

    [Test]
    public void UnifiedItemRows_FilterBySearchTypeAndWarningFlags()
    {
        GameObject itemObject = new("Iron Sword");
        try
        {
            itemObject.AddComponent<BoxCollider>();
            itemObject.AddComponent<GrabbableComponent>();
            ItemComponent item = itemObject.AddComponent<ItemComponent>();
            var row = new SolDatabaseItemPage.ItemRow
            {
                Item = item,
                ItemId = "ITM00010",
                ItemName = "Iron Sword",
                ItemType = ItemType.Weapon,
                SearchText = "iron sword itm00010 weapon",
                IsEquipable = true,
                IsConsumable = false,
                IsStackable = false,
                MissingIcon = true,
                WarningCount = 1
            };

            Assert.That(SolDatabaseItemPage.RowMatchesFilters(row, "iron", SolDatabaseItemPage.ItemFilter.All, false, ItemType.Material), Is.True);
            Assert.That(SolDatabaseItemPage.RowMatchesFilters(row, "itm00010", SolDatabaseItemPage.ItemFilter.Equipable, true, ItemType.Weapon), Is.True);
            Assert.That(SolDatabaseItemPage.RowMatchesFilters(row, "iron", SolDatabaseItemPage.ItemFilter.Consumable, false, ItemType.Material), Is.False);
            Assert.That(SolDatabaseItemPage.RowMatchesFilters(row, "missing", SolDatabaseItemPage.ItemFilter.All, false, ItemType.Material), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(itemObject);
        }
    }

    [Test]
    public void ItemRegistryDefinitionLookup_ReturnsRegistryAuthoredDesignWithoutPrefab()
    {
        ItemRegistry registry = ScriptableObject.CreateInstance<ItemRegistry>();
        try
        {
            SetRegistryEntries(registry, new List<ItemRegistry.Entry>
            {
                new()
                {
                    ItemId = "ITM00055",
                    DisplayName = "Sunberry Tea",
                    ItemType = ItemType.Drink,
                    Value = 12,
                    IsStackable = true,
                    MaxStackSize = 6,
                    IsTradeable = false
                }
            });

            ItemRegistry.Entry definition = registry.GetDefinition("itm55");

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.DisplayName, Is.EqualTo("Sunberry Tea"));
            Assert.That(definition.ItemType, Is.EqualTo(ItemType.Drink));
            Assert.That(definition.Value, Is.EqualTo(12));
            Assert.That(definition.IsStackable, Is.True);
            Assert.That(definition.MaxStackSize, Is.EqualTo(6));
            Assert.That(definition.IsTradeable, Is.False);
            Assert.That(registry.GetVisualPrefab("ITM00055"), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void ItemComponentFacade_ReadsDesignFromRegistryDefinition()
    {
        ItemRegistry registry = ScriptableObject.CreateInstance<ItemRegistry>();
        GameObject itemObject = new("Legacy Sword");
        ItemRegistry previousRegistry = SwapItemRegistryInstance(registry);
        try
        {
            ItemComponent item = AddItemComponent(itemObject, "ITM00056");
            SerializedObject itemObjectData = new(item);
            itemObjectData.Update();
            itemObjectData.FindProperty("_itemName").stringValue = "Prefab Sword";
            itemObjectData.FindProperty("_itemType").enumValueIndex = (int)ItemType.Material;
            itemObjectData.FindProperty("_value").intValue = 1;
            itemObjectData.ApplyModifiedPropertiesWithoutUndo();

            SetRegistryEntries(registry, new List<ItemRegistry.Entry>
            {
                new()
                {
                    ItemId = "ITM00056",
                    DisplayName = "Registry Sword",
                    ItemType = ItemType.Weapon,
                    Value = 99,
                    IsTradeable = true,
                    Damage = 13f,
                    EquipDomain = EquipDomain.WeaponTool,
                    AllowedEquipSlots = new List<EquipmentSlotType> { EquipmentSlotType.RightHand }
                }
            });

            Assert.That(item.ItemName, Is.EqualTo("Registry Sword"));
            Assert.That(item.Type, Is.EqualTo(ItemType.Weapon));
            Assert.That(item.Value, Is.EqualTo(99));
            Assert.That(item.Damage, Is.EqualTo(13f));
            Assert.That(item.AllowedEquipSlots, Contains.Item(EquipmentSlotType.RightHand));
        }
        finally
        {
            SwapItemRegistryInstance(previousRegistry);
            Object.DestroyImmediate(itemObject);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void ShopRuntimeSession_SeedsStockAndPricesFromRegistryDefinitions()
    {
        ItemRegistry registry = ScriptableObject.CreateInstance<ItemRegistry>();
        RpgShopDefinition shop = ScriptableObject.CreateInstance<RpgShopDefinition>();
        GameObject prefabObject = new("Registry Pearl Prefab");
        ShopRuntimeSession session = null;
        ItemRegistry previousRegistry = SwapItemRegistryInstance(registry);
        try
        {
            ItemComponent prefab = AddItemComponent(prefabObject, "ITM00090");
            SetRegistryEntries(registry, new List<ItemRegistry.Entry>
            {
                new()
                {
                    ItemId = "ITM00090",
                    DisplayName = "Registry Pearl",
                    ItemType = ItemType.Material,
                    Value = 10,
                    IsStackable = true,
                    MaxStackSize = 10,
                    IsTradeable = true,
                    Prefab = prefab
                }
            });

            ConfigureShop(shop, "SHP00090", 250, buyMultiplier: 2f, sellMultiplier: 0.5f, "ITM00090", quantity: 2, stockMultiplier: 3f);

            session = new ShopRuntimeSession(shop);

            Assert.That(session.Inventory.Slots.Count, Is.EqualTo(1));
            Assert.That(session.Inventory.Slots[0].Count, Is.EqualTo(2));
            Assert.That(session.Inventory.Slots[0].Item.ItemName, Is.EqualTo("Registry Pearl"));
            Assert.That(session.GetBuyPrice(session.Inventory.Slots[0].Item), Is.EqualTo(60));
            Assert.That(session.GetSellPrice(session.Inventory.Slots[0].Item), Is.EqualTo(5));
        }
        finally
        {
            session?.Dispose();
            SwapItemRegistryInstance(previousRegistry);
            Object.DestroyImmediate(prefabObject);
            Object.DestroyImmediate(shop);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void UnifiedNPCRows_FilterBySearchArchetypeAndFlags()
    {
        GameObject npcObject = new("Gate Guard");
        try
        {
            NPCSoul soul = npcObject.AddComponent<NPCSoul>();
            var row = new SolDatabaseNPCPage.NPCRow
            {
                Soul = soul,
                OwnerId = "OWN00010",
                CharacterName = "Gate Guard",
                Archetype = NPCArchetype.Guard,
                SearchText = "gate guard own00010 guard",
                HasTrader = false,
                IsHostile = false,
                MissingAIConfig = true,
                WarningCount = 2
            };

            Assert.That(SolDatabaseNPCPage.RowMatchesFilters(row, "gate", SolDatabaseNPCPage.NPCFilter.All), Is.True);
            Assert.That(SolDatabaseNPCPage.RowMatchesFilters(row, "own00010", SolDatabaseNPCPage.NPCFilter.Guard), Is.True);
            Assert.That(SolDatabaseNPCPage.RowMatchesFilters(row, "gate", SolDatabaseNPCPage.NPCFilter.MissingAIConfig), Is.True);
            Assert.That(SolDatabaseNPCPage.RowMatchesFilters(row, "gate", SolDatabaseNPCPage.NPCFilter.Trader), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(npcObject);
        }
    }

    [Test]
    public void UnifiedQuestRows_FilterBySearchFlowAndWarningFlags()
    {
        QuestDefinition quest = ScriptableObject.CreateInstance<QuestDefinition>();
        try
        {
            var row = new SolDatabaseQuestPage.QuestRow
            {
                Quest = quest,
                QuestId = "QST00010",
                Title = "The Locked Gate",
                GiverName = "Gate Guard",
                SearchText = "the locked gate qst00010 gate guard",
                AutoOffer = false,
                Repeatable = true,
                IsTimed = false,
                MissingGiver = true,
                HasOrphanedPrereqs = true,
                WarningCount = 3
            };

            Assert.That(SolDatabaseQuestPage.RowMatchesFilters(row, "locked", SolDatabaseQuestPage.QuestFilter.All), Is.True);
            Assert.That(SolDatabaseQuestPage.RowMatchesFilters(row, "qst00010", SolDatabaseQuestPage.QuestFilter.Repeatable), Is.True);
            Assert.That(SolDatabaseQuestPage.RowMatchesFilters(row, "gate", SolDatabaseQuestPage.QuestFilter.MissingGiver), Is.True);
            Assert.That(SolDatabaseQuestPage.RowMatchesFilters(row, "locked", SolDatabaseQuestPage.QuestFilter.Timed), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(quest);
        }
    }

    [Test]
    public void OverviewAggregation_CountsIssueSeverities()
    {
        var issues = new List<SolDatabaseIssue>
        {
            new(SolDatabaseIssueSeverity.Error, SolDatabaseTab.Items, "ITM1", "Bad Item", "Error"),
            new(SolDatabaseIssueSeverity.Warning, SolDatabaseTab.NPCs, "OWN1", "Warn NPC", "Warning"),
            new(SolDatabaseIssueSeverity.Warning, SolDatabaseTab.Quests, "QST1", "Warn Quest", "Warning"),
            new(SolDatabaseIssueSeverity.Info, SolDatabaseTab.Quests, "QST2", "Info Quest", "Info")
        };

        SolDatabaseOverviewPage.CountIssues(issues, out int errors, out int warnings, out int infos);

        Assert.That(errors, Is.EqualTo(1));
        Assert.That(warnings, Is.EqualTo(2));
        Assert.That(infos, Is.EqualTo(1));
    }

    [Test]
    public void Phase2DefinitionIds_UseCategoryPrefixes()
    {
        RpgSkillDefinition skillOne = ScriptableObject.CreateInstance<RpgSkillDefinition>();
        RpgSkillDefinition skillTwo = ScriptableObject.CreateInstance<RpgSkillDefinition>();
        try
        {
            SetDefinitionIdentity(skillOne, "SKL00001", "One");
            SetDefinitionIdentity(skillTwo, "SKL00003", "Three");

            var definitions = new List<RpgDefinition> { skillOne, skillTwo };

            Assert.That(RpgDefinitionEditorUtility.NextIdFromDefinitions(RpgDefinitionIds.SkillPrefix, definitions), Is.EqualTo("SKL00002"));
            Assert.That(RpgDefinitionEditorUtility.NextIdFromDefinitions(RpgDefinitionIds.FactionPrefix, definitions), Is.EqualTo("FAC00001"));
        }
        finally
        {
            Object.DestroyImmediate(skillOne);
            Object.DestroyImmediate(skillTwo);
        }
    }

    [Test]
    public void Phase2SkillValidation_CatchesMissingGoverningStat()
    {
        RpgSkillDefinition skill = ScriptableObject.CreateInstance<RpgSkillDefinition>();
        try
        {
            SetDefinitionIdentity(skill, "SKL00001", "Lockpicking");
            SerializedObject so = new(skill);
            so.Update();
            so.FindProperty("_governingStatId").stringValue = "STA99999";
            so.ApplyModifiedPropertiesWithoutUndo();

            List<RpgAuthoringWarning> warnings = RpgDefinitionValidator.Validate(skill, registry: null);

            Assert.That(warnings.Exists(w => w.Message.Contains("Governing stat")), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(skill);
        }
    }

    [Test]
    public void Phase2DefinitionRows_FilterBySearchAndWarnings()
    {
        RpgStatDefinition stat = ScriptableObject.CreateInstance<RpgStatDefinition>();
        try
        {
            SetDefinitionIdentity(stat, "STA00007", "Endurance");
            var row = new SolDatabaseRpgDefinitionPage<RpgStatDefinition>.DefinitionRow
            {
                Definition = stat,
                Id = "STA00007",
                DisplayName = "Endurance",
                Subtitle = "Primary",
                SearchText = "endurance sta00007 primary",
                WarningCount = 1
            };

            Assert.That(SolDatabaseRpgDefinitionPage<RpgStatDefinition>.RowMatchesFilters(row, "endur", SolDatabaseRpgDefinitionPage<RpgStatDefinition>.DefinitionFilter.All), Is.True);
            Assert.That(SolDatabaseRpgDefinitionPage<RpgStatDefinition>.RowMatchesFilters(row, "sta00007", SolDatabaseRpgDefinitionPage<RpgStatDefinition>.DefinitionFilter.HasWarnings), Is.True);
            Assert.That(SolDatabaseRpgDefinitionPage<RpgStatDefinition>.RowMatchesFilters(row, "missing", SolDatabaseRpgDefinitionPage<RpgStatDefinition>.DefinitionFilter.All), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(stat);
        }
    }

    private static void SetDefinitionIdentity(RpgDefinition definition, string id, string displayName)
    {
        SerializedObject so = new(definition);
        so.Update();
        so.FindProperty("_id").stringValue = id;
        so.FindProperty("_displayName").stringValue = displayName;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static ItemComponent AddItemComponent(GameObject itemObject, string itemId)
    {
        itemObject.AddComponent<BoxCollider>();
        itemObject.AddComponent<GrabbableComponent>();
        ItemComponent item = itemObject.AddComponent<ItemComponent>();
        SerializedObject serializedObject = new(item);
        serializedObject.Update();
        serializedObject.FindProperty("_itemId").stringValue = itemId;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private static void ConfigureShop(
        RpgShopDefinition shop,
        string shopId,
        int gold,
        float buyMultiplier,
        float sellMultiplier,
        string stockItemId,
        int quantity,
        float stockMultiplier)
    {
        SerializedObject so = new(shop);
        so.Update();
        so.FindProperty("_id").stringValue = shopId;
        so.FindProperty("_displayName").stringValue = "Test Shop";
        so.FindProperty("_gold").intValue = gold;
        so.FindProperty("_buyPriceMultiplier").floatValue = buyMultiplier;
        so.FindProperty("_sellPriceMultiplier").floatValue = sellMultiplier;
        so.FindProperty("_restockMode").enumValueIndex = (int)RpgShopRestockMode.Never;
        SerializedProperty stock = so.FindProperty("_stock");
        stock.arraySize = 1;
        SerializedProperty entry = stock.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("ItemId").stringValue = stockItemId;
        entry.FindPropertyRelative("MinQuantity").intValue = quantity;
        entry.FindPropertyRelative("MaxQuantity").intValue = quantity;
        entry.FindPropertyRelative("PriceMultiplier").floatValue = stockMultiplier;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRegistryEntries(ItemRegistry registry, List<ItemRegistry.Entry> entries)
    {
        typeof(ItemRegistry)
            .GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(registry, entries);
    }

    private static ItemRegistry SwapItemRegistryInstance(ItemRegistry registry)
    {
        FieldInfo field = typeof(ItemRegistry).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        ItemRegistry previous = field?.GetValue(null) as ItemRegistry;
        field?.SetValue(null, registry);
        return previous;
    }
}
