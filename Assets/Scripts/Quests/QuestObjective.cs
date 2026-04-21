using System;
using System.Collections.Generic;
using UnityEngine;
using Sol.AI;
using Sol;

namespace Sol.Quests
{
    public enum QuestObjectiveType
    {
        CatchCount = 0,
        CatchTotalValue = 1,
        CatchRarity = 2,
        CatchByPrefix = 3,
        CollectItem = 4,
        DeliverItem = 5,
        TalkToNpc = 6,
        EquipItem = 7,
    }

    [Serializable]
    public class PrefixRequirement
    {
        public string Prefix = string.Empty;
        [Min(1)] public int Count = 1;
    }

    /// <summary>
    /// Polymorphic sequential quest objective. Only the fields relevant to <see cref="Type"/> are used.
    /// </summary>
    [Serializable]
    public class QuestObjective
    {
        public QuestObjectiveType Type = QuestObjectiveType.CatchCount;

        [Tooltip("Human-readable description shown in the HUD tracker (e.g. 'Catch 5 fish').")]
        public string DisplayText = string.Empty;

        [Tooltip("For CatchCount / CollectItem / DeliverItem: target count. For CatchRarity: count of that rarity.")]
        [Min(1)] public int Count = 1;

        [Tooltip("For CollectItem / DeliverItem / EquipItem: the ItemId (e.g. 'ITM00042').")]
        [ItemIdDropdown]
        public string ItemId = string.Empty;

        [Tooltip("For EquipItem: optional tag on ItemComponent (e.g. 'Lure', 'Bait') when ItemId is empty.")]
        public string ItemTag = string.Empty;

        [Tooltip("For EquipItem: when true, restrict matching to the selected equipment slot.")]
        public bool MatchSlot = false;

        [Tooltip("For EquipItem: target equipment slot when MatchSlot is enabled.")]
        public EquipmentSlotType Slot = EquipmentSlotType.MainHand;

        [Tooltip("For CatchTotalValue: total gold value threshold.")]
        public int GoldAmount = 0;

        [Tooltip("For CatchRarity: required fish rarity.")]
        public FishRarity Rarity = FishRarity.Common;

        [Tooltip("For CatchByPrefix: list of prefix+count pairs (e.g. 2 Tiny + 1 Lofty).")]
        public List<PrefixRequirement> PrefixRequirements = new();

        [Tooltip("For DeliverItem / TalkToNpc: the NPC CharacterName.")]
        public string NpcName = string.Empty;
    }
}
