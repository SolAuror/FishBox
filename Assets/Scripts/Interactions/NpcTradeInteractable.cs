using System.Collections.Generic;
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
    /// Attach to an NPC to make them tradeable while alive and lootable when dead.
    /// Requires <see cref="Inventory"/> on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public class NpcTradeInteractable : MonoBehaviour, IInteractable
    {
        private const string DefaultGoldPrefabPath = "Assets/ItemPrefabs/Gold.prefab";

        private enum ConversationOptionId
        {
            Trade,
            Goodbye
        }

        [SerializeField] private string _prompt = "Trade";
        [SerializeField] private string _lootPrompt = "Loot";
        [Header("Conversation")]
        [SerializeField] private bool _useConversationWindow = true;
        [SerializeField] private string _talkPrompt = "Talk";
        [SerializeField] private string _greetingLine = "What can I do for you?";
        [SerializeField] private string _tradeOptionLabel = "Trade";
        [SerializeField] private string _goodbyeOptionLabel = "Goodbye";
        [SerializeField] private Sprite _speakerIcon;
        [Header("Corpse Loot")]
        [Tooltip("Gold item prefab used to convert numeric NPC gold into physical corpse loot.")]
        [SerializeField] private ItemComponent _goldLootItemTemplate;
        [SerializeField] [Min(1)] private int _maxGoldItemizeAttemptsPerOpen = 2000;

        private Inventory _inventory;
        private NPCSoul _soul;
        private bool _goldFullyItemized;
        private bool _triedResolveDefaultGoldTemplate;
        private bool IsLootingCorpse => _soul != null && !_soul.IsAlive;

        public string InteractionPrompt =>
            IsLootingCorpse
                ? _lootPrompt
                : (_useConversationWindow ? _talkPrompt : _prompt);

        private void Awake()
        {
            _inventory = GetComponent<Inventory>();
            _soul = GetComponent<NPCSoul>();
            if (_soul != null)
                _soul.OnDeath += HandleDeath;

            _inventory?.SetContainerType(InventoryContainerType.Inventory);
        }

        private void OnDestroy()
        {
            if (_soul != null)
                _soul.OnDeath -= HandleDeath;
        }

        public bool CanInteract(Interactor interactor)
        {
            return interactor != null
                && interactor.IsPlayer
                && interactor.Inventory != null
                && _inventory != null;
        }

        public GameAction GetInteraction(Interactor interactor)
        {
            if (interactor == null || interactor.Inventory == null || _inventory == null)
                return null;

            if (IsLootingCorpse)
                return BuildLootAction(interactor);

            if (_useConversationWindow)
                return BuildConversationAction(interactor);

            return BuildTradeAction(interactor);
        }

        private GameAction BuildLootAction(Interactor interactor)
        {
            ItemizeGoldForCorpseLoot(interactor);
            return new OpenTradeAction(interactor.Inventory, _inventory, lootMode: true);
        }

        private GameAction BuildTradeAction(Interactor interactor)
        {
            return new OpenTradeAction(interactor.Inventory, _inventory, lootMode: false);
        }

        private GameAction BuildConversationAction(Interactor interactor)
        {
            List<ConversationOptionId> optionIds = new List<ConversationOptionId>(2);
            List<string> optionLabels = new List<string>(2);
            AddConversationOption(ConversationOptionId.Trade, optionIds, optionLabels);
            AddConversationOption(ConversationOptionId.Goodbye, optionIds, optionLabels);

            string speakerName = _soul != null && !string.IsNullOrWhiteSpace(_soul.CharacterName)
                ? _soul.CharacterName
                : gameObject.name;

            return new OpenConversationAction(
                speakerName,
                _greetingLine,
                optionLabels,
                optionIndex => HandleConversationOptionSelected(optionIndex, optionIds, interactor),
                speakerIcon: _speakerIcon);
        }

        private void AddConversationOption(
            ConversationOptionId optionId,
            IList<ConversationOptionId> optionIds,
            IList<string> optionLabels)
        {
            optionIds.Add(optionId);
            optionLabels.Add(GetConversationOptionLabel(optionId));
        }

        private string GetConversationOptionLabel(ConversationOptionId optionId)
        {
            return optionId switch
            {
                ConversationOptionId.Trade => _tradeOptionLabel,
                _ => _goodbyeOptionLabel
            };
        }

        private void HandleConversationOptionSelected(
            int optionIndex,
            IReadOnlyList<ConversationOptionId> optionIds,
            Interactor interactor)
        {
            if (optionIds == null || optionIndex < 0 || optionIndex >= optionIds.Count || interactor == null)
                return;

            if (optionIds[optionIndex] == ConversationOptionId.Trade)
                OpenTradeFromConversation(interactor);
        }

        private void OpenTradeFromConversation(Interactor interactor)
        {
            if (interactor == null || interactor.Inventory == null || _inventory == null)
                return;

            Sol.HUD.TradeUI tradeUi = Sol.HUD.TradeUI.ResolveInstance();
            if (tradeUi == null)
                return;

            tradeUi.OpenTrade(interactor.Inventory, _inventory, freeTrade: false);
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
                ItemComponent goldItem = Instantiate(goldTemplate, _inventory.transform);
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
