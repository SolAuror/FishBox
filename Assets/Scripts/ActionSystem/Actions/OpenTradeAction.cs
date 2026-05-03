using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sol.Actions
{
    /// <summary>
    /// Discrete action: open the trade UI between the actor and an NPC.
    /// Completes immediately because the trade panel is a UI concern.
    /// </summary>
    public class OpenTradeAction : InteractionAction
    {
        private readonly Inventory _playerInventory;
        private readonly Inventory _npcInventory;
        private readonly bool _lootMode;
        private readonly bool _freeTrade;
        private readonly Action _onOpened;

        public bool Succeeded { get; private set; }

        public OpenTradeAction(
            Inventory playerInventory,
            Inventory npcInventory,
            bool lootMode = false,
            bool freeTrade = false,
            Action onOpened = null)
        {
            _playerInventory = playerInventory;
            _npcInventory = npcInventory;
            _lootMode = lootMode;
            _freeTrade = freeTrade;
            _onOpened = onOpened;
        }

        public override bool CanExecute()
        {
            return _playerInventory != null && _npcInventory != null;
        }

        public override void OnStart()
        {
            HUD.TradeUI tradeUi = HUD.TradeUI.ResolveInstance();
            if (tradeUi == null)
            {
                Debug.LogWarning("[OpenTradeAction] TradeUI instance is unavailable.");
                Cancel();
                return;
            }

            if (_lootMode)
                tradeUi.OpenLoot(_playerInventory, _npcInventory);
            else
                tradeUi.OpenTrade(_playerInventory, _npcInventory, _freeTrade);

            Succeeded = tradeUi.IsOpen;
            if (!Succeeded)
            {
                Cancel();
                return;
            }

            _onOpened?.Invoke();
            Complete();
        }
    }

    /// <summary>
    /// Opens the conversation UI with a list of selectable responses.
    /// </summary>
    public class OpenConversationAction : InteractionAction
    {
        private readonly string _speakerName;
        private readonly string _dialogueLine;
        private readonly IReadOnlyList<string> _options;
        private readonly Func<int, bool> _onOptionSelected;
        private readonly Action _onClosed;
        private readonly Sprite _speakerIcon;

        public bool Succeeded { get; private set; }

        public OpenConversationAction(
            string speakerName,
            string dialogueLine,
            IReadOnlyList<string> options,
            Func<int, bool> onOptionSelected,
            Action onClosed = null,
            Sprite speakerIcon = null)
        {
            _speakerName = speakerName;
            _dialogueLine = dialogueLine;
            _options = options;
            _onOptionSelected = onOptionSelected;
            _onClosed = onClosed;
            _speakerIcon = speakerIcon;
        }

        public override bool CanExecute()
        {
            return true;
        }

        public override void OnStart()
        {
            HUD.ConversationWindowSystem conversationUi = ResolveConversationUi();
            if (conversationUi == null)
            {
                Debug.LogWarning("[OpenConversationAction] ConversationWindowSystem instance not found.");
                Cancel();
                return;
            }

            conversationUi.ShowConversation(
                _speakerName,
                _dialogueLine,
                _options,
                _onOptionSelected,
                _onClosed,
                _speakerIcon,
                Target != null ? Target.transform : null,
                Context != null ? Context.Transform : null);

            Succeeded = conversationUi.IsVisible;
            if (!Succeeded)
            {
                Cancel();
                return;
            }

            Complete();
        }

        private static HUD.ConversationWindowSystem ResolveConversationUi()
        {
            if (HUD.ConversationWindowSystem.Instance != null)
                return HUD.ConversationWindowSystem.Instance;

            HUD.ConversationWindowSystem[] found = UnityEngine.Object.FindObjectsByType<HUD.ConversationWindowSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (found == null || found.Length == 0)
                return null;

            HUD.ConversationWindowSystem resolved = found[0];
            if (resolved != null && !resolved.gameObject.activeSelf)
                resolved.gameObject.SetActive(true);

            return HUD.ConversationWindowSystem.Instance ?? resolved;
        }
    }
}
