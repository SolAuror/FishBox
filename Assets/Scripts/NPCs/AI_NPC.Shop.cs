using Sol;
using Sol.Actions;
using Sol.Grab;
using Sol.Rpg;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sol.AI
{
    /// <summary>
    /// AI_NPC partial - trade and corpse-loot behaviour. Replaces the legacy NpcTraderInteractable.
    /// Trade is gated on <see cref="IsTrader"/> (derived from the NPCSoul trader checkbox).
    /// </summary>
    public partial class AI_NPC
    {
        private const string DefaultGoldPrefabPath = "Assets/ItemPrefabs/Gold.prefab";

        [Header("Corpse Loot")]
        [Header("Shop")]
        [Tooltip("RPG shop definition used for merchant trade. When assigned, merchant stock/prices come from the shop system.")]
        [ShopIdDropdown]
        [SerializeField] private string _shopId = string.Empty;

        [Header("Corpse Loot")]
        [Tooltip("Gold item id (registry-backed). When set, takes precedence over the direct prefab reference.")]
        [ItemIdDropdown]
        [SerializeField] private string _goldLootItemId = string.Empty;
        [Tooltip("Gold item prefab used to convert numeric NPC gold into physical corpse loot. Used as fallback if no id is set.")]
        [SerializeField] private ItemComponent _goldLootItemTemplate;
        [SerializeField] [Min(1)] private int _maxGoldItemizeAttemptsPerOpen = 2000;

        private bool _goldFullyItemized;
        private bool _triedResolveDefaultGoldTemplate;

        /// <summary>
        /// True when this NPC is currently allowed to trade. Designers and runtime systems
        /// can toggle the backing NPCSoul flag to enable or disable trade.
        /// </summary>
        public bool IsTrader
        {
            get => (soul != null && soul.CanTrade) || !string.IsNullOrWhiteSpace(_shopId);
            set
            {
                if (soul != null)
                    soul.CanTrade = value;
            }
        }

        public string ShopId => _shopId;

        private bool IsLootingCorpse => soul != null && !soul.IsAlive;

        private GameAction BuildTradeAction(Interactor interactor)
        {
            if (!IsTrader)
                return null;

            ShopRuntimeSession shopSession = ResolveShopSession();
            if (shopSession != null)
                return new OpenShopAction(interactor.Inventory, shopSession);

            if (!string.IsNullOrWhiteSpace(_shopId))
                return null;

            return new OpenTradeAction(interactor.Inventory, inventory, lootMode: false);
        }

        private GameAction BuildLootAction(Interactor interactor)
        {
            ItemizeGoldForCorpseLoot(interactor);
            return new OpenTradeAction(interactor.Inventory, inventory, lootMode: true);
        }

        private void OpenTradeFromConversation(Interactor interactor)
        {
            if (!IsTrader || interactor == null || interactor.Inventory == null || inventory == null)
                return;

            Sol.HUD.TradeUI tradeUi = Sol.HUD.TradeUI.ResolveInstance();
            if (tradeUi == null)
                return;

            ShopRuntimeSession shopSession = ResolveShopSession();
            if (shopSession != null)
            {
                tradeUi.OpenShop(interactor.Inventory, shopSession);
                return;
            }

            if (string.IsNullOrWhiteSpace(_shopId))
                tradeUi.OpenTrade(interactor.Inventory, inventory, freeTrade: false);
        }

        private ShopRuntimeSession ResolveShopSession()
        {
            if (string.IsNullOrWhiteSpace(_shopId))
                return null;

            return ShopRuntimeStore.GetOrCreateSession(_shopId);
        }

        private void ItemizeGoldForCorpseLoot(Interactor looter = null)
        {
            if (_goldFullyItemized || inventory == null || inventory.Gold <= 0)
                return;

            ItemComponent goldTemplate = ResolveGoldLootTemplate();
            if (goldTemplate == null)
            {
                TryTransferNumericGoldToLooter(looter);
                return;
            }

            int attempts = Mathf.Max(1, _maxGoldItemizeAttemptsPerOpen);
            int converted = 0;
            int availableGold = inventory.Gold;

            while (converted < availableGold && converted < attempts)
            {
                ItemComponent goldItem = Instantiate(goldTemplate, inventory.transform);
                goldItem.gameObject.SetActive(false);

                if (!inventory.AddPhysical(goldItem))
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

            inventory.Gold = Mathf.Max(0, availableGold - converted);
            if (inventory.Gold > 0)
                TryTransferNumericGoldToLooter(looter);

            _goldFullyItemized = inventory.Gold <= 0;
        }

        private ItemComponent ResolveGoldLootTemplate()
        {
            if (!string.IsNullOrWhiteSpace(_goldLootItemId))
            {
                ItemRegistry registry = ItemRegistry.Get();
                ItemComponent fromRegistry = registry != null ? registry.GetPrefab(_goldLootItemId) : null;
                if (fromRegistry != null)
                    return fromRegistry;
            }

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
            if (inventory == null || inventory.Gold <= 0)
                return;

            if (looter?.Inventory == null)
            {
                Debug.LogWarning($"[AI_NPC.Shop] '{name}' could not transfer corpse gold: no looter inventory context.", this);
                return;
            }

            looter.Inventory.Gold += inventory.Gold;
            inventory.Gold = 0;
            _goldFullyItemized = true;
        }
    }
}
