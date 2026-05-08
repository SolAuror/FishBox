using System;
using System.Collections.Generic;
using Sol.Grab;
using Sol.ToD;
using UnityEngine;

namespace Sol.Rpg
{
    [Serializable]
    public sealed class ShopStockSaveEntry
    {
        public string ItemId = string.Empty;
        public int Quantity;
    }

    public sealed class ShopRuntimeSession
    {
        private readonly Dictionary<string, float> _stockPriceMultipliers = new(StringComparer.OrdinalIgnoreCase);
        private readonly GameObject _inventoryHost;

        public string ShopId { get; }
        public RpgShopDefinition Definition { get; }
        public Inventory Inventory { get; }
        public double LastRestockRealtime { get; private set; }
        public int LastRestockInGameDay { get; private set; }

        public ShopRuntimeSession(RpgShopDefinition definition)
        {
            Definition = definition;
            ShopId = definition != null ? definition.Id : string.Empty;
            _inventoryHost = new GameObject($"ShopRuntime_{ShopId}");
            UnityEngine.Object.DontDestroyOnLoad(_inventoryHost);
            Inventory = _inventoryHost.AddComponent<Inventory>();
            _inventoryHost.SetActive(false);
            SeedFromDefinition();
        }

        public void Dispose()
        {
            if (_inventoryHost != null)
                DestroyRuntimeObject(_inventoryHost);
        }

        public void SeedFromDefinition()
        {
            ClearInventory();
            _stockPriceMultipliers.Clear();
            if (Definition == null)
                return;

            Inventory.Gold = Definition.Gold;
            IReadOnlyList<RpgShopStockEntry> stock = Definition.Stock;
            if (stock != null)
            {
                for (int i = 0; i < stock.Count; i++)
                {
                    RpgShopStockEntry entry = stock[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId))
                        continue;

                    string itemId = entry.ItemId.Trim();
                    _stockPriceMultipliers[itemId] = Mathf.Max(0f, entry.PriceMultiplier);
                    int quantity = UnityEngine.Random.Range(Mathf.Max(0, entry.MinQuantity), Mathf.Max(entry.MinQuantity, entry.MaxQuantity) + 1);
                    AddStock(itemId, quantity);
                }
            }

            MarkRestockedNow();
        }

        public void Restore(int gold, IReadOnlyList<ShopStockSaveEntry> stock, double lastRestockRealtime, int lastRestockInGameDay)
        {
            ClearInventory();
            BuildPriceMultipliers();
            Inventory.Gold = Mathf.Max(0, gold);
            if (stock != null)
            {
                for (int i = 0; i < stock.Count; i++)
                {
                    ShopStockSaveEntry entry = stock[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId) || entry.Quantity <= 0)
                        continue;

                    AddStock(entry.ItemId, entry.Quantity);
                }
            }

