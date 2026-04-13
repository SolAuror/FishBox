namespace Sol.Actions
{
    public enum ConsumeActionOutcome
    {
        None = 0,
        Success = 1,
        Cancelled = 2,
        InvalidSlot = 3,
        NotConsumable = 4,
        MissingInventory = 5,
        MissingInteractor = 6,
        ConsumeRejected = 7
    }

    /// <summary>
    /// Discrete action: consume an item from inventory via Inventory.Use().
    /// Delegates stat application to the Inventory system.
    /// Requires an Interactor for the consumption pipeline (same path as player).
    /// </summary>
    public class ConsumeAction : ItemAction
    {
        private readonly InventorySlot _slot;
        private readonly Interactor _interactor;
        private bool _started;
        private ConsumeActionOutcome _preconditionFailure = ConsumeActionOutcome.None;

        public ConsumeActionOutcome Outcome { get; private set; } = ConsumeActionOutcome.None;
        public bool Succeeded => Outcome == ConsumeActionOutcome.Success;
        public bool Failed => IsCancelled || (IsComplete && !Succeeded);
        public bool HasResolved => IsComplete || IsCancelled;

        public ConsumeAction(InventorySlot slot, Interactor interactor)
        {
            _slot = slot;
            _interactor = interactor;
        }

        public override bool CanExecute()
        {
            if (_slot == null || _slot.Item == null)
            {
                _preconditionFailure = ConsumeActionOutcome.InvalidSlot;
                return false;
            }

            if (!_slot.Item.IsConsumable)
            {
                _preconditionFailure = ConsumeActionOutcome.NotConsumable;
                return false;
            }

            if (Context?.Inventory == null)
            {
                _preconditionFailure = ConsumeActionOutcome.MissingInventory;
                return false;
            }

            if (_interactor == null)
            {
                _preconditionFailure = ConsumeActionOutcome.MissingInteractor;
                return false;
            }

            _preconditionFailure = ConsumeActionOutcome.None;
            return true;
        }

        public override void OnStart()
        {
            _started = true;
            if (!CanExecute())
            {
                Outcome = ResolvePreconditionFailure();
                Cancel();
                return;
            }

            bool consumed = Context.Inventory.Use(_slot, _interactor);
            Outcome = consumed
                ? ConsumeActionOutcome.Success
                : ConsumeActionOutcome.ConsumeRejected;
            Complete();
        }

        public override void OnCancel()
        {
            if (Outcome != ConsumeActionOutcome.None)
                return;

            Outcome = _started
                ? ConsumeActionOutcome.Cancelled
                : ResolvePreconditionFailure();
        }

        private ConsumeActionOutcome ResolvePreconditionFailure()
        {
            if (_preconditionFailure != ConsumeActionOutcome.None)
                return _preconditionFailure;

            if (_slot == null || _slot.Item == null)
                return ConsumeActionOutcome.InvalidSlot;

            if (!_slot.Item.IsConsumable)
                return ConsumeActionOutcome.NotConsumable;

            if (Context?.Inventory == null)
                return ConsumeActionOutcome.MissingInventory;

            if (_interactor == null)
                return ConsumeActionOutcome.MissingInteractor;

            return ConsumeActionOutcome.ConsumeRejected;
        }
    }
}
