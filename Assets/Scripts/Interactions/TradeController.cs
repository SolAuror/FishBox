using Sol.Audio;
using UnityEngine;

namespace Sol
{
    /// <summary>
    /// Transfers items between two inventories. Used by the trade UI.
    /// Supports gold-based buying and selling.
    /// </summary>
    public static class TradeController
    {
        public static int TransferStack(InventorySlot slot, Inventory from, Inventory to, int maxCount = int.MaxValue)
        {
            if (slot?.Item == null || from == null || to == null || maxCount <= 0)
                return 0;

            int moveCount = System.Math.Min(slot.Count, maxCount);
            if (moveCount <= 0)
                return 0;

            if (slot.Item.Type == Grab.ItemType.Gold)
                return TransferGoldStack(slot, from, to, moveCount);

            int moved = 0;
            from.BeginBulkUpdate();
            to.BeginBulkUpdate();
            try
            {
                for (int i = 0; i < moveCount; i++)
                {
                    if (!TransferItem(slot, from, to))
                        break;
                    moved++;
                }
            }
            finally
            {
                to.EndBulkUpdate();
                from.EndBulkUpdate();
            }

            return moved;
        }

        public static int TransferGoldStack(InventorySlot slot, Inventory from, Inventory to, int maxCount = int.MaxValue)
        {
            if (slot?.Item == null || from == null || to == null)
                return 0;

            if (slot.Item.Type != Grab.ItemType.Gold || slot.Count <= 0)
                return 0;

            int moveCount = System.Math.Min(slot.Count, System.Math.Max(1, maxCount));
            int moved = 0;
            int totalGold = 0;

            for (int i = 0; i < moveCount; i++)
            {
                var goldItem = slot.PopItem();
                if (goldItem == null)
                    break;

                totalGold += UnityEngine.Mathf.Max(1, goldItem.Value);
                UnityEngine.Object.Destroy(goldItem.gameObject);
                moved++;
            }

            if (moved <= 0)
                return 0;

            from.BeginBulkUpdate();
            to.BeginBulkUpdate();
            try
            {
                from.Remove(slot, moved);
                to.Gold += totalGold;
            }
            finally
            {
                to.EndBulkUpdate();
                from.EndBulkUpdate();
            }
            return moved;
        }

        /// <summary>
        /// Extract exactly one item instance from a slot for transfer.
        /// Preserves stack integrity by popping from extras first.
        /// </summary>
        private static Grab.ItemComponent ExtractTransferItem(InventorySlot slot)
        {
            if (slot == null || slot.Item == null || slot.Count <= 0)
                return null;

            return slot.PopItem();
        }

        /// <summary>
        /// Restore an extracted item when transfer fails before source removal.
        /// If the popped item was from extras, push it back. If it was the primary
        /// slot item, PopItem did not mutate slot structure so nothing to restore.
        /// </summary>
        private static void RollbackExtractedItem(InventorySlot slot, Grab.ItemComponent extracted)
        {
            if (slot == null || extracted == null)
                return;

            if (slot.Item != extracted)
                slot.PushExtra(extracted);
        }

        /// <summary>
        /// Move one unit of the given slot's item from <paramref name="from"/> to <paramref name="to"/>.
        /// Returns true if the transfer succeeded.
        /// </summary>
        public static bool TransferItem(InventorySlot slot, Inventory from, Inventory to)
        {
            if (slot?.Item == null || from == null || to == null) return false;

            var itemToTransfer = ExtractTransferItem(slot);
            if (itemToTransfer == null) return false;

            // Target must have room.
            if (!to.Add(itemToTransfer))
            {
                RollbackExtractedItem(slot, itemToTransfer);
                return false;
            }

            ApplyOwnershipAfterTransfer(itemToTransfer, from, to, boughtByPlayer: false, soldToNpc: false);

            // Remove from source only after successful add.
            from.Remove(slot, 1);
            return true;
        }

        /// <summary>
        /// Player sells an item to the NPC. NPC pays gold to the player.
        /// Returns true if the sale succeeded.
        /// </summary>
        public static bool SellItem(InventorySlot slot, Inventory playerInv, Inventory npcInv)
        {
            if (slot?.Item == null || playerInv == null || npcInv == null) return false;

            int price = slot.Item.Value;

            // NPC must have enough gold to buy.
            if (npcInv.Gold < price) return false;

            var itemToTransfer = ExtractTransferItem(slot);
            if (itemToTransfer == null) return false;

            // Target must have room.
            if (!npcInv.Add(itemToTransfer))
            {
                RollbackExtractedItem(slot, itemToTransfer);
                return false;
            }

            ApplyOwnershipAfterTransfer(itemToTransfer, playerInv, npcInv, boughtByPlayer: false, soldToNpc: true);

            playerInv.Remove(slot, 1);
            npcInv.Gold -= price;
            playerInv.Gold += price;
            Vector3 playerPos = playerInv.transform != null ? playerInv.transform.position : Vector3.zero;
            AudioService.Instance?.PlaySfx(AudioEvent.GoldGained, playerPos);
            return true;
        }

        /// <summary>
        /// Player buys an item from the NPC. Player pays gold to the NPC.
        /// Returns true if the purchase succeeded.
        /// </summary>
        public static bool BuyItem(InventorySlot slot, Inventory playerInv, Inventory npcInv)
        {
            if (slot?.Item == null || playerInv == null || npcInv == null) return false;

            int price = slot.Item.Value;

            // Player must have enough gold.
            if (playerInv.Gold < price) return false;

            var itemToTransfer = ExtractTransferItem(slot);
            if (itemToTransfer == null) return false;

            // Player must have room.
            if (!playerInv.Add(itemToTransfer))
            {
                RollbackExtractedItem(slot, itemToTransfer);
                return false;
            }

            ApplyOwnershipAfterTransfer(itemToTransfer, npcInv, playerInv, boughtByPlayer: true, soldToNpc: false);

            npcInv.Remove(slot, 1);
            playerInv.Gold -= price;
            npcInv.Gold += price;
            Vector3 playerPos = playerInv.transform != null ? playerInv.transform.position : Vector3.zero;
            AudioService.Instance?.PlaySfx(AudioEvent.GoldSpent, playerPos);
            return true;
        }

        private static void ApplyOwnershipAfterTransfer(
            Grab.ItemComponent item,
            Inventory from,
            Inventory to,
            bool boughtByPlayer,
            bool soldToNpc)
        {
            if (item == null || to == null)
                return;

            bool destinationIsPlayer = to.Owner != null && to.Owner.CompareTag("Player");

            if (boughtByPlayer)
            {
                item.SetOwner(to.Owner);
                item.SetStolen(false);
                return;
            }

            if (soldToNpc)
            {
                if (to.HasOwner)
                    item.SetOwnerId(to.OwnerId);
                else
                    item.SetOwner(to.Owner);

                item.SetStolen(false);
                return;
            }

            if (destinationIsPlayer)
            {
                bool shouldBeStolen = from != null
                    && from.HasOwner
                    && !from.IsOwnedBy(to.Owner);

                if (shouldBeStolen)
                    item.SetOwnerId(from.OwnerId);
                else
                    item.SetOwner(to.Owner);

                item.SetStolen(shouldBeStolen);
                return;
            }

            if (to.HasOwner)
                item.SetOwnerId(to.OwnerId);
            else
                item.SetOwner(to.Owner);

            item.SetStolen(false);
        }
    }
}


