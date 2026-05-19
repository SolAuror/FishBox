using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Sol;
using Sol.Actions;
using Sol.Grab;
using Sol.Quests;
using Sol.Rpg;
using UnityEngine;

namespace Sol.AI
{
    /// <summary>
    /// AI_NPC partial - conversation/dialogue surface and IInteractable wiring.
    /// Routes to the authored DialogueGraph when one is present, otherwise falls back
    /// to the flat Trade/Quest/Goodbye flow.
    /// </summary>
    public partial class AI_NPC
    {
        private enum ConversationOptionId
        {
            Trade,
            Quest,
            QuestTurnIn,
            QuestDeliver,
            Goodbye
        }

        private enum ConversationFlowState
        {
            Inactive,
            ShowingNode,
            ResolvingOption,
            OpeningTrade,
            ShowingQuestPrompt,
            TransitioningNode,
            Closed
        }

        #region Conversation Inspector Settings

        [Header("Interaction Prompts")]
        [SerializeField] private string _prompt = "Trade";
        [SerializeField] private string _lootPrompt = "Loot";
        [SerializeField] private string _talkPrompt = "Talk";

        [Header("Conversation")]
        [SerializeField] private bool _useConversationWindow = true;
        [SerializeField] private string _greetingLine = "What can I do for you?";
        [SerializeField] private string _tradeOptionLabel = "Trade";
        [SerializeField] private string _goodbyeOptionLabel = "Goodbye";
        [SerializeField] private Sprite _speakerIcon;

        [Header("Dialogue Graph")]
        [Tooltip("Authored branching dialogue graph. Empty graph (no nodes) ==> NPC uses the legacy flat conversation flow.")]
        [SerializeField] private DialogueGraph _dialogueGraph = new();
        [Tooltip("NPC-local conversation flags used by this NPC's dialogue graph.")]
        [SerializeField] private List<string> _conversationFlags = new();

        #endregion

        public DialogueGraph DialogueGraph => _dialogueGraph;

        public string InteractionPrompt =>
            IsLootingCorpse
                ? _lootPrompt
                : (_useConversationWindow ? _talkPrompt : _prompt);

        public bool CanInteract(Interactor interactor)
        {
            return interactor != null
                && interactor.IsPlayer
                && interactor.Inventory != null
                && inventory != null;
        }

        public GameAction GetInteraction(Interactor interactor)
        {
            if (interactor == null || interactor.Inventory == null || inventory == null)
                return null;

            if (IsLootingCorpse)
                return BuildLootAction(interactor);

            if (_useConversationWindow)
                return BuildConversationAction(interactor);

            return BuildTradeAction(interactor);
        }

        // ----- Conversation builders -----

        private GameAction BuildConversationAction(Interactor interactor)
        {
            BeginConversation();

            if (_dialogueGraph != null && _dialogueGraph.Nodes != null && _dialogueGraph.Nodes.Count > 0)
                return BuildAuthoredDialogueAction(interactor, _dialogueGraph.RootNodeId);

            return BuildLegacyConversationAction(interactor);
        }

        private GameAction BuildLegacyConversationAction(Interactor interactor)
        {
            List<ConversationOptionId> optionIds = new(4);
            List<string> optionLabels = new(4);
            Action pendingAfterClose = null;

            if (IsTrader)
                AddConversationOption(ConversationOptionId.Trade, optionIds, optionLabels);

            string speakerName = ResolveSpeakerName();
            string speakerReference = ResolveSpeakerReference();

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
                if (IsQuestGiver && questManager.GetOfferableQuests(speakerReference).Count > 0)
                    AddConversationOption(ConversationOptionId.Quest, optionIds, optionLabels);
            }

            AddConversationOption(ConversationOptionId.Goodbye, optionIds, optionLabels);

            return new OpenConversationAction(
                speakerName,
                _greetingLine,
                optionLabels,
                optionIndex =>
                {
                    HandleConversationOptionSelected(optionIndex, optionIds, interactor, out pendingAfterClose);
                    return true;
                },
                onClosed: () =>
                {
                    EndConversation();

                    Action pending = pendingAfterClose;
                    pendingAfterClose = null;
                    pending?.Invoke();
                },
                speakerIcon: _speakerIcon);
        }

