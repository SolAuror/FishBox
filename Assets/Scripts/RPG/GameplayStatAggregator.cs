using System.Collections.Generic;
using UnityEngine;

namespace Sol.Rpg
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/RPG/Gameplay Stat Aggregator")]
    public sealed class GameplayStatAggregator : MonoBehaviour
    {
        private readonly List<IGameplayStatModifierProvider> _providers = new();
        private bool _dirty = true;

        public void MarkDirty()
        {
            _dirty = true;
        }

        public float Evaluate(string statId, float baseValue, GameplayTagSet targetTags)
        {
            RefreshIfDirty();
            return GameplayStatSystem.Evaluate(statId, baseValue, _providers, targetTags);
        }

        private void Awake()
        {
            MarkDirty();
        }

        private void OnEnable()
        {
            MarkDirty();
        }

        private void RefreshIfDirty()
        {
            if (!_dirty)
                return;

            GameplayStatSystem.EnsureEquipmentModifierProvider(gameObject);
            _providers.Clear();
            GetComponents(_providers);
            for (int i = _providers.Count - 1; i >= 0; i--)
            {
                if (_providers[i] is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                    _providers.RemoveAt(i);
            }
            _dirty = false;
        }
    }
}
