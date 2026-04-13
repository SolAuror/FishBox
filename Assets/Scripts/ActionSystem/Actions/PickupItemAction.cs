using Sol.Grab;
using UnityEngine;

namespace Sol.Actions
{
    public enum PickupItemActionOutcome
    {
        None = 0,
        Success = 1,
        Cancelled = 2,
        MissingItem = 3,
        ItemUnavailable = 4,
        MissingInventory = 5,
        InventoryFull = 6,
        InventoryRejected = 7
    }

    /// <summary>
    /// Discrete action: pick up a world item and add it to the actor's inventory.
    /// Completes immediately and disables the world GameObject on success.
    /// </summary>
    public class PickupItemAction : ItemAction
    {
        private readonly ItemComponent _item;
        private bool _started;
        private PickupItemActionOutcome _preconditionFailure = PickupItemActionOutcome.None;

        public PickupItemActionOutcome Outcome { get; private set; } = PickupItemActionOutcome.None;
        public bool Succeeded => Outcome == PickupItemActionOutcome.Success;
        public bool Failed => IsCancelled || (IsComplete && !Succeeded);
        public bool HasResolved => IsComplete || IsCancelled;

        public PickupItemAction(ItemComponent item)
        {
            _item = item;
        }

        public override bool CanExecute()
        {
            if (_item == null)
            {
                _preconditionFailure = PickupItemActionOutcome.MissingItem;
                return false;
            }

            if (!_item.gameObject.activeInHierarchy)
            {
                _preconditionFailure = PickupItemActionOutcome.ItemUnavailable;
                return false;
            }

            if (Context?.Inventory == null)
            {
                _preconditionFailure = PickupItemActionOutcome.MissingInventory;
                return false;
            }

            _preconditionFailure = PickupItemActionOutcome.None;
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

            bool markedStolen = false;
            if (_item.WouldBeStealing(Context.Actor))
            {
                _item.SetStolen(true);
                markedStolen = true;
            }

            bool inventoryAtCapacity = Context.Inventory.Count >= Context.Inventory.Capacity;
            bool added = Context.Inventory.Add(_item);
            if (!added && markedStolen)
                _item.SetStolen(false);

            if (added)
            {
                if (_item != null && _item.Type == ItemType.Gold)
                    Object.Destroy(_item.gameObject);
                else
                    _item.gameObject.SetActive(false);

                Outcome = PickupItemActionOutcome.Success;
            }
            else
            {
                Outcome = inventoryAtCapacity
                    ? PickupItemActionOutcome.InventoryFull
                    : PickupItemActionOutcome.InventoryRejected;
            }

            Complete();
        }

        public override void OnCancel()
        {
            if (Outcome != PickupItemActionOutcome.None)
                return;

            Outcome = _started
                ? PickupItemActionOutcome.Cancelled
                : ResolvePreconditionFailure();
        }

        private PickupItemActionOutcome ResolvePreconditionFailure()
        {
            if (_preconditionFailure != PickupItemActionOutcome.None)
                return _preconditionFailure;

            if (_item == null)
                return PickupItemActionOutcome.MissingItem;

            if (!_item.gameObject.activeInHierarchy)
                return PickupItemActionOutcome.ItemUnavailable;

            if (Context?.Inventory == null)
                return PickupItemActionOutcome.MissingInventory;

            return PickupItemActionOutcome.InventoryRejected;
        }
    }
}
