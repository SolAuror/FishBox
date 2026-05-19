using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Sol.Actions;
using Sol.AI;
using Sol.Grab;
using Sol.Rpg;
using UnityEngine;

namespace Sol.Editor.Tests
{
    public sealed class CapabilityTagMigrationTests
    {
        [Test]
        public void GameplayTagSetAddsAndRemovesRuntimePaths()
        {
            GameplayTagSet tags = new();

            tags.AddRuntimeTagPath("Job.Trader");
            Assert.That(tags.HasTagOrChild(GameplayCapabilityTags.JobTrader), Is.True);
            Assert.That(tags.HasTagOrChild("Job"), Is.True);

            tags.RemoveRuntimeTagPath("Job.Trader");
            Assert.That(tags.HasTagOrChild(GameplayCapabilityTags.JobTrader), Is.False);
        }

        [Test]
        public void ItemCapabilitiesFollowRegistryTags()
        {
            FieldInfo registryInstanceField = typeof(ItemRegistry).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(registryInstanceField, Is.Not.Null);
            ItemRegistry previousRegistry = (ItemRegistry)registryInstanceField.GetValue(null);

            GameObject itemObject = new("Tagged Capability Item");
            ItemRegistry registry = ScriptableObject.CreateInstance<ItemRegistry>();

            try
            {
                ItemComponent item = itemObject.AddComponent<ItemComponent>();
                SetField(item, "_itemId", "ITM99001");
                SetField(item, "_isConsumable", false);
                SetField(item, "_isTradeable", true);

                ItemRegistry.Entry entry = new()
                {
                    ItemId = "ITM99001",
                    DisplayName = "Tagged Capability Item",
                    ItemType = ItemType.Consumable,
                    IsTradeable = true,
                    CapabilityTagsMigrated = true,
                    Prefab = item,
                    Tags = GameplayStatModifierTests.Tags(GameplayCapabilityTags.ItemConsumable)
                };

                SetField(registry, "_entries", new List<ItemRegistry.Entry> { entry });
                registryInstanceField.SetValue(null, registry);

                Assert.That(item.IsConsumable, Is.True);
                Assert.That(item.GetAvailableActions(), Does.Contain(ItemActionType.Use));
                Assert.That(item.IsTradeable, Is.True);
            }
            finally
            {
                registryInstanceField.SetValue(null, previousRegistry);
                Object.DestroyImmediate(registry);
                Object.DestroyImmediate(itemObject);
            }
        }

        [Test]
        public void ItemRuntimeTagsDriveActionsAndSnapshots()
        {
            GameObject itemObject = new("Runtime Tagged Item");
            try
            {
                ItemComponent item = itemObject.AddComponent<ItemComponent>();
                SetField(item, "_itemType", ItemType.Material);
                SetField(item, "_isConsumable", false);
                SetField(item, "_isTradeable", false);

                item.SetRuntimeTag(GameplayCapabilityTags.ItemConsumable, true);
                item.SetRuntimeTag(GameplayCapabilityTags.ItemTradeable, true);
                item.SetRuntimeTag(GameplayCapabilityTags.StateStolen, true);

                Assert.That(item.IsConsumable, Is.True);
                Assert.That(item.GetAvailableActions(), Does.Contain(ItemActionType.Use));
                Assert.That(item.IsTradeable, Is.True);
                Assert.That(item.IsStolen, Is.True);
                Assert.That(item.CollectTagPaths(), Does.Contain(GameplayCapabilityTags.StateStolen));
            }
            finally
            {
                Object.DestroyImmediate(itemObject);
            }
        }