        private GameAction BuildAuthoredDialogueAction(Interactor interactor, string nodeId)
        {
            ConversationSession session = new(this, interactor, nodeId);
            return session.BuildInitialAction();
        }

        private sealed class ConversationSession
        {
            private readonly AI_NPC _npc;
            private readonly Interactor _interactor;
            private readonly List<DialogueOption> _visibleOptions = new();
            private readonly List<string> _optionLabels = new();
            private string _currentNodeId;
            private Action _pendingAfterClose;
            private bool _closed;

            public ConversationFlowState State { get; private set; } = ConversationFlowState.Inactive;

            public ConversationSession(AI_NPC npc, Interactor interactor, string nodeId)
            {
                _npc = npc;
                _interactor = interactor;
                _currentNodeId = nodeId;
            }

            public GameAction BuildInitialAction()
            {
                DialogueNode node = PrepareNode(_currentNodeId);
                if (node == null)
                {
                    State = ConversationFlowState.Closed;
                    _npc.EndConversation();
                    return null;
                }

                State = ConversationFlowState.ShowingNode;
                return new OpenConversationAction(
                    _npc.ResolveSpeakerName(),
                    node.SpeakerLine,
                    _optionLabels,
                    SelectOption,
                    OnClosed,
                    _npc._speakerIcon);
            }

            private DialogueNode PrepareNode(string nodeId)
            {
                DialogueNode node = _npc.ResolveNode(nodeId);
                if (node == null)
                    return null;

                _currentNodeId = node.NodeId;
                _visibleOptions.Clear();
                _optionLabels.Clear();
                _npc.BuildVisibleOptions(node, _visibleOptions, _optionLabels);
                return node;
            }

            private bool SelectOption(int optionIndex)
            {
                State = ConversationFlowState.ResolvingOption;

                if (optionIndex < 0 || optionIndex >= _visibleOptions.Count)
                {
                    State = ConversationFlowState.Closed;
                    return true;
                }

                DialogueOption option = _visibleOptions[optionIndex];
                _npc.ApplyDialogueFlagMutation(option);

                if (option.Action == DialogueOptionAction.OpenTrade)
                {
                    if (_npc.IsTrader)
                    {
                        State = ConversationFlowState.OpeningTrade;
                        _pendingAfterClose = () => _npc.OpenTradeFromConversation(_interactor);
                    }
                    else
                    {
                        State = ConversationFlowState.Closed;
                    }

                    return true;
                }

                DialogueNode node = _npc.ResolveNode(_currentNodeId);
                bool shouldEnd = _npc.HandleAuthoredOption(
                    _interactor,
                    node,
                    option,
                    out string nextNodeId,
                    applyFlagMutation: false);

                if (!shouldEnd && !string.IsNullOrWhiteSpace(nextNodeId))
                {
                    State = ConversationFlowState.TransitioningNode;
                    return !ShowNode(nextNodeId);
                }

                State = IsQuestAction(option.Action) ? ConversationFlowState.ShowingQuestPrompt : ConversationFlowState.Closed;
                return true;
            }

            private bool ShowNode(string nodeId)
            {
                DialogueNode node = PrepareNode(nodeId);
                if (node == null)
                {
                    State = ConversationFlowState.Closed;
                    _npc.EndConversation();
                    return false;
                }

                HUD.ConversationWindowSystem ui = HUD.ConversationWindowSystem.ResolveInstance(true);
                if (ui == null)
                {
                    State = ConversationFlowState.Closed;
                    _npc.EndConversation();
                    return false;
                }

                State = ConversationFlowState.ShowingNode;
                ui.ShowConversation(
                    _npc.ResolveSpeakerName(),
                    node.SpeakerLine,
                    _optionLabels,
                    SelectOption,
                    OnClosed,
                    _npc._speakerIcon);
                return true;
            }

            private void OnClosed()
            {
                if (_closed)
                    return;

                _closed = true;
                State = ConversationFlowState.Closed;
                _npc.EndConversation();

                Action pending = _pendingAfterClose;
                _pendingAfterClose = null;
                pending?.Invoke();
            }

