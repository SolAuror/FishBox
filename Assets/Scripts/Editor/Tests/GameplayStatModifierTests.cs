using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Sol.Combat;
using Sol.Editor;
using Sol.Grab;
using Sol.Rpg;
using UnityEngine;

namespace Sol.Editor.Tests
{
    public sealed class GameplayStatModifierTests
    {
        internal const string StatId = "Test.Stat";

        [Test]
        public void EvaluateCombinesProvidersOperationsAndTagConditions()
        {
            GameplayTagSet targetTags = Tags("Trait.Fast");
            List<IGameplayStatModifierProvider> providers = new()
            {
                new TestProvider(
                    Modifier(GameplayStatModifierOperation.Override, 20f),
                    Modifier(GameplayStatModifierOperation.FlatAdd, 5f),
                    Modifier(GameplayStatModifierOperation.PercentAdd, 0.5f),
                    Modifier(GameplayStatModifierOperation.PercentMultiply, 0.2f),
                    Modifier(GameplayStatModifierOperation.AbsoluteMultiply, 0.5f),
                    Modifier(GameplayStatModifierOperation.Minimum, 10f),
                    Modifier(GameplayStatModifierOperation.Maximum, 100f),
                    Modifier(GameplayStatModifierOperation.FlatAdd, 3f, requireTags: Tags("Trait.Fast")),
                    Modifier(GameplayStatModifierOperation.FlatAdd, 100f, forbidTags: Tags("Trait.Fast")),
                    Modifier(GameplayStatModifierOperation.FlatAdd, 2f, legacyRequiredTag: "Trait.Fast")),
                new TestProvider(Modifier(GameplayStatModifierOperation.FlatAdd, 1f))
            };

            float result = GameplayStatSystem.Evaluate(StatId, 10f, providers, targetTags);

            Assert.That(result, Is.EqualTo(27.9f).Within(0.001f));
        }

