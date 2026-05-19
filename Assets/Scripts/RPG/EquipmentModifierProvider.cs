using System.Collections.Generic;
using Sol.Grab;
using UnityEngine;

namespace Sol.Rpg
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Equipment))]
    [AddComponentMenu("Sol/RPG/Equipment Modifier Provider")]
    public sealed class EquipmentModifierProvider : MonoBehaviour, IGameplayStatModifierProvider
    {
        [SerializeField] private Equipment _equipment;

        private readonly GameplayStatModifierSet _runtimeModifiers = new();
        private readonly HashSet<ItemComponent> _seenItems = new();
        private bool _dirty = true;

        public GameplayStatModifierSet StatModifiers
        {
            get
            {
                RebuildIfDirty();
                return _runtimeModifiers;
            }
        }

        private void OnEnable()
        {
            ResolveEquipment();
            if (_equipment != null)
                _equipment.OnChanged += MarkDirty;

            MarkDirty();
            MarkProviderCacheDirty();
        }

        private void OnDisable()
        {
            if (_equipment != null)
                _equipment.OnChanged -= MarkDirty;

            MarkProviderCacheDirty();
        }

        private void OnValidate()
        {
            if (_equipment == null)
                _equipment = GetComponent<Equipment>();
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        private void MarkProviderCacheDirty()
        {
            if (TryGetComponent(out GameplayStatAggregator aggregator))
                aggregator.MarkDirty();
        }

        private void ResolveEquipment()
        {
            if (_equipment == null)
                _equipment = GetComponent<Equipment>();
        }

        private void RebuildIfDirty()
        {
            if (!_dirty)
                return;

            _dirty = false;
            ResolveEquipment();

            List<GameplayStatModifier> modifiers = GameplayStatModifierSet.GetMutableModifiers(_runtimeModifiers);
            modifiers.Clear();
            _seenItems.Clear();

            if (_equipment == null)
                return;

            foreach (KeyValuePair<EquipmentSlotType, ItemComponent> pair in _equipment.Equipped)
            {
                ItemComponent item = pair.Value;
                if (item == null || !_seenItems.Add(item))
                    continue;

                IReadOnlyList<GameplayStatModifier> source = (item as IGameplayStatModifierProvider)?.StatModifiers?.Modifiers;
                if (source == null)
                    continue;

                for (int i = 0; i < source.Count; i++)
                {
                    if (source[i] != null)
                        modifiers.Add(source[i]);
                }
            }
        }
    }
}
