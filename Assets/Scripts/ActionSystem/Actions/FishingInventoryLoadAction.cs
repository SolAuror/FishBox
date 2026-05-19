using Sol.Fishing;
using Sol.Grab;
using Sol.Rpg;

namespace Sol.Actions
{
    /// <summary>
    /// Treat bait and lure like rod-bound equippables from inventory:
    /// left-click loads them onto the first compatible rod instead of using or dropping them.
    /// </summary>
    public sealed class FishingInventoryLoadAction : ItemAction
    {
        private readonly InventorySlot _slot;
        private readonly Inventory _inventory;
        private readonly Interactor _interactor;

        public bool Succeeded { get; private set; }

        public FishingInventoryLoadAction(InventorySlot slot, Inventory inventory, Interactor interactor)
        {
            _slot = slot;
            _inventory = inventory;
            _interactor = interactor;
        }

        public static bool CanHandle(ItemComponent item)
        {
            if (item == null)
                return false;

            return item.Tags.HasTagOrChild(GameplayCapabilityTags.ItemFishingLure) || FishingBaitItem.IsSupportedBait(item);
        }

        public override bool CanExecute()
        {
            return _slot?.Item != null
                && _inventory != null
                && _interactor?.Owner != null
                && Context?.Equipment != null
                && Context.FishingState != null
                && CanHandle(_slot.Item);
        }

        public override void OnStart()
        {
            Succeeded = TryLoadOntoFirstAvailableRod(
                _slot,
                _inventory,
                Context.Equipment,
                Context.FishingState);
            Complete();
        }

        private static bool TryLoadOntoFirstAvailableRod(
            InventorySlot slot,
            Inventory inventory,
            Equipment equipment,
            FishingState fishingState)
        {
            if (slot?.Item == null || inventory == null || equipment == null || fishingState == null)
                return false;

            ItemComponent item = slot.Item;
            if (!CanHandle(item))
                return false;

            if (TryLoadOntoEquippedRod(slot, inventory, fishingState))
                return true;

            ItemComponent rodToEquip = FindFirstCompatibleRod(item, inventory, equipment);
            if (rodToEquip == null)
                return false;

            if (!EnsureRodEquipped(rodToEquip, equipment))
                return false;

            return TryLoadOntoEquippedRod(slot, inventory, fishingState);
        }

        private static bool TryLoadOntoEquippedRod(InventorySlot slot, Inventory inventory, FishingState fishingState)
        {
            if (slot?.Item == null || inventory == null || fishingState == null || fishingState.HasLineOut)
                return false;

            if (slot.Item.Tags.HasTagOrChild(GameplayCapabilityTags.ItemFishingLure))
                return fishingState.TryLoadLure(slot, inventory);

            if (FishingBaitItem.IsSupportedBait(slot.Item))
                return fishingState.TryLoadBait(slot, inventory);

            return false;
        }

        private static ItemComponent FindFirstCompatibleRod(ItemComponent item, Inventory inventory, Equipment equipment)
        {
            if (item == null || inventory == null || equipment == null)
                return null;

            for (int i = 0; i < inventory.Slots.Count; i++)
            {
                InventorySlot slot = inventory.Slots[i];
                ItemComponent candidate = slot?.Item;
                if (candidate == null)
                    continue;

                FishingRodItem rod = candidate.GetComponent<FishingRodItem>();
                if (!candidate.Tags.HasTagOrChild(GameplayCapabilityTags.ItemFishingRod) || !CanRodAcceptItem(rod, item))
                    continue;

                if (equipment.IsEquipped(candidate) || CanSwitchTo(candidate, equipment))
                    return candidate;
            }

            return null;
        }

        private static bool CanRodAcceptItem(FishingRodItem rod, ItemComponent item)
        {
            if (rod == null || item == null)
                return false;

            if (item.Tags.HasTagOrChild(GameplayCapabilityTags.ItemFishingLure))
                return rod.LoadedLureItem == null;

            if (FishingBaitItem.IsSupportedBait(item))
                return rod.LoadedLureItem != null;

            return false;
        }

        private static bool CanSwitchTo(ItemComponent rodItem, Equipment equipment)
        {
            if (rodItem == null || equipment == null)
                return false;

            if (!TryResolveEquipmentSlot(rodItem, out EquipmentSlotType slot))
                return false;

            if (!equipment.IsSlotOccupied(slot))
                return true;

            foreach (var kv in equipment.Equipped)
            {
                if (kv.Key != slot)
                    continue;

                ItemComponent equippedItem = kv.Value;
                return equippedItem != null && equippedItem.Tags.HasTagOrChild(GameplayCapabilityTags.ItemFishingRod);
            }

            return false;
        }

        private static bool EnsureRodEquipped(ItemComponent rodItem, Equipment equipment)
        {
            if (rodItem == null || equipment == null)
                return false;

            if (equipment.IsEquipped(rodItem))
                return true;

            if (!TryResolveEquipmentSlot(rodItem, out EquipmentSlotType slot))
                return false;

            if (equipment.IsSlotOccupied(slot))
            {
                foreach (var kv in equipment.Equipped)
                {
                    if (kv.Key != slot || kv.Value == rodItem)
                        continue;

                    if (kv.Value == null || !kv.Value.Tags.HasTagOrChild(GameplayCapabilityTags.ItemFishingRod))
                        return false;

                    equipment.Unequip(slot);
                    break;
                }
            }

            return equipment.Equip(rodItem);
        }

        private static bool TryResolveEquipmentSlot(ItemComponent item, out EquipmentSlotType slot)
        {
            if (item == null)
            {
                slot = default;
                return false;
            }

            return ItemTypeRules.TryResolveDefaultSlot(item, out slot);
        }
    }
}
