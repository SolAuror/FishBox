using UnityEngine;
using Sol.AI;
using Sol.Actions;
using Sol.Grab;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sol
{
    /// <summary>
    /// Attach to an NPC to make their inventory lootable after death.
    /// The simplified build no longer supports live NPC trading.
    /// Requires <see cref="Inventory"/> on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public class NpcTradeInteractable : MonoBehaviour, IInteractable
    {
        private const string DefaultGoldPrefabPath = "Assets/ItemPrefabs/Gold.prefab";

        [SerializeField] private string _lootPrompt = "Loot";
        [Header("Corpse Loot")]
        [Tooltip("Gold item prefab used to convert numeric NPC gold into physical corpse loot.")]
        [SerializeField] private ItemComponent _goldLootItemTemplate;
        [SerializeField] [Min(1)] private int _maxGoldItemizeAttemptsPerOpen = 2000;

        private Inventory _inventory;
        private NPCSoul _soul;
        private bool _goldFullyItemized;
        private bool _triedResolveDefaultGoldTemplate;
        private bool IsLootingCorpse => _soul != null && !_soul.IsAlive;

        public string InteractionPrompt => _lootPrompt;

        private void Awake()
        {
            _inventory = GetComponent<Inventory>();
            _soul = GetComponent<NPCSoul>();
            if (_soul != null) _soul.OnDeath += HandleDeath;

            // NPCs are living-actor inventories, not world containers.
            _inventory?.SetContainerType(InventoryContainerType.Inventory);
        }

        private void OnDestroy()
        {
            if (_soul != null) _soul.OnDeath -= HandleDeath;
        }

        public bool CanInteract(Interactor interactor)
        {
            // Only corpse looting remains in the simplified build.
            return interactor != null
                && interactor.IsPlayer
                && interactor.Inventory != null
                && _inventory != null
                && IsLootingCorpse;
        }

        public GameAction GetInteraction(Interactor interactor)
        {
            if (!IsLootingCorpse)
                return null;

            ItemizeGoldForCorpseLoot(interactor);

            return new OpenTradeAction(interactor.Inventory, _inventory, lootMode: true);
        }

        private void HandleDeath()
        {
            ItemizeGoldForCorpseLoot();
        }

        private void ItemizeGoldForCorpseLoot(Interactor looter = null)
        {
            if (_goldFullyItemized || _inventory == null || _inventory.Gold <= 0)
                return;

            ItemComponent goldTemplate = ResolveGoldLootTemplate();
            if (goldTemplate == null)
            {
                TryTransferNumericGoldToLooter(looter);
                return;
            }

            int attempts = Mathf.Max(1, _maxGoldItemizeAttemptsPerOpen);
            int converted = 0;
            int availableGold = _inventory.Gold;

            while (converted < availableGold && converted < attempts)
            {
                var goldItem = Instantiate(goldTemplate, _inventory.transform);
                goldItem.gameObject.SetActive(false);

                if (!_inventory.AddPhysical(goldItem))
                {
                    Destroy(goldItem.gameObject);
                    break;
                }

                converted++;
            }

            if (converted <= 0)
            {
                TryTransferNumericGoldToLooter(looter);
                return;
            }

            _inventory.Gold = Mathf.Max(0, availableGold - converted);
            if (_inventory.Gold > 0)
                TryTransferNumericGoldToLooter(looter);

            _goldFullyItemized = _inventory.Gold <= 0;
        }

        private ItemComponent ResolveGoldLootTemplate()
        {
            if (_goldLootItemTemplate != null)
                return _goldLootItemTemplate;

            if (_triedResolveDefaultGoldTemplate)
                return null;

            _triedResolveDefaultGoldTemplate = true;

#if UNITY_EDITOR
            _goldLootItemTemplate = AssetDatabase.LoadAssetAtPath<ItemComponent>(DefaultGoldPrefabPath);
#endif

            return _goldLootItemTemplate;
        }

        private void TryTransferNumericGoldToLooter(Interactor looter)
        {
            if (_inventory == null || _inventory.Gold <= 0)
                return;

            if (looter?.Inventory == null)
            {
                Debug.LogWarning($"[NpcTradeInteractable] '{name}' could not transfer corpse gold: no looter inventory context.", this);
                return;
            }

            looter.Inventory.Gold += _inventory.Gold;
            _inventory.Gold = 0;
            _goldFullyItemized = true;
        }
    }
}
