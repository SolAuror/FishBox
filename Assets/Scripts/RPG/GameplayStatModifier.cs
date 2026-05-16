using System;
using System.Collections.Generic;
using Sol.Grab;
using UnityEngine;

namespace Sol.Rpg
{
    public static class GameplayStatIds
    {
        public const string MaxHealth = "Vital.MaxHealth";
        public const string MaxStamina = "Vital.MaxStamina";
        public const string ArmorRating = "Combat.ArmorRating";
        public const string StaminaCost = "Combat.StaminaCost";
        public const string StaminaRegen = "Combat.StaminaRegen";
        public const string IncomingDamage = "Combat.IncomingDamage";
    }

    public enum GameplayStatModifierOperation
    {
        FlatAdd = 0,
        PercentAdd = 1,
        PercentMultiply = 2,
        Minimum = 3,
        Maximum = 4
    }

    [Serializable]
    public sealed class GameplayStatModifier
    {
        [SerializeField] private string _statId = GameplayStatIds.IncomingDamage;
        [SerializeField] private GameplayStatModifierOperation _operation = GameplayStatModifierOperation.FlatAdd;
        [SerializeField] private float _value;
        [SerializeField] private string _requiredTargetTag = string.Empty;

        public string StatId => _statId?.Trim() ?? string.Empty;
        public GameplayStatModifierOperation Operation => _operation;
        public float Value => _value;
        public string RequiredTargetTag => _requiredTargetTag?.Trim() ?? string.Empty;

        public bool AppliesTo(string statId, GameplayTagSet targetTags)
        {
            if (string.IsNullOrWhiteSpace(statId) || !string.Equals(StatId, statId.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            return string.IsNullOrWhiteSpace(RequiredTargetTag)
                || (targetTags != null && (targetTags.HasExact(RequiredTargetTag) || targetTags.HasTagOrChild(RequiredTargetTag)));
        }

        public void Normalize()
        {
            _statId = string.IsNullOrWhiteSpace(_statId) ? GameplayStatIds.IncomingDamage : _statId.Trim();
            _requiredTargetTag = GameplayTagUtility.NormalizePathOrEmpty(_requiredTargetTag);
        }
    }

    [Serializable]
    public sealed class GameplayStatModifierSet
    {
        [SerializeField] private List<GameplayStatModifier> _modifiers = new();

        public IReadOnlyList<GameplayStatModifier> Modifiers => _modifiers;

        internal static List<GameplayStatModifier> GetMutableModifiers(GameplayStatModifierSet set)
        {
            set._modifiers ??= new List<GameplayStatModifier>();
            return set._modifiers;
        }

        public void Normalize()
        {
            _modifiers ??= new List<GameplayStatModifier>();
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (_modifiers[i] == null)
                    _modifiers.RemoveAt(i);
                else
                    _modifiers[i].Normalize();
            }
        }
    }

    public interface IGameplayStatModifierProvider
    {
        GameplayStatModifierSet StatModifiers { get; }
    }

    public static class GameplayStatSystem
    {
        private static readonly List<IGameplayStatModifierProvider> ProviderScratch = new();
        private static readonly HashSet<ItemComponent> ItemScratch = new();

        public static float Evaluate(string statId, float baseValue, GameObject target, GameObject source = null)
        {
            if (target == null)
                return baseValue;

            GameplayTagSet targetTags = target.GetGameplayTags();
            ProviderScratch.Clear();
            target.GetComponents(ProviderScratch);

            Equipment equipment = target.GetComponent<Equipment>();
            if (equipment != null)
            {
                ItemScratch.Clear();
                foreach (KeyValuePair<EquipmentSlotType, ItemComponent> pair in equipment.Equipped)
                {
                    ItemComponent item = pair.Value;
                    if (item != null && ItemScratch.Add(item) && item is IGameplayStatModifierProvider provider)
                        ProviderScratch.Add(provider);
                }
            }

            return Evaluate(statId, baseValue, ProviderScratch, targetTags);
        }

        public static float Evaluate(string statId, float baseValue, IReadOnlyList<IGameplayStatModifierProvider> providers, GameplayTagSet targetTags)
        {
            if (providers == null || providers.Count == 0)
                return baseValue;

            float flat = 0f;
            float percentAdd = 0f;
            float percentMultiply = 1f;
            float? minimum = null;
            float? maximum = null;

            for (int p = 0; p < providers.Count; p++)
            {
                IReadOnlyList<GameplayStatModifier> modifiers = providers[p]?.StatModifiers?.Modifiers;
                if (modifiers == null)
                    continue;

                for (int i = 0; i < modifiers.Count; i++)
                {
                    GameplayStatModifier modifier = modifiers[i];
                    if (modifier == null || !modifier.AppliesTo(statId, targetTags))
                        continue;

                    switch (modifier.Operation)
                    {
                        case GameplayStatModifierOperation.FlatAdd:
                            flat += modifier.Value;
                            break;
                        case GameplayStatModifierOperation.PercentAdd:
                            percentAdd += modifier.Value;
                            break;
                        case GameplayStatModifierOperation.PercentMultiply:
                            percentMultiply *= 1f + modifier.Value;
                            break;
                        case GameplayStatModifierOperation.Minimum:
                            minimum = minimum.HasValue ? Mathf.Max(minimum.Value, modifier.Value) : modifier.Value;
                            break;
                        case GameplayStatModifierOperation.Maximum:
                            maximum = maximum.HasValue ? Mathf.Min(maximum.Value, modifier.Value) : modifier.Value;
                            break;
                    }
                }
            }

            float result = (baseValue + flat) * (1f + percentAdd) * percentMultiply;
            if (minimum.HasValue)
                result = Mathf.Max(result, minimum.Value);
            if (maximum.HasValue)
                result = Mathf.Min(result, maximum.Value);
            return result;
        }
    }
}
