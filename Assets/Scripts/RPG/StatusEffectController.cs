using System.Collections.Generic;
using Sol.Combat;
using UnityEngine;

namespace Sol.Rpg
{
    [AddComponentMenu("Sol/RPG/Status Effect Controller")]
    [DisallowMultipleComponent]
    public sealed class StatusEffectController : MonoBehaviour, IGameplayStatModifierProvider
    {
        [SerializeField] private List<ActiveStatusEffect> _activeEffects = new();

        private readonly GameplayStatModifierSet _runtimeModifiers = new();

        public IReadOnlyList<ActiveStatusEffect> ActiveEffects => _activeEffects;
        public GameplayStatModifierSet StatModifiers => BuildRuntimeModifiers();

        public bool Apply(StatusEffectDefinition definition, GameObject source = null)
        {
            if (definition == null)
                return false;

            ActiveStatusEffect existing = Find(definition);
            if (existing != null)
            {
                if (definition.StackRule == StatusEffectStackRule.IgnoreIfActive)
                    return false;

                if (definition.StackRule == StatusEffectStackRule.AddStackRefreshDuration)
                    existing.Stacks = Mathf.Min(definition.MaxStacks, Mathf.Max(1, existing.Stacks + 1));

                existing.Remaining = definition.Duration;
                existing.TickCountdown = definition.TickInterval;
                GrantTags(definition);
                GameplayEvents.RaiseStatusApplied(gameObject, definition);
                return true;
            }

            ActiveStatusEffect instance = new()
            {
                Definition = definition,
                Remaining = definition.Duration,
                TickCountdown = definition.TickInterval,
                Stacks = 1
            };
            _activeEffects.Add(instance);
            GrantTags(definition);
            GameplayEvents.RaiseStatusApplied(gameObject, definition);
            return true;
        }

        public bool Remove(StatusEffectDefinition definition)
        {
            ActiveStatusEffect existing = Find(definition);
            if (existing == null)
                return false;

            _activeEffects.Remove(existing);
            RebuildGrantedTags();
            GameplayEvents.RaiseStatusRemoved(gameObject, definition);
            return true;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                ActiveStatusEffect effect = _activeEffects[i];
                StatusEffectDefinition definition = effect?.Definition;
                if (definition == null)
                {
                    _activeEffects.RemoveAt(i);
                    RebuildGrantedTags();
                    continue;
                }

                if (definition.PeriodicDamage > 0f && definition.TickInterval > 0f)
                {
                    effect.TickCountdown -= deltaTime;
                    while (effect.TickCountdown <= 0f)
                    {
                        effect.TickCountdown += definition.TickInterval;
                        ApplyPeriodicDamage(definition, Mathf.Max(1, effect.Stacks));
                    }
                }

                if (definition.Duration > 0f)
                {
                    effect.Remaining -= deltaTime;
                    if (effect.Remaining <= 0f)
                    {
                        _activeEffects.RemoveAt(i);
                        RebuildGrantedTags();
                        GameplayEvents.RaiseStatusRemoved(gameObject, definition);
                    }
                }
            }
        }

        private GameplayStatModifierSet BuildRuntimeModifiers()
        {
            List<GameplayStatModifier> modifiers = GameplayStatModifierSet.GetMutableModifiers(_runtimeModifiers);
            modifiers.Clear();

            for (int i = 0; i < _activeEffects.Count; i++)
            {
                ActiveStatusEffect effect = _activeEffects[i];
                IReadOnlyList<GameplayStatModifier> source = effect?.Definition?.StatModifiers?.Modifiers;
                if (source == null)
                    continue;

                for (int j = 0; j < source.Count; j++)
                {
                    if (source[j] != null)
                        modifiers.Add(source[j]);
                }
            }

            return _runtimeModifiers;
        }

        private void ApplyPeriodicDamage(StatusEffectDefinition definition, int stacks)
        {
            Combatant target = Combatant.ResolveOrAdd(gameObject);
            if (target == null)
                return;

            DamagePacket packet = DamagePacket.StatusTick(
                null,
                target,
                definition.PeriodicDamage * stacks,
                definition.PeriodicDamageTags);
            CombatResolver.ApplyDamagePacket(packet);
        }

        private ActiveStatusEffect Find(StatusEffectDefinition definition)
        {
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                if (_activeEffects[i]?.Definition == definition)
                    return _activeEffects[i];
            }

            return null;
        }

        private void GrantTags(StatusEffectDefinition definition)
        {
            GameplayTagSet tags = gameObject.GetGameplayTags();
            foreach (string path in definition.Tags.EnumerateTagPaths())
                tags.AddRuntimeTagPath(path);
            foreach (string path in definition.GrantedTags.EnumerateTagPaths())
                tags.AddRuntimeTagPath(path);
        }

        private void RebuildGrantedTags()
        {
            GameplayTagSet tags = gameObject.GetGameplayTags();
            tags.ClearRuntimeTagsWithPrefix("Status");

            for (int i = 0; i < _activeEffects.Count; i++)
            {
                StatusEffectDefinition definition = _activeEffects[i]?.Definition;
                if (definition != null)
                    GrantTags(definition);
            }
        }
    }
}
