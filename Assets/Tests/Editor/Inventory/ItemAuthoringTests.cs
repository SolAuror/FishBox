using NUnit.Framework;
using System.Collections.Generic;
using Sol;
using Sol.AI;
using Sol.Editor;
using Sol.Grab;
using Sol.Player;
using UnityEditor;
using UnityEngine;

public sealed class ItemAuthoringTests
{
    [Test]
    public void ItemIdNormalization_UsesItemPrefix()
    {
        Assert.That(EntityCodeUtility.NormalizeOrEmpty("42", EntityCodeUtility.ItemPrefix), Is.EqualTo("ITM00042"));
        Assert.That(EntityCodeUtility.NormalizeOrEmpty("itm7", EntityCodeUtility.ItemPrefix), Is.EqualTo("ITM00007"));
    }

    [Test]
    public void NextItemIdFromEntries_UsesRegistryEntriesOnly()
    {
        var entries = new List<ItemRegistry.Entry>
        {
            new() { ItemId = "ITM00001" },
            new() { ItemId = "ITM00003" },
            new() { ItemId = "ITM00002" }
        };

        Assert.That(ItemAuthoringEditorUtility.NextItemIdFromEntries(entries), Is.EqualTo("ITM00004"));
    }

    [Test]
    public void CachedItemDatabaseRow_FiltersBySearchTypeAndFlags()
    {
        GameObject itemObject = new("Sunberry Tea");
        try
        {
            ItemComponent item = AddItemComponent(itemObject);
            var row = new ItemDatabaseWindow.ItemDatabaseRow
            {
                Item = item,
                ItemId = "ITM00077",
                ItemName = "Sunberry Tea",
                TypeName = "Food",
                ItemType = ItemType.Food,
                SearchText = "sunberry tea itm00077 food assets/itemprefabs/sunberry tea.prefab",
                IsConsumable = true,
                IsEquipable = false,
                IsStackable = true,
                MissingIcon = true,
                WarningCount = 2
            };

            Assert.That(ItemDatabaseWindow.RowMatchesFilters(row, "sunberry", ItemDatabaseWindow.ItemFilter.All, false, ItemType.Material), Is.True);
            Assert.That(ItemDatabaseWindow.RowMatchesFilters(row, "itm00077", ItemDatabaseWindow.ItemFilter.Consumable, true, ItemType.Food), Is.True);
            Assert.That(ItemDatabaseWindow.RowMatchesFilters(row, "sunberry", ItemDatabaseWindow.ItemFilter.Equipable, false, ItemType.Material), Is.False);
            Assert.That(ItemDatabaseWindow.RowMatchesFilters(row, "missing", ItemDatabaseWindow.ItemFilter.All, false, ItemType.Material), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(itemObject);
        }
    }

    [Test]
    public void ItemActionSystem_CannotUseItemWhenUseOccasionIsNever()
    {
        GameObject owner = new("Owner");
        GameObject itemObject = new("Forbidden Potion");
        try
        {
            Inventory inventory = owner.AddComponent<Inventory>();
            ItemComponent item = AddItemComponent(itemObject);
            SerializedObject serializedObject = new(item);
            serializedObject.Update();
            serializedObject.FindProperty("_itemType").enumValueIndex = (int)ItemType.Consumable;
            serializedObject.FindProperty("_isConsumable").boolValue = true;
            serializedObject.FindProperty("_useOccasion").enumValueIndex = (int)ItemUseOccasion.Never;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            bool canUse = ItemActionSystem.CanExecute(
                ItemActionType.Use,
                new InventorySlot(item),
                inventory,
                new Interactor(owner, isPlayer: false));

            Assert.That(canUse, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(itemObject);
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void FillMissingTemplateDefaults_PreservesTunedValues()
    {
        GameObject itemObject = new("Tuned Snack");
        try
        {
            ItemComponent item = AddItemComponent(itemObject);
            SerializedObject serializedObject = new(item);
            serializedObject.Update();
            serializedObject.FindProperty("_itemName").stringValue = "Tuned Snack";
            serializedObject.FindProperty("_itemType").enumValueIndex = (int)ItemType.Food;
            serializedObject.FindProperty("_value").intValue = 42;
            serializedObject.FindProperty("_isStackable").boolValue = true;
            serializedObject.FindProperty("_isConsumable").boolValue = true;
            serializedObject.FindProperty("_isTradeable").boolValue = false;
            serializedObject.FindProperty("_maxStackSize").intValue = 7;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            ItemAuthoringEditorUtility.FillMissingTemplateDefaults(item, ItemAuthoringTemplate.Consumable, item.ItemName);

            Assert.That(item.Type, Is.EqualTo(ItemType.Food));
            Assert.That(item.Value, Is.EqualTo(42));
            Assert.That(item.IsStackable, Is.True);
            Assert.That(item.IsConsumable, Is.True);
            Assert.That(item.IsTradeable, Is.False);
            Assert.That(item.MaxStackSize, Is.EqualTo(7));
            Assert.That(item.UseEffects.Count, Is.EqualTo(1));
            Assert.That(item.AuthoringTemplate, Is.EqualTo(ItemAuthoringTemplate.Consumable));
        }
        finally
        {
            Object.DestroyImmediate(itemObject);
        }
    }

    [Test]
    public void InventoryUse_AppliesPlayerHealthAndStaminaEffectsBeforeConsuming()
    {
        GameObject owner = new("Player");
        GameObject itemObject = new("Potion");
        try
        {
            PlayerSoul soul = owner.AddComponent<PlayerSoul>();
            soul.MaxHealth = 100f;
            soul.Health = 25f;
            soul.MaxStamina = 100f;
            soul.Stamina = 10f;
            Inventory inventory = owner.AddComponent<Inventory>();
            ItemComponent item = AddItemComponent(itemObject);
            ConfigureConsumable(item, ItemUseEffectType.HealHealthFlat, 15f, ItemUseEffectType.RestoreStaminaPercent, 25f);

            Assert.That(inventory.Add(item), Is.True);
            Assert.That(inventory.Use(inventory.Slots[0], new Interactor(owner, isPlayer: true)), Is.True);

            Assert.That(soul.Health, Is.EqualTo(40f).Within(0.001f));
            Assert.That(soul.Stamina, Is.EqualTo(35f).Within(0.001f));
            Assert.That(inventory.Slots.Count, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(itemObject);
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void InventoryUse_AppliesNpcEffectsBeforeConsuming()
    {
        GameObject owner = new("NPC");
        GameObject itemObject = new("Snack");
        try
        {
            NPCSoul soul = owner.AddComponent<NPCSoul>();
            soul.MaxHealth = 80f;
            soul.Health = 20f;
            Inventory inventory = owner.AddComponent<Inventory>();
            ItemComponent item = AddItemComponent(itemObject);
            ConfigureConsumable(item, ItemUseEffectType.HealHealthPercent, 50f);

            Assert.That(inventory.Add(item), Is.True);
            Assert.That(inventory.Use(inventory.Slots[0], new Interactor(owner, isPlayer: false)), Is.True);

            Assert.That(soul.Health, Is.EqualTo(60f).Within(0.001f));
            Assert.That(inventory.Slots.Count, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(itemObject);
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void Validator_CatchesCommonAuthoringProblems()
    {
        GameObject itemObject = new("Bad Bait");
        try
        {
            ItemComponent item = AddItemComponent(itemObject);
            SerializedObject serializedObject = new(item);
            serializedObject.Update();
            serializedObject.FindProperty("_itemId").stringValue = string.Empty;
            serializedObject.FindProperty("_itemName").stringValue = "Item";
            serializedObject.FindProperty("_itemType").enumValueIndex = (int)ItemType.Consumable;
            serializedObject.FindProperty("_isStackable").boolValue = true;
            serializedObject.FindProperty("_maxStackSize").intValue = 1;
            serializedObject.FindProperty("_authoringTemplate").enumValueIndex = (int)ItemAuthoringTemplate.FishingBait;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            var warnings = ItemAuthoringValidator.Validate(item);

            Assert.That(warnings.Exists(w => w.Message.Contains("placeholder")), Is.True);
            Assert.That(warnings.Exists(w => w.Message.Contains("max stack size")), Is.True);
            Assert.That(warnings.Exists(w => w.Message.Contains("no use effects")), Is.True);
            Assert.That(warnings.Exists(w => w.Message.Contains("FishingBaitItem")), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(itemObject);
        }
    }

    private static ItemComponent AddItemComponent(GameObject itemObject)
    {
        itemObject.AddComponent<BoxCollider>();
        itemObject.AddComponent<GrabbableComponent>();
        return itemObject.AddComponent<ItemComponent>();
    }

    private static void ConfigureConsumable(ItemComponent item, ItemUseEffectType effectType, float amount)
    {
        ConfigureConsumable(item, effectType, amount, effectType, 0f, includeSecond: false);
    }

    private static void ConfigureConsumable(
        ItemComponent item,
        ItemUseEffectType firstType,
        float firstAmount,
        ItemUseEffectType secondType,
        float secondAmount,
        bool includeSecond = true)
    {
        SerializedObject serializedObject = new(item);
        serializedObject.Update();
        serializedObject.FindProperty("_itemType").enumValueIndex = (int)ItemType.Consumable;
        serializedObject.FindProperty("_isConsumable").boolValue = true;
        SerializedProperty effects = serializedObject.FindProperty("_useEffects");
        effects.arraySize = includeSecond ? 2 : 1;
        SetEffect(effects.GetArrayElementAtIndex(0), firstType, firstAmount);
        if (includeSecond)
            SetEffect(effects.GetArrayElementAtIndex(1), secondType, secondAmount);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetEffect(SerializedProperty effect, ItemUseEffectType effectType, float amount)
    {
        effect.FindPropertyRelative("_effectType").enumValueIndex = (int)effectType;
        effect.FindPropertyRelative("_amount").floatValue = amount;
    }
}
