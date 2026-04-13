using UnityEngine;

namespace Sol.Actions
{
    /// <summary>
    /// Discrete action: open the trade UI between the actor and an NPC.
    /// Completes immediately — the trade panel is a UI concern, not a state system.
    /// </summary>
    public class OpenTradeAction : InteractionAction
    {
        private readonly Inventory _playerInventory;
        private readonly Inventory _npcInventory;
        private readonly bool _lootMode;

        public bool Succeeded { get; private set; }

        public OpenTradeAction(Inventory playerInventory, Inventory npcInventory, bool lootMode = false)
        {
            _playerInventory = playerInventory;
            _npcInventory = npcInventory;
            _lootMode = lootMode;
        }

        public override bool CanExecute()
        {
            return _playerInventory != null && _npcInventory != null;
        }

        public override void OnStart()
        {
            if (HUD.TradeUI.Instance == null)
            {
                Debug.LogWarning("[OpenTradeAction] TradeUI.Instance is null.");
                Cancel();
                return;
            }
            HUD.TradeUI.Instance.Open(_playerInventory, _npcInventory, _lootMode);
            Succeeded = true;
            Complete();
        }
    }
}