        [Test]
        public void InventorySecurityStateIsTagBacked()
        {
            GameObject sourceObject = new("Tagged Container Source");
            GameObject targetObject = new("Tagged Container Target");

            try
            {
                Inventory source = sourceObject.AddComponent<Inventory>();
                SetField(source, "_containerType", InventoryContainerType.Container);
                source.Lock(lockLevel: 2);

                Assert.That(source.Tags.HasTagOrChild(GameplayCapabilityTags.StateLocked), Is.True);
                Assert.That(source.IsLocked, Is.True);

                Inventory target = targetObject.AddComponent<Inventory>();
                SetField(target, "_containerType", InventoryContainerType.Container);
                target.ApplySavedTagPaths(source.CollectTagPaths());

                Assert.That(target.IsLocked, Is.True);
                Assert.That(target.IsLockpickable, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(sourceObject);
                Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void InteractionPointRuntimeTagsAreSnapshotted()
        {
            GameObject pointObject = new("Tagged Interaction Point");
            GameObject restoredObject = new("Restored Tagged Interaction Point");
            try
            {
                InteractionPoint point = pointObject.AddComponent<InteractionPoint>();
                point.SetRuntimeTag(GameplayCapabilityTags.InteractionHarvestable, true);
                point.SetRuntimeTag(GameplayCapabilityTags.StateDepleted, true);

                List<string> snapshot = point.CollectTagPaths();
                Assert.That(snapshot, Does.Contain(GameplayCapabilityTags.InteractionHarvestable));
                Assert.That(snapshot, Does.Contain(GameplayCapabilityTags.StateDepleted));

                InteractionPoint restored = restoredObject.AddComponent<InteractionPoint>();
                restored.ApplySavedTagPaths(snapshot);
                Assert.That(restored.Tags.HasTagOrChild(GameplayCapabilityTags.StateDepleted), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(pointObject);
                Object.DestroyImmediate(restoredObject);
            }
        }

        [Test]
        public void NpcRoleCapabilitiesFollowTags()
        {
            GameObject npcObject = new("Tagged Role NPC");
            try
            {
                NPCSoul soul = npcObject.AddComponent<NPCSoul>();
                AI_NPC aiNpc = npcObject.AddComponent<AI_NPC>();
                SetField(aiNpc, "soul", soul);

                Assert.That(aiNpc.IsTrader, Is.False);
                Assert.That(aiNpc.IsQuestGiver, Is.False);

                soul.SetTagState(GameplayCapabilityTags.JobTrader, true);
                soul.SetTagState(GameplayCapabilityTags.JobQuestGiver, true);

                Assert.That(aiNpc.IsTrader, Is.True);
                Assert.That(aiNpc.IsQuestGiver, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(npcObject);
            }
        }

        [Test]
        public void TaggedInteractionPointBuildsOpenShopAction()
        {
            FieldInfo registryInstanceField = typeof(RpgDefinitionRegistry).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(registryInstanceField, Is.Not.Null);
            RpgDefinitionRegistry previousRegistry = (RpgDefinitionRegistry)registryInstanceField.GetValue(null);

            GameObject pointObject = new("Tagged Shop Point");
            GameObject playerObject = new("Shop Player");
            RpgDefinitionRegistry registry = ScriptableObject.CreateInstance<RpgDefinitionRegistry>();
            RpgShopDefinition shop = ScriptableObject.CreateInstance<RpgShopDefinition>();

            try
            {
                SetFieldInHierarchy(shop, "_id", "SHP99001");
                SetFieldInHierarchy(registry, "_shops", new List<RpgShopDefinition> { shop });
                registryInstanceField.SetValue(null, registry);

                InteractionPoint point = pointObject.AddComponent<InteractionPoint>();
                SetField(point, "_tags", GameplayStatModifierTests.Tags(GameplayCapabilityTags.JobTrader));
                SetField(point, "_shopId", "SHP99001");

                playerObject.AddComponent<Inventory>();
                Interactor interactor = new(playerObject, isPlayer: true);

                Assert.That(point.CanInteract(interactor), Is.True);
                Assert.That(point.GetInteraction(interactor), Is.TypeOf<OpenShopAction>());
            }
            finally
            {
                ShopRuntimeStore.Clear();
                registryInstanceField.SetValue(null, previousRegistry);
                Object.DestroyImmediate(shop);
                Object.DestroyImmediate(registry);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(pointObject);
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName} on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void SetFieldInHierarchy(object target, string fieldName, object value)
        {
            System.Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail($"Missing field {fieldName} on {target.GetType().Name}.");
        }
    }
}
