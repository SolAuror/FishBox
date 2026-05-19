using System;
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
        private readonly HashSet<string> _grantedRuntimePaths = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<ActiveStatusEffect> ActiveEffects => _activeEffects;
        public GameplayStatModifierSet StatModifiers => BuildRuntimeModifiers();

        private void OnEnable()
        {
            RebuildGrantedTags();
            MarkProviderCacheDirty();
        }

        private void OnDisable()
        {
            ClearGrantedRuntimeTags();
            MarkProviderCacheDirty();
        }

        public bool Apply(StatusEffectDefinition definition, GameObject source = null)
        {
            if (definition == null)
                return false;

            if (IsNegated(definition))
                return false;

            ApplyNullifies(definition);
            ApplyGroupReplacement(definition);

            ActiveStatusEffect existing = Find(definition);
            if (existing != null)
            {
                if (definition.StackRule == StatusEffectStackRule.IgnoreIfActive)
                    return false;

                if (definition.StackRule == StatusEffectStackRule.AddStackRefreshDuration)
                    existing.Stacks = Mathf.Min(definition.MaxStacks, Mathf.Max(1, existing.Stacks + 1));

                existing.Remaining = definition.Duration;
                existing.TickCountdown = definition.TickInterval;
                ResolvePhase(existing);
                RebuildGrantedTags();
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
            ResolvePhase(instance);
            RebuildGrantedTags();
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
            bool tagsDirty = false;
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                ActiveStatusEffect effect = _activeEffects[i];
                StatusEffectDefinition definition = effect?.Definition;
                if (definition == null)
                {
                    _activeEffects.RemoveAt(i);
                    tagsDirty = true;
                    continue;
                }

                if (ResolvePhase(effect))
                    tagsDirty = true;

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
                        tagsDirty = true;
                        GameplayEvents.RaiseStatusRemoved(gameObject, definition);
                    }
                }
            }

            if (tagsDirty)
                RebuildGrantedTags();
        }

        private GameplayStatModifierSet BuildRuntimeModifiers()
        {
            List<GameplayStatModifier> modifiers = GameplayStatModifierSet.GetMutableModifiers(_runtimeModifiers);
            modifiers.Clear();

            for (int i = 0; i < _activeEffects.Count; i++)
            {
                ActiveStatusEffect effect = _activeEffects[i];
                AddModifiers(modifiers, effect?.Definition?.StatModifiers?.Modifiers);

                StatusEffectPhase phase = GetCurrentPhase(effect);
                AddModifiers(modifiers, phase?.StatModifiers?.Modifiers);
            }

            return _runtimeModifiers;
        }

        private static void AddModifiers(List<GameplayStatModifier> destination, IReadOnlyList<GameplayStatModifier> source)
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                    destination.Add(source[i]);
            }
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

        private bool IsNegated(StatusEffectDefinition definition)
        {
            IReadOnlyList<StatusEffectDefinition> negatedBy = definition.NegatedBy;
            if (negatedBy == null)
                return false;

            for (int i = 0; i < negatedBy.Count; i++)
            {
                StatusEffectDefinition blocker = negatedBy[i];
                if (blocker != null && Find(blocker) != null)
                    return true;
            }

            return false;
        }

        private void ApplyNullifies(StatusEffectDefinition definition)
        {
            IReadOnlyList<StatusEffectDefinition> nullifies = definition.Nullifies;
            if (nullifies == null)
                return;

            bool removedAny = false;
            for (int i = 0; i < nullifies.Count; i++)
            {
                StatusEffectDefinition target = nullifies[i];
                ActiveStatusEffect existing = target != null ? Find(target) : null;
                if (existing == null)
                    continue;

                _activeEffects.Remove(existing);
                removedAny = true;
                GameplayEvents.RaiseStatusRemoved(gameObject, target);
            }

            if (removedAny)
                RebuildGrantedTags();
        }

        private void ApplyGroupReplacement(StatusEffectDefinition definition)
        {
            if (definition.Group == StatusEffectGroup.None)
                return;

            bool removedAny = false;
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                StatusEffectDefinition activeDefinition = _activeEffects[i]?.Definition;
                if (activeDefinition == null || activeDefinition == definition || activeDefinition.Group != definition.Group)
                    continue;

                _activeEffects.RemoveAt(i);
                removedAny = true;
                GameplayEvents.RaiseStatusRemoved(gameObject, activeDefinition);
            }

            if (removedAny)
                RebuildGrantedTags();
        }

        private bool ResolvePhase(ActiveStatusEffect effect)
        {
            if (effect?.Definition == null)
                return false;

            int resolved = ResolvePhase(effect.Definition, Mathf.Max(1, effect.Stacks));
            if (effect.CurrentPhase == resolved)
                return false;

            effect.CurrentPhase = resolved;
            return true;
        }

        private static int ResolvePhase(StatusEffectDefinition definition, int stacks)
        {
            IReadOnlyList<StatusEffectPhase> phases = definition?.Phases;
            if (phases == null || phases.Count == 0)
                return -1;

            int resolved = -1;
            int highestMinStacks = int.MinValue;
            for (int i = 0; i < phases.Count; i++)
            {
                StatusEffectPhase phase = phases[i];
                if (phase == null || stacks < phase.MinStacks || phase.MinStacks < highestMinStacks)
                    continue;

                resolved = i;
                highestMinStacks = phase.MinStacks;
            }

            return resolved;
        }

        private static StatusEffectPhase GetCurrentPhase(ActiveStatusEffect effect)
        {
            IReadOnlyList<StatusEffectPhase> phases = effect?.Definition?.Phases;
            int index = effect != null ? effect.CurrentPhase : -1;
            return phases != null && index >= 0 && index < phases.Count ? phases[index] : null;
        }

        private void GrantTags(ActiveStatusEffect effect)
        {
            StatusEffectDefinition definition = effect?.Definition;
            if (definition == null)
                return;

            GrantTags(definition.Tags);
            GrantTags(definition.GrantedTags);
            GrantTags(GetCurrentPhase(effect)?.GrantedTags);
        }

        private void GrantTags(GameplayTagSet source)
        {
            if (source == null)
                return;

            GameplayTagSet tags = gameObject.GetGameplayTags();
            foreach (string path in source.EnumerateTagPaths())
            {
                tags.AddRuntimeTagPath(path);
                _grantedRuntimePaths.Add(path);
            }
        }

        private void RebuildGrantedTags()
        {
            ClearGrantedRuntimeTags();

            for (int i = 0; i < _activeEffects.Count; i++)
            {
                ResolvePhase(_activeEffects[i]);
                GrantTags(_activeEffects[i]);
            }
        }

        private void ClearGrantedRuntimeTags()
        {
            if (_grantedRuntimePaths.Count == 0)
                return;

            GameplayTagSet tags = gameObject.GetGameplayTags();
            foreach (string path in _grantedRuntimePaths)
                tags.RemoveRuntimeTagPath(path);
            _grantedRuntimePaths.Clear();
        }

        private void OnValidate()
        {
            _activeEffects ??= new List<ActiveStatusEffect>();
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                if (_activeEffects[i] == null)
                    _activeEffects.RemoveAt(i);
            }
        }

        private void MarkProviderCacheDirty()
        {
            if (TryGetComponent(out GameplayStatAggregator aggregator))
                aggregator.MarkDirty();
        }
    }
}
