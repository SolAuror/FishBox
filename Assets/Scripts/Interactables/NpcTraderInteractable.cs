using System.Collections.Generic;
using UnityEngine;
using Sol.AI;
using Sol.Actions;
using Sol.Grab;
using Sol.Quests;

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
    public class NpcTrader : MonoBehaviour, IInteractable
    {
        private const string DefaultGoldPrefabPath = "Assets/ItemPrefabs/Gold.prefab";

        private enum ConversationOptionId
        {
            Trade,
            Quest,
            QuestTurnIn,
            QuestDeliver,
            Goodbye
        }

#region Inspector Settings

        [Tooltip("Inspector: tunes prompt.")]
        [SerializeField] private string _prompt = "Trade";

        [Tooltip("Inspector: tunes loot prompt.")]
        [SerializeField] private string _lootPrompt = "Loot";

        [Header("Conversation")]
        [Tooltip("Inspector: tunes use conversation window.")]
        [SerializeField] private bool _useConversationWindow = true;
        [SerializeField] private string _talkPrompt = "Talk";
        
        [Tooltip("Inspector: tunes greeting line.")]
        [SerializeField] private string _greetingLine = "What can I do for you?";
        [SerializeField] private string _tradeOptionLabel = "Trade";
        
        [Tooltip("Inspector: tunes goodbye option label.")]
        [SerializeField] private string _goodbyeOptionLabel = "Goodbye";
        
        [Tooltip("Inspector: tunes speaker icon.")]
        [SerializeField] private Sprite _speakerIcon;
        
        [Header("Corpse Loot")]
        [Tooltip("Gold item id (registry-backed). When set, takes precedence over the direct prefab reference.")]
        [ItemIdDropdown]
        [SerializeField] private string _goldLootItemId = string.Empty;
        [Tooltip("Gold item prefab used to convert numeric NPC gold into physical corpse loot. Used as fallback if no id is set.")]
        [SerializeField] private ItemComponent _goldLootItemTemplate;
        [SerializeField] [Min(1)] private int _maxGoldItemizeAttemptsPerOpen = 2000;
#endregion

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
            // Set IsConversing true on the NPC for rotation suppression
            var aiNpc = GetComponent<Sol.AI.AI_NPC>();
            aiNpc?.BeginConversation();
            List<ConversationOptionId> optionIds = new List<ConversationOptionId>(4);
            List<string> optionLabels = new List<string>(4);
            AddConversationOption(ConversationOptionId.Trade, optionIds, optionLabels);

            string speakerName = _soul != null && !string.IsNullOrWhiteSpace(_soul.CharacterName)
                ? _soul.CharacterName
                : gameObject.name;
            string speakerReference = _soul != null && !string.IsNullOrWhiteSpace(_soul.OwnerId)
                ? _soul.OwnerId
                : speakerName;

 // Quest conversation options - order: turn-in, deliver, offer, then goodbye.
            QuestManager questManager = QuestManager.Instance;
            if (questManager != null)
            {
                if (HasQuestReadyToTurnIn(questManager, speakerReference))
                    AddConversationOption(ConversationOptionId.QuestTurnIn, optionIds, optionLabels);
                if (HasDeliverableForNpc(questManager, speakerReference, interactor))
                    AddConversationOption(
                        ConversationOptionId.QuestDeliver,
                        optionIds,
                        optionLabels,
                        HasGoldPaymentForNpc(questManager, speakerReference, interactor) ? "Pay gold" : null);
                if (questManager.GetOfferableQuests(speakerReference).Count > 0)
                    AddConversationOption(ConversationOptionId.Quest, optionIds, optionLabels);
            }

            AddConversationOption(ConversationOptionId.Goodbye, optionIds, optionLabels);

            return new OpenConversationAction(
                speakerName,
                _greetingLine,
                optionLabels,
                optionIndex =>
                {
                    HandleConversationOptionSelected(optionIndex, optionIds, interactor);
                    // End conversation state when an option is selected
                    var aiNpc2 = GetComponent<Sol.AI.AI_NPC>();
                    aiNpc2?.EndConversation();
                },
                speakerIcon: _speakerIcon);
        }

        private void AddConversationOption(
            ConversationOptionId optionId,
            IList<ConversationOptionId> optionIds,
            IList<string> optionLabels,
            string labelOverride = null)
        {
            optionIds.Add(optionId);
            optionLabels.Add(string.IsNullOrWhiteSpace(labelOverride)
                ? GetConversationOptionLabel(optionId)
                : labelOverride);
        }

        private string GetConversationOptionLabel(ConversationOptionId optionId)
        {
            return optionId switch
            {
                ConversationOptionId.Trade => _tradeOptionLabel,
                ConversationOptionId.Quest => "Quest",
                ConversationOptionId.QuestTurnIn => "Quest complete",
                ConversationOptionId.QuestDeliver => "Deliver item",
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

            string speakerName = _soul != null && !string.IsNullOrWhiteSpace(_soul.CharacterName)
                ? _soul.CharacterName
                : gameObject.name;
            string speakerReference = _soul != null && !string.IsNullOrWhiteSpace(_soul.OwnerId)
                ? _soul.OwnerId
                : speakerName;

            switch (optionIds[optionIndex])
            {
                case ConversationOptionId.Trade:
                    OpenTradeFromConversation(interactor);
                    break;
                case ConversationOptionId.Quest:
                    OfferNextQuest(speakerReference);
                    break;
                case ConversationOptionId.QuestTurnIn:
                    TurnInReadyQuest(speakerReference);
                    break;
                case ConversationOptionId.QuestDeliver:
                    DeliverItemToNpc(speakerReference, interactor);
                    break;
            }
        }

        // ----- Quest helpers -----

        private static bool HasQuestReadyToTurnIn(QuestManager questManager, string speakerName)
        {
            var active = questManager.Active;
            for (int i = 0; i < active.Count; i++)
            {
                QuestSaveData quest = active[i];

                    if (quest.State != QuestState.ReadyToTurnIn) continue;

                QuestDefinition questDefinition = QuestRegistry.Get()?.Find(quest.QuestId);

                    if (questDefinition == null) continue;

                    if (string.IsNullOrWhiteSpace(questDefinition.GiverNpcName)) continue;

                    if (QuestManager.NpcReferenceMatches(questDefinition.GiverNpcName, speakerName))

                return true;
            }
            return false;
        }

        private static bool HasDeliverableForNpc(QuestManager questManager, string speakerName, Interactor interactor)
        {
            if (interactor?.Inventory == null) return false;
            var active = questManager.Active;
            for (int i = 0; i < active.Count; i++)
            {
                QuestSaveData quest = active[i];

                    if (quest.State != QuestState.Active) continue;

                QuestDefinition questDefinition = QuestRegistry.Get()?.Find(quest.QuestId);

                    if (questDefinition == null) continue;

                    if (quest.CurrentObjectiveIndex < 0 || quest.CurrentObjectiveIndex >= questDefinition.Objectives.Count) continue;

                QuestObjective obj = questDefinition.Objectives[quest.CurrentObjectiveIndex];

                    if (obj.Type != QuestObjectiveType.DeliverItem) continue;

                    if (!QuestManager.NpcReferenceMatches(obj.NpcName, speakerName)) continue;

                    if (CanPayGoldObjective(interactor.Inventory, obj)) return true;

                    if (PlayerHasAnyDeliverableItem(interactor.Inventory, obj)) return true;
            }
            return false;
        }

        private static bool HasGoldPaymentForNpc(QuestManager questManager, string speakerName, Interactor interactor)
        {
            if (interactor?.Inventory == null) return false;
            var active = questManager.Active;
            for (int i = 0; i < active.Count; i++)
            {
                QuestSaveData quest = active[i];
                if (quest.State != QuestState.Active) continue;

                QuestDefinition questDefinition = QuestRegistry.Get()?.Find(quest.QuestId);
                if (questDefinition == null) continue;
                if (quest.CurrentObjectiveIndex < 0 || quest.CurrentObjectiveIndex >= questDefinition.Objectives.Count) continue;

                QuestObjective obj = questDefinition.Objectives[quest.CurrentObjectiveIndex];
                if (obj.Type != QuestObjectiveType.DeliverItem) continue;
                if (!QuestManager.NpcReferenceMatches(obj.NpcName, speakerName)) continue;

                if (CanPayGoldObjective(interactor.Inventory, obj))
                    return true;
            }

            return false;
        }

        private static bool CanPayGoldObjective(Inventory inv, QuestObjective objective)
        {
            if (inv == null || objective == null)
                return false;

            int goldAmount = objective.GetRequiredGoldPaymentAmount();
            return goldAmount > 0 && inv.Gold >= goldAmount;
        }

        private static bool PlayerHasAnyDeliverableItem(Inventory inv, QuestObjective objective)
        {
            if (inv == null || objective == null) return false;

            if (objective.IsGoldPaymentObjective())
                return false;

            var slots = inv.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s?.Item == null) continue;
                if (objective.MatchesAnyAcceptableItemId(s.Item.ItemId))
                    return true;
            }
            return false;
        }

        private void OfferNextQuest(string speakerName)
        {
            QuestManager questManager = QuestManager.Instance;

            if (questManager == null) return;

            var offerable = questManager.GetOfferableQuests(speakerName);

            if (offerable.Count == 0) return;

            QuestDefinition questDefinition = offerable[0];
            Sol.HUD.ConfirmationPromptSystem confirmationPrompt = Sol.HUD.ConfirmationPromptSystem.Instance;

            if (confirmationPrompt == null) 
            { 
                questManager.TryAccept(questDefinition.QuestId, speakerName); return; 
            }

            confirmationPrompt.Show(
                questDefinition.Title,
                questDefinition.Summary,
                confirmAction: () => questManager.TryAccept(questDefinition.QuestId, speakerName),
                cancelAction: null,
                confirmLabel: "Accept",
                cancelLabel: "Decline",
                icon: _speakerIcon);
        }

        private void TurnInReadyQuest(string speakerName)
        {
            QuestManager questManager = QuestManager.Instance;

            if (questManager == null) return;

            var active = questManager.Active;
            QuestSaveData target = null;
            QuestDefinition targetDef = null;

            for (int i = 0; i < active.Count; i++)
            {
                QuestSaveData quest = active[i];
                if (quest.State != QuestState.ReadyToTurnIn) continue;
                QuestDefinition questDefinition = QuestRegistry.Get()?.Find(quest.QuestId);
                if (questDefinition == null) continue;
                if (!QuestManager.NpcReferenceMatches(questDefinition.GiverNpcName, speakerName)) continue;
                target = quest; targetDef = questDefinition; break;
            }

            if (target == null || targetDef == null) return;

            Sol.HUD.ConfirmationPromptSystem confirmationPrompt = Sol.HUD.ConfirmationPromptSystem.Instance;

            string desc = $"Turn in \"{targetDef.Title}\"?";

            if (targetDef.Reward != null && (targetDef.Reward.Gold > 0 || (targetDef.Reward.Items != null && targetDef.Reward.Items.Count > 0)))
                desc += $"\nReward: {targetDef.Reward.Gold} gold" + (targetDef.Reward.Items.Count > 0 ? " + items" : string.Empty);

            if (confirmationPrompt == null) { questManager.TryTurnIn(target.QuestId, speakerName); return; }

            confirmationPrompt.Show(
                targetDef.Title,
                desc,
                confirmAction: () => questManager.TryTurnIn(target.QuestId, speakerName),
                cancelAction: null,
                confirmLabel: "Turn in",
                cancelLabel: "Not yet",
                icon: _speakerIcon);
        }

        private void DeliverItemToNpc(string speakerName, Interactor interactor)
        {
            QuestManager questManager = QuestManager.Instance;
            if (questManager == null || interactor?.Inventory == null || _inventory == null) return;

            var active = questManager.Active;
            for (int i = 0; i < active.Count; i++)
            {
                QuestSaveData quest = active[i];

                if (quest.State != QuestState.Active) continue;
                

                QuestDefinition questDefinition = QuestRegistry.Get()?.Find(quest.QuestId);

                if (questDefinition == null) continue;

                if (quest.CurrentObjectiveIndex < 0 || quest.CurrentObjectiveIndex >= questDefinition.Objectives.Count) continue;


                QuestObjective obj = questDefinition.Objectives[quest.CurrentObjectiveIndex];

                if (obj.Type != QuestObjectiveType.DeliverItem) continue;

                if (!QuestManager.NpcReferenceMatches(obj.NpcName, speakerName)) continue;

                if (TryPayGoldObjective(questManager, speakerName, interactor, obj))
                    return;

 // Transfer one matching item from player - this NPC.
                var slots = interactor.Inventory.Slots;
                for (int s = 0; s < slots.Count; s++)
                {
                    var slot = slots[s];
                    if (slot?.Item == null) continue;
                    if (!obj.MatchesAnyAcceptableItemId(slot.Item.ItemId)) continue;
                    ItemComponent item = slot.Item;
                    string deliveredItemId = item.ItemId;
                    interactor.Inventory.Remove(slot, 1);
                    _inventory.Add(item);
                    questManager.NotifyDeliveredItem(speakerName, deliveredItemId);
                    return;
                }
            }
        }

        private bool TryPayGoldObjective(
            QuestManager questManager,
            string speakerName,
            Interactor interactor,
            QuestObjective objective)
        {
            int goldAmount = objective.GetRequiredGoldPaymentAmount();
            if (goldAmount <= 0 || interactor?.Inventory == null || interactor.Inventory.Gold < goldAmount)
                return false;

            interactor.Inventory.Gold -= goldAmount;
            _inventory.Gold += goldAmount;
            Sol.Audio.AudioService.Instance?.PlaySfx(
                Sol.Audio.AudioEvent.GoldSpent,
                interactor.Transform != null ? interactor.Transform.position : transform.position);
            questManager.NotifyPaidGoldToNpc(speakerName, goldAmount);
            return true;
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
