using Sol.Grab;

namespace Sol.Actions
{
    /// <summary>
    /// Discrete action: equip an item via the Equipment state system.
    /// If the item is already equipped, it is unequipped instead (toggle).
 /// Completes immediately - no transform, physics, or animation logic here.
    /// </summary>
    public class EquipItemAction : ItemAction
    {
        private readonly ItemComponent _item;

        public bool Succeeded { get; private set; }

        public EquipItemAction(ItemComponent item) => _item = item;

        public override bool CanExecute()
        {
            return _item != null
                && Context.Equipment != null
                && Sol.ItemTypeRules.IsEquipableItem(_item);
        }

        public override void OnStart()
        {
            if (Context.Equipment.IsEquipped(_item))
                Succeeded = Context.Equipment.UnequipItem(_item);
            else
                Succeeded = Context.Equipment.Equip(_item);

            Complete();
        }
    }
}
