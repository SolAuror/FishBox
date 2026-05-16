using System.Collections.Generic;
using UnityEngine;

namespace Sol.Rpg
{
    public enum TraitReaction
    {
        None = 0,
        PreventDeath = 1,
        IgnoreStaminaCosts = 2
    }

    [CreateAssetMenu(fileName = "TRT00001_NewTrait", menuName = "Sol/RPG/Trait Definition")]
    public sealed class TraitDefinition : RpgDefinition
    {
        [Header("Trait")]
        [SerializeField] private GameplayTagSet _tags = new();
        [SerializeField] private GameplayTagSet _grantedTags = new();
        [SerializeField] private GameplayStatModifierSet _statModifiers = new();
        [SerializeField] private List<TraitReaction> _reactions = new();

        public GameplayTagSet Tags => _tags ?? GameplayTagSet.Empty;
        public GameplayTagSet GrantedTags => _grantedTags ?? GameplayTagSet.Empty;
        public GameplayStatModifierSet StatModifiers => _statModifiers;
        public IReadOnlyList<TraitReaction> Reactions => _reactions;

        private void OnValidate()
        {
            _tags ??= new GameplayTagSet();
            _grantedTags ??= new GameplayTagSet();
            _statModifiers ??= new GameplayStatModifierSet();
            _reactions ??= new List<TraitReaction>();
            _tags.Normalize();
            _grantedTags.Normalize();
            _statModifiers.Normalize();
        }
    }
}
