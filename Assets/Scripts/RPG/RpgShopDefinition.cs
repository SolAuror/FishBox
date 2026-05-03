using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sol.Rpg
{
    public enum RpgShopRestockMode
    {
        Never = 0,
        EveryRealtimeHours = 1,
        EveryInGameDays = 2
    }

    [Serializable]
    public sealed class RpgShopStockEntry
    {
        [ItemIdDropdown]
        public string ItemId = string.Empty;
        [Min(0)]
        public int MinQuantity = 1;
        [Min(1)]
        public int MaxQuantity = 1;
        [Min(0f)]
        public float PriceMultiplier = 1f;
    }

    [CreateAssetMenu(fileName = "SHP00001_NewShop", menuName = "Sol/RPG/Shop Definition")]
    public sealed class RpgShopDefinition : RpgDefinition
    {
        [Header("Shop")]
        [NpcIdDropdown]
        [SerializeField] private string _ownerNpcId = string.Empty;
        [Tooltip("Optional faction id (FAC#####).")]
        [SerializeField] private string _factionId = string.Empty;
        [Min(0)]
        [SerializeField] private int _gold = 100;
        [Min(0f)]
        [SerializeField] private float _buyPriceMultiplier = 1f;
        [Min(0f)]
        [SerializeField] private float _sellPriceMultiplier = 0.5f;
        [SerializeField] private RpgShopRestockMode _restockMode = RpgShopRestockMode.EveryInGameDays;
        [Min(0f)]
        [SerializeField] private float _restockRealtimeHours = 24f;
        [Min(0)]
        [SerializeField] private int _restockInGameDays = 1;
        [SerializeField] private List<RpgShopStockEntry> _stock = new();

        public string OwnerNpcId => _ownerNpcId;
        public string FactionId => _factionId;
        public int Gold => _gold;
        public float BuyPriceMultiplier => _buyPriceMultiplier;
        public float SellPriceMultiplier => _sellPriceMultiplier;
        public RpgShopRestockMode RestockMode => _restockMode;
        public float RestockRealtimeHours => _restockRealtimeHours;
        public int RestockInGameDays => _restockInGameDays;
        public IReadOnlyList<RpgShopStockEntry> Stock => _stock;

        private void OnValidate()
        {
            if (_stock == null)
                return;

            for (int i = 0; i < _stock.Count; i++)
            {
                RpgShopStockEntry entry = _stock[i];
                if (entry == null)
                    continue;

                entry.MaxQuantity = Mathf.Max(entry.MinQuantity, entry.MaxQuantity);
            }
        }
    }
}
