using Sol.Grab;
using Sol.Rpg;
using UnityEngine;

namespace Sol
{
    [AddComponentMenu("Sol/Interactables/Harvestable Interaction Point")]
    public class HarvestableInteractionPoint : InteractionPoint
    {
        [Header("Harvest")]
        [ItemIdDropdown]
        [SerializeField] private string _itemId = string.Empty;
        [Min(1)]
        [SerializeField] private int _quantity = 1;
        [SerializeField] private bool _depleteAfterUse = true;
        [Min(0f)]
        [SerializeField] private float _respawnSeconds;

        private bool _depleted;
        private float _respawnAt;

        public override bool CanInteract(Interactor interactor)
        {
            RefreshRespawn();
            return !_depleted && base.CanInteract(interactor);
        }

        protected override void OnUseCompleted(Interactor interactor)
        {
            GiveHarvestItems(interactor);

            if (_depleteAfterUse)
            {
                _depleted = true;
                SetRuntimeTag(GameplayCapabilityTags.StateDepleted, true);
                if (_respawnSeconds > 0f)
                    _respawnAt = Time.time + _respawnSeconds;
            }
        }

        private void GiveHarvestItems(Interactor interactor)
        {
            if (interactor?.Inventory == null)
            {
                Debug.LogWarning($"[{nameof(HarvestableInteractionPoint)}] '{name}' could not give harvest item because the interactor has no Inventory.", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(_itemId))
            {
                Debug.LogWarning($"[{nameof(HarvestableInteractionPoint)}] '{name}' has no harvest item id configured.", this);
                return;
            }

            ItemRegistry registry = ItemRegistry.Get();
            ItemComponent prefab = registry != null ? registry.GetPrefab(_itemId) : null;
            if (prefab == null)
            {
                Debug.LogWarning($"[{nameof(HarvestableInteractionPoint)}] ItemRegistry could not resolve item id '{_itemId}'. TODO: assign a valid FishBox item prefab instead of creating a parallel item system.", this);
                return;
            }

            int count = Mathf.Max(1, _quantity);
            for (int i = 0; i < count; i++)
            {
                ItemComponent runtimeItem = Instantiate(prefab, interactor.Inventory.transform);
                runtimeItem.gameObject.SetActive(false);

                if (interactor.Inventory.Add(runtimeItem))
                    continue;

                Destroy(runtimeItem.gameObject);
                Debug.LogWarning($"[{nameof(HarvestableInteractionPoint)}] '{name}' could not add '{_itemId}' to '{interactor.Owner.name}' inventory.", this);
                break;
            }
        }

        private void RefreshRespawn()
        {
            if (!_depleted || _respawnSeconds <= 0f)
                return;

            if (Time.time < _respawnAt)
                return;

            _depleted = false;
            SetRuntimeTag(GameplayCapabilityTags.StateDepleted, false);
            _respawnAt = 0f;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _itemId = EntityCodeUtility.NormalizeOrEmpty(_itemId, EntityCodeUtility.ItemPrefix);
            _quantity = Mathf.Max(1, _quantity);
            _respawnSeconds = Mathf.Max(0f, _respawnSeconds);
        }
    }
}