            LastRestockRealtime = Math.Max(0d, lastRestockRealtime);
            LastRestockInGameDay = Math.Max(0, lastRestockInGameDay);
        }

        public List<ShopStockSaveEntry> CollectStockSaveData()
        {
            List<ShopStockSaveEntry> stock = new();
            IReadOnlyList<InventorySlot> slots = Inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                if (slot?.Item == null || string.IsNullOrWhiteSpace(slot.Item.ItemId))
                    continue;

                stock.Add(new ShopStockSaveEntry
                {
                    ItemId = slot.Item.ItemId,
                    Quantity = Mathf.Max(0, slot.Count)
                });
            }

            return stock;
        }

        public void TryRestock()
        {
            if (Definition == null)
                return;

            switch (Definition.RestockMode)
            {
                case RpgShopRestockMode.EveryRealtimeHours:
                    if (Time.realtimeSinceStartupAsDouble - LastRestockRealtime >= Definition.RestockRealtimeHours * 3600d)
                        SeedFromDefinition();
                    break;
                case RpgShopRestockMode.EveryInGameDays:
                    int day = ResolveCurrentInGameDay();
                    if (day - LastRestockInGameDay >= Definition.RestockInGameDays)
                        SeedFromDefinition();
                    break;
            }
        }

        public int GetBuyPrice(ItemComponent item)
        {
            if (item == null)
                return 0;

            float stockMultiplier = _stockPriceMultipliers.TryGetValue(item.ItemId, out float multiplier) ? multiplier : 1f;
            return Price(item.Value, Definition != null ? Definition.BuyPriceMultiplier : 1f, stockMultiplier, ensureNonZero: true);
        }

        public int GetSellPrice(ItemComponent item)
        {
            if (item == null)
                return 0;

            return Price(item.Value, Definition != null ? Definition.SellPriceMultiplier : 0.5f, 1f, ensureNonZero: false);
        }

        public bool BuyItem(InventorySlot slot, Inventory playerInventory)
        {
            if (slot?.Item == null || playerInventory == null)
                return false;

            int price = GetBuyPrice(slot.Item);
            if (playerInventory.Gold < price)
                return false;

            ItemComponent item = ExtractTransferItem(slot);
            if (item == null)
                return false;

            if (!playerInventory.Add(item))
            {
                RollbackExtractedItem(slot, item);
                return false;
            }

            item.SetOwner(playerInventory.Owner);
            item.SetStolen(false);
            Inventory.Remove(slot, 1);
            playerInventory.Gold -= price;
            Inventory.Gold += price;
            return true;
        }

        public bool SellItem(InventorySlot slot, Inventory playerInventory)
        {
            if (slot?.Item == null || playerInventory == null)
                return false;

            int price = GetSellPrice(slot.Item);
            if (Inventory.Gold < price)
                return false;

            ItemComponent item = ExtractTransferItem(slot);
            if (item == null)
                return false;

            if (!Inventory.Add(item))
            {
                RollbackExtractedItem(slot, item);
                return false;
            }

            item.SetOwnerId(Definition != null ? Definition.OwnerNpcId : string.Empty);
            item.SetStolen(false);
            playerInventory.Remove(slot, 1);
            Inventory.Gold -= price;
            playerInventory.Gold += price;
            return true;
        }

        private void BuildPriceMultipliers()
        {
            _stockPriceMultipliers.Clear();
            IReadOnlyList<RpgShopStockEntry> stock = Definition?.Stock;
            if (stock == null)
                return;

            for (int i = 0; i < stock.Count; i++)
            {
                RpgShopStockEntry entry = stock[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.ItemId))
                    _stockPriceMultipliers[entry.ItemId.Trim()] = Mathf.Max(0f, entry.PriceMultiplier);
            }
        }

        private void AddStock(string itemId, int quantity)
        {
            if (quantity <= 0)
                return;

            ItemRegistry registry = ItemRegistry.Get();
            for (int i = 0; i < quantity; i++)
            {
                ItemComponent item = registry != null ? registry.InstantiateWorldItem(itemId, Inventory.transform) : null;
                if (item == null)
                    break;

                if (!Inventory.Add(item))
                {
                    DestroyRuntimeObject(item.gameObject);
                    break;
                }
            }
        }

        private void ClearInventory()
        {
            if (Inventory == null)
                return;

            Inventory.BeginBulkUpdate();
            while (Inventory.Slots.Count > 0)
            {
                InventorySlot slot = Inventory.Slots[Inventory.Slots.Count - 1];
                foreach (ItemComponent item in slot.EnumerateItems())
                {
                    if (item != null)
                        DestroyRuntimeObject(item.gameObject);
                }
                Inventory.Remove(slot, slot.Count);
            }
            Inventory.Gold = 0;
            Inventory.EndBulkUpdate();
        }

        private void MarkRestockedNow()
        {
            LastRestockRealtime = Time.realtimeSinceStartupAsDouble;
            LastRestockInGameDay = ResolveCurrentInGameDay();
        }

        private static int ResolveCurrentInGameDay()
        {
            Calendar calendar = UnityEngine.Object.FindFirstObjectByType<Calendar>();
            return calendar != null ? calendar.TotalDaysElapsed : 0;
        }

        private static int Price(int baseValue, float primaryMultiplier, float stockMultiplier, bool ensureNonZero)
        {
            if (baseValue <= 0)
                return 0;

            int price = Mathf.RoundToInt(baseValue * Mathf.Max(0f, primaryMultiplier) * Mathf.Max(0f, stockMultiplier));
            return ensureNonZero ? Mathf.Max(1, price) : Mathf.Max(0, price);
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }

        private static ItemComponent ExtractTransferItem(InventorySlot slot)
        {
            if (slot == null || slot.Item == null || slot.Count <= 0)
                return null;

            return slot.PopItem();
        }

        private static void RollbackExtractedItem(InventorySlot slot, ItemComponent extracted)
        {
            if (slot == null || extracted == null)
                return;

            if (slot.Item != extracted)
                slot.PushExtra(extracted);
        }
    }
}
