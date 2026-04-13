namespace Sol.Actions
{
    /// <summary>
    /// Discrete action: player sells an item to an NPC via gold exchange.
    /// Delegates to TradeController — no direct Inventory mutation here.
    /// Completes immediately.
    /// </summary>
    public class SellItemAction : ItemAction
    {
        private readonly InventorySlot _slot;
        private readonly Inventory _playerInventory;
        private readonly Inventory _npcInventory;

        public bool Succeeded { get; private set; }

        public SellItemAction(InventorySlot slot, Inventory playerInventory, Inventory npcInventory)
        {
            _slot = slot;
            _playerInventory = playerInventory;
            _npcInventory = npcInventory;
        }

        public override bool CanExecute()
        {
            return _slot?.Item != null && _playerInventory != null && _npcInventory != null;
        }

        public override void OnStart()
        {
            Succeeded = TradeController.SellItem(_slot, _playerInventory, _npcInventory);
            Complete();
        }
    }
}
