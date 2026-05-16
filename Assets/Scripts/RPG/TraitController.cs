using System.Collections.Generic;
using UnityEngine;

namespace Sol.Rpg
{
    [AddComponentMenu("Sol/RPG/Trait Controller")]
    [DisallowMultipleComponent]
    public sealed class TraitController : MonoBehaviour, IGameplayStatModifierProvider
    {
        [SerializeField] private List<TraitDefinition> _traits = new();

        private readonly GameplayStatModifierSet _runtimeModifiers = new();

        public IReadOnlyList<TraitDefinition> Traits => _traits;
        public GameplayStatModifierSet StatModifiers => BuildRuntimeModifiers();

        private void OnEnable()
        {
            RebuildTraitTags();
        }

        private void OnDisable()
        {
            gameObject.GetGameplayTags().ClearRuntimeTagsWithPrefix("Trait");
        }

        private void OnValidate()
        {
            _traits ??= new List<TraitDefinition>();
        }

        public bool HasReaction(TraitReaction reaction)
        {
            for (int i = 0; i < _traits.Count; i++)
            {
                IReadOnlyList<TraitReaction> reactions = _traits[i]?.Reactions;
                if (reactions == null)
                    continue;

                for (int j = 0; j < reactions.Count; j++)
                {
                    if (reactions[j] == reaction)
                        return true;
                }
            }

            return false;
        }

        private GameplayStatModifierSet BuildRuntimeModifiers()
        {
            List<GameplayStatModifier> modifiers = GameplayStatModifierSet.GetMutableModifiers(_runtimeModifiers);
            modifiers.Clear();

            for (int i = 0; i < _traits.Count; i++)
            {
                IReadOnlyList<GameplayStatModifier> source = _traits[i]?.StatModifiers?.Modifiers;
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

        private void RebuildTraitTags()
        {
            GameplayTagSet tags = gameObject.GetGameplayTags();
            tags.ClearRuntimeTagsWithPrefix("Trait");
            for (int i = 0; i < _traits.Count; i++)
            {
                TraitDefinition trait = _traits[i];
                if (trait == null)
                    continue;

                foreach (string path in trait.Tags.EnumerateTagPaths())
                    tags.AddRuntimeTagPath(path);
                foreach (string path in trait.GrantedTags.EnumerateTagPaths())
                    tags.AddRuntimeTagPath(path);
            }
        }
    }
}