            private static bool IsQuestAction(DialogueOptionAction action)
            {
                return action == DialogueOptionAction.OfferQuest
                    || action == DialogueOptionAction.TurnInQuest
                    || action == DialogueOptionAction.DeliverItemForQuest
                    || action == DialogueOptionAction.NotifyTalkedAboutTopic;
            }
        }

        private DialogueNode ResolveNode(string nodeId)
        {
            if (_dialogueGraph?.Nodes == null || _dialogueGraph.Nodes.Count == 0)
                return null;

            string target = string.IsNullOrWhiteSpace(nodeId) ? _dialogueGraph.RootNodeId : nodeId;
            for (int i = 0; i < _dialogueGraph.Nodes.Count; i++)
            {
                DialogueNode n = _dialogueGraph.Nodes[i];
                if (n != null
                    && string.Equals(n.NodeId, target, System.StringComparison.OrdinalIgnoreCase)
                    && IsNodeVisible(n))
                {
                    return n;
                }
            }

            for (int i = 0; i < _dialogueGraph.Nodes.Count; i++)
            {
                DialogueNode n = _dialogueGraph.Nodes[i];
                if (n != null && IsNodeVisible(n))
                    return n;
            }

            return _dialogueGraph.Nodes[0];
        }

        private bool IsNodeVisible(DialogueNode node)
        {
            return node == null || IsOptionVisible(node.Visibility, null);
        }

