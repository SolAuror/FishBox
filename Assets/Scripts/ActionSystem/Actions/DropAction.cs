namespace Sol.Actions
{
    /// <summary>
    /// Discrete action: drop an item from inventory.
    /// Delegates to Equipment.DetachForDrop (if equipped) and Inventory.Remove,
    /// then delegates world placement to ItemActionSystem.ExecuteDrop.
    /// Completes immediately.
    /// </summary>
    public class DropAction : ItemAction
    {
        private readonly InventorySlot _slot;
        private readonly Interactor _interactor;

        public bool Succeeded { get; private set; }

        public DropAction(InventorySlot slot, Interactor interactor)
        {
            _slot = slot;
            _interactor = interactor;
        }

        public override bool CanExecute()
        {
            return _slot != null
                && _slot.Item != null
                && Context.Inventory != null
                && _interactor != null;
        }

        public override void OnStart()
        {
            Succeeded = ItemActionSystem.Execute(ItemActionType.Drop, _slot, Context.Inventory, _interactor);
            Complete();
        }
    }
}
