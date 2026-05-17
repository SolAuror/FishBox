using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
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

        internal static GameplayStatModifier Modifier(
            GameplayStatModifierOperation operation,
            float value,
            GameplayTagSet requireTags = null,
            GameplayTagSet forbidTags = null,
            string legacyRequiredTag = "")
        {
            GameplayStatModifier modifier = new();
            SetField(modifier, "_statId", StatId);
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
    }
}
