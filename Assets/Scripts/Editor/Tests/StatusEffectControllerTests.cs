using System.Collections.Generic;
using NUnit.Framework;
using Sol.Rpg;
using UnityEngine;

namespace Sol.Editor.Tests
{
    public sealed class StatusEffectControllerTests
    {
        [Test]
        public void ApplyHonorsNegateNullifyAndGroupRules()
        {
            GameObject target = new("Status Rules Test");
            StatusEffectController controller = target.AddComponent<StatusEffectController>();
            try
            {
                StatusEffectDefinition shield = Status("Shield");
                StatusEffectDefinition poison = Status("Poison", negatedBy: new[] { shield });
                StatusEffectDefinition cure = Status("Cure", nullifies: new[] { shield });
                StatusEffectDefinition stanceA = Status("Stance A", group: StatusEffectGroup.Stance);
                StatusEffectDefinition stanceB = Status("Stance B", group: StatusEffectGroup.Stance);

                Assert.That(controller.Apply(shield), Is.True);
                Assert.That(controller.Apply(poison), Is.False);
                Assert.That(Contains(controller, shield), Is.True);
                Assert.That(Contains(controller, poison), Is.False);

                Assert.That(controller.Apply(cure), Is.True);
                Assert.That(Contains(controller, shield), Is.False);
                Assert.That(Contains(controller, cure), Is.True);

                Assert.That(controller.Apply(stanceA), Is.True);
                Assert.That(controller.Apply(stanceB), Is.True);
                Assert.That(Contains(controller, stanceA), Is.False);
                Assert.That(Contains(controller, stanceB), Is.True);
                Assert.That(Contains(controller, cure), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void PhasesContributeTagsAndModifiersAlongsideTopLevelStatusData()
        {
            GameObject target = new("Status Phase Test");
            StatusEffectController controller = target.AddComponent<StatusEffectController>();
            try
            {
                StatusEffectDefinition bleeding = Status(
                    "Bleeding",
                    tags: GameplayStatModifierTests.Tags("Status.Bleeding"),
                    modifiers: GameplayStatModifierTests.ModifierSet(
                        GameplayStatModifierTests.Modifier(GameplayStatModifierOperation.FlatAdd, 1f)),
                    phases: new[]
                    {
                        Phase(1, "Mild", "Status.Bleeding.Mild", 2f),
                        Phase(3, "Heavy", "Status.Bleeding.Heavy", 5f)
                    },
                    stackRule: StatusEffectStackRule.AddStackRefreshDuration,
                    maxStacks: 5);

                Assert.That(controller.Apply(bleeding), Is.True);
                Assert.That(target.GetGameplayTags().HasTagOrChild("Status.Bleeding"), Is.True);
                Assert.That(target.GetGameplayTags().HasTagOrChild("Status.Bleeding.Mild"), Is.True);
                Assert.That(target.GetGameplayTags().HasTagOrChild("Status.Bleeding.Heavy"), Is.False);
                Assert.That(GameplayStatSystem.Evaluate(GameplayStatModifierTests.StatId, 10f, target), Is.EqualTo(13f).Within(0.001f));

                controller.Apply(bleeding);
                controller.Apply(bleeding);

                Assert.That(target.GetGameplayTags().HasTagOrChild("Status.Bleeding"), Is.True);
                Assert.That(target.GetGameplayTags().HasTagOrChild("Status.Bleeding.Mild"), Is.False);
                Assert.That(target.GetGameplayTags().HasTagOrChild("Status.Bleeding.Heavy"), Is.True);
                Assert.That(GameplayStatSystem.Evaluate(GameplayStatModifierTests.StatId, 10f, target), Is.EqualTo(16f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        private static bool Contains(StatusEffectController controller, StatusEffectDefinition definition)
        {
            IReadOnlyList<ActiveStatusEffect> active = controller.ActiveEffects;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i]?.Definition == definition)
                    return true;
            }

            return false;
        }

        private static StatusEffectDefinition Status(
            string name,
            GameplayTagSet tags = null,
            GameplayStatModifierSet modifiers = null,
            StatusEffectDefinition[] negatedBy = null,
            StatusEffectDefinition[] nullifies = null,
            StatusEffectGroup group = StatusEffectGroup.None,
            StatusEffectPhase[] phases = null,
            StatusEffectStackRule stackRule = StatusEffectStackRule.RefreshDuration,
            int maxStacks = 1)
        {
            StatusEffectDefinition definition = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            definition.name = name;
            GameplayStatModifierTests.SetField(definition, "_tags", tags ?? new GameplayTagSet());
            GameplayStatModifierTests.SetField(definition, "_statModifiers", modifiers ?? new GameplayStatModifierSet());
            GameplayStatModifierTests.SetField(definition, "_negatedBy", new List<StatusEffectDefinition>(negatedBy ?? new StatusEffectDefinition[0]));
            GameplayStatModifierTests.SetField(definition, "_nullifies", new List<StatusEffectDefinition>(nullifies ?? new StatusEffectDefinition[0]));
            GameplayStatModifierTests.SetField(definition, "_group", group);
            GameplayStatModifierTests.SetField(definition, "_phases", new List<StatusEffectPhase>(phases ?? new StatusEffectPhase[0]));
            GameplayStatModifierTests.SetField(definition, "_stackRule", stackRule);
            GameplayStatModifierTests.SetField(definition, "_maxStacks", maxStacks);
            return definition;
        }

        private static StatusEffectPhase Phase(int minStacks, string label, string tagPath, float modifierValue)
        {
            StatusEffectPhase phase = new();
            GameplayStatModifierTests.SetField(phase, "_minStacks", minStacks);
            GameplayStatModifierTests.SetField(phase, "_label", label);
            GameplayStatModifierTests.SetField(phase, "_grantedTags", GameplayStatModifierTests.Tags(tagPath));
            GameplayStatModifierTests.SetField(phase, "_statModifiers", GameplayStatModifierTests.ModifierSet(
                GameplayStatModifierTests.Modifier(GameplayStatModifierOperation.FlatAdd, modifierValue)));
            return phase;
        }
    }
}
