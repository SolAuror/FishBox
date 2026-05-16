using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sol.Rpg
{
    public enum StatusEffectStackRule
    {
        RefreshDuration = 0,
        AddStackRefreshDuration = 1,
        IgnoreIfActive = 2
    }

    [CreateAssetMenu(fileName = "STS00001_NewStatus", menuName = "Sol/RPG/Status Effect Definition")]
    public sealed class StatusEffectDefinition : RpgDefinition
    {
        [Header("Status")]
        [SerializeField] private GameplayTagSet _tags = new();
        [SerializeField] private GameplayTagSet _grantedTags = new();
        [SerializeField] [Min(0f)] private float _duration = 10f;
        [SerializeField] private StatusEffectStackRule _stackRule = StatusEffectStackRule.RefreshDuration;
        [SerializeField] [Min(1)] private int _maxStacks = 1;

        [Header("Periodic Effect")]
        [SerializeField] [Min(0f)] private float _tickInterval = 1f;
        [SerializeField] [Min(0f)] private float _periodicDamage;
        [SerializeField] private List<string> _periodicDamageTags = new() { "Damage.Poison" };

        [Header("Modifiers")]
        [SerializeField] private GameplayStatModifierSet _statModifiers = new();

        public GameplayTagSet Tags => _tags ?? GameplayTagSet.Empty;
        public GameplayTagSet GrantedTags => _grantedTags ?? GameplayTagSet.Empty;
        public float Duration => Mathf.Max(0f, _duration);
        public StatusEffectStackRule StackRule => _stackRule;
        public int MaxStacks => Mathf.Max(1, _maxStacks);
        public float TickInterval => Mathf.Max(0f, _tickInterval);
        public float PeriodicDamage => Mathf.Max(0f, _periodicDamage);
        public IReadOnlyList<string> PeriodicDamageTags => _periodicDamageTags;
        public GameplayStatModifierSet StatModifiers => _statModifiers;

        private void OnValidate()
        {
            _tags ??= new GameplayTagSet();
            _grantedTags ??= new GameplayTagSet();
            _statModifiers ??= new GameplayStatModifierSet();
            _periodicDamageTags ??= new List<string>();
            _tags.Normalize();
            _grantedTags.Normalize();
            _statModifiers.Normalize();
            _duration = Mathf.Max(0f, _duration);
            _tickInterval = Mathf.Max(0f, _tickInterval);
            _periodicDamage = Mathf.Max(0f, _periodicDamage);
            _maxStacks = Mathf.Max(1, _maxStacks);

            for (int i = _periodicDamageTags.Count - 1; i >= 0; i--)
            {
                string normalized = GameplayTagUtility.NormalizePathOrEmpty(_periodicDamageTags[i]);
                if (string.IsNullOrEmpty(normalized))
                    _periodicDamageTags.RemoveAt(i);
                else
                    _periodicDamageTags[i] = normalized;
            }
        }
    }

    [Serializable]
    public sealed class ActiveStatusEffect
    {
        public StatusEffectDefinition Definition;
        public float Remaining;
        public float TickCountdown;
        public int Stacks = 1;
    }
}
