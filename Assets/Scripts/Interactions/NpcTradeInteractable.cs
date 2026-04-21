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
    public class NpcTradeInteractable : MonoBehaviour, IInteractable
    {
        private const string DefaultGoldPrefabPath = "Assets/ItemPrefabs/Gold.prefab";

        private enum ConversationOptionId
        {
            Trade,
            QuestOffer,
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
        [Tooltip("Gold item prefab used to convert numeric NPC gold into physical corpse loot.")]
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

 // Quest conversation options - order: turn-in, deliver, offer, then goodbye.
            QuestManager qm = QuestManager.Instance;
            if (qm != null)
            {
                if (HasQuestReadyToTurnIn(qm, speakerName))
                    AddConversationOption(ConversationOptionId.QuestTurnIn, optionIds, optionLabels);
                if (HasDeliverableForNpc(qm, speakerName, interactor))
                    AddConversationOption(ConversationOptionId.QuestDeliver, optionIds, optionLabels);
                if (qm.GetOfferableQuests(speakerName).Count > 0)
                    AddConversationOption(ConversationOptionId.QuestOffer, optionIds, optionLabels);
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
                ConversationOptionId.QuestOffer => "Any work?",
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

            switch (optionIds[optionIndex])
            {
                case ConversationOptionId.Trade:
                    OpenTradeFromConversation(interactor);
                    break;
                case ConversationOptionId.QuestOffer:
                    OfferNextQuest(speakerName);
                    break;
                case ConversationOptionId.QuestTurnIn:
                    TurnInReadyQuest(speakerName);
                    break;
                case ConversationOptionId.QuestDeliver:
                    DeliverItemToNpc(speakerName, interactor);
                    break;
            }
        }

        // ----- Quest helpers -----

        private static bool HasQuestReadyToTurnIn(QuestManager qm, string speakerName)
        {
            var active = qm.Active;
            for (int i = 0; i < active.Count; i++)
            {
                QuestSaveData q = active[i];
                if (q.State != QuestState.ReadyToTurnIn) continue;
                QuestDefinition def = QuestRegistry.Get()?.Find(q.QuestId);
                if (def == null) continue;
                if (string.IsNullOrWhiteSpace(def.GiverNpcName)) continue;
                if (string.Equals(def.GiverNpcName, speakerName, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool HasDeliverableForNpc(QuestManager qm, string speakerName, Interactor interactor)
        {
            if (interactor?.Inventory == null) return false;
            var active = qm.Active;
            for (int i = 0; i < active.Count; i++)
            {
                QuestSaveData q = active[i];
                if (q.State != QuestState.Active) continue;
                QuestDefinition def = QuestRegistry.Get()?.Find(q.QuestId);
                if (def == null) continue;
                if (q.CurrentObjectiveIndex < 0 || q.CurrentObjectiveIndex >= def.Objectives.Count) continue;
                QuestObjective obj = def.Objectives[q.CurrentObjectiveIndex];
                if (obj.Type != QuestObjectiveType.DeliverItem) continue;
                if (!string.Equals(obj.NpcName, speakerName, System.StringComparison.OrdinalIgnoreCase)) continue;
                if (PlayerHasItem(interactor.Inventory, obj.ItemId)) return true;
            }
            return false;
        }

        private static bool PlayerHasItem(Inventory inv, string itemId)
        {
            if (inv == null || string.IsNullOrWhiteSpace(itemId)) return false;
            var slots = inv.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s?.Item == null) continue;
                if (string.Equals(s.Item.ItemId, itemId, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private void OfferNextQuest(string speakerName)
        {
            QuestManager qm = QuestManager.Instance;
            if (qm == null) return;
            var offerable = qm.GetOfferableQuests(speakerName);
            if (offerable.Count == 0) return;
            QuestDefinition def = offerable[0];
            Sol.HUD.DialoguePromptSystem dlg = Sol.HUD.DialoguePromptSystem.Instance;
            if (dlg == null) { qm.TryAccept(def.QuestId, speakerName); return; }
            dlg.Show(
                def.Title,
                def.Summary,
                confirmAction: () => qm.TryAccept(def.QuestId, speakerName),
                cancelAction: null,
                confirmLabel: "Accept",
                cancelLabel: "Decline",
                icon: _speakerIcon);
        }

        private void TurnInReadyQuest(string speakerName)
        {
            QuestManager qm = QuestManager.Instance;
            if (qm == null) return;
            var active = qm.Active;
            QuestSaveData target = null;
            QuestDefinition targetDef = null;
            for (int i = 0; i < active.Count; i++)
            {
                QuestSaveData q = active[i];
                if (q.State != QuestState.ReadyToTurnIn) continue;
                QuestDefinition def = QuestRegistry.Get()?.Find(q.QuestId);
                if (def == null) continue;
                if (!string.Equals(def.GiverNpcName, speakerName, System.StringComparison.OrdinalIgnoreCase)) continue;
                target = q; targetDef = def; break;
            }
            if (target == null || targetDef == null) return;
            Sol.HUD.DialoguePromptSystem dlg = Sol.HUD.DialoguePromptSystem.Instance;
            string desc = $"Turn in \"{targetDef.Title}\"?";
            if (targetDef.Reward != null && (targetDef.Reward.Gold > 0 || (targetDef.Reward.Items != null && targetDef.Reward.Items.Count > 0)))
                desc += $"\nReward: {targetDef.Reward.Gold} gold" + (targetDef.Reward.Items.Count > 0 ? " + items" : string.Empty);
            if (dlg == null) { qm.TryTurnIn(target.QuestId, speakerName); return; }
            dlg.Show(
                targetDef.Title,
                desc,
                confirmAction: () => qm.TryTurnIn(target.QuestId, speakerName),
                cancelAction: null,
                confirmLabel: "Turn in",
                cancelLabel: "Not yet",
                icon: _speakerIcon);
        }

        private void DeliverItemToNpc(string speakerName, Interactor interactor)
        {
            QuestManager qm = QuestManager.Instance;
            if (qm == null || interactor?.Inventory == null || _inventory == null) return;

            var active = qm.Active;
            for (int i = 0; i < active.Count; i++)
            {
                QuestSaveData q = active[i];
                if (q.State != QuestState.Active) continue;
                QuestDefinition def = QuestRegistry.Get()?.Find(q.QuestId);
                if (def == null) continue;
                if (q.CurrentObjectiveIndex < 0 || q.CurrentObjectiveIndex >= def.Objectives.Count) continue;
                QuestObjective obj = def.Objectives[q.CurrentObjectiveIndex];
                if (obj.Type != QuestObjectiveType.DeliverItem) continue;
                if (!string.Equals(obj.NpcName, speakerName, System.StringComparison.OrdinalIgnoreCase)) continue;

 // Transfer one matching item from player - this NPC.
                var slots = interactor.Inventory.Slots;
                for (int s = 0; s < slots.Count; s++)
                {
                    var slot = slots[s];
                    if (slot?.Item == null) continue;
                    if (!string.Equals(slot.Item.ItemId, obj.ItemId, System.StringComparison.OrdinalIgnoreCase)) continue;
                    ItemComponent item = slot.Item;
                    interactor.Inventory.Remove(slot, 1);
                    _inventory.Add(item);
                    qm.NotifyDeliveredItem(speakerName, obj.ItemId);
                    return;
                }
            }
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
