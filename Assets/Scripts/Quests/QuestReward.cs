using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sol.Quests
{
    [Serializable]
    public class ItemReward
    {
        [Tooltip("ItemId from ItemRegistry (e.g. 'ITM00042').")]
        public string ItemId = string.Empty;
        [Min(1)] public int Count = 1;
    }

    [Serializable]
    public class QuestReward
    {
        [Min(0)] public int Gold = 0;
        public List<ItemReward> Items = new();
    }
}
