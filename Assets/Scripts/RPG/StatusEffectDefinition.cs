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

    public enum StatusEffectGroup
    {
        None = 0,
        Stance = 1,
        Hunger = 2,
        Sleep = 3,
        Curse = 4,
        Faith = 5,
        Mount = 6
    }

    [Serializable]
    public sealed class StatusEffectPhase
    {
        [SerializeField] private string _label = "Mild";
        [SerializeField] [Min(0)] private int _minStacks = 1;
        [SerializeField] private GameplayTagSet _grantedTags = new();
        [SerializeField] private GameplayStatModifierSet _statModifiers = new();

        public string Label => string.IsNullOrWhiteSpace(_label) ? "Phase" : _label.Trim();
        public int MinStacks => Mathf.Max(0, _minStacks);
        public GameplayTagSet GrantedTags => _grantedTags ?? GameplayTagSet.Empty;
        public GameplayStatModifierSet StatModifiers => _statModifiers;

        public void Normalize()
        {
            _label = string.IsNullOrWhiteSpace(_label) ? "Phase" : _label.Trim();
            _minStacks = Mathf.Max(0, _minStacks);
            _grantedTags ??= new GameplayTagSet();
            _statModifiers ??= new GameplayStatModifierSet();
            _grantedTags.Normalize();
            _statModifiers.Normalize();
        }
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
        [SerializeField] private StatusEffectGroup _group = StatusEffectGroup.None;
        [SerializeField] private List<StatusEffectDefinition> _negatedBy = new();
        [SerializeField] private List<StatusEffectDefinition> _nullifies = new();

        [Header("Periodic Effect")]
        [SerializeField] [Min(0f)] private float _tickInterval = 1f;
        [SerializeField] [Min(0f)] private float _periodicDamage;
        [SerializeField] private List<string> _periodicDamageTags = new() { "Damage.Poison" };

        [Header("Modifiers")]
        [SerializeField] private GameplayStatModifierSet _statModifiers = new();

        [Header("Phases")]
        [SerializeField] private List<StatusEffectPhase> _phases = new();

        public GameplayTagSet Tags => _tags ?? GameplayTagSet.Empty;
        public GameplayTagSet GrantedTags => _grantedTags ?? GameplayTagSet.Empty;
        public float Duration => Mathf.Max(0f, _duration);
        public StatusEffectStackRule StackRule => _stackRule;
        public int MaxStacks => Mathf.Max(1, _maxStacks);
        public float TickInterval => Mathf.Max(0f, _tickInterval);
        public float PeriodicDamage => Mathf.Max(0f, _periodicDamage);
        public IReadOnlyList<string> PeriodicDamageTags => _periodicDamageTags;
        public GameplayStatModifierSet StatModifiers => _statModifiers;
        public StatusEffectGroup Group => _group;
        public IReadOnlyList<StatusEffectDefinition> NegatedBy => _negatedBy;
        public IReadOnlyList<StatusEffectDefinition> Nullifies => _nullifies;
        public IReadOnlyList<StatusEffectPhase> Phases => _phases;

        private void OnValidate()
        {
            _tags ??= new GameplayTagSet();
            _grantedTags ??= new GameplayTagSet();
            _statModifiers ??= new GameplayStatModifierSet();
            _periodicDamageTags ??= new List<string>();
            _negatedBy ??= new List<StatusEffectDefinition>();
            _nullifies ??= new List<StatusEffectDefinition>();
            _phases ??= new List<StatusEffectPhase>();
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

            RemoveNullDefinitions(_negatedBy);
            RemoveNullDefinitions(_nullifies);
            for (int i = _phases.Count - 1; i >= 0; i--)
            {
                if (_phases[i] == null)
                    _phases.RemoveAt(i);
                else
                    _phases[i].Normalize();
            }
        }

        private static void RemoveNullDefinitions(List<StatusEffectDefinition> definitions)
        {
            if (definitions == null)
                return;

            for (int i = definitions.Count - 1; i >= 0; i--)
            {
                if (definitions[i] == null)
                    definitions.RemoveAt(i);
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
        public int CurrentPhase = -1;
    }
}