        [Test]
        public void EvaluateIgnoresMissingRequiredTagsAndPresentForbiddenTags()
        {
            GameplayTagSet targetTags = Tags("Status.Burning");
            List<IGameplayStatModifierProvider> providers = new()
            {
                new TestProvider(
                    Modifier(GameplayStatModifierOperation.FlatAdd, 10f, requireTags: Tags("Trait.Fast")),
                    Modifier(GameplayStatModifierOperation.FlatAdd, 10f, forbidTags: Tags("Status.Burning")))
            };

            float result = GameplayStatSystem.Evaluate(StatId, 5f, providers, targetTags);

            Assert.That(result, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void StatDropdownIncludesBuiltInRuntimeStatsAndRegistryStats()
        {
            List<ReferenceDropdown.Entry> entries = ReferenceDropdown.GetEntries(ReferenceDropdown.ReferenceKind.Stat);

            Assert.That(ContainsEntry(entries, GameplayStatIds.IncomingDamage), Is.True);

            RpgDefinitionRegistry registry = RpgDefinitionRegistry.Get();
            if (registry?.Stats != null && registry.Stats.Count > 0)
                Assert.That(ContainsEntry(entries, registry.Stats[0].Id), Is.True);
        }

        [Test]
        public void EvaluateUsesEquipmentModifierProviderWithoutDoubleCountingMultiSlotItem()
        {
            FieldInfo registryInstanceField = typeof(ItemRegistry).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(registryInstanceField, Is.Not.Null);
            ItemRegistry previousRegistry = (ItemRegistry)registryInstanceField.GetValue(null);

            GameObject actor = new("Equipment Modifier Actor");
            GameObject itemObject = new("Two Slot Modifier Item");
            ItemRegistry registry = ScriptableObject.CreateInstance<ItemRegistry>();

            try
            {
                Equipment equipment = actor.AddComponent<Equipment>();
                ItemComponent item = itemObject.AddComponent<ItemComponent>();
                SetField(item, "_itemId", "ITM99999");

                ItemRegistry.Entry entry = new()
                {
                    ItemId = "ITM99999",
                    DisplayName = "Two Slot Modifier Item",
                    Prefab = item,
                    StatModifiers = ModifierSet(Modifier(GameplayStatModifierOperation.FlatAdd, 5f))
                };
                SetField(registry, "_entries", new List<ItemRegistry.Entry> { entry });
                registryInstanceField.SetValue(null, registry);

                FieldInfo equippedField = typeof(Equipment).GetField("_equipped", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(equippedField, Is.Not.Null);
                Dictionary<EquipmentSlotType, ItemComponent> equipped =
                    (Dictionary<EquipmentSlotType, ItemComponent>)equippedField.GetValue(equipment);
                equipped[EquipmentSlotType.RightHand] = item;
                equipped[EquipmentSlotType.LeftHand] = item;

                float result = GameplayStatSystem.Evaluate(StatId, 10f, actor);

                Assert.That(actor.GetComponent<GameplayStatAggregator>(), Is.Not.Null);
                Assert.That(actor.GetComponent<EquipmentModifierProvider>(), Is.Not.Null);
                Assert.That(result, Is.EqualTo(15f).Within(0.001f));
            }
            finally
            {
                registryInstanceField.SetValue(null, previousRegistry);
                Object.DestroyImmediate(registry);
                Object.DestroyImmediate(itemObject);
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void EvaluateRefreshesAggregatorWhenEquipmentArrivesAfterFirstRead()
        {
            FieldInfo registryInstanceField = typeof(ItemRegistry).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(registryInstanceField, Is.Not.Null);
            ItemRegistry previousRegistry = (ItemRegistry)registryInstanceField.GetValue(null);

            GameObject actor = new("Late Equipment Modifier Actor");
            GameObject itemObject = new("Late Modifier Item");
            ItemRegistry registry = ScriptableObject.CreateInstance<ItemRegistry>();

            try
            {
                Assert.That(GameplayStatSystem.Evaluate(StatId, 10f, actor), Is.EqualTo(10f).Within(0.001f));
                GameplayStatAggregator aggregator = actor.GetComponent<GameplayStatAggregator>();
                Assert.That(aggregator, Is.Not.Null);

                Equipment equipment = actor.AddComponent<Equipment>();
                ItemComponent item = itemObject.AddComponent<ItemComponent>();
                SetField(item, "_itemId", "ITM99998");

                ItemRegistry.Entry entry = new()
                {
                    ItemId = "ITM99998",
                    DisplayName = "Late Modifier Item",
                    Prefab = item,
                    StatModifiers = ModifierSet(Modifier(GameplayStatModifierOperation.FlatAdd, 7f))
                };
                SetField(registry, "_entries", new List<ItemRegistry.Entry> { entry });
                registryInstanceField.SetValue(null, registry);

                FieldInfo equippedField = typeof(Equipment).GetField("_equipped", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(equippedField, Is.Not.Null);
                Dictionary<EquipmentSlotType, ItemComponent> equipped =
                    (Dictionary<EquipmentSlotType, ItemComponent>)equippedField.GetValue(equipment);
                equipped[EquipmentSlotType.RightHand] = item;

                float result = GameplayStatSystem.Evaluate(StatId, 10f, actor);

                Assert.That(actor.GetComponent<EquipmentModifierProvider>(), Is.Not.Null);
                Assert.That(result, Is.EqualTo(17f).Within(0.001f));
            }
            finally
            {
                registryInstanceField.SetValue(null, previousRegistry);
                Object.DestroyImmediate(registry);
                Object.DestroyImmediate(itemObject);
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void CreateMeleeHitAppliesOutgoingDamageModifiersAfterAttackProfile()
        {
            GameObject attackerObject = new("Outgoing Damage Attacker");
            GameObject targetObject = new("Outgoing Damage Target");

            try
            {
                Combatant attacker = attackerObject.AddComponent<Combatant>();
                Combatant target = targetObject.AddComponent<Combatant>();
                ModifierProvider provider = attackerObject.AddComponent<ModifierProvider>();
                provider.StatModifiers = ModifierSet(Modifier(
                    GameplayStatModifierOperation.FlatAdd,
                    2f,
                    statId: GameplayStatIds.OutgoingDamage));

                CombatHit hit = attacker.CreateMeleeHit(
                    target,
                    10f,
                    Vector3.forward,
                    CombatAttackProfile.Power);

                Assert.That(hit.BaseDamage, Is.EqualTo(19.5f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(attackerObject);
            }
        }

        [Test]
        public void CombatantUsesGenericActorVitalsContract()
        {
            GameObject actor = new("Generic Vitals Actor");
            try
            {
                TestActorVitals vitals = actor.AddComponent<TestActorVitals>();
                Combatant combatant = actor.AddComponent<Combatant>();

                Assert.That(combatant.Health, Is.EqualTo(40f).Within(0.001f));
                Assert.That(combatant.MaxHealth, Is.EqualTo(50f).Within(0.001f));
                Assert.That(combatant.TrySpendStamina(10f), Is.True);
                Assert.That(vitals.Stamina, Is.EqualTo(15f).Within(0.001f));

                combatant.TakeDamage(12f);

                Assert.That(vitals.Health, Is.EqualTo(28f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        internal static GameplayStatModifier Modifier(
            GameplayStatModifierOperation operation,
            float value,
            GameplayTagSet requireTags = null,
            GameplayTagSet forbidTags = null,
            string legacyRequiredTag = "",
            string statId = StatId)
        {
            GameplayStatModifier modifier = new();
            SetField(modifier, "_statId", statId);
            SetField(modifier, "_operation", operation);
            SetField(modifier, "_value", value);
            SetField(modifier, "_requiredTargetTag", legacyRequiredTag);
            if (requireTags != null)
                SetField(modifier, "_requireTags", requireTags);
            if (forbidTags != null)
                SetField(modifier, "_forbidTags", forbidTags);
            return modifier;
        }

        internal static GameplayStatModifierSet ModifierSet(params GameplayStatModifier[] modifiers)
        {
            GameplayStatModifierSet set = new();
            SetField(set, "_modifiers", new List<GameplayStatModifier>(modifiers));
            return set;
        }

        private static bool ContainsEntry(IReadOnlyList<ReferenceDropdown.Entry> entries, string id)
        {
            if (entries == null || string.IsNullOrWhiteSpace(id))
                return false;

            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].Id, id, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        internal static GameplayTagSet Tags(params string[] paths)
        {
            GameplayTagSet set = new();
            for (int i = 0; i < paths.Length; i++)
                set.AddRuntimeTagPath(paths[i]);
            return set;
        }

        internal static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName} on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private sealed class TestProvider : IGameplayStatModifierProvider
        {
            public TestProvider(params GameplayStatModifier[] modifiers)
            {
                StatModifiers = ModifierSet(modifiers);
            }

            public GameplayStatModifierSet StatModifiers { get; }
        }

        private sealed class ModifierProvider : MonoBehaviour, IGameplayStatModifierProvider
        {
            public GameplayStatModifierSet StatModifiers { get; set; }
        }

        private sealed class TestActorVitals : MonoBehaviour, IActorVitals
        {
            public float Health { get; set; } = 40f;
            public float MaxHealth { get; set; } = 50f;
            public float Stamina { get; set; } = 25f;
            public float MaxStamina { get; set; } = 30f;
            public bool IsAlive => Health > 0f;

            public void TakeDamage(float amount)
            {
                Health = Mathf.Max(0f, Health - Mathf.Abs(amount));
            }

            public void Heal(float amount)
            {
                Health = Mathf.Min(MaxHealth, Health + Mathf.Abs(amount));
            }

            public bool SpendStamina(float amount)
            {
                float cost = Mathf.Abs(amount);
                if (Stamina < cost)
                    return false;

                Stamina -= cost;
                return true;
            }

            public void RestoreStamina(float amount)
            {
                Stamina = Mathf.Min(MaxStamina, Stamina + Mathf.Abs(amount));
            }
        }
    }
}