        // ----- Legacy enum-based option handling -----

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
                _ => _goodbyeOptionLabel,
            };
        }

        private void HandleConversationOptionSelected(
            int optionIndex,
            IReadOnlyList<ConversationOptionId> optionIds,
            Interactor interactor,
            out Action pendingAfterClose)
        {
            pendingAfterClose = null;

            if (optionIds == null || optionIndex < 0 || optionIndex >= optionIds.Count || interactor == null)
                return;

            string speakerReference = ResolveSpeakerReference();

            switch (optionIds[optionIndex])
            {
                case ConversationOptionId.Trade:
                    pendingAfterClose = () => OpenTradeFromConversation(interactor);
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

        // ----- Authored option handling -----

        private bool HandleAuthoredOption(
            Interactor interactor,
            DialogueNode node,
            DialogueOption opt,
            out string nextNodeId,
            bool applyFlagMutation = true)
        {
            nextNodeId = null;

            if (opt == null)
                return true; // end

            if (applyFlagMutation)
                ApplyDialogueFlagMutation(opt);

            string speakerReference = ResolveSpeakerReference();

            switch (opt.Action)
            {
                case DialogueOptionAction.EndConversation:
                    return true;

                case DialogueOptionAction.OpenTrade:
                    // ConversationSession opens trade after the UI close path completes.
                    return true;

                case DialogueOptionAction.OfferQuest:
                    OfferAuthoredQuest(speakerReference, opt.QuestId);
                    return true;

                case DialogueOptionAction.TurnInQuest:
                    TurnInAuthoredQuest(speakerReference, opt.QuestId);
                    return true;

                case DialogueOptionAction.DeliverItemForQuest:
                    DeliverItemToNpc(speakerReference, interactor);
                    return true;

                case DialogueOptionAction.NotifyTalkedAboutTopic:
                    QuestManager.Instance?.NotifyTalkedAboutTopic(speakerReference, opt.Topic);
                    nextNodeId = opt.NextNodeId;
                    return string.IsNullOrWhiteSpace(nextNodeId);

                case DialogueOptionAction.GoToNode:
                default:
                    nextNodeId = opt.NextNodeId;
                    return string.IsNullOrWhiteSpace(nextNodeId);
            }
        }

        public bool HasConversationFlag(string flag)
        {
            string normalized = NormalizeConversationFlag(flag);
            if (string.IsNullOrEmpty(normalized) || _conversationFlags == null)
                return false;

            for (int i = 0; i < _conversationFlags.Count; i++)
            {
                if (string.Equals(NormalizeConversationFlag(_conversationFlags[i]), normalized, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public void SetConversationFlag(string flag, bool enabled)
        {
            string normalized = NormalizeConversationFlag(flag);
            if (string.IsNullOrEmpty(normalized))
                return;

            _conversationFlags ??= new List<string>();
            int index = FindConversationFlagIndex(normalized);
            if (enabled)
            {
                if (index < 0)
                    _conversationFlags.Add(normalized);
                return;
            }

            if (index >= 0)
                _conversationFlags.RemoveAt(index);
        }

        public void ToggleConversationFlag(string flag)
        {
            SetConversationFlag(flag, !HasConversationFlag(flag));
        }

        public List<string> CollectConversationFlags()
        {
            List<string> result = new();
            if (_conversationFlags == null)
                return result;

            for (int i = 0; i < _conversationFlags.Count; i++)
            {
                string normalized = NormalizeConversationFlag(_conversationFlags[i]);
                if (!string.IsNullOrEmpty(normalized) && !ContainsFlag(result, normalized))
                    result.Add(normalized);
            }

            return result;
        }

        public void ApplyConversationFlags(IReadOnlyList<string> flags)
        {
            _conversationFlags ??= new List<string>();
            _conversationFlags.Clear();
            if (flags == null)
                return;

            for (int i = 0; i < flags.Count; i++)
            {
                string normalized = NormalizeConversationFlag(flags[i]);
                if (!string.IsNullOrEmpty(normalized) && !ContainsFlag(_conversationFlags, normalized))
                    _conversationFlags.Add(normalized);
            }
        }

        private void ApplyDialogueFlagMutation(DialogueOption opt)
        {
            if (opt == null || opt.FlagMutation == DialogueFlagMutation.None)
                return;

            switch (opt.FlagMutation)
            {
                case DialogueFlagMutation.Set:
                    SetConversationFlag(opt.LocalFlag, true);
                    break;
                case DialogueFlagMutation.Clear:
                    SetConversationFlag(opt.LocalFlag, false);
                    break;
                case DialogueFlagMutation.Toggle:
                    ToggleConversationFlag(opt.LocalFlag);
                    break;
            }
        }

        private int FindConversationFlagIndex(string normalizedFlag)
        {
            if (_conversationFlags == null)
                return -1;

            for (int i = 0; i < _conversationFlags.Count; i++)
            {
                if (string.Equals(NormalizeConversationFlag(_conversationFlags[i]), normalizedFlag, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static bool ContainsFlag(IReadOnlyList<string> flags, string normalizedFlag)
        {
            if (flags == null)
                return false;

            for (int i = 0; i < flags.Count; i++)
            {
                if (string.Equals(NormalizeConversationFlag(flags[i]), normalizedFlag, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string NormalizeConversationFlag(string flag)
        {
            return string.IsNullOrWhiteSpace(flag) ? string.Empty : flag.Trim();
        }

        private void BuildVisibleOptions(
            DialogueNode node,
            List<DialogueOption> visibleOptions,
            List<string> optionLabels)
        {
            if (node?.Options == null) return;

            for (int i = 0; i < node.Options.Count; i++)
            {
                DialogueOption opt = node.Options[i];
                if (opt == null) continue;

                if (!IsOptionVisible(opt.Visibility, opt.LocalFlag)) continue;
                if (IsTradeOption(opt.Action) && !IsTrader) continue;
                if (opt.Action == DialogueOptionAction.OfferQuest && !IsQuestGiver) continue;

                visibleOptions.Add(opt);
                optionLabels.Add(string.IsNullOrWhiteSpace(opt.Label)
                    ? FallbackLabelForAction(opt.Action)
                    : opt.Label);
            }
        }

        // ----- Visibility -----

        private static bool IsTradeOption(DialogueOptionAction action)
        {
            return action == DialogueOptionAction.OpenTrade;
        }

        private static string FallbackLabelForAction(DialogueOptionAction action)
        {
            return action switch
            {
                DialogueOptionAction.EndConversation => "Goodbye",
                DialogueOptionAction.OpenTrade => "Trade",
                DialogueOptionAction.OfferQuest => "Quest",
                DialogueOptionAction.TurnInQuest => "Turn in quest",
                DialogueOptionAction.DeliverItemForQuest => "Deliver item",
                DialogueOptionAction.NotifyTalkedAboutTopic => "...",
                _ => "...",
            };
        }

        private bool IsOptionVisible(DialogueOptionVisibility visibility, string localFlag)
        {
            if (visibility == null)
                return true;

            if (!TagVisibilityPasses(visibility))
                return false;

            if (visibility.Rule == DialogueVisibilityRule.Always)
                return true;

            if (visibility.Rule == DialogueVisibilityRule.LocalFlagSet)
                return HasConversationFlag(localFlag);

            if (visibility.Rule == DialogueVisibilityRule.LocalFlagNotSet)
                return !HasConversationFlag(localFlag);

            if (visibility.Rule == DialogueVisibilityRule.ScheduleActivity)
                return IsCurrentScheduleActivity(visibility.ScheduleActivity);

            if (visibility.Rule == DialogueVisibilityRule.ScheduleLocation)
                return IsCurrentScheduleLocation(visibility.ScheduleLocationId);

            if (visibility.Rule == DialogueVisibilityRule.TimeWindow)
                return IsScheduleHourInWindow(visibility.StartHour, visibility.EndHour);

            QuestManager qm = QuestManager.Instance;
            if (qm == null)
                return visibility.Rule == DialogueVisibilityRule.QuestNotStarted;

            string questId = visibility.QuestId;
            if (string.IsNullOrWhiteSpace(questId))
                return true;

            QuestSaveData active = FindActiveSaveData(qm, questId);
            bool isCompleted = qm.IsCompleted(questId);

            switch (visibility.Rule)
            {
                case DialogueVisibilityRule.QuestActive:
                    return active != null && active.State == QuestState.Active;
                case DialogueVisibilityRule.QuestNotStarted:
                    return active == null && !isCompleted;
                case DialogueVisibilityRule.QuestReadyToTurnIn:
                    return active != null && active.State == QuestState.ReadyToTurnIn;
                case DialogueVisibilityRule.QuestCompleted:
                    return isCompleted;
                case DialogueVisibilityRule.QuestOnObjective:
                    if (active == null || active.State != QuestState.Active)
                        return false;
                    if (visibility.RequiredObjectiveIndex < 0)
                        return true;
                    return active.CurrentObjectiveIndex == visibility.RequiredObjectiveIndex;
                default:
                    return true;
            }
        }

        public bool IsDialogueVisibilityVisibleForTests(DialogueOptionVisibility visibility, string localFlag = null)
        {
            return IsOptionVisible(visibility, localFlag);
        }

        private bool TagVisibilityPasses(DialogueOptionVisibility visibility)
        {
            GameplayTagSet speakerTags = soul != null ? soul.Tags : GameplayTagSet.Empty;
            if (visibility.RequiredSpeakerTags != null
                && !visibility.RequiredSpeakerTags.IsEmpty()
                && !speakerTags.HasAllTagsOrChildren(visibility.RequiredSpeakerTags))
            {
                return false;
            }

            if (visibility.ForbiddenSpeakerTags != null
                && speakerTags.HasAnyTagOrChild(visibility.ForbiddenSpeakerTags))
            {
                return false;
            }

            GameplayTagSet playerTags = ResolvePlayerTags();
            if (visibility.RequiredPlayerTags != null
                && !visibility.RequiredPlayerTags.IsEmpty()
                && !playerTags.HasAllTagsOrChildren(visibility.RequiredPlayerTags))
            {
                return false;
            }

            return visibility.ForbiddenPlayerTags == null
                || !playerTags.HasAnyTagOrChild(visibility.ForbiddenPlayerTags);
        }

        private static GameplayTagSet ResolvePlayerTags()
        {
            Sol.Player.PlayerSoul player = FindFirstObjectByType<Sol.Player.PlayerSoul>();
            return player != null ? player.Tags : GameplayTagSet.Empty;
        }

        private static QuestSaveData FindActiveSaveData(QuestManager qm, string questId)
        {
            var active = qm.Active;
            for (int i = 0; i < active.Count; i++)
            {
                if (string.Equals(active[i].QuestId, questId, System.StringComparison.OrdinalIgnoreCase))
                    return active[i];
            }
            return null;
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
            if (!IsQuestGiver)
                return;

            QuestManager questManager = QuestManager.Instance;
            if (questManager == null) return;

            var offerable = questManager.GetOfferableQuests(speakerName);
            if (offerable.Count == 0) return;

            QuestDefinition questDefinition = offerable[0];
            ShowQuestOfferPrompt(questManager, speakerName, questDefinition);
        }

        private void OfferAuthoredQuest(string speakerName, string questId)
        {
            if (!IsQuestGiver)
                return;

            QuestManager questManager = QuestManager.Instance;
            if (questManager == null) return;

            QuestDefinition def = QuestRegistry.Get()?.Find(questId);
            if (def == null) return;

            // Only offer if the quest is currently offerable from this speaker.
            var offerable = questManager.GetOfferableQuests(speakerName);
            bool found = false;
            for (int i = 0; i < offerable.Count; i++)
            {
                if (string.Equals(offerable[i].QuestId, questId, System.StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
            if (!found) return;

            ShowQuestOfferPrompt(questManager, speakerName, def);
        }

        private void ShowQuestOfferPrompt(QuestManager questManager, string speakerName, QuestDefinition def)
        {
            HUD.ConfirmationPromptSystem confirmationPrompt = HUD.ConfirmationPromptSystem.Instance;
            if (confirmationPrompt == null)
            {
                questManager.TryAccept(def.QuestId, speakerName);
                return;
            }

            confirmationPrompt.Show(
                def.Title,
                def.Summary,
                confirmAction: () => questManager.TryAccept(def.QuestId, speakerName),
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
            ShowTurnInPrompt(questManager, speakerName, target, targetDef);
        }

        private void TurnInAuthoredQuest(string speakerName, string questId)
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
                if (!string.Equals(quest.QuestId, questId, System.StringComparison.OrdinalIgnoreCase)) continue;

                QuestDefinition def = QuestRegistry.Get()?.Find(quest.QuestId);
                if (def == null) continue;

                target = quest; targetDef = def; break;
            }

            if (target == null || targetDef == null) return;
            ShowTurnInPrompt(questManager, speakerName, target, targetDef);
        }

        private void ShowTurnInPrompt(QuestManager questManager, string speakerName, QuestSaveData target, QuestDefinition targetDef)
        {
            HUD.ConfirmationPromptSystem confirmationPrompt = HUD.ConfirmationPromptSystem.Instance;

            string desc = $"Turn in \"{targetDef.Title}\"?";
            if (targetDef.Reward != null && (targetDef.Reward.Gold > 0 || (targetDef.Reward.Items != null && targetDef.Reward.Items.Count > 0)))
                desc += $"\nReward: {targetDef.Reward.Gold} gold" + (targetDef.Reward.Items.Count > 0 ? " + items" : string.Empty);

            if (confirmationPrompt == null)
            {
                questManager.TryTurnIn(target.QuestId, speakerName);
                return;
            }

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
            if (questManager == null || interactor?.Inventory == null || inventory == null) return;

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

                var slots = interactor.Inventory.Slots;
                for (int s = 0; s < slots.Count; s++)
                {
                    var slot = slots[s];
                    if (slot?.Item == null) continue;
                    if (!obj.MatchesAnyAcceptableItemId(slot.Item.ItemId)) continue;
                    ItemComponent item = slot.Item;
                    string deliveredItemId = item.ItemId;
                    interactor.Inventory.Remove(slot, 1);
                    inventory.Add(item);
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
            inventory.Gold += goldAmount;
            Audio.AudioService.Instance?.PlaySfx(
                Audio.AudioEvent.GoldSpent,
                interactor.Transform != null ? interactor.Transform.position : transform.position);
            questManager.NotifyPaidGoldToNpc(speakerName, goldAmount);
            return true;
        }

        // ----- Speaker name/reference helpers -----

        private string ResolveSpeakerName()
        {
            return soul != null && !string.IsNullOrWhiteSpace(soul.CharacterName)
                ? soul.CharacterName
                : gameObject.name;
        }

        private string ResolveSpeakerReference()
        {
            return soul != null && !string.IsNullOrWhiteSpace(soul.OwnerId)
                ? soul.OwnerId
                : ResolveSpeakerName();
        }
    }
}
