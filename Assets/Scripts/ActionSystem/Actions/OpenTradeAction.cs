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

            tradeUi.Open(_playerInventory, _npcInventory, _lootMode, _freeTrade);
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
        private readonly Action<int> _onOptionSelected;
        private readonly Action _onClosed;
        private readonly Sprite _speakerIcon;

        public bool Succeeded { get; private set; }

        public OpenConversationAction(
            string speakerName,
            string dialogueLine,
            IReadOnlyList<string> options,
            Action<int> onOptionSelected,
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
            HUD.ConversationWindowSystem conversationUi = HUD.ConversationWindowSystem.ResolveInstance(createIfMissing: true);
            if (conversationUi == null)
            {
                Debug.LogWarning("[OpenConversationAction] ConversationWindowSystem instance is unavailable.");
                Cancel();
                return;
            }

            conversationUi.ShowConversation(
                _speakerName,
                _dialogueLine,
                _options,
                HandleOptionSelected,
                _onClosed,
                _speakerIcon);

            Succeeded = conversationUi.IsVisible;
            if (!Succeeded)
            {
                Cancel();
                return;
            }

            Complete();
        }

        private void HandleOptionSelected(int optionIndex)
        {
            _onOptionSelected?.Invoke(optionIndex);
            HUD.ConversationWindowSystem.Instance?.Hide();
        }
    }
}
